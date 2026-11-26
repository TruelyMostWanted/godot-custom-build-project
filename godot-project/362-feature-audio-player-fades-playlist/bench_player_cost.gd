extends SceneTree
## Benchmark (not part of the pass/fail checks): cost per AudioStreamPlayer node per frame for #362.
## 200 players play a 30 s silent WAV. Measures the average Performance.TIME_PROCESS over 300 frames,
## minus an empty-scene baseline, divided by 200, for: plain playing, position_signal_interval = 0.1
## with a no-op handler, and all players in fade_in(10.0). Prints a Markdown table.
##
## Run (headless, dummy audio driver, from the repository folder of the test project):
##   godot --headless --audio-driver Dummy --path godot-project -s res://362-feature-audio-player-fades-playlist/bench_player_cost.gd

const PLAYERS := 200
const FRAMES := 300

var _root_node: Node


func _make_wav(seconds: float) -> AudioStreamWAV:
	var wav := AudioStreamWAV.new()
	wav.format = AudioStreamWAV.FORMAT_16_BITS
	wav.mix_rate = 44100
	wav.stereo = false
	var data := PackedByteArray()
	data.resize(int(seconds * 44100.0) * 2)
	wav.data = data
	return wav


func _noop(_position: float) -> void:
	pass


## Average TIME_PROCESS (seconds) over FRAMES frames, after a short warm-up.
func _measure() -> float:
	for i in 10:
		await process_frame
	var total := 0.0
	for i in FRAMES:
		await process_frame
		total += Performance.get_monitor(Performance.TIME_PROCESS)
	return total / FRAMES


func _clear() -> void:
	if _root_node != null:
		_root_node.queue_free()
	await process_frame
	_root_node = Node.new()
	root.add_child(_root_node)


func _run_scenario(mode: int, wav: AudioStreamWAV) -> float:
	await _clear()
	for i in PLAYERS:
		var player := AudioStreamPlayer.new()
		player.stream = wav
		_root_node.add_child(player)
		if mode == 2:
			player.set("position_signal_interval", 0.1)
			if player.has_signal("position_changed"):
				player.connect("position_changed", _noop)
		if mode == 3 and player.has_method("fade_in"):
			player.call("fade_in", 10.0)
		else:
			player.play()
	return await _measure()


func _initialize() -> void:
	_run.call_deferred()


func _run() -> void:
	var wav := _make_wav(30.0)
	await _clear()
	var baseline := await _measure()
	var names := ["plain playing (warm-up)", "plain playing", "position_signal_interval = 0.1", "fade_in(10.0)"]
	print("| Scenario | Avg frame process (us) | Per player (us) |")
	print("|---|---|---|")
	print("| empty scene (baseline) | %.1f | - |" % (baseline * 1000000.0))
	for mode in (4 if ClassDB.class_has_method("AudioStreamPlayer", "fade_in") else 2):
		var avg := await _run_scenario(mode, wav)
		var per_player := (avg - baseline) * 1000000.0 / PLAYERS
		print("| %s | %.1f | %.3f |" % [names[mode], avg * 1000000.0, per_player])
	quit()
