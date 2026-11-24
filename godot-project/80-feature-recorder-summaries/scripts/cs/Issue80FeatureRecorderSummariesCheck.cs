using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.Issue80FeatureRecorderSummaries;

/// <summary>C# checks of 80-feature-recorder-summaries. Check the same things as scripts/gd/check.gd.</summary>
public partial class Issue80FeatureRecorderSummariesCheck : FeatureCheckCs
{
    private const string Dir = "user://80-feature-recorder-summaries";
    private const int Frames = 40;
    private const string Gauge = "check_summary_gauge_cs";

    protected override string CheckName => "80-feature-recorder-summaries";

    protected override async Task RunChecks()
    {
        CheckBindings();
        CheckDefaults();
        CheckBudgetResource();
        await CheckExactSummary();
        await CheckHistogramSummary();
        CheckBudgetOnRecording();
    }

    private void CheckBindings()
    {
        string[] classes =
        [
            "PerformanceSummary", "PerformanceColumnSummary", "PerformanceBudget", "PerformanceBudgetRule",
            "PerformanceBudgetResult", "PerformanceBudgetFailure",
        ];
        foreach (string cls in classes)
        {
            Expect(ClassDB.ClassExists(cls), $"{cls} is registered");
        }
        foreach (string method in new[] { "get_summary", "check_budget" })
        {
            Expect(ClassDB.ClassHasMethod("PerformanceRecorder", method), $"PerformanceRecorder.{method}() is bound");
        }
        Expect(ProjectSettings.HasSetting("debug/performance_recording/budget"), "project setting debug/performance_recording/budget exists");
        ExpectEqual((int)PerformanceBudgetRule.StatisticEnum.Max, 5, "5 statistics");
        ExpectEqual((int)PerformanceBudgetFailure.Reason.Max, 3, "3 failure reasons");
    }

    private void CheckDefaults()
    {
        var profile = new PerformanceRecordingProfile();
        ExpectEqual(profile.HitchThresholdUsec, 50000, "default hitch threshold is 50000 µs");
        ExpectEqual(profile.WriteSummaryJson, false, "summary JSON is off by default");
        var rule = new PerformanceBudgetRule();
        ExpectEqual(rule.Column, "frame_time", "default rule column is frame_time");
        ExpectEqual(rule.Statistic, PerformanceBudgetRule.StatisticEnum.Percentile, "default rule statistic is a percentile");
        ExpectEqual((float)rule.Percentile, 95.0f, "default percentile is 95");
        ExpectEqual((float)rule.Limit, 16667.0f, "default limit is 16667 µs");
        ExpectEqual(rule.GetDescription(), "frame_time p95", "rule description");
        rule.Percentile = 150.0;
        ExpectEqual((float)rule.Percentile, 100.0f, "percentile is clamped to 100");
    }

    private void CheckBudgetResource()
    {
        var budget = new PerformanceBudget();
        var rule = new PerformanceBudgetRule
        {
            Column = Gauge,
            Statistic = PerformanceBudgetRule.StatisticEnum.Maximum,
            Limit = 123.0,
        };
        budget.AddRule(rule);
        DirAccess.MakeDirRecursiveAbsolute(Dir);
        string path = Dir + "/budget_cs.tres";
        ExpectEqual(ResourceSaver.Save(budget, path), Error.Ok, "a budget can be saved");
        var loaded = ResourceLoader.Load(path, "", ResourceLoader.CacheMode.Ignore) as PerformanceBudget;
        Expect(loaded != null, "the saved budget loads as PerformanceBudget");
        if (loaded != null)
        {
            ExpectEqual(loaded.GetRuleCount(), 1, "the loaded budget has one rule");
            PerformanceBudgetRule loadedRule = loaded.Rules[0];
            ExpectEqual(loadedRule.Statistic, PerformanceBudgetRule.StatisticEnum.Maximum, "the rule statistic is saved");
            ExpectEqual((float)loadedRule.Limit, 123.0f, "the rule limit is saved");
        }

        // An empty summary: every column rule fails with Reason.ColumnMissing.
        PerformanceBudgetResult result = budget.CheckSummary(new PerformanceSummary());
        Expect(!result.IsPassed(), "a rule on a missing column fails");
        ExpectEqual(result.GetExitCode(), 1, "exit code 1 on failure");
        ExpectEqual(result.GetFailures()[0].GetReason(), PerformanceBudgetFailure.Reason.ColumnMissing, "reason is ColumnMissing");
    }

