using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using CampaignForge.ServerManager.Services;
using Forms = System.Windows.Forms;

namespace CampaignForge.ServerManager;

public partial class MainWindow : Window
{
    private readonly ComponentManager _components = new();
    private readonly SystemMonitor _monitor = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(3) };
    private readonly Forms.NotifyIcon _tray = new();
    private bool _allowClose;
    private bool _refreshing;

    public MainWindow()
    {
        InitializeComponent();
        _tray.Icon = System.Drawing.SystemIcons.Application;
        _tray.Text = "Campaign Forge Server Manager";
        _tray.Visible = true;
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Open Server Manager", null, (_, _) => RestoreWindow());
        menu.Items.Add("Exit Manager", null, (_, _) => ExitManager());
        _tray.ContextMenuStrip = menu;
        _tray.DoubleClick += (_, _) => RestoreWindow();
        _timer.Tick += async (_, _) => await RefreshAllAsync();
        Loaded += async (_, _) => { Log("Server Manager started. No component startup settings were changed."); await RefreshAllAsync(); _timer.Start(); };
        StateChanged += (_, _) => { if (WindowState == WindowState.Minimized) HideToTray(); };
    }

    private async Task RefreshAllAsync()
    {
        if (_refreshing) return;
        _refreshing = true;
        try
        {
            var sqlTask = _components.GetSqlStatusAsync();
            var ollamaTask = _components.GetOllamaStatusAsync();
            var api = _components.GetApiStatus();
            var snapshot = _monitor.Read();
            var sql = await sqlTask;
            var ollama = await ollamaTask;

            SetStatus(SqlState, SqlDetail, sql.State, sql.Detail, sql.IsRunning);
            SetStatus(OllamaState, OllamaDetail, ollama.State, ollama.Detail, ollama.IsRunning);
            SetStatus(ApiState, ApiDetail, api.State, api.Detail, api.IsRunning);
            CpuValue.Text = $"{snapshot.CpuPercent:0}%";
            RamValue.Text = $"{snapshot.MemoryUsedGb:0.0} / {snapshot.MemoryTotalGb:0.0} GB";
            RamDetail.Text = $"{snapshot.MemoryTotalGb - snapshot.MemoryUsedGb:0.0} GB available";
            GpuValue.Text = snapshot.GpuName.Contains("unavailable", StringComparison.OrdinalIgnoreCase) ? "Unavailable" : $"{snapshot.GpuPercent:0}% Â· {snapshot.GpuMemoryUsedMb:0} MB";
            GpuDetail.Text = snapshot.GpuName + (snapshot.GpuTemperatureC > 0 ? $" Â· {snapshot.GpuTemperatureC:0}Â°C" : "");
            DiskValue.Text = $"C: {snapshot.CFreeGb:0} GB";
            DiskDetail.Text = $"D: {snapshot.DFreeGb:0} GB free";
            await RefreshModelAsync();
            FooterStatus.Text = $"Last updated {DateTime.Now:T} Â· Refreshing every 3 seconds";
        }
        catch (Exception ex) { Log("Refresh error: " + ex.Message); }
        finally { _refreshing = false; }
    }

    private async Task RefreshModelAsync()
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            var json = await http.GetStringAsync("http://127.0.0.1:11434/api/ps");
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("models", out var models) && models.GetArrayLength() > 0)
            {
                var model = models[0];
                var name = model.TryGetProperty("name", out var n) ? n.GetString() : "Model loaded";
                var size = model.TryGetProperty("size", out var s) ? s.GetInt64() / 1024d / 1024d / 1024d : 0;
                var processor = model.TryGetProperty("size_vram", out var v) ? $" Â· VRAM {v.GetInt64() / 1024d / 1024d / 1024d:0.0} GB" : "";
                ModelValue.Text = $"{name} Â· {size:0.0} GB{processor}";
            }
            else ModelValue.Text = "Ollama running Â· No model currently loaded";
        }
        catch { ModelValue.Text = "Ollama is not responding"; }
    }

    private static void SetStatus(System.Windows.Controls.TextBlock stateBlock, System.Windows.Controls.TextBlock detailBlock, string state, string detail, bool running)
    {
        stateBlock.Text = state.ToUpperInvariant();
        stateBlock.Foreground = running ? System.Windows.Media.Brushes.LightGreen : System.Windows.Media.Brushes.Khaki;
        detailBlock.Text = detail;
    }

    private async void StartAll_Click(object sender, RoutedEventArgs e)
    {
        await RunActionAsync("Start Campaign Forge", async () =>
        {
            var sql = await _components.StartSqlIfNeededAsync(); Log(sql);
            var sqlStatus = await _components.GetSqlStatusAsync();
            if (!sqlStatus.IsRunning)
            {
                Log("Startup paused: SQL Server is not confirmed running. API was not started.");
                await RefreshAllAsync();
                return;
            }
            var ollama = await _components.StartOllamaAsync(); Log(ollama);
            var api = _components.StartApi(); Log(api);
            await Task.Delay(1500);
            await RefreshAllAsync();
        });
    }

    private async void StopAll_Click(object sender, RoutedEventArgs e)
    {
        await RunActionAsync("Stop Campaign Forge", async () =>
        {
            Log(_components.StopApi());
            Log(_components.StopOllama());
            Log("SQL Server was deliberately left running and unchanged.");
            await RefreshAllAsync();
        });
    }

    private async void SqlStart_Click(object sender, RoutedEventArgs e) => await RunActionAsync("SQL Server check", async () => { Log(await _components.StartSqlIfNeededAsync()); await RefreshAllAsync(); });
    private async void OllamaStart_Click(object sender, RoutedEventArgs e) => await RunActionAsync("Start Ollama", async () => { Log(await _components.StartOllamaAsync()); await RefreshAllAsync(); });
    private async void OllamaStop_Click(object sender, RoutedEventArgs e) => await RunActionAsync("Stop Ollama", async () => { Log(_components.StopOllama()); await RefreshAllAsync(); });
    private async void ApiStart_Click(object sender, RoutedEventArgs e) => await RunActionAsync("Start API", async () => { Log(_components.StartApi()); await Task.Delay(1000); await RefreshAllAsync(); });
    private async void ApiStop_Click(object sender, RoutedEventArgs e) => await RunActionAsync("Stop API", async () => { Log(_components.StopApi()); await Task.Delay(1000); await RefreshAllAsync(); });
    private async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshAllAsync();

    private async Task RunActionAsync(string name, Func<Task> action)
    {
        FooterStatus.Text = name + "â€¦";
        try { await action(); }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223) { Log(name + " cancelled at the Windows UAC prompt."); }
        catch (Exception ex) { Log(name + " failed: " + ex.Message); System.Windows.MessageBox.Show(this, ex.Message, name, MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private void Log(string message)
    {
        LogBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        LogBox.ScrollToEnd();
    }

    private void HideToTray()
    {
        Hide();
        _tray.BalloonTipTitle = "Campaign Forge Server Manager";
        _tray.BalloonTipText = "Still running in the notification area.";
        _tray.ShowBalloonTip(1500);
    }

    private void RestoreWindow() { Show(); WindowState = WindowState.Normal; Activate(); }

    private void ExitManager()
    {
        _allowClose = true;
        _timer.Stop();
        _tray.Visible = false;
        _tray.Dispose();
        _components.Dispose(); // Does not stop any server component.
        Close();
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            HideToTray();
            return;
        }
        base.OnClosing(e);
    }
}
