using System.Linq;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.Issue78FeaturePerformanceRecorder;

/// <summary>C# checks of 78-feature-performance-recorder. Check the same things as scripts/gd/check.gd.</summary>
public partial class Issue78FeaturePerformanceRecorderCheck : FeatureCheckCs
{
    private const string Dir = "user://78-feature-performance-recorder";
    private const int Frames = 40;

    private long _flushedRows;
    private string _stoppedPath = "";
    private bool _started;

    protected override string CheckName => "78-feature-performance-recorder";

    protected override async Task RunChecks()
    {
        CheckBindings();
        CheckProfileDefaults();
        await CheckRecording();
        await CheckMemoryOnly();
    }

    private void CheckBindings()
    {
        Expect(Engine.HasSingleton("PerformanceRecorder"), "PerformanceRecorder is a singleton");
        Expect(ClassDB.ClassExists("PerformanceRecordingProfile"), "PerformanceRecordingProfile is registered");
        string[] methods =
        [
            "start", "stop", "flush", "is_recording", "register_metric", "get_metric_id", "set_metric", "add_metric",
            "has_metric_source", "get_column_names", "get_column", "get_recorded_frame_count", "get_output_path",
        ];
        foreach (string method in methods)
        {
            Expect(ClassDB.ClassHasMethod("PerformanceRecorder", method), $"PerformanceRecorder.{method}() is bound");
        }
        foreach (string signal in new[] { "recording_started", "recording_stopped", "flushed" })
        {
            Expect(ClassDB.ClassHasSignal("PerformanceRecorder", signal), $"signal {signal} exists");
        }
        foreach (string setting in new[] { "enabled", "profile", "output_path" })
        {
            Expect(ProjectSettings.HasSetting("debug/performance_recording/" + setting), $"project setting debug/performance_recording/{setting} exists");
        }
        ExpectEqual((int)PerformanceRecorder.Builtin.Max, 10, "10 builtins");
    }

    private void CheckProfileDefaults()
    {
        var profile = new PerformanceRecordingProfile();
        ExpectEqual(profile.Builtins.Length, 8, "default profile has 8 builtins");
        ExpectEqual(profile.Monitors.Length, 8, "default profile has 8 monitors");
        ExpectEqual(profile.FlushIntervalFrames, 300, "default flush interval is 300 frames");
        ExpectEqual(profile.OutputPath, "user://performance/{date}_{time}.csv", "default output path");
    }

