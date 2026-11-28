extends Node
## Benchmark (not part of the pass/fail checks): cost of running timers.
## For N in [1000, 10000, 100000]: N Timer nodes that never fire (wait_time 1000), N Timer nodes that fire
## (loop, wait_time spread 0.1..1.0 s) and, only when the engine has TimerServer, the same two variants as
## TimerInstance code timers. Per variant: creation time, average Performance TIME_PROCESS plus
## TIME_PHYSICS_PROCESS per frame over 300 frames (after 30 warm-up frames) minus a 0-timer baseline,
## and the change of MEMORY_STATIC. Prints one Markdown table and quits.
## The Timer-node variants use only the old Timer API, so the script runs unchanged on upstream Godot 4.8.
##
## README (run headless from the repository folder of the test project):
##   godot --headless --path godot-project res://timers/bench/bench_timers.tscn

const COUNTS: Array[int] = [1000, 10000, 100000]
const WARMUP_FRAMES := 30
const MEASURE_FRAMES := 300

var _container: Node
var _instances: Array = []
var _baseline := 0.0
var _rows: PackedStringArray = []


func _ready() -> void:
	var has_server := ClassDB.class_exists("TimerServer")
	print("Timer benchmark, engine %s, TimerServer: %s" % [Engine.get_version_info().string, has_server])
	_baseline = await _measure_frames()
	for n in COUNTS:
		await _run("Timer nodes, idle", n, false, false)
		await _run("Timer nodes, firing", n, true, false)
		if has_server:
			await _run("TimerInstance, idle", n, false, true)
			await _run("TimerInstance, firing", n, true, true)
	print("")
	print("| variant | N | create ms | process µs/frame | memory KB |")
	print("|---|---|---|---|---|")
	for row in _rows:
		print(row)
	print("")
	print("Baseline (no timers): %.1f µs per frame (process + physics), subtracted." % _baseline)
	get_tree().quit()


# Average of TIME_PROCESS + TIME_PHYSICS_PROCESS in µs over MEASURE_FRAMES, after WARMUP_FRAMES.
func _measure_frames() -> float:
	for i in WARMUP_FRAMES:
		await get_tree().process_frame
	var sum := 0.0
	for i in MEASURE_FRAMES:
		await get_tree().process_frame
		sum += Performance.get_monitor(Performance.TIME_PROCESS)
		sum += Performance.get_monitor(Performance.TIME_PHYSICS_PROCESS)
	return sum / MEASURE_FRAMES * 1.0e6


func _wait_time(index: int, count: int, firing: bool) -> float:
	if not firing:
		return 1000.0
	return 0.1 + 0.9 * float(index) / float(count)


func _run(variant: String, count: int, firing: bool, use_server: bool) -> void:
	var memory_before := Performance.get_monitor(Performance.MEMORY_STATIC)
	var started := Time.get_ticks_usec()
	if use_server:
		_create_instances(count, firing)
	else:
		_create_nodes(count, firing)
	var create_ms := (Time.get_ticks_usec() - started) / 1000.0
	var measured := await _measure_frames()
	var memory_kb := (Performance.get_monitor(Performance.MEMORY_STATIC) - memory_before) / 1024.0
	_rows.append("| %s | %d | %.1f | %.1f | %.0f |" % [variant, count, create_ms, measured - _baseline, memory_kb])
	print("done: %s, N=%d" % [variant, count])
	await _free_all()


func _create_nodes(count: int, firing: bool) -> void:
	_container = Node.new()
	add_child(_container)
	for i in count:
		var timer := Timer.new()
		timer.wait_time = _wait_time(i, count, firing)
		timer.one_shot = false
		_container.add_child(timer)
		timer.start()


func _create_instances(count: int, firing: bool) -> void:
	var server := Engine.get_singleton("TimerServer")
	_instances.resize(count)
	for i in count:
		var info: Resource = ClassDB.instantiate("TimerInfo")
		info.set("total_time", _wait_time(i, count, firing))
		info.set("loop", true)
		var instance: Object = server.call("create_timer", info)
		instance.call("start")
		_instances[i] = instance


func _free_all() -> void:
	for instance in _instances:
		instance.call("stop")
	_instances.clear()
	if _container:
		_container.queue_free()
		_container = null
	for i in 3:
		await get_tree().process_frame
