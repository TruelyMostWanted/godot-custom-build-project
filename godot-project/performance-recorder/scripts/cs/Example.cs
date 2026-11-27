using System.Threading.Tasks;
using Godot;
using Recorder = Godot.PerformanceRecorder;

namespace TruelyInteractive.PerformanceRecorder;

/// <summary>Minimal example of Performance Recorder: the snippets of the wiki guide, in order. Same as scripts/gd/Example.gd.</summary>
public partial class Example : Node
{
    private const int Frames = 30;

    public override async void _Ready()
    {
        // Record frame times to a CSV file.
        Recorder.RecordingStopped += path => GD.Print($"CSV written to {path}");
        Recorder.Start();
        await WaitFrames();

        // Choose the columns.
        var profile = new PerformanceRecordingProfile
        {
            Builtins = [(int)Recorder.Builtin.FrameTime, (int)Recorder.Builtin.GpuTime, (int)Recorder.Builtin.ProcessMemory],
            Monitors = [(int)Performance.Monitor.TimeFps, (int)Performance.Monitor.RenderTotalDrawCallsInFrame],
            SampleIntervalFrames = 2, // one row every second frame
            OutputPath = "user://perf/{platform}_{date}_{time}.csv",
        };
        Recorder.Start(profile);
        await WaitFrames();

        // Record your own values.
        int enemiesId = Recorder.RegisterMetric("enemies_alive");
        int spawnedId = Recorder.RegisterMetric("enemies_spawned", Recorder.MetricKind.Counter);
        var metricsProfile = new PerformanceRecordingProfile { Metrics = ["enemies_alive", "enemies_spawned"] };
        Recorder.Start(metricsProfile);
        Recorder.SetMetric(enemiesId, GetTree().GetNodeCountInGroup("enemies"));
        Recorder.AddMetric(spawnedId, 1);
        await WaitFrames();

        // Keep only the seconds before a hitch.
        var hitchProfile = new PerformanceRecordingProfile
        {
            FlushIntervalFrames = 0,
            BufferFrames = 600, // 10 s at 60 FPS
            OutputPath = "user://hitches/{date}_{time}.csv",
        };
        Recorder.Start(hitchProfile);
        await WaitFrames();
        Recorder.Flush(); // writes the last rows

        // Read the statistics of a run.
        Recorder.Stop();
        PerformanceSummary summary = Recorder.GetSummary();
        PerformanceColumnSummary frameTime = summary.GetColumn("frame_time");
        GD.Print($"{summary.GetFrameCount()} frames, mean {frameTime.GetMean():F0} us, max {frameTime.GetMaximum():F0} us");
        GD.Print("p95: ", frameTime.GetP95(), ", histogram: ", string.Join(", ", frameTime.GetHistogram([8333.0, 16667.0, 33333.0])));

        // Count hitches.
        var countProfile = new PerformanceRecordingProfile { HitchThresholdUsec = 33333 }; // slower than 30 FPS
        Recorder.Start(countProfile);
        await WaitFrames();
        summary = Recorder.GetSummary();
        GD.Print($"{summary.GetHitchCount()} hitches, in frames {string.Join(", ", summary.GetHitchFrames())}");

        // Write the statistics as JSON.
        var jsonProfile = new PerformanceRecordingProfile { OutputPath = "user://perf/run.csv", WriteSummaryJson = true };
        Recorder.Start(jsonProfile);
        await WaitFrames();
        Recorder.Stop();
        GD.Print(Recorder.GetSummary().ToText()[..80]);

        // Create a performance budget.
        var budget = new PerformanceBudget();
        budget.AddRule(new PerformanceBudgetRule { Column = "frame_time", Statistic = PerformanceBudgetRule.StatisticEnum.Percentile, Percentile = 95.0, Limit = 16667.0 });
        budget.AddRule(new PerformanceBudgetRule { Statistic = PerformanceBudgetRule.StatisticEnum.HitchCount, Limit = 0 });
        ResourceSaver.Save(budget, "user://perf/budget.tres");

        // Check a performance budget in CI (a test scene would quit with the exit code).
        PerformanceBudgetResult result = Recorder.CheckBudget(GD.Load<PerformanceBudget>("user://perf/budget.tres"));
        GD.Print(result.ToText());
        GD.Print("Exit code: ", result.GetExitCode());

        // Record .NET garbage collection.
        var dotnetProfile = new PerformanceRecordingProfile { MetricSources = ["dotnet"] };
        Recorder.Start(dotnetProfile);
        await WaitFrames();
        Recorder.Stop();

        // Keep the rows on mobile and download them on the Web: the output path to download.
        GD.Print("Output path: ", Recorder.GetOutputPath());
        GetTree().Quit();
    }

    private async Task WaitFrames()
    {
        for (int i = 0; i < Frames; i++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }
}
