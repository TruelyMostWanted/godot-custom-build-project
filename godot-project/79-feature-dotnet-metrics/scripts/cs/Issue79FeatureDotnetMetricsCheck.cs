using System;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.Issue79FeatureDotnetMetrics;

/// <summary>C# checks of 79-feature-dotnet-metrics. Check the same things as scripts/gd/check.gd.</summary>
public partial class Issue79FeatureDotnetMetricsCheck : FeatureCheckCs
{
    private static readonly string[] Values =
    [
        "gc_allocated_bytes", "gc_total_memory_bytes", "gc_heap_size_bytes", "gc_fragmented_bytes", "gc_committed_bytes",
        "gc_pause_time_percent", "gc_pause_total_ms", "gc_collections_gen0", "gc_collections_gen1", "gc_collections_gen2",
        "threadpool_threads", "threadpool_pending", "threadpool_completed", "lock_contentions", "jit_compiled_methods",
    ];

    private static readonly string[] Deltas = ["gc_allocated_bytes_delta", "gc_collections_delta", "gc_pause_delta_ms"];

    private const int Frames = 10;
    private const int AllocationBytes = 8 * 1024 * 1024;

    // Keeps the allocations alive until they are measured.
    private static object _keepAlive;

    protected override string CheckName => "79-feature-dotnet-metrics";

    protected override async Task RunChecks()
    {
        CheckRegistration();
        await CheckMonitorsChangeAfterAllocations();
        await CheckRecording();
    }

    private static double ReadMonitor(string value) => Performance.GetCustomMonitor("dotnet/" + value).AsDouble();

    private async Task NextFrame() => await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

    private void CheckRegistration()
    {
        Expect(PerformanceRecorder.HasMetricSource("dotnet"), "the .NET module registers the metric source \"dotnet\"");
        foreach (string value in Values)
        {
            Expect(Performance.HasCustomMonitor("dotnet/" + value), $"Performance custom monitor dotnet/{value} exists");
        }
        foreach (string value in Deltas)
        {
            Expect(!Performance.HasCustomMonitor("dotnet/" + value), $"delta {value} is recorder-only");
        }
    }

    private async Task CheckMonitorsChangeAfterAllocations()
    {
        await NextFrame();
        double allocatedBefore = ReadMonitor("gc_allocated_bytes");
        double gen2Before = ReadMonitor("gc_collections_gen2");
        double jit = ReadMonitor("jit_compiled_methods");

        // Monitors are read at most once per frame: the same frame gives the same value.
        byte[][] blocks = new byte[8][];
        for (int i = 0; i < blocks.Length; i++)
        {
            blocks[i] = new byte[AllocationBytes / blocks.Length];
        }
        _keepAlive = blocks;
        ExpectEqual(ReadMonitor("gc_allocated_bytes"), allocatedBefore, "the cached value does not change within a frame");

        await NextFrame();
        double allocatedAfter = ReadMonitor("gc_allocated_bytes");
        Expect(allocatedAfter - allocatedBefore >= AllocationBytes,
            $"dotnet/gc_allocated_bytes grows by the allocated {AllocationBytes} bytes ({allocatedAfter - allocatedBefore})");
        Expect(Math.Abs(allocatedAfter - GC.GetTotalAllocatedBytes(false)) < 64 * 1024 * 1024,
            "dotnet/gc_allocated_bytes matches GC.GetTotalAllocatedBytes()");

        _keepAlive = null;
        GC.Collect();
        GC.WaitForPendingFinalizers();
        await NextFrame();
        Expect(ReadMonitor("gc_collections_gen2") > gen2Before, "dotnet/gc_collections_gen2 counts GC.Collect()");
        Expect(ReadMonitor("gc_heap_size_bytes") > 0, "dotnet/gc_heap_size_bytes is known after a collection");

        if (OS.GetName() != "iOS")
        {
            Expect(jit > 0, $"dotnet/jit_compiled_methods is above 0 ({jit})");
        }
        Expect(ReadMonitor("threadpool_threads") >= 0, "dotnet/threadpool_threads is a number");
    }

    private async Task CheckRecording()
    {
        var profile = new PerformanceRecordingProfile
        {
            Builtins = [(int)PerformanceRecorder.Builtin.Frame],
            Monitors = [],
            MetricSources = ["dotnet"],
            FlushIntervalFrames = 0,
            OutputPath = "",
        };

        ExpectEqual(PerformanceRecorder.Start(profile), Error.Ok, "Start() with metric source \"dotnet\" returns OK");
        await NextFrame();
        await NextFrame();
        _keepAlive = new byte[AllocationBytes];
        for (int i = 0; i < Frames; i++)
        {
            await NextFrame();
        }
        PerformanceRecorder.Stop();
        _keepAlive = null;

        string[] names = PerformanceRecorder.GetColumnNames();
        ExpectEqual(names.Length, 1 + Values.Length + Deltas.Length, $"frame plus {Values.Length + Deltas.Length} dotnet columns");
        foreach (string value in Values)
        {
            Expect(Array.IndexOf(names, "dotnet/" + value) >= 0, $"column dotnet/{value} exists");
        }
        foreach (string value in Deltas)
        {
            Expect(Array.IndexOf(names, "dotnet/" + value) >= 0, $"column dotnet/{value} exists");
        }

        double[] allocated = PerformanceRecorder.GetColumn("dotnet/gc_allocated_bytes");
        double[] deltas = PerformanceRecorder.GetColumn("dotnet/gc_allocated_bytes_delta");
        Expect(allocated.Length >= Frames, $"at least {Frames} rows recorded ({allocated.Length})");
        Expect(double.IsNaN(deltas[0]), "the first delta of a recording is empty");

        double sum = 0;
        for (int i = 1; i < deltas.Length; i++)
        {
            sum += deltas[i];
        }
        Expect(sum >= AllocationBytes, $"the deltas contain the allocation of {AllocationBytes} bytes ({sum})");
        Expect(Math.Abs(allocated[^1] - allocated[0] - sum) < 1.0, "the deltas add up to the growth of the total");
    }
}
