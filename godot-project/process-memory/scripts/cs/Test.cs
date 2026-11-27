using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.ProcessMemory;

/// <summary>Full API test of Process Memory (OS.GetProcessMemoryUsage and OS.GetProcessMemoryInfo). Same tests as scripts/gd/Test.gd.</summary>
public partial class Test : FeatureTest
{
    private static readonly string[] Keys = ["resident", "private", "peak", "heap"];
    private const long Mib = 1024 * 1024;
    private const long BlockMib = 64;

    protected override Task RunTests()
    {
        TestUsage();
        TestInfo();
        TestPlatforms();
        TestGrowth();
        return Task.CompletedTask;
    }

    private void TestUsage()
    {
        ulong usage = OS.GetProcessMemoryUsage();
        Expect(usage > 10 * Mib, $"GetProcessMemoryUsage is a plausible byte count (> 10 MiB): {usage}");
    }

    private void TestInfo()
    {
        Godot.Collections.Dictionary info = OS.GetProcessMemoryInfo();
        ExpectEqual(info.Count, Keys.Length, $"GetProcessMemoryInfo has exactly {Keys.Length} entries");
        foreach (string key in Keys)
        {
            Expect(info.ContainsKey(key), $"the info has the key '{key}'");
            long value = info[key].AsInt64();
            Expect(value >= -1, $"'{key}' is -1 or a byte count: {value}");
        }
        long resident = info["resident"].AsInt64();
        long peak = info["peak"].AsInt64();
        Expect(resident > 0, $"resident memory is known: {resident}");
        Expect(peak >= resident, "peak is not lower than resident");
    }

    // Windows: private is known, heap is not. Linux, Android, macOS, iOS and Web know the heap.
    private void TestPlatforms()
    {
        Godot.Collections.Dictionary info = OS.GetProcessMemoryInfo();
        string platform = OS.GetName();
        if (platform == "Windows")
        {
            Expect(info["private"].AsInt64() > 0, "private memory is known on Windows");
            ExpectEqual(info["heap"].AsInt64(), -1L, "heap is -1 on Windows");
        }
        else
        {
            Expect(info["heap"].AsInt64() > 0, $"heap is known on {platform}");
        }
    }

    // Touching 64 MiB must show up in the usage; the Web may reuse free memory, so only "not lower" there.
    private void TestGrowth()
    {
        long before = (long)OS.GetProcessMemoryUsage();
        var block = new byte[BlockMib * Mib];
        System.Array.Fill(block, (byte)1);
        long after = (long)OS.GetProcessMemoryUsage();
        long minimumGrowth = BlockMib * Mib / 2;
        if (OS.GetName() == "Web")
        {
            minimumGrowth = 0;
        }
        Expect(after >= before + minimumGrowth, $"usage grows with a 64 MiB block: {before} -> {after}");
        long peak = OS.GetProcessMemoryInfo()["peak"].AsInt64();
        Expect(peak >= after - 4 * Mib, "peak covers the 64 MiB block");
        System.GC.KeepAlive(block);
    }
}
