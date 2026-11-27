extends FeatureTest
## Full API test of Performance Recorder (PerformanceRecorder, PerformanceRecordingProfile, PerformanceSummary,
## PerformanceColumnSummary, PerformanceBudget, PerformanceBudgetRule, PerformanceBudgetResult, PerformanceBudgetFailure,
## and the "dotnet" metric source). Same tests as scripts/cs/Test.cs.

const DIR := "user://performance-recorder-test"
const FRAMES := 40
const GAUGE := &"test_gauge"
const COUNTER := &"test_counter"
const FRAME_PROFILE_PATH := "res://performance-recorder/resources/frame_timing_profile.tres"
const DOTNET_PROFILE_PATH := "res://performance-recorder/resources/dotnet_profile.tres"
const BUDGET_PATH := "res://performance-recorder/resources/60_fps_budget.tres"
const DOTNET_VALUES := [
	"gc_allocated_bytes", "gc_total_memory_bytes", "gc_heap_size_bytes", "gc_fragmented_bytes", "gc_committed_bytes",
	"gc_pause_time_percent", "gc_pause_total_ms", "gc_collections_gen0", "gc_collections_gen1", "gc_collections_gen2",
	"threadpool_threads", "threadpool_pending", "threadpool_completed", "lock_contentions", "jit_compiled_methods",
]
const DOTNET_DELTAS := ["gc_allocated_bytes_delta", "gc_collections_delta", "gc_pause_delta_ms"]

var _started := false
var _flushed_rows := 0
var _stopped_path := ""


func _run_tests() -> void:
	_test_bindings()
	_test_profile_defaults()
	_test_rule_defaults()
	_test_sample_resources()
	await _test_recording()
	await _test_sampling_options()
	await _test_no_output_path()
	await _test_ring_buffer_flush()
	await _test_summary_exact()
	await _test_summary_histogram()
	_test_budget_on_recording()
	_test_budget_resource()
	await _test_dotnet()


func _record(profile: PerformanceRecordingProfile, frames: int) -> void:
	PerformanceRecorder.start(profile)
	for i in frames:
		await get_tree().process_frame
	PerformanceRecorder.stop()


func _test_bindings() -> void:
	expect(Engine.has_singleton("PerformanceRecorder"), "PerformanceRecorder is a singleton")
	for setting in ["enabled", "profile", "output_path", "budget"]:
		expect(ProjectSettings.has_setting("debug/performance_recording/" + setting), "project setting debug/performance_recording/%s exists" % setting)
	expect_equal(PerformanceRecorder.BUILTIN_MAX, 10, "10 builtins")
	expect_equal(PerformanceBudgetRule.STATISTIC_MAX, 5, "5 statistics")
	expect_equal(PerformanceBudgetFailure.REASON_MAX, 3, "3 failure reasons")
	expect(not PerformanceRecorder.has_metric_source(&"no_such_source"), "has_metric_source is false for an unknown source")
	expect_equal(PerformanceRecorder.get_metric_id(&"no_such_metric"), -1, "get_metric_id is -1 for an unknown metric")
	expect(not PerformanceRecorder.is_recording(), "not recording at the start")


func _test_profile_defaults() -> void:
	var profile := PerformanceRecordingProfile.new()
	expect_equal(profile.builtins.size(), 8, "default profile has 8 builtins")
	expect_equal(profile.monitors.size(), 8, "default profile has 8 monitors")
	expect(profile.metrics.is_empty(), "default profile has no metrics")
	expect(profile.metric_sources.is_empty(), "default profile has no metric sources")
	expect_equal(profile.include_custom_monitors, false, "custom monitors are off by default")
	expect_equal(profile.sample_interval_frames, 1, "default sample interval is 1 frame")
	expect_equal(profile.process_memory_interval_frames, 30, "default process memory interval is 30 frames")
	expect_equal(profile.buffer_frames, 3600, "default buffer is 3600 frames")
	expect_equal(profile.flush_interval_frames, 300, "default flush interval is 300 frames")
	expect_equal(profile.flush_budget_usec, 2000, "default flush budget is 2000 us")
	expect_equal(profile.delimiter, ",", "default delimiter is a comma")
	expect_equal(profile.output_path, "user://performance/{date}_{time}.csv", "default output path")
	expect_equal(profile.hitch_threshold_usec, 50000, "default hitch threshold is 50000 us")
	expect_equal(profile.write_summary_json, false, "summary JSON is off by default")


