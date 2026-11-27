extends FeatureTest
## Full API test of Audio Player Fades And Playlists: the fade, track and signal API of AudioStreamPlayer (2D, 3D),
## AudioStreamPlaylist and AudioStreamPlaybackPlaylist. Same tests as scripts/cs/Test.cs.
## Runs headless with the dummy audio driver: it checks signals, indices and fade gains, not sound.

const TITLE_PATH := "res://audio-player-fades-and-playlists/resources/title.tres"
# Lengths of the three tracks of the time helper playlist, and its start times.
const LENGTHS: Array[float] = [1.0, 2.0, 0.5]
const START_TIMES: Array[float] = [0.0, 1.0, 3.0]
const TOTAL_LENGTH := 3.5

var _fade_finished: int = 0
var _stream_changed: Array[AudioStream] = []
var _tracks: Array[int] = []
var _track_streams: Array[AudioStream] = []
var _positions: int = 0


func _run_tests() -> void:
	_test_playlist_list()
	_test_stream_names()
	_test_time_helpers()
	await _test_fade_in_out()
	await _test_crossfade()
	await _test_playlist_on_player()
	await _test_seek_in_playlist()
	await _test_position_signal()
	await _test_fade_2d_3d()


func _test_playlist_list() -> void:
	var a: AudioStreamWAV = _silence(1.0)
	var b: AudioStreamWAV = _silence(1.0)
	var c: AudioStreamWAV = _silence(1.0)
	var playlist := AudioStreamPlaylist.new()
	expect(playlist.streams.is_empty(), "a new playlist has no streams")
	playlist.add_stream(a)
	playlist.add_stream(c)
	playlist.insert_stream(1, b)
	expect_equal(playlist.streams, [a, b, c], "add_stream appends, insert_stream inserts at the index")
	expect_equal(playlist.stream_count, 3, "stream_count is the size of streams")
	expect_equal(playlist.find_stream(c), 2, "find_stream finds the stream")
	expect_equal(playlist.find_stream(_silence(0.1)), -1, "find_stream returns -1 for a missing stream")
	playlist.move_stream(2, 0)
	expect_equal(playlist.streams, [c, a, b], "move_stream moves a stream")
	playlist.remove_stream(0)
	expect_equal(playlist.streams, [a, b], "remove_stream removes the stream")
	playlist.set_list_stream(1, c)
	expect(playlist.get_list_stream(1) == c, "set_list_stream and get_list_stream replace and read one entry")
	var typed: Array[AudioStream] = [b, c]
	playlist.streams = typed
	expect_equal(playlist.streams, [b, c], "the streams setter replaces the list")
	for i: int in AudioStreamPlaylist.MAX_STREAMS:
		playlist.add_stream(a)
	expect_equal(playlist.streams.size(), AudioStreamPlaylist.MAX_STREAMS, "the list holds at most MAX_STREAMS streams")
	var old := AudioStreamPlaylist.new()
	old.set("stream_count", 2)
	old.set("stream_0", a)
	old.set("stream_1", b)
	expect_equal(old.streams, [a, b], "the old stream_count and stream_N properties load into streams")


func _test_stream_names() -> void:
	var named: AudioStreamWAV = _silence(1.0)
	named.resource_name = "alpha"
	var playlist := AudioStreamPlaylist.new()
	playlist.add_stream(named)
	playlist.add_stream(load(TITLE_PATH))
	playlist.add_stream(_silence(1.0))
	expect_equal(playlist.get_stream_name(0), "alpha", "get_stream_name returns the resource name")
	expect_equal(playlist.get_stream_name(1), "title", "get_stream_name falls back to the file name")
	expect_equal(playlist.get_stream_name(2), "", "get_stream_name is empty without name and path")


