using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;
using Recorder = Godot.PerformanceRecorder;

namespace TruelyInteractive.PerformanceRecorder;

/// <summary>Full API test of Performance Recorder (PerformanceRecorder, PerformanceRecordingProfile, PerformanceSummary,
/// PerformanceColumnSummary, PerformanceBudget, PerformanceBudgetRule, PerformanceBudgetResult, PerformanceBudgetFailure,
/// and the "dotnet" metric source). Same tests as scripts/gd/Test.gd.</summary>
public partial class Test : FeatureTest
{
    private const string Dir = "user://performance-recorder-test";
    private const int Frames = 40;
    private const string Gauge = "test_gauge";
    private const string Counter = "test_counter";
    private const string FrameProfilePath = "res://performance-recorder/resources/frame_timing_profile.tres";
    private const string DotnetProfilePath = "res://performance-recorder/resources/dotnet_profile.tres";
    private const string BudgetPath = "res://performance-recorder/resources/60_fps_budget.tres";

    private static readonly string[] DotnetValues =
    [
        "gc_allocated_bytes", "gc_total_memory_bytes", "gc_heap_size_bytes", "gc_fragmented_bytes", "gc_committed_bytes",
        "gc_pause_time_percent", "gc_pause_total_ms", "gc_collections_gen0", "gc_collections_gen1", "gc_collections_gen2",
        "threadpool_threads", "threadpool_pending", "threadpool_completed", "lock_contentions", "jit_compiled_methods",
    ];

    private static readonly string[] DotnetDeltas = ["gc_allocated_bytes_delta", "gc_collections_delta", "gc_pause_delta_ms"];

    private bool _started;
    private long _flushedRows;
    private string _stoppedPath = "";

    protected override async Task RunTests()
    {
        TestBindings();
        TestProfileDefaults();
        TestRuleDefaults();
        TestSampleResources();
        await TestRecording();
        await TestSamplingOptions();
        await TestNoOutputPath();
        await TestRingBufferFlush();
        await TestSummaryExact();
        await TestSummaryHistogram();
        TestBudgetOnRecording();
        TestBudgetResource();
        await TestDotnet();
    }

