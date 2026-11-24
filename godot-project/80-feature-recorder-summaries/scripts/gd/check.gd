extends FeatureCheck
## GDScript checks of 80-feature-recorder-summaries. Check the same things as scripts/cs/Issue80FeatureRecorderSummariesCheck.cs.

const DIR := "user://80-feature-recorder-summaries"
const FRAMES := 40


func _get_check_name() -> String:
	return "80-feature-recorder-summaries"


func _run_checks() -> void:
	_check_bindings()
	_check_defaults()
	_check_budget_resource()
	await _check_exact_summary()
	await _check_histogram_summary()
	await _check_budget_on_recording()


func _check_bindings() -> void:
	for cls in ["PerformanceSummary", "PerformanceColumnSummary", "PerformanceBudget", "PerformanceBudgetRule",
			"PerformanceBudgetResult", "PerformanceBudgetFailure"]:
		expect(ClassDB.class_exists(cls), "%s is registered" % cls)
	for method in ["get_summary", "check_budget"]:
		expect(ClassDB.class_has_method("PerformanceRecorder", method), "PerformanceRecorder.%s() is bound" % method)
	expect(ProjectSettings.has_setting("debug/performance_recording/budget"), "project setting debug/performance_recording/budget exists")
	expect_equal(PerformanceBudgetRule.STATISTIC_MAX, 5, "5 statistics")
	expect_equal(PerformanceBudgetFailure.REASON_MAX, 3, "3 failure reasons")


func _check_defaults() -> void:
	var profile := PerformanceRecordingProfile.new()
	expect_equal(profile.hitch_threshold_usec, 50000, "default hitch threshold is 50000 µs")
	expect_equal(profile.write_summary_json, false, "summary JSON is off by default")
	var rule := PerformanceBudgetRule.new()
	expect_equal(rule.column, "frame_time", "default rule column is frame_time")
	expect_equal(rule.statistic, PerformanceBudgetRule.STATISTIC_PERCENTILE, "default rule statistic is a percentile")
	expect_equal(rule.percentile, 95.0, "default percentile is 95")
	expect_equal(rule.limit, 16667.0, "default limit is 16667 µs")
	expect_equal(rule.get_description(), "frame_time p95", "rule description")
	rule.percentile = 150.0
	expect_equal(rule.percentile, 100.0, "percentile is clamped to 100")


func _check_budget_resource() -> void:
	var budget := PerformanceBudget.new()
	var rule := PerformanceBudgetRule.new()
	rule.column = "check_summary_gauge"
	rule.statistic = PerformanceBudgetRule.STATISTIC_MAXIMUM
	rule.limit = 123.0
	budget.add_rule(rule)
	DirAccess.make_dir_recursive_absolute(DIR)
	var path := DIR + "/budget_gd.tres"
	expect_equal(ResourceSaver.save(budget, path), OK, "a budget can be saved")
	var loaded := ResourceLoader.load(path, "", ResourceLoader.CACHE_MODE_IGNORE) as PerformanceBudget
	expect(loaded != null, "the saved budget loads as PerformanceBudget")
	if loaded:
		expect_equal(loaded.get_rule_count(), 1, "the loaded budget has one rule")
		var loaded_rule := loaded.rules[0] as PerformanceBudgetRule
		expect_equal(loaded_rule.statistic, PerformanceBudgetRule.STATISTIC_MAXIMUM, "the rule statistic is saved")
		expect_equal(loaded_rule.limit, 123.0, "the rule limit is saved")

	# An empty summary: every column rule fails with REASON_COLUMN_MISSING, hitch rules pass.
	var result := budget.check_summary(PerformanceSummary.new())
	expect(not result.is_passed(), "a rule on a missing column fails")
	expect_equal(result.get_exit_code(), 1, "exit code 1 on failure")
	expect_equal((result.get_failures()[0] as PerformanceBudgetFailure).get_reason(), PerformanceBudgetFailure.REASON_COLUMN_MISSING, "reason is REASON_COLUMN_MISSING")


