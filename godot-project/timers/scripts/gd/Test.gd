extends FeatureTest
## Full API test of Timers (Timer, TimerInfo, TimerInstance, TimerServer). Same tests as scripts/cs/Test.cs.
## Time advances in real frames, so timers are short (0.1 to 0.5 s) and tests use tolerances.

const TOLERANCE := 0.15

## True when Timer nodes run on TimerServer (project setting, read at startup). The checks pass in both states.
var on: bool = false


func _run_tests() -> void:
	on = ProjectSettings.get_setting("application/run/timer_nodes_use_timer_server", false)
	expect(Engine.has_singleton("TimerServer"), "TimerServer is a registered singleton")
	expect(ClassDB.class_exists("TimerInfo"), "TimerInfo is registered")
	expect(ClassDB.class_exists("TimerInstance"), "TimerInstance is registered")
	await _test_countdown_and_loop()
	await _test_stopwatch()
	await _test_pause_and_time()
	await _test_time_scale_and_clocks()
	await _test_ticks_and_alerts()
	await _test_timer_info_on_node()
	await _test_server_lookup_and_all()
	await _test_instance_alive()
	await _test_format_time()
	await _test_godot_compatibility()
	await _test_node_paths()


func _wait(seconds: float) -> void:
	var end := Time.get_ticks_msec() + int(seconds * 1000.0)
	while Time.get_ticks_msec() < end:
		await get_tree().process_frame


func _watch(timer: Object) -> Dictionary:
	var counts := {"started": 0, "stopped": 0, "pause_changed": 0, "ticked": 0, "alert_reached": 0, "timeout": 0}
	timer.started.connect(_bump.bind(counts, "started"))
	timer.stopped.connect(_bump.bind(counts, "stopped"))
	timer.timeout.connect(_bump.bind(counts, "timeout"))
	timer.pause_changed.connect(_bump_value.bind(counts, "pause_changed"))
	timer.ticked.connect(_bump_value.bind(counts, "ticked"))
	timer.alert_reached.connect(_bump_value.bind(counts, "alert_reached"))
	return counts


func _bump(counts: Dictionary, key: String) -> void:
	counts[key] += 1


func _bump_value(value: Variant, counts: Dictionary, key: String) -> void:
	counts[key] += 1
	counts[key + "_last"] = value


func _make_info(total: float, tick_mode: TimerInfo.TimerTickMode = TimerInfo.TICK_MODE_COUNTDOWN, loop: bool = false) -> TimerInfo:
	var info := TimerInfo.new()
	info.total_time = total
	info.tick_mode = tick_mode
	info.loop = loop
	return info


func _add_timer(info: TimerInfo = null) -> Timer:
	var timer := Timer.new()
	add_child(timer)
	if info:
		timer.timer_info = info
	return timer


func _test_countdown_and_loop() -> void:
	var instance := TimerServer.create_timer(_make_info(0.3))
	var counts := _watch(instance)
	expect(instance.is_stopped(), "countdown: new timer is stopped")
	expect(not instance.is_running(), "countdown: new timer is not running")
	instance.start()
	expect_equal(counts.started, 1, "countdown: started signal")
	expect(instance.is_running(), "countdown: running after start")
	expect(absf(instance.get_time_left() - 0.3) < TOLERANCE, "countdown: time_left starts at total_time")
	await _wait(0.1)
	expect(instance.get_time_left() < 0.3, "countdown: time_left decreases")
	expect(absf(instance.get_current_time() - instance.get_time_left()) < 0.01, "countdown: current_time is time_left")
	expect(absf(instance.get_elapsed_time() + instance.get_time_left() - 0.3) < 0.01, "countdown: elapsed + left = total")
	expect_equal(counts.timeout, 0, "countdown: no timeout early")
	await _wait(0.5)
	expect_equal(counts.timeout, 1, "countdown: exactly one timeout")
	expect(instance.is_stopped(), "countdown: stopped after timeout")
	expect_equal(instance.get_time_left(), 0.0, "countdown: time_left is 0 after timeout")

	# Loop: keeps running and fires again.
	var looping := TimerServer.create_timer(_make_info(0.15, TimerInfo.TICK_MODE_COUNTDOWN, true))
	var loop_counts := _watch(looping)
	looping.start()
	await _wait(0.6)
	expect(loop_counts.timeout >= 2, "loop: several timeouts (%d)" % loop_counts.timeout)
	expect(looping.is_running(), "loop: still running")
	looping.stop()
	expect_equal(loop_counts.stopped, 1, "loop: stopped signal")
	expect(looping.is_stopped(), "loop: is_stopped after stop")

	# restart
	var restarted := TimerServer.create_timer(_make_info(5.0))
	restarted.start()
	await _wait(0.2)
	restarted.restart()
	expect(restarted.get_time_left() > 4.9, "restart: time starts over")
	restarted.stop()


