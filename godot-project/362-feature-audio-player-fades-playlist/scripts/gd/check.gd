extends FeatureCheck
## GDScript checks of 362-feature-audio-player-fades-playlist. Check the same things as scripts/cs/Issue362FeatureAudioPlayerFadesPlaylistCheck.cs.
## The checks run headless with the dummy audio driver: they check signals, indices and fade gains, not sound.

var _fade_finished := 0
var _stream_changed: Array[AudioStream] = []
var _tracks: Array[int] = []
var _track_streams: Array[AudioStream] = []
var _positions := 0


func _get_check_name() -> String:
	return "362-feature-audio-player-fades-playlist"


## A silent 16-bit mono WAV of the given length.
func _make_wav(seconds: float) -> AudioStreamWAV:
	var wav := AudioStreamWAV.new()
	wav.format = AudioStreamWAV.FORMAT_16_BITS
	wav.mix_rate = 44100
	wav.stereo = false
	var data := PackedByteArray()
	data.resize(int(seconds * 44100.0) * 2)
	wav.data = data
	return wav


func _wait(seconds: float) -> void:
	await get_tree().create_timer(seconds).timeout


func _on_fade_finished() -> void:
	_fade_finished += 1


func _on_stream_changed(stream: AudioStream) -> void:
	_stream_changed.append(stream)


func _on_track_changed(index: int, stream: AudioStream) -> void:
	_tracks.append(index)
	_track_streams.append(stream)


func _on_position_changed(_position: float) -> void:
	_positions += 1


func _run_checks() -> void:
	_check_list_api()
	_check_time_helpers()
	await _check_fade_in_out()
	await _check_crossfade()
	await _check_playlist_on_player()
	await _check_position_signal()
	await _check_fade_2d_3d()


func _check_list_api() -> void:
	var a := _make_wav(1.0)
	a.resource_name = "alpha"
	var b := _make_wav(1.0)
	var c := _make_wav(1.0)
	var playlist := AudioStreamPlaylist.new()
	expect(playlist.streams.size() == 0, "a new playlist has no streams")
	playlist.add_stream(a)
	playlist.add_stream(c)
	playlist.insert_stream(1, b)
	expect(playlist.streams.size() == 3, "add_stream/insert_stream give three streams")
	expect(playlist.streams[0] == a and playlist.streams[1] == b and playlist.streams[2] == c, "insert_stream inserts at the index")
	expect_equal(playlist.find_stream(c), 2, "find_stream finds the stream")
	expect_equal(playlist.find_stream(_make_wav(0.1)), -1, "find_stream returns -1 for a missing stream")
	playlist.move_stream(2, 0)
	expect(playlist.streams[0] == c and playlist.streams[1] == a and playlist.streams[2] == b, "move_stream moves a stream")
	playlist.remove_stream(0)
	expect(playlist.streams.size() == 2 and playlist.streams[0] == a, "remove_stream removes the stream")
	expect_equal(playlist.get_stream_name(0), "alpha", "get_stream_name returns the resource name")
	expect_equal(playlist.get_stream_name(1), "", "get_stream_name is empty without name and path")

	var typed: Array[AudioStream] = [b, c]
	playlist.streams = typed
	expect(playlist.streams.size() == 2 and playlist.streams[0] == b, "the streams setter replaces the list")

	# Old files: stream_count + stream_N.
	var old := AudioStreamPlaylist.new()
	old.set("stream_count", 2)
	old.set("stream_0", a)
	old.set("stream_1", b)
	expect(old.streams.size() == 2 and old.streams[0] == a and old.streams[1] == b, "old stream_count/stream_N properties load into streams")