func _make_profile(file_name: String) -> PerformanceRecordingProfile:
	var profile := PerformanceRecordingProfile.new()
	profile.builtins = PackedInt32Array([PerformanceRecorder.BUILTIN_FRAME, PerformanceRecorder.BUILTIN_FRAME_TIME])
	profile.monitors = PackedInt32Array()
	profile.metrics = PackedStringArray(["check_summary_gauge"])
	profile.flush_budget_usec = 0
	profile.output_path = DIR + "/" + file_name
	return profile


# Sets the gauge to 1, 2, ... FRAMES, one value per frame.
func _record_ramp(profile: PerformanceRecordingProfile) -> void:
	var gauge := PerformanceRecorder.register_metric(&"check_summary_gauge")
	PerformanceRecorder.set_metric(gauge, 1)
	expect_equal(PerformanceRecorder.start(profile), OK, "start() returns OK")
	for i in FRAMES:
		PerformanceRecorder.set_metric(gauge, i + 1)
		await get_tree().process_frame
	PerformanceRecorder.stop()


func _exact_percentile(sorted: Array, percentile: float) -> float:
	var rank := percentile / 100.0 * (sorted.size() - 1)
	var lower := int(floor(rank))
	var upper := mini(lower + 1, sorted.size() - 1)
	return lerpf(sorted[lower], sorted[upper], rank - lower)


func _check_exact_summary() -> void:
	var profile := _make_profile("exact_gd.csv")
	profile.hitch_threshold_usec = 1 # Every frame is a hitch.
	profile.write_summary_json = true
	await _record_ramp(profile)

	var summary := PerformanceRecorder.get_summary()
	expect(summary.is_percentiles_exact(), "percentiles are exact while the run fits in the ring buffer")
	expect(summary.has_column("check_summary_gauge"), "the summary has the metric column")
	expect(summary.get_column_names().has("frame_time"), "the summary has the frame_time column")
	expect_equal(summary.get_recorded_frame_count(), PerformanceRecorder.get_recorded_frame_count(), "recorded frame count matches the recorder")
	expect(summary.get_duration_usec() > 0, "the run has a duration")

	var values := Array(PerformanceRecorder.get_column(&"check_summary_gauge"))
	values.sort()
	var column := summary.get_column("check_summary_gauge")
	expect(column.is_exact(), "the column percentiles are exact")
	expect_equal(column.get_sample_count(), values.size(), "sample count equals the recorded rows")
	expect_equal(column.get_minimum(), values[0], "minimum is exact")
	expect_equal(column.get_maximum(), values[values.size() - 1], "maximum is exact")
	var total := 0.0
	for value in values:
		total += value
	expect(is_equal_approx(column.get_mean(), total / values.size()), "mean is exact")
	expect(is_equal_approx(column.get_p50(), _exact_percentile(values, 50.0)), "p50 is exact (%s)" % column.get_p50())
	expect(is_equal_approx(column.get_p95(), _exact_percentile(values, 95.0)), "p95 is exact (%s)" % column.get_p95())
	expect(is_equal_approx(column.get_p99(), _exact_percentile(values, 99.0)), "p99 is exact (%s)" % column.get_p99())
	expect(is_equal_approx(column.get_percentile(25.0), _exact_percentile(values, 25.0)), "any percentile is exact")

	var histogram := column.get_histogram(PackedFloat64Array([10.0, 20.0, 30.0]))
	expect_equal(histogram.size(), 4, "a histogram with 3 limits has 4 ranges")
	var counted := 0
	for count in histogram:
		counted += count
	expect_equal(counted, values.size(), "the histogram counts every sample")

	# Hitches: the threshold of 1 µs makes every frame a hitch.
	expect_equal(summary.get_hitch_threshold_usec(), 1, "hitch threshold is taken from the profile")
	expect(summary.get_hitch_count() >= FRAMES - 1, "every frame is a hitch (%d)" % summary.get_hitch_count())
	expect_equal(summary.get_hitch_frames().size(), summary.get_hitch_count(), "hitch frames are listed")

	# JSON next to the CSV.
	var json_path := PerformanceRecorder.get_output_path().get_basename() + ".summary.json"
	expect(FileAccess.file_exists(json_path), "the summary JSON is written next to the CSV")
	var parsed: Variant = JSON.parse_string(FileAccess.get_file_as_string(json_path))
	expect(parsed is Dictionary, "the summary JSON parses")
	if parsed is Dictionary:
		var columns: Dictionary = parsed.get("columns", {})
		expect(columns.has("check_summary_gauge"), "the JSON has the metric column")
		expect(parsed.has("frame_time_histogram"), "the JSON has the frame-time histogram")
		expect_equal(int(parsed["hitches"]["count"]), summary.get_hitch_count(), "the JSON has the hitch count")
	expect_equal(JSON.parse_string(summary.to_text()), parsed, "to_text() is the JSON file content")