func _test_stopwatch() -> void:
	var endless := TimerServer.create_timer(_make_info(0.0, TimerInfo.TICK_MODE_STOPWATCH))
	var endless_counts := _watch(endless)
	endless.start()
	await _wait(0.4)
	expect(absf(endless.get_current_time() - 0.4) < TOLERANCE, "stopwatch without end: counts up (%f)" % endless.get_current_time())
	expect(endless.is_running(), "stopwatch without end: keeps running")
	expect_equal(endless_counts.timeout, 0, "stopwatch without end: never times out")
	endless.stop()
	var final_time := endless.get_current_time()
	await _wait(0.1)
	expect(is_equal_approx(endless.get_current_time(), final_time), "stopwatch: keeps the final time after stop")

	var limited := TimerServer.create_timer(_make_info(0.3, TimerInfo.TICK_MODE_STOPWATCH))
	var limited_counts := _watch(limited)
	limited.start()
	await _wait(0.1)
	expect(limited.get_current_time() > 0.0 and limited.get_current_time() < 0.3, "stopwatch with end: shows elapsed time")
	expect(limited.get_time_left() < 0.3, "stopwatch with end: time_left is the rest")
	await _wait(0.5)
	expect_equal(limited_counts.timeout, 1, "stopwatch with end: timeout at total_time")
	expect(limited.is_stopped(), "stopwatch with end: stopped")


func _test_pause_and_time() -> void:
	var instance := TimerServer.create_timer(_make_info(2.0))
	var counts := _watch(instance)
	instance.start()
	await _wait(0.2)
	instance.pause()
	expect(instance.is_paused(), "pause: is_paused")
	expect(instance.is_running(), "pause: still running while paused")
	expect_equal(counts.pause_changed, 1, "pause: pause_changed emitted")
	expect_equal(counts.pause_changed_last, true, "pause: pause_changed(true)")
	var left := instance.get_time_left()
	await _wait(0.3)
	expect(is_equal_approx(instance.get_time_left(), left), "pause: time does not advance")
	instance.resume()
	expect(not instance.is_paused(), "resume: not paused")
	expect_equal(counts.pause_changed_last, false, "resume: pause_changed(false)")
	await _wait(0.2)
	expect(instance.get_time_left() < left, "resume: time advances again")
	instance.set_paused(true)
	expect(instance.is_paused(), "set_paused(true)")
	instance.set_paused(false)
	expect(not instance.is_paused(), "set_paused(false)")

	# set_current_time / add_time on a paused timer are exact.
	instance.pause()
	instance.set_current_time(1.0)
	expect(is_equal_approx(instance.get_time_left(), 1.0), "set_current_time: sets the time left")
	instance.add_time(0.5)
	expect(is_equal_approx(instance.get_time_left(), 1.5), "add_time: adds to the shown time")
	instance.resume()
	instance.stop()

	# Stopwatch: shown time is elapsed time.
	var stopwatch := TimerServer.create_timer(_make_info(0.0, TimerInfo.TICK_MODE_STOPWATCH))
	stopwatch.start()
	stopwatch.pause()
	stopwatch.set_current_time(10.0)
	expect(is_equal_approx(stopwatch.get_current_time(), 10.0), "stopwatch: set_current_time")
	stopwatch.add_time(2.5)
	expect(is_equal_approx(stopwatch.get_current_time(), 12.5), "stopwatch: add_time")
	expect(is_equal_approx(stopwatch.get_elapsed_time(), 12.5), "stopwatch: elapsed equals shown time")
	stopwatch.stop()