func _test_rule_defaults() -> void:
	var rule := PerformanceBudgetRule.new()
	expect_equal(rule.column, "frame_time", "default rule column is frame_time")
	expect_equal(rule.statistic, PerformanceBudgetRule.STATISTIC_PERCENTILE, "default rule statistic is a percentile")
	expect_equal(rule.percentile, 95.0, "default percentile is 95")
	expect_equal(rule.limit, 16667.0, "default limit is 16667 us")
	expect_equal(rule.get_description(), "frame_time p95", "rule description")
	rule.percentile = 150.0
	expect_equal(rule.percentile, 100.0, "percentile is clamped to 100")


func _test_sample_resources() -> void:
	var frame_profile: PerformanceRecordingProfile = load(FRAME_PROFILE_PATH)
	expect_equal(frame_profile.output_path, "user://performance/frames_{date}_{time}.csv", "frame_timing_profile.tres output path")
	var dotnet_profile: PerformanceRecordingProfile = load(DOTNET_PROFILE_PATH)
	expect_equal(dotnet_profile.metric_sources, PackedStringArray(["dotnet"]), "dotnet_profile.tres records the dotnet source")
	var budget: PerformanceBudget = load(BUDGET_PATH)
	expect_equal(budget.get_rule_count(), 3, "60_fps_budget.tres has 3 rules")
	var maximum: PerformanceBudgetRule = budget.rules[1]
	expect_equal(maximum.statistic, PerformanceBudgetRule.STATISTIC_MAXIMUM, "the second rule is a maximum")
	expect_equal(maximum.limit, 50000.0, "the second rule limit is 50000 us")
	var hitches: PerformanceBudgetRule = budget.rules[2]
	expect_equal(hitches.statistic, PerformanceBudgetRule.STATISTIC_HITCH_COUNT, "the third rule counts hitches")
	expect_equal(hitches.limit, 3.0, "the third rule limit is 3")


func _test_recording() -> void:
	var gauge: int = PerformanceRecorder.register_metric(GAUGE)
	var counter: int = PerformanceRecorder.register_metric(COUNTER, PerformanceRecorder.METRIC_COUNTER)
	expect_equal(PerformanceRecorder.register_metric(GAUGE), gauge, "registering twice returns the same id")
	expect_equal(PerformanceRecorder.get_metric_id(COUNTER), counter, "get_metric_id finds the counter")

	var profile := PerformanceRecordingProfile.new()
	var builtins: PackedInt32Array = profile.builtins
	builtins.append(PerformanceRecorder.BUILTIN_PROCESS_MEMORY_PEAK)
	builtins.append(PerformanceRecorder.BUILTIN_GPU_TIME)
	profile.builtins = builtins
	profile.metrics = PackedStringArray([GAUGE, COUNTER])
	profile.flush_interval_frames = 10
	profile.flush_budget_usec = 0
	profile.output_path = DIR + "/recording_{platform}.csv"
	PerformanceRecorder.recording_started.connect(_on_started)
	PerformanceRecorder.flushed.connect(_on_flushed)
	PerformanceRecorder.recording_stopped.connect(_on_stopped)

	expect_equal(PerformanceRecorder.start(profile), OK, "start returns OK")
	expect(PerformanceRecorder.is_recording(), "is_recording after start")
	expect(_started, "recording_started was emitted")
	var path: String = PerformanceRecorder.get_output_path()
	expect_equal(path, DIR + "/recording_%s.csv" % OS.get_name().to_lower(), "{platform} is replaced in the output path")
	for i in FRAMES:
		PerformanceRecorder.set_metric(gauge, i)
		PerformanceRecorder.add_metric(counter, 1)
		PerformanceRecorder.add_metric(counter, 2)
		await get_tree().process_frame
	expect(_flushed_rows >= 30, "rows were flushed every 10 frames while recording (%d)" % _flushed_rows)
	PerformanceRecorder.stop()
	expect(not PerformanceRecorder.is_recording(), "not recording after stop")
	expect_equal(_stopped_path, path, "recording_stopped carries the path")
	PerformanceRecorder.recording_started.disconnect(_on_started)
	PerformanceRecorder.flushed.disconnect(_on_flushed)
	PerformanceRecorder.recording_stopped.disconnect(_on_stopped)

	var names: PackedStringArray = PerformanceRecorder.get_column_names()
	var rows: int = PerformanceRecorder.get_recorded_frame_count()
	# The frame in which stop() is called is not recorded.
	expect(rows >= FRAMES - 1, "at least %d rows recorded (%d)" % [FRAMES - 1, rows])
	expect(names.has("frame_time"), "columns contain a builtin")
	expect(names.has("time/fps"), "columns contain a monitor")
	expect(names.has(COUNTER), "columns contain a metric")

	var frames: PackedFloat64Array = PerformanceRecorder.get_column(&"frame")
	var step_is_one := true
	for i in range(1, frames.size()):
		if frames[i] != frames[i - 1] + 1:
			step_is_one = false
	expect(step_is_one, "the frame column increases by 1 per row")
	var frame_times: PackedFloat64Array = PerformanceRecorder.get_column(&"frame_time")
	expect(frame_times[-1] > 0, "frame_time is measured")
	var counters: PackedFloat64Array = PerformanceRecorder.get_column(COUNTER)
	expect(counters.count(3.0) >= FRAMES - 2, "a counter holds the sum of one frame and resets after each row")
	var memory: PackedFloat64Array = PerformanceRecorder.get_column(&"process_memory")
	expect(memory[-1] > 0, "process_memory is known")
	var peak: PackedFloat64Array = PerformanceRecorder.get_column(&"process_memory_peak")
	expect(peak[-1] >= memory[-1], "process_memory_peak is at least process_memory")
	var recorder_time: PackedFloat64Array = PerformanceRecorder.get_column(&"recorder_time")
	var total := 0.0
	for value in recorder_time:
		total += value
	var average: float = total / recorder_time.size()
	expect(average < 100.0, "the recorder overhead is small (average %.2f us per row)" % average)

	var lines: PackedStringArray = FileAccess.get_file_as_string(path).strip_edges().split("\n")
	expect_equal(lines[0], ",".join(names), "the CSV header matches get_column_names")
	expect_equal(lines.size(), rows + 1, "the CSV has one line per recorded row")
	var last: PackedStringArray = lines[-1].split(",")
	expect_equal(last.size(), names.size(), "a CSV row has one value per column")
	expect_equal(last[names.find(COUNTER)], "3", "the counter is written as an integer")


