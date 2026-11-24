extends FeatureCheck
## GDScript checks of 78-feature-performance-recorder. Check the same things as scripts/cs/Issue78FeaturePerformanceRecorderCheck.cs.

const DIR := "user://78-feature-performance-recorder"
const FRAMES := 40

var _flushed_rows := 0
var _stopped_path := ""
var _started := false


func _get_check_name() -> String:
	return "78-feature-performance-recorder"


func _run_checks() -> void:
	_check_bindings()
	_check_profile_defaults()
	await _check_recording()
	await _check_memory_only()


func _check_bindings() -> void:
	expect(Engine.has_singleton("PerformanceRecorder"), "PerformanceRecorder is a singleton")
	expect(ClassDB.class_exists("PerformanceRecordingProfile"), "PerformanceRecordingProfile is registered")
	for method in ["start", "stop", "flush", "is_recording", "register_metric", "get_metric_id", "set_metric", "add_metric",
			"has_metric_source", "get_column_names", "get_column", "get_recorded_frame_count", "get_output_path"]:
		expect(ClassDB.class_has_method("PerformanceRecorder", method), "PerformanceRecorder.%s() is bound" % method)
	for sig in ["recording_started", "recording_stopped", "flushed"]:
		expect(ClassDB.class_has_signal("PerformanceRecorder", sig), "signal %s exists" % sig)
	for setting in ["enabled", "profile", "output_path"]:
		expect(ProjectSettings.has_setting("debug/performance_recording/" + setting), "project setting debug/performance_recording/%s exists" % setting)
	expect_equal(PerformanceRecorder.BUILTIN_MAX, 10, "10 builtins")


func _check_profile_defaults() -> void:
	var profile := PerformanceRecordingProfile.new()
	expect_equal(profile.builtins.size(), 8, "default profile has 8 builtins")
	expect_equal(profile.monitors.size(), 8, "default profile has 8 monitors")
	expect_equal(profile.flush_interval_frames, 300, "default flush interval is 300 frames")
	expect_equal(profile.output_path, "user://performance/{date}_{time}.csv", "default output path")