func _test_time_scale_and_clocks() -> void:
	# TimerInfo.time_scale speeds one timer up.
	var fast_info := _make_info(0.8)
	fast_info.time_scale = 4.0
	var fast := TimerServer.create_timer(fast_info)
	var fast_counts := _watch(fast)
	fast.start()
	await _wait(0.5)
	expect_equal(fast_counts.timeout, 1, "time_scale 4: 0.8 s timer ends within 0.5 s")

	# Physics clock.
	var physics_info := _make_info(0.2)
	physics_info.process_callback = TimerInfo.PROCESS_CALLBACK_PHYSICS
	var physics := TimerServer.create_timer(physics_info)
	var physics_counts := _watch(physics)
	physics.start()
	await _wait(0.6)
	expect_equal(physics_counts.timeout, 1, "physics clock: timeout")

	# ignore_time_scale: Engine.time_scale slows the scaled timer only.
	var scaled := TimerServer.create_timer(_make_info(0.2))
	var unscaled_info := _make_info(0.2)
	unscaled_info.ignore_time_scale = true
	var unscaled := TimerServer.create_timer(unscaled_info)
	var scaled_counts := _watch(scaled)
	var unscaled_counts := _watch(unscaled)
	Engine.time_scale = 0.1
	scaled.start()
	unscaled.start()
	await _wait(0.6)
	Engine.time_scale = 1.0
	expect_equal(unscaled_counts.timeout, 1, "ignore_time_scale: unscaled timer ends in real time")
	expect_equal(scaled_counts.timeout, 0, "ignore_time_scale: scaled timer is slowed by Engine.time_scale")
	scaled.stop()


func _test_ticks_and_alerts() -> void:
	var info := _make_info(1.0)
	info.tick_interval = 0.1
	info.alerts = PackedFloat64Array([0.7, 0.3])
	var instance := TimerServer.create_timer(info)
	var counts := _watch(instance)
	instance.start()
	await _wait(0.45)
	expect(counts.ticked >= 2, "ticks: several ticked signals (%d)" % counts.ticked)
	expect(counts.ticked_last < 1.0, "ticks: ticked passes the shown time")
	expect_equal(counts.alert_reached, 1, "alerts: first alert (0.7 s left) reached")
	expect(is_equal_approx(counts.alert_reached_last, 0.7), "alerts: alert_reached passes the alert time")
	await _wait(0.8)
	expect_equal(counts.alert_reached, 2, "alerts: second alert (0.3 s left) reached")
	expect(is_equal_approx(counts.alert_reached_last, 0.3), "alerts: second alert time")
	expect_equal(counts.timeout, 1, "ticks: timeout at the end")