func _test_sampling_options() -> void:
	Performance.add_custom_monitor(&"test/custom", func() -> float: return 7.0)
	var profile := PerformanceRecordingProfile.new()
	profile.builtins = PackedInt32Array([PerformanceRecorder.BUILTIN_FRAME])
	profile.monitors = PackedInt32Array()
	profile.include_custom_monitors = true
	profile.sample_interval_frames = 2
	profile.delimiter = ";"
	profile.output_path = DIR + "/sampling.csv"
	await _record(profile, 20)
	var rows: int = PerformanceRecorder.get_recorded_frame_count()
	expect(rows >= 8 and rows <= 11, "sample_interval_frames = 2 records every second frame (%d rows of 20 frames)" % rows)
	var header: String = FileAccess.get_file_as_string(PerformanceRecorder.get_output_path()).split("\n")[0]
	var header_columns: PackedStringArray = header.split(";")
	expect_equal(header_columns[0], "frame", "the delimiter separates the columns")
	expect(header_columns.has("test/custom"), "include_custom_monitors adds the custom monitors as columns")
	var custom: PackedFloat64Array = PerformanceRecorder.get_column(&"test/custom")
	expect_equal(custom[0], 7.0, "a custom monitor is recorded")
	Performance.remove_custom_monitor(&"test/custom")


func _test_no_output_path() -> void:
	var profile := PerformanceRecordingProfile.new()
	profile.builtins = PackedInt32Array([PerformanceRecorder.BUILTIN_FRAME])
	profile.monitors = PackedInt32Array()
	profile.output_path = ""
	PerformanceRecorder.start(profile)
	await get_tree().process_frame
	await get_tree().process_frame
	expect_equal(PerformanceRecorder.get_output_path(), "", "the rows stay in memory without an output path")
	expect_equal(PerformanceRecorder.flush(), ERR_UNCONFIGURED, "flush without an output path returns ERR_UNCONFIGURED")
	PerformanceRecorder.stop()
	expect(PerformanceRecorder.get_column(&"frame").size() > 0, "the rows are available after stop")


