using System.Diagnostics;
using System.Runtime.Versioning;

namespace NetLens.Network.Adapters;

/// <summary>
/// Collects system-level performance metrics (CPU and RAM usage).
/// Uses PerformanceCounter on Windows for reliable, low-overhead sampling.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class SystemMetricsCollector : IDisposable
{
    private readonly PerformanceCounter _cpuCounter;
    private bool _firstSample = true;

    public SystemMetricsCollector()
    {
        _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
        // First call always returns 0 on Windows — pre-warm it
        _cpuCounter.NextValue();
    }

    /// <summary>
    /// Returns current CPU usage percentage and RAM usage percentage.
    /// </summary>
    public (double CpuPercent, double RamPercent) GetCurrentUsage()
    {
        var cpu = Math.Round(_cpuCounter.NextValue(), 1);

        var totalMemory = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
        var usedMemory = Environment.WorkingSet;
        var ramPercent = totalMemory > 0
            ? Math.Round((double)usedMemory / totalMemory * 100.0, 1)
            : 0.0;

        return (cpu, ramPercent);
    }

    public void Dispose() => _cpuCounter.Dispose();
}
