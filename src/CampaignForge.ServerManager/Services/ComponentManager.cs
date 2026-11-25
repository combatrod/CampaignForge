using System.Diagnostics;
using System.Net.Http;
using System.ServiceProcess;
using CampaignForge.ServerManager.Models;

namespace CampaignForge.ServerManager.Services;

public sealed class ComponentManager : IDisposable
{
    private const string SqlServiceName = "MSSQLSERVER";
    private const string ApiServiceName = "CampaignForge";
    private const string OllamaBaseUrl = "http://127.0.0.1:11434";
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(2) };
    private Process? _ollamaProcess;

    public bool StartedOllamaHere => _ollamaProcess is { HasExited: false };

    public async Task<ComponentStatus> GetSqlStatusAsync()
    {
        var state = ReadServiceState(SqlServiceName);
        var sqlcmdAvailable = FindExecutable("sqlcmd.exe") is not null;
        var connected = sqlcmdAvailable && await CanConnectToSqlAsync();
        var serviceRunning = state.Equals("Running", StringComparison.OrdinalIgnoreCase);
        return new ComponentStatus
        {
            Name = "SQL Server",
            State = connected ? "Online" : state,
            IsRunning = connected || (!sqlcmdAvailable && serviceRunning),
            IsManaged = false,
            Detail = connected
                ? "RALIEN11 · Default instance · CampaignForge database connectivity verified"
                : !sqlcmdAvailable && serviceRunning
                    ? "Windows service is running · sqlcmd.exe not found, so database connectivity was not independently verified"
                    : $"Windows service {SqlServiceName} · {state}; database connection not verified"
        };
    }

    public ComponentStatus GetApiStatus()
    {
        var state = ReadServiceState(ApiServiceName);
        var running = state.Equals("Running", StringComparison.OrdinalIgnoreCase);
        return new ComponentStatus
        {
            Name = "Campaign Forge API",
            State = running ? "Running" : state,
            IsRunning = running,
            IsManaged = true,
            Detail = running ? "Windows Service · HTTP port 5000" : $"Windows Service · {state}"
        };
    }

    public async Task<ComponentStatus> GetOllamaStatusAsync()
    {
        try
        {
            using var response = await _http.GetAsync($"{OllamaBaseUrl}/api/version");
            if (response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                return new ComponentStatus
                {
                    Name = "Ollama",
                    State = "Running",
                    IsRunning = true,
                    IsManaged = StartedOllamaHere,
                    Detail = $"Local API responding · {body.Trim()} · {(StartedOllamaHere ? "Started by Manager" : "Already running / externally started")}"
                };
            }
        }
        catch { }

        return new ComponentStatus
        {
            Name = "Ollama",
            State = "Stopped",
            IsRunning = false,
            IsManaged = true,
            Detail = $"Local API unavailable · Model directory: {GetModelDirectory()}"
        };
    }

    public async Task<string> StartSqlIfNeededAsync()
    {
        if (await CanConnectToSqlAsync()) return "SQL Server is already available. It was not changed.";
        var state = ReadServiceState(SqlServiceName);
        if (state.Equals("Running", StringComparison.OrdinalIgnoreCase))
            return "SQL Server is already running. Its Windows startup configuration was not changed.";

        RunElevatedSc("start", SqlServiceName);
        for (var i = 0; i < 20; i++)
        {
            await Task.Delay(1000);
            state = ReadServiceState(SqlServiceName);
            if (state.Equals("Running", StringComparison.OrdinalIgnoreCase))
            {
                var verified = await CanConnectToSqlAsync();
                return verified
                    ? "SQL Server started and CampaignForge database connectivity verified. Startup configuration unchanged."
                    : "SQL Server service started. Database connectivity could not be independently verified (sqlcmd may be unavailable). Startup configuration unchanged.";
            }
        }
        return $"SQL Server start was requested, but service state is currently {state}. Refresh status before continuing.";
    }

    public string StartApi()
    {
        var state = ReadServiceState(ApiServiceName);
        if (state.Equals("Running", StringComparison.OrdinalIgnoreCase)) return "Campaign Forge API is already running.";
        RunElevatedSc("start", ApiServiceName);
        return "Start request sent to the CampaignForge Windows service.";
    }

    public string StopApi()
    {
        var state = ReadServiceState(ApiServiceName);
        if (!state.Equals("Running", StringComparison.OrdinalIgnoreCase)) return "Campaign Forge API is not running.";
        RunElevatedSc("stop", ApiServiceName);
        return "Stop request sent to the CampaignForge Windows service.";
    }

    public async Task<string> StartOllamaAsync()
    {
        var existing = await GetOllamaStatusAsync();
        if (existing.IsRunning) return "Ollama is already responding. The Manager did not take ownership of the existing process.";

        var executable = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Ollama", "ollama.exe");
        if (!File.Exists(executable)) return $"Ollama executable not found at {executable}.";

        var modelDirectory = GetModelDirectory();
        Directory.CreateDirectory(modelDirectory);
        var start = new ProcessStartInfo(executable, "serve")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(executable)!
        };
        start.Environment["OLLAMA_MODELS"] = modelDirectory;
        start.Environment["OLLAMA_HOST"] = "127.0.0.1:11434";
        _ollamaProcess = Process.Start(start);
        if (_ollamaProcess is null) return "Windows did not start the Ollama process.";

        for (var i = 0; i < 15; i++)
        {
            await Task.Delay(500);
            var status = await GetOllamaStatusAsync();
            if (status.IsRunning) return $"Ollama started. Model directory: {modelDirectory}";
            if (_ollamaProcess.HasExited) return "Ollama exited during startup. Check its installation and logs.";
        }
        return "Ollama process started, but its API did not respond within 7.5 seconds.";
    }

    public string StopOllama()
    {
        if (_ollamaProcess is not { HasExited: false })
            return "Ollama was not started by this Manager. It has been left untouched.";
        _ollamaProcess.CloseMainWindow();
        if (!_ollamaProcess.WaitForExit(3000)) _ollamaProcess.Kill(entireProcessTree: true);
        _ollamaProcess.Dispose();
        _ollamaProcess = null;
        return "Manager-started Ollama process stopped.";
    }

    public async Task<bool> CanConnectToSqlAsync()
    {
        // A TCP check is deliberately not used: SQL Server may be configured for local shared-memory connections.
        // The Manager uses sqlcmd when available; otherwise service state is reported without claiming DB verification.
        var sqlcmd = FindExecutable("sqlcmd.exe");
        if (sqlcmd is null) return false;
        var result = await RunProcessAsync(sqlcmd, "-S . -d CampaignForge -E -b -Q \"SET NOCOUNT ON; SELECT 1\"", 5000);
        return result.ExitCode == 0;
    }

    public static string ReadServiceState(string serviceName)
    {
        try
        {
            using var service = new ServiceController(serviceName);
            return service.Status.ToString();
        }
        catch (InvalidOperationException) { return "Not installed"; }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1060) { return "Not installed"; }
        catch (Exception ex) { return $"Unavailable ({ex.Message})"; }
    }

    private static string GetModelDirectory() =>
        Environment.GetEnvironmentVariable("OLLAMA_MODELS", EnvironmentVariableTarget.User)
        ?? Environment.GetEnvironmentVariable("OLLAMA_MODELS")
        ?? @"D:\AI\Models";

    private static string? FindExecutable(string name)
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var folder in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(folder.Trim('"'), name);
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }

    private static void RunElevatedSc(string verb, string serviceName)
    {
        var start = new ProcessStartInfo("sc.exe", $"{verb} \"{serviceName}\"")
        {
            UseShellExecute = true,
            Verb = "runas",
            WindowStyle = ProcessWindowStyle.Hidden
        };
        using var process = Process.Start(start);
        process?.WaitForExit(10000);
    }

    private static async Task<(int ExitCode, string Output)> RunProcessAsync(string file, string arguments, int timeoutMs)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo(file, arguments)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };
        process.Start();
        using var cts = new CancellationTokenSource(timeoutMs);
        try
        {
            var output = await process.StandardOutput.ReadToEndAsync(cts.Token);
            await process.WaitForExitAsync(cts.Token);
            return (process.ExitCode, output);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(true); } catch { }
            return (-1, "Timed out");
        }
    }

    public void Dispose()
    {
        _http.Dispose();
        _ollamaProcess?.Dispose(); // Do not stop a server simply because the Manager window exits.
    }
}
