namespace CampaignForge.ServerManager.Models;

public sealed class ComponentStatus
{
    public string Name { get; init; } = "";
    public string Detail { get; set; } = "";
    public string State { get; set; } = "Unknown";
    public bool IsRunning { get; set; }
    public bool IsManaged { get; set; }
}

public sealed record ResourceSnapshot(
    double CpuPercent,
    double MemoryUsedGb,
    double MemoryTotalGb,
    double GpuPercent,
    double GpuMemoryUsedMb,
    double GpuMemoryTotalMb,
    double GpuTemperatureC,
    double CFreeGb,
    double DFreeGb,
    string GpuName);
