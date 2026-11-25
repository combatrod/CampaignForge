using System.Diagnostics;
using System.Runtime.InteropServices;
using CampaignForge.ServerManager.Models;

namespace CampaignForge.ServerManager.Services;

public sealed class SystemMonitor
{
    private ulong _lastIdle;
    private ulong _lastKernel;
    private ulong _lastUser;
    private bool _hasCpuSample;

    public ResourceSnapshot Read()
    {
        var cpu = ReadSystemCpu();

        var memory = new MemoryStatusEx { Length = (uint)Marshal.SizeOf<MemoryStatusEx>() };
        GlobalMemoryStatusEx(ref memory);
        var totalGb = memory.TotalPhysical / 1024d / 1024d / 1024d;
        var usedGb = (memory.TotalPhysical - memory.AvailablePhysical) / 1024d / 1024d / 1024d;

        var (gpuName, gpu, gpuUsed, gpuTotal, gpuTemp) = ReadGpu();
        return new ResourceSnapshot(cpu, usedGb, totalGb, gpu, gpuUsed, gpuTotal, gpuTemp,
            GetFreeGb("C"), GetFreeGb("D"), gpuName);
    }

    private double ReadSystemCpu()
    {
        if (!GetSystemTimes(out var idle, out var kernel, out var user)) return 0;
        var idleNow = ToUInt64(idle);
        var kernelNow = ToUInt64(kernel);
        var userNow = ToUInt64(user);
        var result = 0d;
        if (_hasCpuSample)
        {
            var idleDelta = idleNow - _lastIdle;
            var totalDelta = (kernelNow - _lastKernel) + (userNow - _lastUser);
            if (totalDelta > 0) result = Math.Clamp(100d * (totalDelta - idleDelta) / totalDelta, 0, 100);
        }
        _lastIdle = idleNow; _lastKernel = kernelNow; _lastUser = userNow; _hasCpuSample = true;
        return result;
    }

    private static ulong ToUInt64(System.Runtime.InteropServices.ComTypes.FILETIME value) =>
        ((ulong)value.dwHighDateTime << 32) | (uint)value.dwLowDateTime;

    private static double GetFreeGb(string drive)
    {
        try { return new DriveInfo(drive).AvailableFreeSpace / 1024d / 1024d / 1024d; }
        catch { return 0; }
    }

    private static (string Name, double Util, double Used, double Total, double Temp) ReadGpu()
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo("nvidia-smi.exe", "--query-gpu=name,utilization.gpu,memory.used,memory.total,temperature.gpu --format=csv,noheader,nounits")
                {
                    UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true
                }
            };
            process.Start();
            var line = process.StandardOutput.ReadLine();
            if (!process.WaitForExit(1500) || process.ExitCode != 0 || string.IsNullOrWhiteSpace(line)) return ("NVIDIA GPU unavailable", 0, 0, 0, 0);
            var parts = line.Split(',').Select(x => x.Trim()).ToArray();
            if (parts.Length < 5) return ("NVIDIA GPU detected", 0, 0, 0, 0);
            return (parts[0], Parse(parts[1]), Parse(parts[2]), Parse(parts[3]), Parse(parts[4]));
        }
        catch { return ("NVIDIA GPU unavailable", 0, 0, 0, 0); }
    }

    private static double Parse(string value) => double.TryParse(value, out var result) ? result : 0;

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(out System.Runtime.InteropServices.ComTypes.FILETIME idleTime, out System.Runtime.InteropServices.ComTypes.FILETIME kernelTime, out System.Runtime.InteropServices.ComTypes.FILETIME userTime);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhysical;
        public ulong AvailablePhysical;
        public ulong TotalPageFile;
        public ulong AvailablePageFile;
        public ulong TotalVirtual;
        public ulong AvailableVirtual;
        public ulong AvailableExtendedVirtual;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);
}
