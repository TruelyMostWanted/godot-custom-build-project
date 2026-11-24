extends FeatureCheck
## GDScript checks of 79-feature-dotnet-metrics. Check the same things as scripts/cs/Issue79FeatureDotnetMetricsCheck.cs.

const VALUES := [
	"gc_allocated_bytes", "gc_total_memory_bytes", "gc_heap_size_bytes", "gc_fragmented_bytes", "gc_committed_bytes",
	"gc_pause_time_percent", "gc_pause_total_ms", "gc_collections_gen0", "gc_collections_gen1", "gc_collections_gen2",
	"threadpool_threads", "threadpool_pending", "threadpool_completed", "lock_contentions", "jit_compiled_methods",
]
const DELTAS := ["gc_allocated_bytes_delta", "gc_collections_delta", "gc_pause_delta_ms"]
const FRAMES := 10


func _get_check_name() -> String:
	return "79-feature-dotnet-metrics"


func _run_checks() -> void:
	if not ClassDB.class_exists("CSharpScript"):
		# Builds without .NET (and Web) have no "dotnet" source.
		expect(not PerformanceRecorder.has_metric_source(&"dotnet"), "no \"dotnet\" metric source without the .NET module")
		expect(not Performance.has_custom_monitor(&"dotnet/gc_allocated_bytes"), "no dotnet/* monitors without the .NET module")
		return
	_check_registration()
	await _check_monitors()
	await _check_recording()


func _check_registration() -> void:
	expect(PerformanceRecorder.has_metric_source(&"dotnet"), "the .NET module registers the metric source \"dotnet\"")
	for value in VALUES:
		expect(Performance.has_custom_monitor(StringName("dotnet/" + value)), "Performance custom monitor dotnet/%s exists" % value)
	for value in DELTAS:
		expect(not Performance.has_custom_monitor(StringName("dotnet/" + value)), "delta %s is recorder-only" % value)
	# The default profile does not record .NET values.
	expect(PerformanceRecordingProfile.new().metric_sources.is_empty(), "the default profile has no metric sources")


func _check_monitors() -> void:
	await get_tree().process_frame
	var allocated: float = Performance.get_custom_monitor(&"dotnet/gc_allocated_bytes")
	expect(allocated > 0, "dotnet/gc_allocated_bytes is above 0 (%d)" % allocated)
	var threads: float = Performance.get_custom_monitor(&"dotnet/threadpool_threads")
	expect(threads >= 0, "dotnet/threadpool_threads is a number (%d)" % threads)
	var heap: float = Performance.get_custom_monitor(&"dotnet/gc_total_memory_bytes")
	expect(heap > 0, "dotnet/gc_total_memory_bytes is above 0 (%d)" % heap)


func _check_recording() -> void:
	var profile := PerformanceRecordingProfile.new()
	profile.builtins = PackedInt32Array([PerformanceRecorder.BUILTIN_FRAME])
	profile.monitors = PackedInt32Array()
	profile.metric_sources = PackedStringArray(["dotnet"])
	profile.flush_interval_frames = 0
	profile.output_path = ""

	expect_equal(PerformanceRecorder.start(profile), OK, "start() with metric source \"dotnet\" returns OK")
	for i in FRAMES:
		await get_tree().process_frame
	PerformanceRecorder.stop()

	var names := PerformanceRecorder.get_column_names()
	expect_equal(names.size(), 1 + VALUES.size() + DELTAS.size(), "frame plus %d dotnet columns" % (VALUES.size() + DELTAS.size()))
	for value in VALUES + DELTAS:
		expect(names.has("dotnet/" + value), "column dotnet/%s exists" % value)

	var allocated := PerformanceRecorder.get_column(&"dotnet/gc_allocated_bytes")
	var rows := allocated.size()
	expect(rows >= FRAMES - 1, "at least %d rows recorded (%d)" % [FRAMES - 1, rows])
	var growing := true
	for i in range(1, rows):
		growing = growing and allocated[i] >= allocated[i - 1]
	expect(growing and allocated[0] > 0, "dotnet/gc_allocated_bytes is a growing total")

	var deltas := PerformanceRecorder.get_column(&"dotnet/gc_allocated_bytes_delta")
	expect(is_nan(deltas[0]), "the first delta of a recording is empty")
	expect(not is_nan(deltas[rows - 1]) and deltas[rows - 1] >= 0, "later deltas are values >= 0")
	var gen0 := PerformanceRecorder.get_column(&"dotnet/gc_collections_gen0")
	expect(not is_nan(gen0[rows - 1]), "dotnet/gc_collections_gen0 has a value")