    private async Task CheckRecording()
    {
        int gauge = PerformanceRecorder.RegisterMetric("check_gauge_cs");
        int counter = PerformanceRecorder.RegisterMetric("check_counter_cs", PerformanceRecorder.MetricKind.Counter);
        ExpectEqual(PerformanceRecorder.RegisterMetric("check_gauge_cs"), gauge, "registering twice returns the same id");
        ExpectEqual(PerformanceRecorder.GetMetricId("check_counter_cs"), counter, "GetMetricId() finds the counter");

        var profile = new PerformanceRecordingProfile();
        profile.Builtins = [.. profile.Builtins, (int)PerformanceRecorder.Builtin.ProcessMemoryPeak, (int)PerformanceRecorder.Builtin.GpuTime];
        profile.Metrics = ["check_gauge_cs", "check_counter_cs"];
        profile.FlushIntervalFrames = 10;
        profile.FlushBudgetUsec = 0;
        profile.OutputPath = Dir + "/cs_{platform}.csv";

        PerformanceRecorder.RecordingStarted += OnStarted;
        PerformanceRecorder.Flushed += OnFlushed;
        PerformanceRecorder.RecordingStopped += OnStopped;

        ExpectEqual(PerformanceRecorder.Start(profile), Error.Ok, "Start() returns OK");
        Expect(PerformanceRecorder.IsRecording(), "IsRecording() after Start()");
        Expect(_started, "RecordingStarted was emitted");
        string path = PerformanceRecorder.GetOutputPath();
        ExpectEqual(path, $"{Dir}/cs_{OS.GetName().ToLowerInvariant()}.csv", "{platform} is replaced");

        for (int i = 0; i < Frames; i++)
        {
            PerformanceRecorder.SetMetric(gauge, i);
            PerformanceRecorder.AddMetric(counter, 1);
            PerformanceRecorder.AddMetric(counter, 2);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        Expect(_flushedRows >= 30, $"rows were flushed every 10 frames while recording ({_flushedRows})");
        PerformanceRecorder.Stop();
        Expect(!PerformanceRecorder.IsRecording(), "not recording after Stop()");
        ExpectEqual(_stoppedPath, path, "RecordingStopped carries the path");

        string[] names = PerformanceRecorder.GetColumnNames();
        long rows = PerformanceRecorder.GetRecordedFrameCount();
        // The frame in which Stop() is called is not recorded any more.
        Expect(rows >= Frames - 1, $"at least {Frames - 1} rows recorded ({rows})");
        Expect(names.Contains("frame_time") && names.Contains("time/fps") && names.Contains("check_counter_cs"), "columns contain builtins, monitors and metrics");

        double[] frames = PerformanceRecorder.GetColumn("frame");
        bool increasing = true;
        for (int i = 1; i < frames.Length; i++)
        {
            increasing &= frames[i] == frames[i - 1] + 1;
        }
        Expect(increasing, "frame column increases by 1 per row");
        Expect(PerformanceRecorder.GetColumn("frame_time").Last() > 0, "frame_time is measured");
        double[] counters = PerformanceRecorder.GetColumn("check_counter_cs");
        Expect(counters.Count(v => v == 3.0) >= Frames - 2, "counter holds the sum of one frame and resets each row");
        double memory = PerformanceRecorder.GetColumn("process_memory").Last();
        Expect(memory > 0, "process_memory is known");
        Expect(PerformanceRecorder.GetColumn("process_memory_peak").Last() >= memory, "process_memory_peak >= process_memory");
        GD.Print($"  gpu_time (ms) of the last row: {PerformanceRecorder.GetColumn("gpu_time").Last()}");

        double[] recorderTime = PerformanceRecorder.GetColumn("recorder_time");
        double average = recorderTime.Average();
        GD.Print($"  recorder_time: average {average:F2} µs, max {recorderTime.Max():F2} µs per row ({names.Length} columns)");
        Expect(average < 100.0, $"recorder overhead is small (average {average:F2} µs per row)");

        string[] lines = FileAccess.GetFileAsString(path).StripEdges().Split("\n");
        ExpectEqual(lines[0], string.Join(",", names), "CSV header matches GetColumnNames()");
        ExpectEqual((long)lines.Length, rows + 1, "CSV has one line per recorded row");
        string[] last = lines[^1].Split(",");
        ExpectEqual(last.Length, names.Length, "a CSV row has one value per column");
        ExpectEqual(last[System.Array.IndexOf(names, "check_counter_cs")], "3", "the counter value is written as an integer");

        PerformanceRecorder.RecordingStarted -= OnStarted;
        PerformanceRecorder.Flushed -= OnFlushed;
        PerformanceRecorder.RecordingStopped -= OnStopped;
    }

    private async Task CheckMemoryOnly()
    {
        var profile = new PerformanceRecordingProfile
        {
            Builtins = [(int)PerformanceRecorder.Builtin.Frame],
            Monitors = [],
            Metrics = ["check_gauge_cs"],
            BufferFrames = 5,
            FlushIntervalFrames = 0,
            OutputPath = Dir + "/cs_last_rows.csv",
        };
        if (FileAccess.FileExists(profile.OutputPath))
        {
            DirAccess.RemoveAbsolute(profile.OutputPath);
        }

        PerformanceRecorder.Start(profile);
        for (int i = 0; i < 12; i++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        ExpectEqual(PerformanceRecorder.GetColumn("frame").Length, 5, "only the last 5 rows are kept");
        Expect(!FileAccess.FileExists(profile.OutputPath), "no file before Flush()");
        ExpectEqual(PerformanceRecorder.Flush(), Error.Ok, "Flush() returns OK");
        PerformanceRecorder.Stop();
        string[] lines = FileAccess.GetFileAsString(profile.OutputPath).StripEdges().Split("\n");
        ExpectEqual(lines.Length, 6, "Flush() writes the header and the last 5 rows, Stop() adds nothing twice");
    }

    private void OnStarted() => _started = true;

    private void OnFlushed(long rows) => _flushedRows += rows;

    private void OnStopped(string path) => _stoppedPath = path;
}