func _test_timer_info_on_node() -> void:
	var info := _make_info(3.0, TimerInfo.TICK_MODE_STOPWATCH, true)
	info.timer_name = &"info-node"
	info.description = "A stopwatch that loops every 3 s."
	var timer := _add_timer(info)
	expect(is_equal_approx(timer.wait_time, 3.0), "TimerInfo: total_time maps to wait_time")
	expect(not timer.one_shot, "TimerInfo: loop maps to one_shot = false")
	expect_equal(timer.tick_mode, TimerInfo.TICK_MODE_STOPWATCH, "TimerInfo: tick_mode on the node")
	if on:
		expect(timer.get_timer_rid().is_valid(), "Timer node has a timer RID in the tree")
	else:
		expect(not timer.get_timer_rid().is_valid(), "Timer node has no timer RID on Godot's own path")
	expect(timer.timer_info == info, "Timer node keeps the TimerInfo")
	timer.start()
	timer.pause()
	timer.current_time = 1.25
	expect(is_equal_approx(timer.current_time, 1.25), "Timer node: current_time setter")
	expect_equal(timer.get_time_text("{s}.{cs}"), "1.25", "Timer node: get_time_text")
	timer.add_time(1.0)
	expect(is_equal_approx(timer.current_time, 2.25), "Timer node: add_time")
	timer.resume()
	timer.stop()

	# The info applies live.
	info.total_time = 7.0
	expect(is_equal_approx(timer.wait_time, 7.0), "TimerInfo edits apply live to the node")
	timer.timer_info = null
	timer.queue_free()

	# Info with defaults.
	var defaults := TimerInfo.new()
	expect_equal(defaults.tick_mode, TimerInfo.TICK_MODE_COUNTDOWN, "TimerInfo default tick_mode")
	expect(is_equal_approx(defaults.total_time, 1.0), "TimerInfo default total_time")
	expect(not defaults.loop, "TimerInfo default loop")
	expect(is_equal_approx(defaults.time_scale, 1.0), "TimerInfo default time_scale")
	expect(not defaults.ignore_time_scale, "TimerInfo default ignore_time_scale")
	expect_equal(defaults.process_mode, TimerInfo.PROCESS_MODE_PAUSABLE, "TimerInfo default process_mode")
	expect_equal(defaults.process_callback, TimerInfo.PROCESS_CALLBACK_IDLE, "TimerInfo default process_callback")
	expect_equal(defaults.alerts.size(), 0, "TimerInfo default alerts")


func _test_server_lookup_and_all() -> void:
	var count := TimerServer.get_timer_count()
	var info := _make_info(5.0)
	info.timer_name = &"lookup-timer"
	var instance := TimerServer.create_timer(info)
	expect_equal(TimerServer.get_timer_count(), count + 1, "get_timer_count grows")
	expect_equal(TimerServer.get_timer(&"lookup-timer"), instance.get_rid(), "get_timer finds the timer by name")
	expect(TimerServer.get_timers().has(instance.get_rid()), "get_timers lists the timer")
	expect(TimerServer.timer_is_valid(instance.get_rid()), "timer_is_valid")
	expect(TimerServer.timer_get_object(instance.get_rid()) == instance, "timer_get_object returns the TimerInstance")
	expect_equal(TimerServer.timer_get_name(instance.get_rid()), &"lookup-timer", "timer_get_name")

	expect_equal(int(TimerServer.TIMER_EVENT_TIMEOUT), 6, "TimerEvent.Timeout is the last event")

	# RID endpoints.
	var rid := TimerServer.timer_create()
	TimerServer.timer_set_total_time(rid, 4.0)
	TimerServer.timer_set_tick_mode(rid, TimerInfo.TICK_MODE_COUNTDOWN)
	expect(is_equal_approx(TimerServer.timer_get_total_time(rid), 4.0), "RID endpoint: total time")
	TimerServer.timer_start(rid)
	expect(TimerServer.timer_is_running(rid), "RID endpoint: running")
	TimerServer.timer_set_paused(rid, true)
	expect(TimerServer.timer_is_paused(rid), "RID endpoint: paused")
	TimerServer.timer_set_paused(rid, false)
	TimerServer.timer_add_time(rid, 1.0)
	expect(TimerServer.timer_get_time_left(rid) > 4.5, "RID endpoint: add_time")
	TimerServer.timer_stop(rid)
	expect(not TimerServer.timer_is_running(rid), "RID endpoint: stopped")
	TimerServer.free_rid(rid)
	expect(not TimerServer.timer_is_valid(rid), "free_rid invalidates the RID")

	# pause_all / resume_all / stop_all act on instances, and on Timer nodes only when they run on the server.
	var timer := _add_timer()
	var node_counts := _watch(timer)
	timer.start(5.0)
	instance.start()
	TimerServer.pause_all()
	expect(instance.is_paused(), "pause_all pauses the instance")
	if on:
		expect(timer.paused, "pause_all pauses the Timer node")
		expect(timer.is_paused(), "pause_all: is_paused on the node")
		expect_equal(node_counts.pause_changed, 1, "pause_all: node emits pause_changed")
		expect_equal(node_counts.pause_changed_last, true, "pause_all: node pause_changed(true)")
	else:
		expect(not timer.paused, "pause_all does not pause the Timer node on Godot's own path")
		expect_equal(node_counts.pause_changed, 0, "pause_all: node emits no pause_changed")
	TimerServer.resume_all()
	expect(not timer.is_paused(), "resume_all leaves the node running")
	expect(not instance.is_paused(), "resume_all resumes the instance")
	TimerServer.stop_all()
	expect(instance.is_stopped(), "stop_all stops the instance")
	if on:
		expect(timer.is_stopped(), "stop_all stops the node")
		expect_equal(node_counts.stopped, 1, "stop_all: node emits stopped")
	else:
		expect(not timer.is_stopped(), "stop_all does not stop the node on Godot's own path")
		expect_equal(node_counts.stopped, 0, "stop_all: node emits no stopped")
		timer.stop()
	timer.queue_free()


