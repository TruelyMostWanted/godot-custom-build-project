using Godot;

namespace TruelyInteractive.ProcessMemory;

/// <summary>Minimal example of Process Memory: the snippets of the wiki guide, in order. Same as scripts/gd/Example.gd.</summary>
public partial class Example : Node
{
    public override void _Ready()
    {
        // Show the memory of the game (the guide fills a Label from a Timer; the example prints the text).
        ulong usage = OS.GetProcessMemoryUsage();
        string text = $"Memory: {usage / (1024.0 * 1024.0):F1} MiB";
        GD.Print(text);

        // Add memory to the debugger monitors.
        Performance.AddCustomMonitor("game/process_memory_mib", Callable.From(() => OS.GetProcessMemoryUsage() / 1048576.0));
        GD.Print("Monitor: ", Performance.GetCustomMonitor("game/process_memory_mib"), " MiB");

        // Print the details.
        foreach (var (key, value) in OS.GetProcessMemoryInfo())
        {
            long bytes = value.AsInt64();
            GD.Print($"{key}: {(bytes == -1 ? "unknown" : $"{bytes / (1024 * 1024)} MiB")}");
        }
        GetTree().Quit();
    }
}
