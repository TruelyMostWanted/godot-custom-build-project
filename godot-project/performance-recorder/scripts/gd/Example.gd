extends Node
## Minimal example of Performance Recorder: the snippets of the wiki guide, in order.
## Wiki: https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Performance-Recorder
## Left out: the command line (--record-performance), the mobile notifications, the web download and the C++ metric source.
## Files go to user:// instead of res://.

const FRAMES := 30


func _ready() -> void:
	# Record frame times to a CSV file.
	PerformanceRecorder.recording_stopped.connect(func(path: String) -> void: print("CSV written to ", path))
	PerformanceRecorder.start()
	await _wait_frames()

	# Choose the columns.
	var profile := PerformanceRecordingProfile.new()
	profile.builtins = [PerformanceRecorder.BUILTIN_FRAME_TIME, PerformanceRecorder.BUILTIN_GPU_TIME, PerformanceRecorder.BUILTIN_PROCESS_MEMORY]
	profile.monitors = [Performance.TIME_FPS, Performance.RENDER_TOTAL_DRAW_CALLS_IN_FRAME]
	profile.sample_interval_frames = 2 # one row every second frame
	profile.output_path = "user://perf/{platform}_{date}_{time}.csv"
	PerformanceRecorder.start(profile)
	await _wait_frames()

	# Record your own values.
	var enemies_id: int = PerformanceRecorder.register_metric(&"enemies_alive")
	var spawned_id: int = PerformanceRecorder.register_metric(&"enemies_spawned", PerformanceRecorder.METRIC_COUNTER)
	var metrics_profile := PerformanceRecordingProfile.new()
	metrics_profile.metrics = PackedStringArray(["enemies_alive", "enemies_spawned"])
	PerformanceRecorder.start(metrics_profile)
	PerformanceRecorder.set_metric(enemies_id, get_tree().get_node_count_in_group(&"enemies"))
	PerformanceRecorder.add_metric(spawned_id, 1)
	await _wait_frames()

	# Keep only the seconds before a hitch.
	var hitch_profile := PerformanceRecordingProfile.new()
	hitch_profile.flush_interval_frames = 0
	hitch_profile.buffer_frames = 600 # 10 s at 60 FPS
	hitch_profile.output_path = "user://hitches/{date}_{time}.csv"
	PerformanceRecorder.start(hitch_profile)
	await _wait_frames()
	PerformanceRecorder.flush() # writes the last rows

	# Read the statistics of a run.
	PerformanceRecorder.stop()
	var summary: PerformanceSummary = PerformanceRecorder.get_summary()
	var frame_time: PerformanceColumnSummary = summary.get_column("frame_time")
	print("%d frames, mean %.0f us, max %.0f us" % [summary.get_frame_count(), frame_time.get_mean(), frame_time.get_maximum()])
	print("p95: ", frame_time.get_p95(), ", histogram: ", frame_time.get_histogram(PackedFloat64Array([8333.0, 16667.0, 33333.0])))

	# Count hitches.
	var count_profile := PerformanceRecordingProfile.new()
	count_profile.hitch_threshold_usec = 33333 # slower than 30 FPS
	PerformanceRecorder.start(count_profile)
	await _wait_frames()
	summary = PerformanceRecorder.get_summary()
	print("%d hitches, in frames %s" % [summary.get_hitch_count(), summary.get_hitch_frames()])

	# Write the statistics as JSON.
	var json_profile := PerformanceRecordingProfile.new()
	json_profile.output_path = "user://perf/run.csv"
	json_profile.write_summary_json = true
	PerformanceRecorder.start(json_profile)
	await _wait_frames()
	PerformanceRecorder.stop()
	print(PerformanceRecorder.get_summary().to_text().left(80))

	# Create a performance budget.
	var budget := PerformanceBudget.new()
	var p95 := PerformanceBudgetRule.new()
	p95.column = "frame_time"
	p95.statistic = PerformanceBudgetRule.STATISTIC_PERCENTILE
	p95.percentile = 95.0
	p95.limit = 16667.0
	budget.add_rule(p95)
	var hitches := PerformanceBudgetRule.new()
	hitches.statistic = PerformanceBudgetRule.STATISTIC_HITCH_COUNT
	hitches.limit = 0
	budget.add_rule(hitches)
	ResourceSaver.save(budget, "user://perf/budget.tres")

	# Check a performance budget in CI (a test scene would quit with the exit code).
	var result: PerformanceBudgetResult = PerformanceRecorder.check_budget(load("user://perf/budget.tres"))
	print(result.to_text())
	print("Exit code: ", result.get_exit_code())

	# Record .NET garbage collection.
	if PerformanceRecorder.has_metric_source(&"dotnet"):
		var dotnet_profile := PerformanceRecordingProfile.new()
		dotnet_profile.metric_sources = PackedStringArray(["dotnet"])
		PerformanceRecorder.start(dotnet_profile)
		await _wait_frames()
		PerformanceRecorder.stop()

	# Keep the rows on mobile and download them on the Web: the output path to download.
	print("Output path: ", PerformanceRecorder.get_output_path())
	get_tree().quit()


func _wait_frames() -> void:
	for i in FRAMES:
		await get_tree().process_frame