func _test_instance_alive() -> void:
	var instance := TimerServer.create_timer(_make_info(0.2))
	var rid := instance.get_rid()
	var counts := _watch(instance)
	instance.start()
	instance = null
	expect(TimerServer.timer_is_valid(rid), "running TimerInstance stays alive without a reference")
	await _wait(0.5)
	expect_equal(counts.timeout, 1, "dropped running TimerInstance still times out")
	expect(not TimerServer.timer_is_valid(rid), "TimerInstance is freed after it ends")

	# A stopped instance without a reference is freed at once.
	var idle := TimerServer.create_timer(_make_info(1.0))
	var idle_rid := idle.get_rid()
	idle = null
	expect(not TimerServer.timer_is_valid(idle_rid), "stopped TimerInstance is freed with its last reference")


func _test_format_time() -> void:
	expect_equal(TimerServer.format_time(75.4), "01:15", "format_time default")
	expect_equal(TimerServer.format_time(4503.25), "75:03", "format_time: largest unit carries the amount")
	expect_equal(TimerServer.format_time(3723.456, "{h}:{mm}:{ss}.{cs}"), "1:02:03.45", "format_time hours")
	expect_equal(TimerServer.format_time(3723.456, "{s}.{ms}"), "3723.456", "format_time seconds and milliseconds")
	expect_equal(TimerServer.format_time(59.99, "{mm}:{ss}"), "00:59", "format_time truncates")
	expect_equal(TimerServer.format_time(59.01, "{mm}:{ss}", true), "01:00", "format_time rounds up")
	expect_equal(TimerServer.format_time(0.2, "{s}", true), "1", "format_time rounds up to 1")
	expect_equal(TimerServer.format_time(-3.0, "{s}"), "0", "format_time clamps negative")
	var instance := TimerServer.create_timer(_make_info(2.0))
	expect_equal(instance.get_time_text(), "00:02", "TimerInstance.get_time_text")


