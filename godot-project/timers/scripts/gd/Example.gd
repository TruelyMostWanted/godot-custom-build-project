extends Node
## Minimal example of Timers: the snippets of the wiki guide, in order.
## Wiki: https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Timers


func _ready() -> void:
	# The nodes the snippets refer to.
	var round_timer := Timer.new()
	round_timer.name = "RoundTimer"
	add_child(round_timer)
	var label := Label.new()
	label.name = "Label"
	add_child(label)

	# Quick start with a Timer node.
	var timer := Timer.new()
	timer.wait_time = 3.0
	timer.one_shot = true
	timer.timeout.connect(_on_timeout)
	add_child(timer)
	timer.start()

	# Countdown and stopwatch.
	var clock := Timer.new()
	clock.tick_mode = TimerInfo.TICK_MODE_STOPWATCH
	clock.wait_time = 0.0 # Counts up without an end.
	add_child(clock)
	clock.start()
	await get_tree().create_timer(2.5).timeout
	print(clock.current_time) # About 2.5

	# Describe a timer with TimerInfo.
	var info := TimerInfo.new()
	info.timer_name = &"round"
	info.total_time = 90.0
	info.tick_interval = 1.0
	info.alerts = PackedFloat64Array([30.0, 10.0])
	$RoundTimer.timer_info = info
	info.total_time = 120.0 # Applies to $RoundTimer at once.

	# Pause and resume.
	$RoundTimer.pause_changed.connect(func(paused): print("paused: ", paused))
	$RoundTimer.pause()
	$RoundTimer.resume()

	# Ticks and alerts.
	$RoundTimer.ticked.connect(func(time): $Label.text = str(ceili(time)))
	$RoundTimer.alert_reached.connect(func(time): print("%d seconds left!" % time))
	$RoundTimer.start()

	# Show the time as text.
	$Label.text = $RoundTimer.get_time_text() # "02:00"
	$Label.text = $RoundTimer.get_time_text("{m}:{ss}.{cs}") # "2:00.00"
	print(TimerServer.format_time(4503.0, "{mm}:{ss}")) # "75:03"

	# Code timers.
	var cooldown_info := TimerInfo.new()
	cooldown_info.total_time = 2.0
	var cooldown := TimerServer.create_timer(cooldown_info)
	cooldown.start()
	await cooldown.timeout
	print("Ready again.")

	# Use the RID API from GDExtension.
	var rid := TimerServer.timer_create()
	TimerServer.timer_set_total_time(rid, 0.1)
	TimerServer.timer_set_callback(rid, func(event, time):
		if event == TimerServer.TIMER_EVENT_TIMEOUT:
			print("RID timer ended"))
	TimerServer.timer_start(rid)
	await get_tree().create_timer(0.3).timeout
	# Later:
	TimerServer.free_rid(rid)

	# Pause menu: pause, resume and stop all.
	_on_pause_menu_opened()
	_on_pause_menu_closed()
	_on_level_exit()

	# Clocks.
	var clock_info := TimerInfo.new()
	clock_info.total_time = 5.0
	clock_info.process_mode = TimerInfo.PROCESS_MODE_ALWAYS # Runs while the game is paused.
	clock_info.ignore_time_scale = true # Ignores Engine.time_scale.
	clock_info.process_callback = TimerInfo.PROCESS_CALLBACK_PHYSICS
	TimerServer.create_timer(clock_info).start()

	TimerServer.stop_all()
	get_tree().quit()


func _on_timeout() -> void:
	print("Three seconds passed.")


func _on_pause_menu_opened() -> void:
	TimerServer.pause_all()


func _on_pause_menu_closed() -> void:
	TimerServer.resume_all()


func _on_level_exit() -> void:
	TimerServer.stop_all()
