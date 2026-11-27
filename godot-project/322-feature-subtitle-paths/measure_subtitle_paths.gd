extends SceneTree
## Measurement (not part of the pass/fail checks) for #322: cost of 100 synced SubtitlePlayers and of a subtitle switch.
## Prints a Markdown table: average frame time with all stopped and all playing, and the stream switch time per player
## (new stream with another subtitle_path until the SubtitlePlayer has reloaded).
##
## Run (from the repository folder of the test project, with a window or headless):
##   godot --headless --path godot-project -s res://322-feature-subtitle-paths/measure_subtitle_paths.gd

const TRACK_A := "res://322-feature-subtitle-paths/track_a.srt"
const TRACK_B := "res://322-feature-subtitle-paths/track_b.srt"
const COUNT := 100
const FRAMES := 300

var _audios: Array[AudioStreamPlayer] = []
var _players: Array[SubtitlePlayer] = []


func _silence(seconds: float) -> AudioStreamWAV:
	var stream := AudioStreamWAV.new()
	stream.format = AudioStreamWAV.FORMAT_16_BITS
	stream.mix_rate = 8000
	var data := PackedByteArray()
	data.resize(int(seconds * 8000) * 2)
	stream.data = data
	return stream


func _avg_frame_usec() -> float:
	for i in 10:
		await process_frame
	var t0 := Time.get_ticks_usec()
	for i in FRAMES:
		await process_frame
	return float(Time.get_ticks_usec() - t0) / FRAMES


func _initialize() -> void:
	_run()


func _run() -> void:
	var stream_a := _silence(60.0)
	stream_a.subtitle_path = TRACK_A
	var stream_b := _silence(60.0)
	stream_b.subtitle_path = TRACK_B
	for i in COUNT:
		var audio := AudioStreamPlayer.new()
		audio.stream = stream_a
		root.add_child(audio)
		var player := SubtitlePlayer.new()
		root.add_child(player)
		player.sync_node = player.get_path_to(audio)
		_audios.append(audio)
		_players.append(player)

	var stopped := await _avg_frame_usec()
	for audio in _audios:
		audio.play()
	var playing := await _avg_frame_usec()

	var t0 := Time.get_ticks_usec()
	for audio in _audios:
		audio.stream = stream_b
		audio.play()
	await process_frame
	var switch_usec := float(Time.get_ticks_usec() - t0) / COUNT

	print("| Case | Value |")
	print("|---|---|")
	print("| avg frame time, %d SubtitlePlayers, all stopped (usec) | %.1f |" % [COUNT, stopped])
	print("| avg frame time, %d SubtitlePlayers, all playing (usec) | %.1f |" % [COUNT, playing])
	print("| stream switch to another subtitle_path, per player (usec) | %.1f |" % switch_usec)
	quit()