func _test_time_helpers() -> void:
	var playlist := AudioStreamPlaylist.new()
	for length: float in LENGTHS:
		playlist.add_stream(_silence(length))
	playlist.loop = false
	for i: int in LENGTHS.size():
		expect_equal(playlist.get_stream_length(i), LENGTHS[i], "get_stream_length of track %d" % i)
		expect_equal(playlist.get_stream_start_time(i), START_TIMES[i], "get_stream_start_time of track %d" % i)
		expect_equal(playlist.get_stream_index_at(START_TIMES[i] + 0.01), i, "get_stream_index_at just after the start of track %d" % i)
	expect_equal(playlist.get_length(), TOTAL_LENGTH, "get_length is the sum of the lengths")
	expect_equal(playlist.get_stream_index_at(0.99), 0, "get_stream_index_at just before a track change is the earlier track")
	expect_equal(playlist.get_stream_index_at(3.6), -1, "get_stream_index_at past the end is -1 without loop")
	expect_equal(playlist.get_stream_index_at(-0.5), -1, "get_stream_index_at of a negative time is -1")
	playlist.loop = true
	expect_equal(playlist.get_stream_index_at(4.7), 1, "get_stream_index_at wraps with loop (4.7 -> 1.2)")
	playlist.fade_time = 0.3
	expect_equal(playlist.get_stream_start_time(2), 3.0, "fade_time does not change the start times")


func _test_fade_in_out() -> void:
	var player: AudioStreamPlayer = _add_player()
	expect(not player.is_fading(), "is_fading is false at the start")
	expect_equal(player.get_fade_gain(), 1.0, "get_fade_gain is 1 at the start")
	player.fade_in(0.4)
	expect(player.is_playing(), "fade_in starts playback")
	expect(player.is_fading(), "is_fading is true during fade_in")
	expect(player.get_fade_gain() < 0.2, "fade_in starts near silence")
	var last: float = player.get_fade_gain()
	var rising := true
	for i: int in 10:
		await _wait(0.05)
		rising = rising and player.get_fade_gain() >= last - 0.001
		last = player.get_fade_gain()
	expect(rising, "fade_in raises the gain step by step")
	await _wait(0.5)
	expect_equal(player.get_fade_gain(), 1.0, "fade_in ends at gain 1")
	expect_equal(_fade_finished, 1, "fade_finished is emitted after fade_in")
	expect_equal(player.volume_db, 0.0, "fades leave volume_db alone")
	player.fade_out(0.3)
	expect(player.is_fading(), "is_fading is true during fade_out")
	await _wait(0.6)
	expect(not player.is_playing(), "fade_out stops the player at the end")
	expect_equal(_fade_finished, 2, "fade_finished is emitted after fade_out")
	player.play()
	expect_equal(player.get_fade_gain(), 1.0, "play resets the gain to 1")
	player.fade_out(0.2, AudioMixerServer.FADE_CURVE_LINEAR, false)
	await _wait(0.5)
	expect(player.is_playing(), "fade_out with stop_at_end = false keeps playing")
	player.queue_free()


func _test_crossfade() -> void:
	var player: AudioStreamPlayer = _add_player()
	var second: AudioStreamWAV = _silence(30.0)
	player.play()
	_fade_finished = 0
	player.crossfade_to(second, 0.4)
	expect_equal(_stream_changed, [second], "crossfade_to emits stream_changed with the new stream")
	expect(player.stream == second, "crossfade_to sets the stream")
	expect(player.is_fading(), "is_fading is true during the crossfade")
	await _wait(0.8)
	expect(not player.is_fading(), "the crossfade ends")
	expect_equal(_fade_finished, 1, "fade_finished is emitted after the crossfade")
	expect(player.is_playing(), "the player plays the new stream after the crossfade")
	player.queue_free()