func _check_time_helpers() -> void:
	var playlist := AudioStreamPlaylist.new()
	playlist.add_stream(_make_wav(1.0))
	playlist.add_stream(_make_wav(2.0))
	playlist.add_stream(_make_wav(0.5))
	playlist.loop = false
	expect(is_equal_approx(playlist.get_stream_length(1), 2.0), "get_stream_length of track 1 is 2.0")
	expect(is_equal_approx(playlist.get_stream_start_time(0), 0.0), "start time of track 0 is 0")
	expect(is_equal_approx(playlist.get_stream_start_time(1), 1.0), "start time of track 1 is 1")
	expect(is_equal_approx(playlist.get_stream_start_time(2), 3.0), "start time of track 2 is 3")
	expect(is_equal_approx(playlist.get_length(), 3.5), "get_length is the sum of the lengths")
	expect_equal(playlist.get_stream_index_at(0.0), 0, "index_at 0.0 is track 0")
	expect_equal(playlist.get_stream_index_at(0.99), 0, "index_at 0.99 is track 0")
	expect_equal(playlist.get_stream_index_at(1.0), 1, "index_at 1.0 is track 1")
	expect_equal(playlist.get_stream_index_at(2.9), 1, "index_at 2.9 is track 1")
	expect_equal(playlist.get_stream_index_at(3.2), 2, "index_at 3.2 is track 2")
	for i in 3:
		expect_equal(playlist.get_stream_index_at(playlist.get_stream_start_time(i) + 0.01), i, "index_at(start time + 0.01) round trip for track %d" % i)
	expect_equal(playlist.get_stream_index_at(3.6), -1, "index_at past the end is -1 without loop")
	expect_equal(playlist.get_stream_index_at(-0.5), -1, "index_at of a negative time is -1")
	playlist.loop = true
	expect_equal(playlist.get_stream_index_at(3.6), 0, "index_at wraps with loop (3.6 -> 0.1)")
	expect_equal(playlist.get_stream_index_at(4.7), 1, "index_at wraps with loop (4.7 -> 1.2)")
	playlist.loop = false
	playlist.fade_time = 0.3
	expect(is_equal_approx(playlist.get_stream_start_time(2), 3.0), "fade_time does not change start times")


func _check_fade_in_out() -> void:
	var player := AudioStreamPlayer.new()
	player.stream = _make_wav(30.0)
	add_child(player)
	player.fade_finished.connect(_on_fade_finished)
	_fade_finished = 0
	expect(not player.is_fading(), "not fading at the start")
	expect(is_equal_approx(player.get_fade_gain(), 1.0), "gain is 1.0 at the start")
	player.fade_in(0.4)
	expect(player.is_playing(), "fade_in() starts playback")
	expect(player.is_fading(), "is_fading() during fade_in")
	expect(player.get_fade_gain() < 0.2, "gain starts near 0 (got %f)" % player.get_fade_gain())
	var last := player.get_fade_gain()
	var monotonic := true
	for i in 10:
		await _wait(0.05)
		var g := player.get_fade_gain()
		if g < last - 0.001:
			monotonic = false
		last = g
	expect(monotonic, "the gain rises monotonically")
	await _wait(0.5)
	expect(is_equal_approx(player.get_fade_gain(), 1.0), "gain is 1.0 after the fade")
	expect(not player.is_fading(), "not fading after fade_in")
	expect(_fade_finished >= 1, "fade_finished is emitted after fade_in")
	expect(is_equal_approx(player.volume_db, 0.0), "volume_db is untouched by fades")

	_fade_finished = 0
	player.fade_out(0.3)
	expect(player.is_fading(), "is_fading() during fade_out")
	await _wait(0.6)
	expect(not player.is_playing(), "fade_out(stop_at_end) stops the player")
	expect(_fade_finished >= 1, "fade_finished is emitted after fade_out")

	player.play()
	expect(player.is_playing(), "play() starts playback")
	expect(is_equal_approx(player.get_fade_gain(), 1.0), "play() resets the gain to 1.0")
	player.fade_out(0.2, AudioMixerServer.FADE_CURVE_LINEAR, false)
	await _wait(0.5)
	expect(player.is_playing(), "fade_out(stop_at_end = false) keeps playing")
	player.queue_free()


func _check_crossfade() -> void:
	var first := _make_wav(30.0)
	var second := _make_wav(30.0)
	var player := AudioStreamPlayer.new()
	player.stream = first
	add_child(player)
	player.play()
	player.fade_finished.connect(_on_fade_finished)
	player.stream_changed.connect(_on_stream_changed)
	_fade_finished = 0
	_stream_changed.clear()
	player.crossfade_to(second, 0.4)
	expect(_stream_changed.size() == 1 and _stream_changed[0] == second, "crossfade_to emits stream_changed with the new stream")
	expect(player.stream == second, "crossfade_to swaps the stream")
	expect(player.is_fading(), "is_fading() during the crossfade")
	expect(player.is_playing(), "the player keeps playing during the crossfade")
	await _wait(0.8)
	expect(not player.is_fading(), "not fading after the crossfade")
	expect(_fade_finished >= 1, "fade_finished is emitted after the crossfade")
	expect(player.is_playing(), "the player plays the new stream after the crossfade")
	player.queue_free()