    private async Task WaitFrames(int frames)
    {
        for (int i = 0; i < frames; i++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }

    private async Task Record(PerformanceRecordingProfile profile, int frames)
    {
        Recorder.Start(profile);
        await WaitFrames(frames);
        Recorder.Stop();
    }

    private void TestBindings()
    {
        Expect(Engine.HasSingleton("PerformanceRecorder"), "PerformanceRecorder is a singleton");
        foreach (string setting in new[] { "enabled", "profile", "output_path", "budget" })
        {
            Expect(ProjectSettings.HasSetting("debug/performance_recording/" + setting), $"project setting debug/performance_recording/{setting} exists");
        }
        ExpectEqual((int)Recorder.Builtin.Max, 10, "10 builtins");
        ExpectEqual((int)PerformanceBudgetRule.StatisticEnum.Max, 5, "5 statistics");
        ExpectEqual((int)PerformanceBudgetFailure.Reason.Max, 3, "3 failure reasons");
        Expect(!Recorder.HasMetricSource("no_such_source"), "HasMetricSource is false for an unknown source");
        ExpectEqual(Recorder.GetMetricId("no_such_metric"), -1, "GetMetricId is -1 for an unknown metric");
        Expect(!Recorder.IsRecording(), "not recording at the start");
    }

    private void TestProfileDefaults()
    {
        var profile = new PerformanceRecordingProfile();
        ExpectEqual(profile.Builtins.Length, 8, "default profile has 8 builtins");
        ExpectEqual(profile.Monitors.Length, 8, "default profile has 8 monitors");
        ExpectEqual(profile.Metrics.Length, 0, "default profile has no metrics");
        ExpectEqual(profile.MetricSources.Length, 0, "default profile has no metric sources");
        ExpectEqual(profile.IncludeCustomMonitors, false, "custom monitors are off by default");
        ExpectEqual(profile.SampleIntervalFrames, 1, "default sample interval is 1 frame");
        ExpectEqual(profile.ProcessMemoryIntervalFrames, 30, "default process memory interval is 30 frames");
        ExpectEqual(profile.BufferFrames, 3600, "default buffer is 3600 frames");
        ExpectEqual(profile.FlushIntervalFrames, 300, "default flush interval is 300 frames");
        ExpectEqual(profile.FlushBudgetUsec, 2000, "default flush budget is 2000 us");
        ExpectEqual(profile.Delimiter, ",", "default delimiter is a comma");
        ExpectEqual(profile.OutputPath, "user://performance/{date}_{time}.csv", "default output path");
        ExpectEqual(profile.HitchThresholdUsec, 50000, "default hitch threshold is 50000 us");
        ExpectEqual(profile.WriteSummaryJson, false, "summary JSON is off by default");
    }

    private void TestRuleDefaults()
    {
        var rule = new PerformanceBudgetRule();
        ExpectEqual(rule.Column, "frame_time", "default rule column is frame_time");
        ExpectEqual(rule.Statistic, PerformanceBudgetRule.StatisticEnum.Percentile, "default rule statistic is a percentile");
        ExpectEqual(rule.Percentile, 95.0, "default percentile is 95");
        ExpectEqual(rule.Limit, 16667.0, "default limit is 16667 us");
        ExpectEqual(rule.GetDescription(), "frame_time p95", "rule description");
        rule.Percentile = 150.0;
        ExpectEqual(rule.Percentile, 100.0, "percentile is clamped to 100");
    }

    private void TestSampleResources()
    {
        var frameProfile = GD.Load<PerformanceRecordingProfile>(FrameProfilePath);
        ExpectEqual(frameProfile.OutputPath, "user://performance/frames_{date}_{time}.csv", "frame_timing_profile.tres output path");
        var dotnetProfile = GD.Load<PerformanceRecordingProfile>(DotnetProfilePath);
        Expect(dotnetProfile.MetricSources.SequenceEqual(["dotnet"]), "dotnet_profile.tres records the dotnet source");
        var budget = GD.Load<PerformanceBudget>(BudgetPath);
        ExpectEqual(budget.GetRuleCount(), 3, "60_fps_budget.tres has 3 rules");
        PerformanceBudgetRule maximum = budget.Rules[1];
        ExpectEqual(maximum.Statistic, PerformanceBudgetRule.StatisticEnum.Maximum, "the second rule is a maximum");
        ExpectEqual(maximum.Limit, 50000.0, "the second rule limit is 50000 us");
        PerformanceBudgetRule hitches = budget.Rules[2];
        ExpectEqual(hitches.Statistic, PerformanceBudgetRule.StatisticEnum.HitchCount, "the third rule counts hitches");
        ExpectEqual(hitches.Limit, 3.0, "the third rule limit is 3");
    }

    private async Task TestRecording()
    {
        int gauge = Recorder.RegisterMetric(Gauge);
        int counter = Recorder.RegisterMetric(Counter, Recorder.MetricKind.Counter);
        ExpectEqual(Recorder.RegisterMetric(Gauge), gauge, "registering twice returns the same id");
        ExpectEqual(Recorder.GetMetricId(Counter), counter, "GetMetricId finds the counter");

        var profile = new PerformanceRecordingProfile();
        profile.Builtins = [.. profile.Builtins, (int)Recorder.Builtin.ProcessMemoryPeak, (int)Recorder.Builtin.GpuTime];
        profile.Metrics = [Gauge, Counter];
        profile.FlushIntervalFrames = 10;
        profile.FlushBudgetUsec = 0;
        profile.OutputPath = Dir + "/recording_{platform}.csv";
        Recorder.RecordingStarted += OnStarted;
        Recorder.Flushed += OnFlushed;
        Recorder.RecordingStopped += OnStopped;

        ExpectEqual(Recorder.Start(profile), Error.Ok, "Start returns Ok");
        Expect(Recorder.IsRecording(), "IsRecording after Start");
        Expect(_started, "RecordingStarted was emitted");
        string path = Recorder.GetOutputPath();
        ExpectEqual(path, Dir + "/recording_" + OS.GetName().ToLower() + ".csv", "{platform} is replaced in the output path");
        for (int i = 0; i < Frames; i++)
        {
            Recorder.SetMetric(gauge, i);
            Recorder.AddMetric(counter, 1);
            Recorder.AddMetric(counter, 2);
            await WaitFrames(1);
        }
        Expect(_flushedRows >= 30, $"rows were flushed every 10 frames while recording ({_flushedRows})");
        Recorder.Stop();
        Expect(!Recorder.IsRecording(), "not recording after Stop");
        ExpectEqual(_stoppedPath, path, "RecordingStopped carries the path");
        Recorder.RecordingStarted -= OnStarted;
        Recorder.Flushed -= OnFlushed;
        Recorder.RecordingStopped -= OnStopped;

        string[] names = Recorder.GetColumnNames();
        long rows = Recorder.GetRecordedFrameCount();
        // The frame in which Stop() is called is not recorded.
        Expect(rows >= Frames - 1, $"at least {Frames - 1} rows recorded ({rows})");
        Expect(names.Contains("frame_time"), "columns contain a builtin");
        Expect(names.Contains("time/fps"), "columns contain a monitor");
        Expect(names.Contains(Counter), "columns contain a metric");

        double[] frames = Recorder.GetColumn("frame");
        bool stepIsOne = true;
        for (int i = 1; i < frames.Length; i++)
        {
            if (frames[i] != frames[i - 1] + 1)
            {
                stepIsOne = false;
            }
        }
        Expect(stepIsOne, "the frame column increases by 1 per row");
        double[] frameTimes = Recorder.GetColumn("frame_time");
        Expect(frameTimes[^1] > 0, "frame_time is measured");
        double[] counters = Recorder.GetColumn(Counter);
        Expect(counters.Count(value => value == 3.0) >= Frames - 2, "a counter holds the sum of one frame and resets after each row");
        double[] memory = Recorder.GetColumn("process_memory");
        Expect(memory[^1] > 0, "process_memory is known");
        double[] peak = Recorder.GetColumn("process_memory_peak");
        Expect(peak[^1] >= memory[^1], "process_memory_peak is at least process_memory");
        double average = Recorder.GetColumn("recorder_time").Average();
        Expect(average < 100.0, $"the recorder overhead is small (average {average:F2} us per row)");

        string[] lines = FileAccess.GetFileAsString(path).Trim().Split('\n');
        ExpectEqual(lines[0], string.Join(",", names), "the CSV header matches GetColumnNames");
        ExpectEqual((long)lines.Length, rows + 1, "the CSV has one line per recorded row");
        string[] last = lines[^1].Split(',');
        ExpectEqual(last.Length, names.Length, "a CSV row has one value per column");
        ExpectEqual(last[Array.IndexOf(names, Counter)], "3", "the counter is written as an integer");
    }

    private async Task TestSamplingOptions()
    {
        Performance.AddCustomMonitor("test/custom", Callable.From(() => 7.0));
        var profile = new PerformanceRecordingProfile
        {
            Builtins = [(int)Recorder.Builtin.Frame],
            Monitors = [],
            IncludeCustomMonitors = true,
            SampleIntervalFrames = 2,
            Delimiter = ";",
            OutputPath = Dir + "/sampling.csv",
        };
        await Record(profile, 20);
        long rows = Recorder.GetRecordedFrameCount();
        Expect(rows >= 8 && rows <= 11, $"SampleIntervalFrames = 2 records every second frame ({rows} rows of 20 frames)");
        string header = FileAccess.GetFileAsString(Recorder.GetOutputPath()).Split('\n')[0];
        string[] headerColumns = header.Split(';');
        ExpectEqual(headerColumns[0], "frame", "the delimiter separates the columns");
        Expect(headerColumns.Contains("test/custom"), "IncludeCustomMonitors adds the custom monitors as columns");
        double[] custom = Recorder.GetColumn("test/custom");
        ExpectEqual(custom[0], 7.0, "a custom monitor is recorded");
        Performance.RemoveCustomMonitor("test/custom");
    }

    private async Task TestNoOutputPath()
    {
        var profile = new PerformanceRecordingProfile
        {
            Builtins = [(int)Recorder.Builtin.Frame],
            Monitors = [],
            OutputPath = "",
        };
        Recorder.Start(profile);
        await WaitFrames(2);
        ExpectEqual(Recorder.GetOutputPath(), "", "the rows stay in memory without an output path");
        ExpectEqual(Recorder.Flush(), Error.Unconfigured, "Flush without an output path returns Unconfigured");
        Recorder.Stop();
        Expect(Recorder.GetColumn("frame").Length > 0, "the rows are available after Stop");
    }

    private async Task TestRingBufferFlush()
    {
        var profile = new PerformanceRecordingProfile
        {
            Builtins = [(int)Recorder.Builtin.Frame],
            Monitors = [],
            BufferFrames = 5,
            FlushIntervalFrames = 0,
            OutputPath = Dir + "/last_rows.csv",
        };
        DirAccess.RemoveAbsolute(profile.OutputPath);
        Recorder.Start(profile);
        await WaitFrames(12);
        ExpectEqual(Recorder.GetColumn("frame").Length, 5, "only the last 5 rows are kept");
        Expect(!FileAccess.FileExists(profile.OutputPath), "no file before Flush");
        ExpectEqual(Recorder.Flush(), Error.Ok, "Flush returns Ok");
        Recorder.Stop();
        string[] lines = FileAccess.GetFileAsString(profile.OutputPath).Trim().Split('\n');
        ExpectEqual(lines.Length, 6, "Flush writes the header and the last 5 rows, Stop adds nothing twice");
    }

    // A recording that sets the gauge to 1, 2, ... Frames, one value per frame.
    private async Task RecordRamp(PerformanceRecordingProfile profile)
    {
        int gauge = Recorder.RegisterMetric(Gauge);
        profile.Builtins = [(int)Recorder.Builtin.Frame, (int)Recorder.Builtin.FrameTime];
        profile.Monitors = [];
        profile.Metrics = [Gauge];
        profile.FlushBudgetUsec = 0;
        Recorder.SetMetric(gauge, 1);
        Recorder.Start(profile);
        for (int i = 0; i < Frames; i++)
        {
            Recorder.SetMetric(gauge, i + 1);
            await WaitFrames(1);
        }
        Recorder.Stop();
    }

    private static double ExactPercentile(double[] sorted, double percentile)
    {
        double rank = percentile / 100.0 * (sorted.Length - 1);
        int lower = (int)Math.Floor(rank);
        int upper = Math.Min(lower + 1, sorted.Length - 1);
        return sorted[lower] + (sorted[upper] - sorted[lower]) * (rank - lower);
    }

    private async Task TestSummaryExact()
    {
        var profile = new PerformanceRecordingProfile
        {
            HitchThresholdUsec = 1, // every frame is a hitch
            WriteSummaryJson = true,
            OutputPath = Dir + "/exact.csv",
        };
        await RecordRamp(profile);

        PerformanceSummary summary = Recorder.GetSummary();
        Expect(summary.IsPercentilesExact(), "percentiles are exact while the run fits in the ring buffer");
        Expect(summary.HasColumn(Gauge), "HasColumn finds the metric column");
        Expect(!summary.HasColumn("no_such_column"), "HasColumn is false for an unknown column");
        Expect(summary.GetColumnNames().Contains("frame_time"), "GetColumnNames has frame_time");
        ExpectEqual(summary.GetColumns().Count, summary.GetColumnNames().Length, "GetColumns has one entry per column");
        ExpectEqual(summary.GetRecordedFrameCount(), Recorder.GetRecordedFrameCount(), "the recorded frame count matches the recorder");
        Expect(summary.GetFrameCount() >= Frames - 1, $"GetFrameCount counts the frames of the run ({summary.GetFrameCount()})");
        Expect(summary.GetDurationUsec() > 0, "the run has a duration");

        double[] values = Recorder.GetColumn(Gauge).OrderBy(value => value).ToArray();
        PerformanceColumnSummary column = summary.GetColumn(Gauge);
        ExpectEqual(column.GetName().ToString(), Gauge, "the column summary has the column name");
        Expect(column.IsExact(), "the column percentiles are exact");
        ExpectEqual(column.GetSampleCount(), values.Length, "the sample count equals the recorded rows");
        ExpectEqual(column.GetMinimum(), values[0], "the minimum is exact");
        ExpectEqual(column.GetMaximum(), values[^1], "the maximum is exact");
        ExpectEqual((float)column.GetMean(), (float)values.Average(), "the mean is exact");
        ExpectEqual((float)column.GetP50(), (float)ExactPercentile(values, 50.0), "p50 is exact");
        ExpectEqual((float)column.GetP95(), (float)ExactPercentile(values, 95.0), "p95 is exact");
        ExpectEqual((float)column.GetP99(), (float)ExactPercentile(values, 99.0), "p99 is exact");
        ExpectEqual((float)column.GetPercentile(25.0), (float)ExactPercentile(values, 25.0), "any percentile is exact");

        long[] histogram = column.GetHistogram([10.0, 20.0, 30.0]);
        ExpectEqual(histogram.Length, 4, "a histogram with 3 limits has 4 ranges");
        ExpectEqual((int)histogram.Sum(), values.Length, "the histogram counts every sample");

        ExpectEqual(summary.GetHitchThresholdUsec(), 1, "the hitch threshold is taken from the profile");
        Expect(summary.GetHitchCount() >= Frames - 1, $"every frame is a hitch ({summary.GetHitchCount()})");
        ExpectEqual(summary.GetHitchFrames().Length, summary.GetHitchCount(), "the hitch frames are listed");

        string jsonPath = Recorder.GetOutputPath().GetBaseName() + ".summary.json";
        Expect(FileAccess.FileExists(jsonPath), "the summary JSON is written next to the CSV");
        Godot.Collections.Dictionary parsed = Json.ParseString(FileAccess.GetFileAsString(jsonPath)).AsGodotDictionary();
        Godot.Collections.Dictionary columns = parsed["columns"].AsGodotDictionary();
        Expect(columns.ContainsKey(Gauge), "the JSON has the metric column");
        Expect(parsed.ContainsKey("frame_time_histogram"), "the JSON has the frame-time histogram");
        int jsonHitches = (int)parsed["hitches"].AsGodotDictionary()["count"].AsDouble();
        ExpectEqual(jsonHitches, summary.GetHitchCount(), "the JSON has the hitch count");
        Godot.Collections.Dictionary fromText = Json.ParseString(summary.ToText()).AsGodotDictionary();
        ExpectEqual(fromText.Count, parsed.Count, "ToText is the content of the JSON file");
    }

    private async Task TestSummaryHistogram()
    {
        var profile = new PerformanceRecordingProfile
        {
            BufferFrames = 8,
            FlushIntervalFrames = 4,
            HitchThresholdUsec = 0, // no hitches
            OutputPath = Dir + "/histogram.csv",
        };
        await RecordRamp(profile);

        PerformanceSummary summary = Recorder.GetSummary();
        Expect(!summary.IsPercentilesExact(), "percentiles come from the histogram when the run is longer than the ring buffer");
        ExpectEqual(summary.GetHitchCount(), 0, "a threshold of 0 disables hitches");
        PerformanceColumnSummary column = summary.GetColumn(Gauge);
        Expect(!column.IsExact(), "the column uses the histogram");
        Expect(column.GetSampleCount() > 8, $"the statistics cover the whole run, not only the ring buffer ({column.GetSampleCount()})");
        ExpectEqual(column.GetMinimum(), 1.0, "the minimum covers the whole run");
        ExpectEqual(column.GetMaximum(), (double)Frames, "the maximum covers the whole run");
        double median = (1.0 + Frames) / 2.0;
        Expect(Math.Abs(column.GetP50() - median) <= median * 0.15, $"the histogram p50 is close to the exact median ({column.GetP50()} vs {median})");
        Expect(column.GetP99() <= column.GetMaximum(), "p99 stays below the maximum");
        Expect(column.GetP50() >= column.GetMinimum(), "p50 stays above the minimum");
        string jsonPath = Recorder.GetOutputPath().GetBaseName() + ".summary.json";
        Expect(!FileAccess.FileExists(jsonPath), "no summary JSON without WriteSummaryJson");
    }

    // Uses the summary of the last recording: the ramp 1..Frames without hitches.
    private void TestBudgetOnRecording()
    {
        var budget = new PerformanceBudget();
        budget.AddRule(new PerformanceBudgetRule { Column = Gauge, Statistic = PerformanceBudgetRule.StatisticEnum.Maximum, Limit = Frames });
        budget.AddRule(new PerformanceBudgetRule { Statistic = PerformanceBudgetRule.StatisticEnum.HitchCount, Limit = 0 });
        PerformanceBudgetResult result = Recorder.CheckBudget(budget);
        Expect(result.IsPassed(), $"the budget passes: {result.ToText()}");
        ExpectEqual(result.GetExitCode(), 0, "exit code 0 when passed");
        ExpectEqual(result.GetCheckedRuleCount(), 2, "both rules are checked");
        ExpectEqual(result.GetFailureCount(), 0, "no failures when passed");

        var failRule = new PerformanceBudgetRule { Column = Gauge, Statistic = PerformanceBudgetRule.StatisticEnum.Mean, Limit = 5.0 };
        budget.AddRule(failRule);
        result = Recorder.CheckBudget(budget);
        Expect(!result.IsPassed(), "the budget fails with a mean over the limit");
        ExpectEqual(result.GetFailureCount(), 1, "one failure");
        ExpectEqual(result.GetExitCode(), 1, "exit code 1 when failed");
        Expect(result.ToText().Contains(failRule.GetDescription()), "ToText names the failed rule");
        PerformanceBudgetFailure failure = result.GetFailures()[0];
        ExpectEqual(failure.GetReason(), PerformanceBudgetFailure.Reason.OverLimit, "the reason is OverLimit");
        Expect(failure.GetRule() == failRule, "the failure names its rule");
        ExpectEqual(failure.GetLimit(), 5.0, "the failure carries the limit");
        Expect(failure.GetActualValue() > 5.0, "the failure carries the actual value");
        Expect(failure.GetMessage().Contains(failRule.GetDescription()), "the message names the rule");
    }

    private void TestBudgetResource()
    {
        var budget = new PerformanceBudget();
        var rule = new PerformanceBudgetRule { Column = "missing_column", Statistic = PerformanceBudgetRule.StatisticEnum.Maximum, Limit = 123.0 };
        budget.AddRule(rule);
        string path = Dir + "/budget.tres";
        ExpectEqual(ResourceSaver.Save(budget, path), Error.Ok, "a budget can be saved");
        var loaded = (PerformanceBudget)ResourceLoader.Load(path, "", ResourceLoader.CacheMode.Ignore);
        ExpectEqual(loaded.GetRuleCount(), 1, "the loaded budget has one rule");
        PerformanceBudgetRule loadedRule = loaded.Rules[0];
        ExpectEqual(loadedRule.Statistic, PerformanceBudgetRule.StatisticEnum.Maximum, "the rule statistic is saved");
        ExpectEqual(loadedRule.Limit, 123.0, "the rule limit is saved");

        // An empty summary: a rule on a missing column fails, a hitch rule passes.
        budget.AddRule(new PerformanceBudgetRule { Statistic = PerformanceBudgetRule.StatisticEnum.HitchCount, Limit = 0 });
        PerformanceBudgetResult result = budget.CheckSummary(new PerformanceSummary());
        Expect(!result.IsPassed(), "a rule on a missing column fails");
        ExpectEqual(result.GetFailureCount(), 1, "only the column rule fails");
        PerformanceBudgetFailure failure = result.GetFailures()[0];
        ExpectEqual(failure.GetReason(), PerformanceBudgetFailure.Reason.ColumnMissing, "the reason is ColumnMissing");
    }

    private async Task TestDotnet()
    {
        Expect(Recorder.HasMetricSource("dotnet"), "the .NET module registers the metric source dotnet");
        foreach (string value in DotnetValues)
        {
            Expect(Performance.HasCustomMonitor("dotnet/" + value), $"custom monitor dotnet/{value} exists");
        }
        foreach (string value in DotnetDeltas)
        {
            Expect(!Performance.HasCustomMonitor("dotnet/" + value), $"delta {value} is recorder-only");
        }
        var allocated = (double)Performance.GetCustomMonitor("dotnet/gc_allocated_bytes");
        Expect(allocated > 0, $"dotnet/gc_allocated_bytes is above 0 ({allocated})");

        var profile = new PerformanceRecordingProfile
        {
            Builtins = [(int)Recorder.Builtin.Frame],
            Monitors = [],
            MetricSources = ["dotnet"],
            FlushIntervalFrames = 0,
            OutputPath = "",
        };
        await Record(profile, 10);
        string[] names = Recorder.GetColumnNames();
        foreach (string value in DotnetValues.Concat(DotnetDeltas))
        {
            Expect(names.Contains("dotnet/" + value), $"column dotnet/{value} exists");
        }
        double[] totals = Recorder.GetColumn("dotnet/gc_allocated_bytes");
        bool growing = true;
        for (int i = 1; i < totals.Length; i++)
        {
            if (totals[i] < totals[i - 1])
            {
                growing = false;
            }
        }
        Expect(growing, "dotnet/gc_allocated_bytes is a growing total");
        double[] deltas = Recorder.GetColumn("dotnet/gc_allocated_bytes_delta");
        Expect(double.IsNaN(deltas[0]), "the first delta of a recording is empty");
        Expect(deltas[^1] >= 0, "later deltas are values of 0 or more");
    }

    private void OnStarted()
    {
        _started = true;
    }

    private void OnFlushed(long rows)
    {
        _flushedRows += rows;
    }

    private void OnStopped(string path)
    {
        _stoppedPath = path;
    }
}