func _check_recording() -> void:
	var gauge := PerformanceRecorder.register_metric(&"check_gauge")
	var counter := PerformanceRecorder.register_metric(&"check_counter", PerformanceRecorder.METRIC_COUNTER)
	expect_equal(PerformanceRecorder.register_metric(&"check_gauge"), gauge, "registering twice returns the same id")
	expect_equal(PerformanceRecorder.get_metric_id(&"check_counter"), counter, "get_metric_id() finds the counter")

	var profile := PerformanceRecordingProfile.new()
	var builtins := profile.builtins
	builtins.append(PerformanceRecorder.BUILTIN_PROCESS_MEMORY_PEAK)
	builtins.append(PerformanceRecorder.BUILTIN_GPU_TIME)
	profile.builtins = builtins
	profile.metrics = PackedStringArray(["check_gauge", "check_counter"])
	profile.flush_interval_frames = 10
	profile.flush_budget_usec = 0
	profile.output_path = DIR + "/gd_{platform}.csv"

	PerformanceRecorder.recording_started.connect(_on_started)
	PerformanceRecorder.flushed.connect(_on_flushed)
	PerformanceRecorder.recording_stopped.connect(_on_stopped)

	expect_equal(PerformanceRecorder.start(profile), OK, "start() returns OK")
	expect(PerformanceRecorder.is_recording(), "is_recording() after start()")
	expect(_started, "recording_started was emitted")
	var path := PerformanceRecorder.get_output_path()
	expect_equal(path, DIR + "/gd_%s.csv" % OS.get_name().to_lower(), "{platform} is replaced")

	for i in FRAMES:
		PerformanceRecorder.set_metric(gauge, i)
		PerformanceRecorder.add_metric(counter, 1)
		PerformanceRecorder.add_metric(counter, 2)
		await get_tree().process_frame
	expect(_flushed_rows >= 30, "rows were flushed every 10 frames while recording (%d)" % _flushed_rows)
	PerformanceRecorder.stop()
	expect(not PerformanceRecorder.is_recording(), "not recording after stop()")
	expect_equal(_stopped_path, path, "recording_stopped carries the path")

	var names := PerformanceRecorder.get_column_names()
	var rows := PerformanceRecorder.get_recorded_frame_count()
	# The frame in which stop() is called is not recorded any more.
	expect(rows >= FRAMES - 1, "at least %d rows recorded (%d)" % [FRAMES - 1, rows])
	expect(names.has("frame_time") and names.has("time/fps") and names.has("check_counter"), "columns contain builtins, monitors and metrics")

	# Per-frame values.
	var frames := PerformanceRecorder.get_column(&"frame")
	var increasing := true
	for i in range(1, frames.size()):
		increasing = increasing and frames[i] == frames[i - 1] + 1
	expect(increasing, "frame column increases by 1 per row")
	var frame_times := PerformanceRecorder.get_column(&"frame_time")
	expect(frame_times[frame_times.size() - 1] > 0, "frame_time is measured")
	var counters := PerformanceRecorder.get_column(&"check_counter")
	expect(counters.count(3.0) >= FRAMES - 2, "counter holds the sum of one frame and resets each row")
	var memory := PerformanceRecorder.get_column(&"process_memory")
	expect(memory[memory.size() - 1] > 0, "process_memory is known")
	var peak := PerformanceRecorder.get_column(&"process_memory_peak")
	expect(peak[peak.size() - 1] >= memory[memory.size() - 1], "process_memory_peak >= process_memory")
	var gpu := PerformanceRecorder.get_column(&"gpu_time")
	print("  gpu_time (ms) of the last row: %s" % gpu[gpu.size() - 1])

	# Overhead of the recorder itself.
	var recorder_time := PerformanceRecorder.get_column(&"recorder_time")
	var total := 0.0
	var maximum := 0.0
	for value in recorder_time:
		total += value
		maximum = maxf(maximum, value)
	var average := total / recorder_time.size()
	print("  recorder_time: average %.2f µs, max %.2f µs per row (%d columns)" % [average, maximum, names.size()])
	expect(average < 100.0, "recorder overhead is small (average %.2f µs per row)" % average)

	# CSV: header plus one line per row.
	var lines := FileAccess.get_file_as_string(path).strip_edges().split("\n")
	expect_equal(lines[0], ",".join(names), "CSV header matches get_column_names()")
	expect_equal(lines.size(), rows + 1, "CSV has one line per recorded row")
	var last := lines[lines.size() - 1].split(",")
	expect_equal(last.size(), names.size(), "a CSV row has one value per column")
	expect_equal(last[names.find("check_counter")], "3", "the counter value is written as an integer")

	PerformanceRecorder.recording_started.disconnect(_on_started)
	PerformanceRecorder.flushed.disconnect(_on_flushed)
	PerformanceRecorder.recording_stopped.disconnect(_on_stopped)


func _check_memory_only() -> void:
	var profile := PerformanceRecordingProfile.new()
	profile.builtins = PackedInt32Array([PerformanceRecorder.BUILTIN_FRAME])
	profile.monitors = PackedInt32Array()
	profile.metrics = PackedStringArray(["check_gauge"])
	profile.buffer_frames = 5
	profile.flush_interval_frames = 0
	profile.output_path = DIR + "/gd_last_rows.csv"
	if FileAccess.file_exists(profile.output_path):
		DirAccess.remove_absolute(profile.output_path)

	PerformanceRecorder.start(profile)
	for i in 12:
		await get_tree().process_frame
	expect_equal(PerformanceRecorder.get_column(&"frame").size(), 5, "only the last 5 rows are kept")
	expect(not FileAccess.file_exists(profile.output_path), "no file before flush()")
	expect_equal(PerformanceRecorder.flush(), OK, "flush() returns OK")
	PerformanceRecorder.stop()
	var lines := FileAccess.get_file_as_string(profile.output_path).strip_edges().split("\n")
	expect_equal(lines.size(), 6, "flush() writes the header and the last 5 rows, stop() adds nothing twice")


func _on_started() -> void:
	_started = true


func _on_flushed(rows: int) -> void:
	_flushed_rows += rows


func _on_stopped(path: String) -> void:
	_stopped_path = path