func _check_playlist_on_player() -> void:
	var first := _make_wav(0.3)
	var second := _make_wav(0.3)
	var playlist := AudioStreamPlaylist.new()
	playlist.add_stream(first)
	playlist.add_stream(second)
	playlist.loop = false
	var player := AudioStreamPlayer.new()
	player.stream = playlist
	add_child(player)
	expect_equal(player.get_current_track_index(), -1, "no track index without playback")
	expect(player.get_current_track_stream() == null, "no track stream without playback")
	player.track_changed.connect(_on_track_changed)
	_tracks.clear()
	_track_streams.clear()
	player.play()
	await get_tree().process_frame
	await get_tree().process_frame
	expect(_tracks.size() >= 1 and _tracks[0] == 0 and _track_streams[0] == first, "track_changed(0, first) arrives after play()")
	expect_equal(player.get_current_track_index(), 0, "get_current_track_index is 0")
	expect(player.get_current_track_stream() == first, "get_current_track_stream is the first stream")
	var pos_a := player.get_position_in_playlist()
	await _wait(0.1)
	var pos_b := player.get_position_in_playlist()
	expect(pos_b > pos_a, "get_position_in_playlist increases (%f -> %f)" % [pos_a, pos_b])
	for i in 40: # At most 2 seconds.
		if _tracks.has(1):
			break
		await _wait(0.05)
	expect(_tracks.has(1), "track_changed(1, second) arrives later")
	if _tracks.has(1):
		expect(_track_streams[_tracks.find(1)] == second, "track_changed(1) carries the second stream")
	expect_equal(player.get_current_track_index(), 1, "get_current_track_index is 1")
	expect(player.get_current_track_stream() == second, "get_current_track_stream is the second stream")
	player.queue_free()

	# Single streams: index 0 and the stream itself.
	var single := AudioStreamPlayer.new()
	var wav := _make_wav(5.0)
	single.stream = wav
	add_child(single)
	single.play()
	await get_tree().process_frame
	expect_equal(single.get_current_track_index(), 0, "single stream: track index is 0")
	expect(single.get_current_track_stream() == wav, "single stream: track stream is the stream")
	single.queue_free()


func _check_position_signal() -> void:
	var player := AudioStreamPlayer.new()
	player.stream = _make_wav(30.0)
	add_child(player)
	player.position_changed.connect(_on_position_changed)
	expect(is_equal_approx(player.position_signal_interval, 0.0), "position_signal_interval is 0 by default")
	player.position_signal_interval = -1.0
	expect(is_equal_approx(player.position_signal_interval, 0.0), "a negative interval clamps to 0")
	_positions = 0
	player.play()
	for i in 6:
		await get_tree().process_frame
	expect_equal(_positions, 0, "position_changed is off by default")
	player.position_signal_interval = 0.1
	await _wait(0.5)
	expect(_positions >= 1, "position_changed is emitted with an interval (%d)" % _positions)
	player.queue_free()


func _check_fade_2d_3d() -> void:
	var p2d := AudioStreamPlayer2D.new()
	p2d.stream = _make_wav(30.0)
	add_child(p2d)
	p2d.fade_finished.connect(_on_fade_finished)
	_fade_finished = 0
	p2d.fade_in(0.2)
	expect(p2d.is_playing(), "2D: fade_in() starts playback")
	expect(p2d.get_fade_gain() < 0.3, "2D: gain starts near 0")
	await _wait(0.5)
	expect(is_equal_approx(p2d.get_fade_gain(), 1.0), "2D: gain is 1.0 after the fade")
	expect(_fade_finished >= 1, "2D: fade_finished is emitted")
	p2d.queue_free()

	var p3d := AudioStreamPlayer3D.new()
	p3d.stream = _make_wav(30.0)
	add_child(p3d)
	p3d.fade_finished.connect(_on_fade_finished)
	_fade_finished = 0
	p3d.fade_in(0.2)
	expect(p3d.is_playing(), "3D: fade_in() starts playback")
	expect(p3d.get_fade_gain() < 0.3, "3D: gain starts near 0")
	await _wait(0.5)
	expect(is_equal_approx(p3d.get_fade_gain(), 1.0), "3D: gain is 1.0 after the fade")
	expect(_fade_finished >= 1, "3D: fade_finished is emitted")
	p3d.queue_free()