func _test_godot_compatibility() -> void:
	var timer := _add_timer()
	var counts := _watch(timer)
	expect(timer.is_stopped(), "compat: new Timer is stopped")
	timer.start(5)
	expect(is_equal_approx(timer.wait_time, 5.0), "compat: start(5) sets wait_time")
	expect(absf(timer.time_left - 5.0) < 0.1, "compat: time_left is 5 after start(5)")
	expect(not timer.is_stopped(), "compat: running after start")
	expect_equal(timer.is_processing_internal(), not on, "compat: node processes internally only on Godot's own path")
	timer.paused = true
	expect(timer.paused, "compat: paused property")
	timer.paused = false
	timer.stop()
	expect(timer.is_stopped(), "compat: stop -> is_stopped")
	expect_equal(counts.started, 1, "compat: started signal on the node")
	expect_equal(counts.stopped, 1, "compat: stopped signal on the node")
	timer.queue_free()

	# One shot and autostart as before.
	var one_shot := Timer.new()
	one_shot.wait_time = 0.15
	one_shot.one_shot = true
	one_shot.autostart = true
	var shot_counts := _watch(one_shot)
	add_child(one_shot)
	expect(not one_shot.is_stopped(), "compat: autostart starts in the tree")
	await _wait(0.5)
	expect_equal(shot_counts.timeout, 1, "compat: one_shot fires once")
	expect(one_shot.is_stopped(), "compat: one_shot stops after timeout")
	one_shot.queue_free()

	# Physics process callback on the node.
	var physics := Timer.new()
	physics.wait_time = 0.15
	physics.one_shot = true
	physics.process_callback = Timer.TIMER_PROCESS_PHYSICS
	var physics_counts := _watch(physics)
	add_child(physics)
	physics.start()
	await _wait(0.5)
	expect_equal(physics_counts.timeout, 1, "compat: physics process_callback fires")
	physics.queue_free()


# Timer nodes on both paths (setting on or off).
func _test_node_paths() -> void:
	# Changing wait_time on a running node keeps the time left of the current cycle.
	var looping := _add_timer()
	var loop_counts := _watch(looping)
	looping.one_shot = false
	looping.start(1.0)
	await _wait(0.25)
	looping.wait_time = 0.5
	expect(looping.time_left > 0.5, "node: wait_time change keeps time_left (%f)" % looping.time_left)
	await _wait(0.8)
	expect(loop_counts.timeout >= 1, "node: loop fires after the wait_time change (%d)" % loop_counts.timeout)
	looping.stop()
	looping.queue_free()

	# Signals started / stopped / pause_changed, and no stopped at a one-shot's natural end.
	var timer := _add_timer()
	timer.one_shot = true
	var counts := _watch(timer)
	timer.start(0.2)
	expect_equal(counts.started, 1, "node signals: started on start()")
	timer.pause()
	expect_equal(counts.pause_changed, 1, "node signals: pause_changed on pause()")
	expect_equal(counts.pause_changed_last, true, "node signals: pause_changed(true)")
	timer.resume()
	expect_equal(counts.pause_changed_last, false, "node signals: pause_changed(false)")
	timer.stop()
	expect_equal(counts.stopped, 1, "node signals: stopped on stop()")
	timer.start(0.15)
	await _wait(0.5)
	expect_equal(counts.timeout, 1, "node signals: one-shot emits timeout at its end")
	expect_equal(counts.stopped, 1, "node signals: no stopped at the natural end")
	timer.queue_free()

	# Ticks and alerts on a node work only on the server path.
	var info := _make_info(1.0)
	info.tick_interval = 0.1
	info.alerts = PackedFloat64Array([0.7, 0.3])
	var ticking := _add_timer(info)
	var tick_counts := _watch(ticking)
	ticking.start()
	expect_equal(ticking.is_processing_internal(), not on, "node: internal processing only on Godot's own path")
	await _wait(0.45)
	if on:
		expect(tick_counts.ticked >= 2, "node: ticked on the server path (%d)" % tick_counts.ticked)
		expect(tick_counts.alert_reached >= 1, "node: alert_reached on the server path")
	else:
		expect_equal(tick_counts.ticked, 0, "node: no ticked on Godot's own path")
		expect_equal(tick_counts.alert_reached, 0, "node: no alert_reached on Godot's own path")
	ticking.stop()
	ticking.timer_info = null
	ticking.queue_free()
