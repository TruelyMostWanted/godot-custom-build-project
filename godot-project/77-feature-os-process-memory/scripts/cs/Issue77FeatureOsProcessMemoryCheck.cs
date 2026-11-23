using System.Diagnostics;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.Issue77FeatureOsProcessMemory;

/// <summary>C# checks of 77-feature-os-process-memory. Check the same things as scripts/gd/check.gd.</summary>
public partial class Issue77FeatureOsProcessMemoryCheck : FeatureCheckCs
{
    private static readonly string[] Keys = ["resident", "private", "peak", "heap"];
    private const long MiB = 1024 * 1024;

    protected override string CheckName => "77-feature-os-process-memory";

    protected override Task RunChecks()
    {
        CheckBindings();
        CheckUsage();
        CheckInfo();
        CheckGrowth();
        CheckCost();
        return Task.CompletedTask;
    }

    private void CheckBindings()
    {
        Expect(ClassDB.ClassHasMethod("OS", "get_process_memory_usage"), "OS.get_process_memory_usage() is bound");
        Expect(ClassDB.ClassHasMethod("OS", "get_process_memory_info"), "OS.get_process_memory_info() is bound");
    }

    // Known on Windows, Linux, macOS, Android and iOS (BSD only with /proc).
    private void CheckUsage()
    {
        ulong usage = OS.GetProcessMemoryUsage();
        GD.Print($"  process memory usage: {usage} bytes");
        Expect(usage > 0, $"process memory usage is known ({usage} bytes)");
        // A running Godot project with a window and the .NET runtime needs more than 10 MiB.
        Expect(usage > 10 * MiB, "process memory usage is plausible (> 10 MiB)");
    }

    private void CheckInfo()
    {
        Godot.Collections.Dictionary info = OS.GetProcessMemoryInfo();
        GD.Print($"  process memory info: {info}");
        ExpectEqual(info.Count, Keys.Length, $"info has exactly {Keys.Length} entries");
        foreach (string key in Keys)
        {
            if (!Expect(info.ContainsKey(key), $"info has the key '{key}'"))
            {
                continue;
            }
            Expect(info[key].VariantType == Variant.Type.Int, $"'{key}' is an int");
            Expect((long)info[key] >= -1, $"'{key}' is -1 or a byte count ({(long)info[key]})");
        }
        long resident = (long)info["resident"];
        long peak = (long)info["peak"];
        Expect(resident > 0, $"resident memory is known ({resident} bytes)");
        if (peak != -1)
        {
            Expect(peak >= resident, $"peak ({peak}) is not lower than resident ({resident})");
        }
        string os = OS.GetName();
        if (os == "Windows")
        {
            Expect((long)info["private"] > 0, "private memory is known on Windows");
            ExpectEqual((long)info["heap"], -1L, "heap is -1 on Windows");
        }
        if (os is "Linux" or "Android" or "macOS" or "iOS")
        {
            Expect((long)info["heap"] > 0, $"heap is known on {os}");
        }
    }

    // Touching 64 MiB of native memory must show up in the usage.
    private void CheckGrowth()
    {
        ulong before = OS.GetProcessMemoryUsage();
        var block = new byte[64 * MiB];
        System.Array.Fill(block, (byte)1);
        ulong after = OS.GetProcessMemoryUsage();
        GD.Print($"  usage before: {before} bytes, with 64 MiB block: {after} bytes");
        Expect(after >= before + 32 * (ulong)MiB, $"usage grows by at least 32 MiB with a 64 MiB managed block ({before} -> {after})");
        long peak = (long)OS.GetProcessMemoryInfo()["peak"];
        if (peak != -1)
        {
            Expect(peak >= (long)after - 4 * MiB, "peak covers the 64 MiB block");
        }
        System.GC.KeepAlive(block);
    }

    // Cheap enough for every few frames: target < 10 µs per call; checked with a generous margin.
    private void CheckCost()
    {
        const int calls = 1000;
        var watch = Stopwatch.StartNew();
        ulong sum = 0;
        for (int i = 0; i < calls; i++)
        {
            sum += OS.GetProcessMemoryUsage();
        }
        double usageUsec = watch.Elapsed.TotalMicroseconds / calls;
        watch.Restart();
        for (int i = 0; i < 100; i++)
        {
            OS.GetProcessMemoryInfo();
        }
        double infoUsec = watch.Elapsed.TotalMicroseconds / 100;
        GD.Print($"  cost: GetProcessMemoryUsage() {usageUsec:F2} µs, GetProcessMemoryInfo() {infoUsec:F2} µs per call");
        Expect(sum > 0, "repeated calls return values");
        Expect(usageUsec < 50.0, $"GetProcessMemoryUsage() takes < 50 µs per call ({usageUsec:F2} µs)");
    }
}