func _test_playlist_on_player() -> void:
	var first: AudioStreamWAV = _silence(0.3)
	var second: AudioStreamWAV = _silence(0.3)
	var playlist := AudioStreamPlaylist.new()
	playlist.add_stream(first)
	playlist.add_stream(second)
	playlist.loop = false
	var player: AudioStreamPlayer = _add_player()
	player.stream = playlist
	expect_equal(player.get_current_track_index(), -1, "get_current_track_index is -1 without playback")
	expect(player.get_current_track_stream() == null, "get_current_track_stream is null without playback")
	player.play()
	await _frames(2)
	expect_equal(_tracks, [0], "track_changed(0) arrives after play")
	expect_equal(_track_streams, [first], "track_changed carries the stream of the track")
	expect_equal(player.get_current_track_index(), 0, "get_current_track_index is the playing track")
	expect(player.get_current_track_stream() == first, "get_current_track_stream is the playing stream")
	var playback: AudioStreamPlaybackPlaylist = player.get_stream_playback()
	expect_equal(playback.get_current_index(), 0, "AudioStreamPlaybackPlaylist.get_current_index is the playing track")
	expect(playback.get_current_stream() == first, "AudioStreamPlaybackPlaylist.get_current_stream is the playing stream")
	await _wait_for_track(1)
	expect_equal(_tracks, [0, 1], "track_changed(1) arrives when the second track starts")
	expect(player.get_position_in_playlist() > 0.3, "get_position_in_playlist counts from the start of the playlist")
	expect(player.get_track_position() < 0.3, "get_track_position counts from the start of the track")
	expect_equal(playback.get_position_in_playlist(), player.get_position_in_playlist(), "the playback reports the same playlist position")
	expect_equal(playback.get_position_in_stream(), player.get_track_position(), "the playback reports the same track position")
	player.queue_free()
	var single: AudioStreamPlayer = _add_player()
	single.play()
	await _frames(1)
	expect_equal(single.get_current_track_index(), 0, "a single stream is track 0")
	expect(single.get_current_track_stream() == single.stream, "a single stream is its own track stream")
	single.queue_free()


func _test_seek_in_playlist() -> void:
	var playlist := AudioStreamPlaylist.new()
	for length: float in LENGTHS:
		playlist.add_stream(_silence(length))
	var player: AudioStreamPlayer = _add_player()
	player.stream = playlist
	player.play()
	player.seek(playlist.get_stream_start_time(2) + 0.1)
	await _frames(2)
	expect_equal(player.get_current_track_index(), 2, "seek across the playlist starts the track at that time")
	player.queue_free()


func _test_position_signal() -> void:
	var player: AudioStreamPlayer = _add_player()
	expect_equal(player.position_signal_interval, 0.0, "position_signal_interval is 0 by default")
	player.position_signal_interval = -1.0
	expect_equal(player.position_signal_interval, 0.0, "a negative position_signal_interval is clamped to 0")
	player.play()
	await _frames(6)
	expect_equal(_positions, 0, "position_changed is off by default")
	player.position_signal_interval = 0.1
	await _wait(0.5)
	expect(_positions >= 1, "position_changed is emitted with an interval")
	player.queue_free()


func _test_fade_2d_3d() -> void:
	for player: Variant in [AudioStreamPlayer2D.new(), AudioStreamPlayer3D.new()]:
		player.stream = _silence(30.0)
		add_child(player)
		player.fade_finished.connect(_on_fade_finished)
		_fade_finished = 0
		player.fade_in(0.2)
		expect(player.is_playing(), "%s: fade_in starts playback" % player.get_class())
		await _wait(0.5)
		expect_equal(player.get_fade_gain(), 1.0, "%s: fade_in ends at gain 1" % player.get_class())
		expect_equal(_fade_finished, 1, "%s: fade_finished is emitted" % player.get_class())
		player.queue_free()


## Adds a player with 30 seconds of silence to the tree and connects its signals.
func _add_player() -> AudioStreamPlayer:
	var player := AudioStreamPlayer.new()
	player.stream = _silence(30.0)
	add_child(player)
	player.fade_finished.connect(_on_fade_finished)
	player.stream_changed.connect(_on_stream_changed)
	player.position_changed.connect(_on_position_changed)
	player.track_changed.connect(_on_track_changed)
	return player


func _silence(seconds: float) -> AudioStreamWAV:
	var wav := AudioStreamWAV.new()
	wav.format = AudioStreamWAV.FORMAT_16_BITS
	wav.mix_rate = 44100
	var data := PackedByteArray()
	data.resize(int(seconds * 44100.0) * 2)
	wav.data = data
	return wav


func _wait(seconds: float) -> void:
	await get_tree().create_timer(seconds).timeout


func _frames(count: int) -> void:
	for i: int in count:
		await get_tree().process_frame


func _wait_for_track(index: int) -> void:
	for i: int in 40: # at most 2 seconds
		if _tracks.has(index):
			return
		await _wait(0.05)


func _on_fade_finished() -> void:
	_fade_finished += 1


func _on_stream_changed(stream: AudioStream) -> void:
	_stream_changed.append(stream)


func _on_track_changed(index: int, stream: AudioStream) -> void:
	_tracks.append(index)
	_track_streams.append(stream)


func _on_position_changed(_position: float) -> void:
	_positions += 1