func _check_histogram_summary() -> void:
	var profile := _make_profile("histogram_gd.csv")
	profile.buffer_frames = 8
	profile.flush_interval_frames = 4
	profile.hitch_threshold_usec = 0 # No hitches.
	await _record_ramp(profile)

	var summary := PerformanceRecorder.get_summary()
	expect(not summary.is_percentiles_exact(), "percentiles come from the histogram when the run is longer than the ring buffer")
	expect_equal(summary.get_hitch_count(), 0, "a threshold of 0 disables hitches")
	var column := summary.get_column("check_summary_gauge")
	expect(not column.is_exact(), "the column uses the histogram")
	expect(column.get_sample_count() > 8, "the statistics cover the whole run, not only the ring buffer (%d)" % column.get_sample_count())
	expect_equal(column.get_minimum(), 1.0, "the minimum covers the whole run")
	expect_equal(column.get_maximum(), float(FRAMES), "the maximum covers the whole run")
	var median := (1.0 + FRAMES) / 2.0
	expect(absf(column.get_p50() - median) <= median * 0.15, "histogram p50 is close to the exact median (%s vs %s)" % [column.get_p50(), median])
	expect(column.get_p99() <= column.get_maximum() and column.get_p50() >= column.get_minimum(), "estimates stay within min and max")
	expect(not FileAccess.file_exists(PerformanceRecorder.get_output_path().get_basename() + ".summary.json"), "no summary JSON without write_summary_json")


func _check_budget_on_recording() -> void:
	# Uses the summary of the last recording (the ramp 1..FRAMES).
	var budget := PerformanceBudget.new()
	var pass_rule := PerformanceBudgetRule.new()
	pass_rule.column = "check_summary_gauge"
	pass_rule.statistic = PerformanceBudgetRule.STATISTIC_MAXIMUM
	pass_rule.limit = FRAMES
	budget.add_rule(pass_rule)
	var hitch_rule := PerformanceBudgetRule.new()
	hitch_rule.statistic = PerformanceBudgetRule.STATISTIC_HITCH_COUNT
	hitch_rule.limit = 0
	budget.add_rule(hitch_rule)

	var result := PerformanceRecorder.check_budget(budget)
	expect(result.is_passed(), "the budget passes: %s" % result.to_text())
	expect_equal(result.get_exit_code(), 0, "exit code 0 when passed")
	expect_equal(result.get_checked_rule_count(), 2, "both rules are checked")

	var fail_rule := PerformanceBudgetRule.new()
	fail_rule.column = "check_summary_gauge"
	fail_rule.statistic = PerformanceBudgetRule.STATISTIC_MEAN
	fail_rule.limit = 5.0
	budget.add_rule(fail_rule)
	result = PerformanceRecorder.check_budget(budget)
	expect(not result.is_passed(), "the budget fails with a mean over the limit")
	expect_equal(result.get_failure_count(), 1, "one failure")
	expect_equal(result.get_exit_code(), 1, "exit code 1 when failed")
	var failure := result.get_failures()[0] as PerformanceBudgetFailure
	expect_equal(failure.get_reason(), PerformanceBudgetFailure.REASON_OVER_LIMIT, "reason is REASON_OVER_LIMIT")
	expect_equal(failure.get_rule(), fail_rule, "the failure names its rule")
	expect_equal(failure.get_limit(), 5.0, "the failure carries the limit")
	expect(failure.get_actual_value() > 5.0, "the failure carries the actual value")
	print("  " + result.to_text().replace("\n", "\n  "))