    private static PerformanceRecordingProfile MakeProfile(string fileName)
    {
        return new PerformanceRecordingProfile
        {
            Builtins = [(int)PerformanceRecorder.Builtin.Frame, (int)PerformanceRecorder.Builtin.FrameTime],
            Monitors = [],
            Metrics = [Gauge],
            FlushBudgetUsec = 0,
            OutputPath = Dir + "/" + fileName,
        };
    }

    // Sets the gauge to 1, 2, ... Frames, one value per frame.
    private async Task RecordRamp(PerformanceRecordingProfile profile)
    {
        int gauge = PerformanceRecorder.RegisterMetric(Gauge);
        PerformanceRecorder.SetMetric(gauge, 1);
        ExpectEqual(PerformanceRecorder.Start(profile), Error.Ok, "Start() returns OK");
        for (int i = 0; i < Frames; i++)
        {
            PerformanceRecorder.SetMetric(gauge, i + 1);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        PerformanceRecorder.Stop();
    }

    private static double ExactPercentile(double[] sorted, double percentile)
    {
        double rank = percentile / 100.0 * (sorted.Length - 1);
        int lower = (int)Math.Floor(rank);
        int upper = Math.Min(lower + 1, sorted.Length - 1);
        return sorted[lower] + (sorted[upper] - sorted[lower]) * (rank - lower);
    }

    private async Task CheckExactSummary()
    {
        PerformanceRecordingProfile profile = MakeProfile("exact_cs.csv");
        profile.HitchThresholdUsec = 1; // Every frame is a hitch.
        profile.WriteSummaryJson = true;
        await RecordRamp(profile);

        PerformanceSummary summary = PerformanceRecorder.GetSummary();
        Expect(summary.IsPercentilesExact(), "percentiles are exact while the run fits in the ring buffer");
        Expect(summary.HasColumn(Gauge), "the summary has the metric column");
        Expect(summary.GetColumnNames().Contains("frame_time"), "the summary has the frame_time column");
        ExpectEqual(summary.GetRecordedFrameCount(), PerformanceRecorder.GetRecordedFrameCount(), "recorded frame count matches the recorder");
        Expect(summary.GetDurationUsec() > 0, "the run has a duration");

        double[] values = PerformanceRecorder.GetColumn(Gauge).OrderBy(v => v).ToArray();
        PerformanceColumnSummary column = summary.GetColumn(Gauge);
        Expect(column.IsExact(), "the column percentiles are exact");
        ExpectEqual(column.GetSampleCount(), (long)values.Length, "sample count equals the recorded rows");
        ExpectEqual((float)column.GetMinimum(), (float)values[0], "minimum is exact");
        ExpectEqual((float)column.GetMaximum(), (float)values[^1], "maximum is exact");
        ExpectEqual((float)column.GetMean(), (float)values.Average(), "mean is exact");
        ExpectEqual((float)column.GetP50(), (float)ExactPercentile(values, 50.0), "p50 is exact");
        ExpectEqual((float)column.GetP95(), (float)ExactPercentile(values, 95.0), "p95 is exact");
        ExpectEqual((float)column.GetP99(), (float)ExactPercentile(values, 99.0), "p99 is exact");
        ExpectEqual((float)column.GetPercentile(25.0), (float)ExactPercentile(values, 25.0), "any percentile is exact");

        long[] histogram = column.GetHistogram([10.0, 20.0, 30.0]);
        ExpectEqual(histogram.Length, 4, "a histogram with 3 limits has 4 ranges");
        ExpectEqual(histogram.Sum(), (long)values.Length, "the histogram counts every sample");

        // Hitches: the threshold of 1 µs makes every frame a hitch.
        ExpectEqual(summary.GetHitchThresholdUsec(), 1L, "hitch threshold is taken from the profile");
        Expect(summary.GetHitchCount() >= Frames - 1, $"every frame is a hitch ({summary.GetHitchCount()})");
        ExpectEqual((long)summary.GetHitchFrames().Length, summary.GetHitchCount(), "hitch frames are listed");

        // JSON next to the CSV.
        string jsonPath = PerformanceRecorder.GetOutputPath().GetBaseName() + ".summary.json";
        Expect(FileAccess.FileExists(jsonPath), "the summary JSON is written next to the CSV");
        Variant parsed = Json.ParseString(FileAccess.GetFileAsString(jsonPath));
        Expect(parsed.VariantType == Variant.Type.Dictionary, "the summary JSON parses");
        if (parsed.VariantType == Variant.Type.Dictionary)
        {
            Godot.Collections.Dictionary root = parsed.AsGodotDictionary();
            Expect(root["columns"].AsGodotDictionary().ContainsKey(Gauge), "the JSON has the metric column");
            Expect(root.ContainsKey("frame_time_histogram"), "the JSON has the frame-time histogram");
            ExpectEqual((long)root["hitches"].AsGodotDictionary()["count"].AsDouble(), summary.GetHitchCount(), "the JSON has the hitch count");
        }
        ExpectEqual(summary.ToText(), FileAccess.GetFileAsString(jsonPath), "ToText() is the JSON file content");
    }

    private async Task CheckHistogramSummary()
    {
        PerformanceRecordingProfile profile = MakeProfile("histogram_cs.csv");
        profile.BufferFrames = 8;
        profile.FlushIntervalFrames = 4;
        profile.HitchThresholdUsec = 0; // No hitches.
        await RecordRamp(profile);

        PerformanceSummary summary = PerformanceRecorder.GetSummary();
        Expect(!summary.IsPercentilesExact(), "percentiles come from the histogram when the run is longer than the ring buffer");
        ExpectEqual(summary.GetHitchCount(), 0L, "a threshold of 0 disables hitches");
        PerformanceColumnSummary column = summary.GetColumn(Gauge);
        Expect(!column.IsExact(), "the column uses the histogram");
        Expect(column.GetSampleCount() > 8, $"the statistics cover the whole run, not only the ring buffer ({column.GetSampleCount()})");
        ExpectEqual((float)column.GetMinimum(), 1.0f, "the minimum covers the whole run");
        ExpectEqual((float)column.GetMaximum(), (float)Frames, "the maximum covers the whole run");
        double median = (1.0 + Frames) / 2.0;
        Expect(Math.Abs(column.GetP50() - median) <= median * 0.15, $"histogram p50 is close to the exact median ({column.GetP50()} vs {median})");
        Expect(column.GetP99() <= column.GetMaximum() && column.GetP50() >= column.GetMinimum(), "estimates stay within min and max");
        Expect(!FileAccess.FileExists(PerformanceRecorder.GetOutputPath().GetBaseName() + ".summary.json"), "no summary JSON without WriteSummaryJson");
    }

    private void CheckBudgetOnRecording()
    {
        // Uses the summary of the last recording (the ramp 1..Frames).
        var budget = new PerformanceBudget();
        budget.AddRule(new PerformanceBudgetRule { Column = Gauge, Statistic = PerformanceBudgetRule.StatisticEnum.Maximum, Limit = Frames });
        budget.AddRule(new PerformanceBudgetRule { Statistic = PerformanceBudgetRule.StatisticEnum.HitchCount, Limit = 0 });

        PerformanceBudgetResult result = PerformanceRecorder.CheckBudget(budget);
        Expect(result.IsPassed(), $"the budget passes: {result.ToText()}");
        ExpectEqual(result.GetExitCode(), 0, "exit code 0 when passed");
        ExpectEqual(result.GetCheckedRuleCount(), 2, "both rules are checked");

        var failRule = new PerformanceBudgetRule { Column = Gauge, Statistic = PerformanceBudgetRule.StatisticEnum.Mean, Limit = 5.0 };
        budget.AddRule(failRule);
        result = PerformanceRecorder.CheckBudget(budget);
        Expect(!result.IsPassed(), "the budget fails with a mean over the limit");
        ExpectEqual(result.GetFailureCount(), 1, "one failure");
        ExpectEqual(result.GetExitCode(), 1, "exit code 1 when failed");
        PerformanceBudgetFailure failure = result.GetFailures()[0];
        ExpectEqual(failure.GetReason(), PerformanceBudgetFailure.Reason.OverLimit, "reason is OverLimit");
        Expect(failure.GetRule() == failRule, "the failure names its rule");
        ExpectEqual((float)failure.GetLimit(), 5.0f, "the failure carries the limit");
        Expect(failure.GetActualValue() > 5.0, "the failure carries the actual value");
        GD.Print("  " + result.ToText().Replace("\n", "\n  "));
    }
}