func _test_ring_buffer_flush() -> void:
	var profile := PerformanceRecordingProfile.new()
	profile.builtins = PackedInt32Array([PerformanceRecorder.BUILTIN_FRAME])
	profile.monitors = PackedInt32Array()
	profile.buffer_frames = 5
	profile.flush_interval_frames = 0
	profile.output_path = DIR + "/last_rows.csv"
	DirAccess.remove_absolute(profile.output_path)
	PerformanceRecorder.start(profile)
	for i in 12:
		await get_tree().process_frame
	expect_equal(PerformanceRecorder.get_column(&"frame").size(), 5, "only the last 5 rows are kept")
	expect(not FileAccess.file_exists(profile.output_path), "no file before flush")
	expect_equal(PerformanceRecorder.flush(), OK, "flush returns OK")
	PerformanceRecorder.stop()
	var lines: PackedStringArray = FileAccess.get_file_as_string(profile.output_path).strip_edges().split("\n")
	expect_equal(lines.size(), 6, "flush writes the header and the last 5 rows, stop adds nothing twice")


# A recording that sets the gauge to 1, 2, ... FRAMES, one value per frame.
func _record_ramp(profile: PerformanceRecordingProfile) -> void:
	var gauge: int = PerformanceRecorder.register_metric(GAUGE)
	profile.builtins = PackedInt32Array([PerformanceRecorder.BUILTIN_FRAME, PerformanceRecorder.BUILTIN_FRAME_TIME])
	profile.monitors = PackedInt32Array()
	profile.metrics = PackedStringArray([GAUGE])
	profile.flush_budget_usec = 0
	PerformanceRecorder.set_metric(gauge, 1)
	PerformanceRecorder.start(profile)
	for i in FRAMES:
		PerformanceRecorder.set_metric(gauge, i + 1)
		await get_tree().process_frame
	PerformanceRecorder.stop()


func _exact_percentile(sorted: Array, percentile: float) -> float:
	var rank: float = percentile / 100.0 * (sorted.size() - 1)
	var lower: int = floori(rank)
	var upper: int = mini(lower + 1, sorted.size() - 1)
	return lerpf(sorted[lower], sorted[upper], rank - lower)


func _test_summary_exact() -> void:
	var profile := PerformanceRecordingProfile.new()
	profile.hitch_threshold_usec = 1 # every frame is a hitch
	profile.write_summary_json = true
	profile.output_path = DIR + "/exact.csv"
	await _record_ramp(profile)

	var summary: PerformanceSummary = PerformanceRecorder.get_summary()
	expect(summary.is_percentiles_exact(), "percentiles are exact while the run fits in the ring buffer")
	expect(summary.has_column(GAUGE), "has_column finds the metric column")
	expect(not summary.has_column("no_such_column"), "has_column is false for an unknown column")
	expect(summary.get_column_names().has("frame_time"), "get_column_names has frame_time")
	expect_equal(summary.get_columns().size(), summary.get_column_names().size(), "get_columns has one entry per column")
	expect_equal(summary.get_recorded_frame_count(), PerformanceRecorder.get_recorded_frame_count(), "the recorded frame count matches the recorder")
	expect(summary.get_frame_count() >= FRAMES - 1, "get_frame_count counts the frames of the run (%d)" % summary.get_frame_count())
	expect(summary.get_duration_usec() > 0, "the run has a duration")

	var values: Array = Array(PerformanceRecorder.get_column(GAUGE))
	values.sort()
	var column: PerformanceColumnSummary = summary.get_column(GAUGE)
	expect_equal(column.get_name(), String(GAUGE), "the column summary has the column name")
	expect(column.is_exact(), "the column percentiles are exact")
	expect_equal(column.get_sample_count(), values.size(), "the sample count equals the recorded rows")
	expect_equal(column.get_minimum(), values[0], "the minimum is exact")
	expect_equal(column.get_maximum(), values[-1], "the maximum is exact")
	var total := 0.0
	for value in values:
		total += value
	expect(is_equal_approx(column.get_mean(), total / values.size()), "the mean is exact")
	expect(is_equal_approx(column.get_p50(), _exact_percentile(values, 50.0)), "p50 is exact (%s)" % column.get_p50())
	expect(is_equal_approx(column.get_p95(), _exact_percentile(values, 95.0)), "p95 is exact (%s)" % column.get_p95())
	expect(is_equal_approx(column.get_p99(), _exact_percentile(values, 99.0)), "p99 is exact (%s)" % column.get_p99())
	expect(is_equal_approx(column.get_percentile(25.0), _exact_percentile(values, 25.0)), "any percentile is exact")

	var histogram: PackedInt64Array = column.get_histogram(PackedFloat64Array([10.0, 20.0, 30.0]))
	expect_equal(histogram.size(), 4, "a histogram with 3 limits has 4 ranges")
	var counted := 0
	for count in histogram:
		counted += count
	expect_equal(counted, values.size(), "the histogram counts every sample")

	expect_equal(summary.get_hitch_threshold_usec(), 1, "the hitch threshold is taken from the profile")
	expect(summary.get_hitch_count() >= FRAMES - 1, "every frame is a hitch (%d)" % summary.get_hitch_count())
	expect_equal(summary.get_hitch_frames().size(), summary.get_hitch_count(), "the hitch frames are listed")

	var json_path: String = PerformanceRecorder.get_output_path().get_basename() + ".summary.json"
	expect(FileAccess.file_exists(json_path), "the summary JSON is written next to the CSV")
	var parsed: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(json_path))
	var columns: Dictionary = parsed["columns"]
	expect(columns.has(String(GAUGE)), "the JSON has the metric column")
	expect(parsed.has("frame_time_histogram"), "the JSON has the frame-time histogram")
	expect_equal(int(parsed["hitches"]["count"]), summary.get_hitch_count(), "the JSON has the hitch count")
	expect_equal(JSON.parse_string(summary.to_text()), parsed, "to_text is the content of the JSON file")


func _test_summary_histogram() -> void:
	var profile := PerformanceRecordingProfile.new()
	profile.buffer_frames = 8
	profile.flush_interval_frames = 4
	profile.hitch_threshold_usec = 0 # no hitches
	profile.output_path = DIR + "/histogram.csv"
	await _record_ramp(profile)

	var summary: PerformanceSummary = PerformanceRecorder.get_summary()
	expect(not summary.is_percentiles_exact(), "percentiles come from the histogram when the run is longer than the ring buffer")
	expect_equal(summary.get_hitch_count(), 0, "a threshold of 0 disables hitches")
	var column: PerformanceColumnSummary = summary.get_column(GAUGE)
	expect(not column.is_exact(), "the column uses the histogram")
	expect(column.get_sample_count() > 8, "the statistics cover the whole run, not only the ring buffer (%d)" % column.get_sample_count())
	expect_equal(column.get_minimum(), 1.0, "the minimum covers the whole run")
	expect_equal(column.get_maximum(), float(FRAMES), "the maximum covers the whole run")
	var median: float = (1.0 + FRAMES) / 2.0
	expect(absf(column.get_p50() - median) <= median * 0.15, "the histogram p50 is close to the exact median (%s vs %s)" % [column.get_p50(), median])
	expect(column.get_p99() <= column.get_maximum(), "p99 stays below the maximum")
	expect(column.get_p50() >= column.get_minimum(), "p50 stays above the minimum")
	var json_path: String = PerformanceRecorder.get_output_path().get_basename() + ".summary.json"
	expect(not FileAccess.file_exists(json_path), "no summary JSON without write_summary_json")


# Uses the summary of the last recording: the ramp 1..FRAMES without hitches.
func _test_budget_on_recording() -> void:
	var budget := PerformanceBudget.new()
	var pass_rule := PerformanceBudgetRule.new()
	pass_rule.column = String(GAUGE)
	pass_rule.statistic = PerformanceBudgetRule.STATISTIC_MAXIMUM
	pass_rule.limit = FRAMES
	budget.add_rule(pass_rule)
	var hitch_rule := PerformanceBudgetRule.new()
	hitch_rule.statistic = PerformanceBudgetRule.STATISTIC_HITCH_COUNT
	hitch_rule.limit = 0
	budget.add_rule(hitch_rule)
	var result: PerformanceBudgetResult = PerformanceRecorder.check_budget(budget)
	expect(result.is_passed(), "the budget passes: %s" % result.to_text())
	expect_equal(result.get_exit_code(), 0, "exit code 0 when passed")
	expect_equal(result.get_checked_rule_count(), 2, "both rules are checked")
	expect_equal(result.get_failure_count(), 0, "no failures when passed")

	var fail_rule := PerformanceBudgetRule.new()
	fail_rule.column = String(GAUGE)
	fail_rule.statistic = PerformanceBudgetRule.STATISTIC_MEAN
	fail_rule.limit = 5.0
	budget.add_rule(fail_rule)
	result = PerformanceRecorder.check_budget(budget)
	expect(not result.is_passed(), "the budget fails with a mean over the limit")
	expect_equal(result.get_failure_count(), 1, "one failure")
	expect_equal(result.get_exit_code(), 1, "exit code 1 when failed")
	expect(result.to_text().contains(fail_rule.get_description()), "to_text names the failed rule")
	var failure: PerformanceBudgetFailure = result.get_failures()[0]
	expect_equal(failure.get_reason(), PerformanceBudgetFailure.REASON_OVER_LIMIT, "the reason is REASON_OVER_LIMIT")
	expect(failure.get_rule() == fail_rule, "the failure names its rule")
	expect_equal(failure.get_limit(), 5.0, "the failure carries the limit")
	expect(failure.get_actual_value() > 5.0, "the failure carries the actual value")
	expect(failure.get_message().contains(fail_rule.get_description()), "the message names the rule")


func _test_budget_resource() -> void:
	var budget := PerformanceBudget.new()
	var rule := PerformanceBudgetRule.new()
	rule.column = "missing_column"
	rule.statistic = PerformanceBudgetRule.STATISTIC_MAXIMUM
	rule.limit = 123.0
	budget.add_rule(rule)
	var path: String = DIR + "/budget.tres"
	expect_equal(ResourceSaver.save(budget, path), OK, "a budget can be saved")
	var loaded: PerformanceBudget = ResourceLoader.load(path, "", ResourceLoader.CACHE_MODE_IGNORE)
	expect_equal(loaded.get_rule_count(), 1, "the loaded budget has one rule")
	var loaded_rule: PerformanceBudgetRule = loaded.rules[0]
	expect_equal(loaded_rule.statistic, PerformanceBudgetRule.STATISTIC_MAXIMUM, "the rule statistic is saved")
	expect_equal(loaded_rule.limit, 123.0, "the rule limit is saved")

	# An empty summary: a rule on a missing column fails, a hitch rule passes.
	var hitch_rule := PerformanceBudgetRule.new()
	hitch_rule.statistic = PerformanceBudgetRule.STATISTIC_HITCH_COUNT
	hitch_rule.limit = 0
	budget.add_rule(hitch_rule)
	var result: PerformanceBudgetResult = budget.check_summary(PerformanceSummary.new())
	expect(not result.is_passed(), "a rule on a missing column fails")
	expect_equal(result.get_failure_count(), 1, "only the column rule fails")
	var failure: PerformanceBudgetFailure = result.get_failures()[0]
	expect_equal(failure.get_reason(), PerformanceBudgetFailure.REASON_COLUMN_MISSING, "the reason is REASON_COLUMN_MISSING")


func _test_dotnet() -> void:
	expect(PerformanceRecorder.has_metric_source(&"dotnet"), "the .NET module registers the metric source dotnet")
	for value in DOTNET_VALUES:
		expect(Performance.has_custom_monitor(StringName("dotnet/" + value)), "custom monitor dotnet/%s exists" % value)
	for value in DOTNET_DELTAS:
		expect(not Performance.has_custom_monitor(StringName("dotnet/" + value)), "delta %s is recorder-only" % value)
	var allocated: float = Performance.get_custom_monitor(&"dotnet/gc_allocated_bytes")
	expect(allocated > 0, "dotnet/gc_allocated_bytes is above 0 (%d)" % allocated)

	var profile := PerformanceRecordingProfile.new()
	profile.builtins = PackedInt32Array([PerformanceRecorder.BUILTIN_FRAME])
	profile.monitors = PackedInt32Array()
	profile.metric_sources = PackedStringArray(["dotnet"])
	profile.flush_interval_frames = 0
	profile.output_path = ""
	await _record(profile, 10)
	var names: PackedStringArray = PerformanceRecorder.get_column_names()
	for value in DOTNET_VALUES + DOTNET_DELTAS:
		expect(names.has("dotnet/" + value), "column dotnet/%s exists" % value)
	var totals: PackedFloat64Array = PerformanceRecorder.get_column(&"dotnet/gc_allocated_bytes")
	var rows: int = totals.size()
	var growing := true
	for i in range(1, rows):
		if totals[i] < totals[i - 1]:
			growing = false
	expect(growing, "dotnet/gc_allocated_bytes is a growing total")
	var deltas: PackedFloat64Array = PerformanceRecorder.get_column(&"dotnet/gc_allocated_bytes_delta")
	expect(is_nan(deltas[0]), "the first delta of a recording is empty")
	expect(deltas[rows - 1] >= 0, "later deltas are values of 0 or more")


func _on_started() -> void:
	_started = true


func _on_flushed(rows: int) -> void:
	_flushed_rows += rows


func _on_stopped(path: String) -> void:
	_stopped_path = path
