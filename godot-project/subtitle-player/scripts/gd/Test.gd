extends FeatureTest
## Full API test of Subtitle Player: SubtitlePlayer, SubtitlePlayback, the subtitle_path of streams and players,
## and the playing_changed / stream_changed signals of the players. Same tests as scripts/cs/Test.cs.
## Signals are logged as "+X" (segment_started), "-X" (segment_ended) and "=X" (segment_changed, "=" alone for none).

const RES := "res://subtitle-player/resources/"
const LINE_A := RES + "line_a.srt"
const LINE_B := RES + "line_b.srt"
const SIDECAR_STREAM := RES + "test/sidecar.tres"
const VIDEO := RES + "test/testsrc.webm"

var _events: Array[String] = []


func _run_tests() -> void:
	_test_defaults()
	_test_playback()
	_test_playback_skip_and_seek()
	await _test_own_clock()
	await _test_animation_sync()
	await _test_audio_sync()
	await _test_video_sync()
	_test_subtitle_path_properties()
	await _test_playing_changed()
	await _test_subtitle_sources()
	await _test_stream_switch()
	await _test_hidden_when_stopped()


func _test_defaults() -> void:
	var player := SubtitlePlayer.new()
	expect(player.subtitle == null, "subtitle is null by default")
	expect(player.sync_node.is_empty(), "sync_node is empty by default")
	expect_equal(player.time_offset, 0.0, "time_offset is 0 by default")
	expect(not player.autoplay, "autoplay is off by default")
	expect(player.get_playback() is SubtitlePlayback, "get_playback returns the SubtitlePlayback of the node")
	player.free()


func _test_playback() -> void:
	var playback := SubtitlePlayback.new()
	_log(playback)
	playback.subtitle = _three_segments()
	playback.update(0.5)
	expect_equal(_take(), "+A =A", "update starts A")
	playback.update(1.0)
	expect_equal(_take(), "-A =", "the end time is exclusive")
	playback.update(3.5)
	expect_equal(_take(), "+B +C =C", "overlapping segments start in order, the current one starts last")
	expect_equal(playback.get_active_segments().size(), 2, "get_active_segments returns both")
	expect_equal(playback.get_time(), 3.5, "get_time is the last time")
	playback.update(4.5)
	expect_equal(_take(), "-B", "B ends, C stays current")
	expect_equal(playback.get_current_segment().text, "C", "get_current_segment is C")
	playback.subtitle.time_offset = 10.0
	playback.update(10.5)
	expect_equal(_take(), "-C +A =A", "the time_offset of the subtitle moves the segments")
	playback.reset()
	expect_equal(_take(), "-A =", "reset ends the active segments")
	expect(playback.get_current_segment() == null, "nothing is active after reset")


func _test_playback_skip_and_seek() -> void:
	var playback := SubtitlePlayback.new()
	_log(playback)
	playback.subtitle = _three_segments()
	playback.update(0.5)
	_take()
	playback.update(10.0)
	expect_equal(_take(), "-A +B -B +C -C =", "update reports segments skipped by a long frame")
	playback.seek(0.5)
	expect_equal(_take(), "+A =A", "seek backwards")
	playback.seek(10.0)
	expect_equal(_take(), "-A =", "seek forward skips silently")
	var unsorted := Subtitle.new()
	var segments: Array[SubtitleSegment] = [_segment(3.0, 4.0, "late"), _segment(0.0, 1.0, "early")]
	unsorted.segments = segments
	playback.subtitle = unsorted
	playback.update(0.5)
	expect_equal(playback.get_current_segment().text, "early", "unsorted segments are found")
	playback.update(3.5)
	expect_equal(playback.get_current_segment().text, "late", "unsorted segments are found in time order")
	_events.clear()


func _test_own_clock() -> void:
	var player := SubtitlePlayer.new()
	player.process_mode = Node.PROCESS_MODE_PAUSABLE
	_log(player)
	player.subtitle = _three_segments()
	add_child(player)
	await _frames(2)
	expect(not player.is_playing(), "without autoplay the player does not play")
	expect_equal(_take(), "", "nothing is emitted before play")
	player.play(0.5)
	expect(player.is_playing(), "play starts the own clock")
	expect_equal(_take(), "+A =A", "play emits the segment at the start position")
	player.seek(2.5)
	expect_equal(_take(), "-A +B =B", "seek forward")
	await _frames(2)
	expect(player.get_playback_position() > 2.5, "the own clock advances")
	player.seek(0.25)
	expect_equal(_take(), "-B +A =A", "seek backwards")
	get_tree().paused = true
	var paused_at: float = player.get_playback_position()
	await _frames(3)
	expect_equal(player.get_playback_position(), paused_at, "the own clock stands still while the tree is paused")
	get_tree().paused = false
	player.stop()
	expect(not player.is_playing(), "stop stops the own clock")
	expect_equal(_take(), "-A =", "stop ends the active segment")
	player.time_offset = 2.0
	player.play(0.5)
	expect_equal(_take(), "+B =B", "time_offset is added to the position")
	expect_equal(player.get_playback_position(), 0.5, "get_playback_position is without time_offset")
	player.queue_free()
	var autoplayer := SubtitlePlayer.new()
	autoplayer.subtitle = _three_segments()
	autoplayer.autoplay = true
	add_child(autoplayer)
	expect(autoplayer.is_playing(), "autoplay starts the own clock when ready")
	expect_equal(autoplayer.get_playback().get_current_segment().text, "A", "autoplay starts at 0")
	autoplayer.queue_free()


func _test_animation_sync() -> void:
	var animation := Animation.new()
	animation.length = 10.0
	var library := AnimationLibrary.new()
	library.add_animation(&"talk", animation)
	var animation_player := AnimationPlayer.new()
	animation_player.add_animation_library(&"", library)
	add_child(animation_player)
	var player: SubtitlePlayer = _synced_player(animation_player)
	player.subtitle = _three_segments()
	animation_player.play(&"talk")
	animation_player.seek(2.5, true)
	await _frames(2)
	expect(player.is_playing(), "the player follows a playing AnimationPlayer")
	expect_equal(_take(), "+B =B", "AnimationPlayer sync: B at 2.5 s")
	animation_player.seek(0.5, true)
	await _frames(2)
	expect_equal(_take(), "-B +A =A", "AnimationPlayer sync: seeking backwards")
	animation_player.stop()
	await _frames(2)
	expect(not player.is_playing(), "a stopped AnimationPlayer stops the subtitles")
	expect_equal(_take(), "-A =", "AnimationPlayer sync: stop ends the segment")
	player.queue_free()
	animation_player.queue_free()


func _test_audio_sync() -> void:
	var audio: AudioStreamPlayer = _audio_player("")
	var player: SubtitlePlayer = _synced_player(audio)
	player.subtitle = _three_segments()
	audio.play(2.5)
	await _frames(2)
	expect(player.is_playing(), "the player follows a playing AudioStreamPlayer")
	expect_equal(_take(), "+B =B", "AudioStreamPlayer sync: B at 2.5 s")
	audio.seek(0.5)
	await _frames(2)
	expect_equal(_take(), "-B +A =A", "AudioStreamPlayer sync: seeking backwards")
	audio.stop()
	await _frames(2)
	expect(not player.is_playing(), "a stopped AudioStreamPlayer stops the subtitles")
	expect_equal(_take(), "-A =", "AudioStreamPlayer sync: stop ends the segment")
	player.queue_free()
	audio.queue_free()


func _test_video_sync() -> void:
	var video := VideoStreamPlayer.new()
	var streams: Array[VideoStream] = []
	video.stream_changed.connect(func(stream: VideoStream) -> void: streams.append(stream))
	video.stream = load(VIDEO)
	expect_equal(streams, [video.stream], "VideoStreamPlayer emits stream_changed")
	add_child(video)
	# The clip is about 1 s long: "early" until 0.5 s, then "late".
	var subtitle := Subtitle.new()
	subtitle.add_segment(_segment(0.0, 0.5, "early"))
	subtitle.add_segment(_segment(0.5, 5.0, "late"))
	var player: SubtitlePlayer = _synced_player(video)
	player.subtitle = subtitle
	video.play()
	await _frames(2)
	expect(player.is_playing(), "the player follows a playing VideoStreamPlayer")
	expect_equal(_take(), "+early =early", "VideoStreamPlayer sync: early at the start")
	video.stream_position = 0.7
	await _frames(2)
	expect_equal(_take(), "-early +late =late", "VideoStreamPlayer sync: seeking forward")
	video.stop()
	await _frames(2)
	expect(not player.is_playing(), "a stopped VideoStreamPlayer stops the subtitles")
	expect_equal(_take(), "-late =", "VideoStreamPlayer sync: stop ends the segment")
	player.queue_free()
	video.queue_free()


func _test_subtitle_path_properties() -> void:
	var stream := AudioStreamWAV.new()
	expect_equal(stream.subtitle_path, "", "AudioStream.subtitle_path is empty by default")
	stream.subtitle_path = LINE_A
	expect_equal(stream.subtitle_path, LINE_A, "AudioStream.subtitle_path can be set")
	var video: VideoStream = load(VIDEO)
	video.subtitle_path = LINE_B
	expect_equal(video.subtitle_path, LINE_B, "VideoStream.subtitle_path can be set")
	for player: Variant in [AudioStreamPlayer.new(), AudioStreamPlayer2D.new(), AudioStreamPlayer3D.new(), VideoStreamPlayer.new()]:
		expect_equal(player.subtitle_path, "", "%s.subtitle_path is empty by default" % player.get_class())
		player.subtitle_path = LINE_B
		expect_equal(player.subtitle_path, LINE_B, "%s.subtitle_path can be set" % player.get_class())
		expect(player.has_signal("playing_changed"), "%s has the signal playing_changed" % player.get_class())
		player.free()
	video.subtitle_path = ""


func _test_playing_changed() -> void:
	var audio: AudioStreamPlayer = _audio_player("")
	var events: Array[bool] = []
	audio.playing_changed.connect(func(playing: bool) -> void: events.append(playing))
	audio.play()
	await _frames(3)
	audio.stream_paused = true
	await _frames(3)
	audio.stream_paused = false
	await _frames(3)
	audio.seek(1.0)
	await _frames(3)
	audio.stop()
	await _frames(3)
	expect_equal(events, [true, false, true, false], "playing_changed follows play, pause, resume and stop, but not seek")
	audio.queue_free()


func _test_subtitle_sources() -> void:
	var audio: AudioStreamPlayer = _audio_player(LINE_A)
	var player: SubtitlePlayer = _synced_player(audio)
	audio.play()
	await _frames(3)
	expect_equal(_first_text(player), "Track A", "the subtitle_path of the stream is loaded")
	expect(player.subtitle == null, "subtitle returns only an explicit subtitle")
	audio.stop()
	audio.subtitle_path = LINE_B
	audio.play()
	await _frames(3)
	expect_equal(_first_text(player), "Track B", "the subtitle_path of the player wins over the stream's")
	var explicit := Subtitle.new()
	explicit.add_segment(_segment(0.0, 5.0, "Explicit"))
	player.subtitle = explicit
	await _frames(3)
	expect_equal(_first_text(player), "Explicit", "an explicit subtitle wins over the paths")
	audio.stream = load(SIDECAR_STREAM)
	audio.subtitle_path = ""
	player.subtitle = null
	audio.play()
	await _frames(3)
	expect_equal(_first_text(player), "Sidecar", "a sidecar file next to the stream file is found")
	audio.queue_free()
	player.queue_free()
	_events.clear()


func _test_stream_switch() -> void:
	var audio: AudioStreamPlayer = _audio_player(LINE_A)
	var player: SubtitlePlayer = _synced_player(audio)
	audio.play()
	await _frames(3)
	audio.stream = _silence(LINE_B)
	audio.play()
	await _frames(3)
	expect_equal(_first_text(player), "Track B", "a new stream shows its subtitle_path")
	audio.crossfade_to(_silence(LINE_A), 0.1)
	await get_tree().create_timer(0.5).timeout
	expect_equal(_first_text(player), "Track A", "crossfade_to shows the subtitle_path of the new stream")
	audio.queue_free()
	player.queue_free()
	_events.clear()


func _test_hidden_when_stopped() -> void:
	var audio: AudioStreamPlayer = _audio_player(LINE_A)
	var player: SubtitlePlayer = _synced_player(audio)
	audio.play()
	await _frames(3)
	expect_equal(_events.back(), "=Track A", "the text is shown while playing")
	audio.stop()
	await _frames(3)
	expect_equal(_events.back(), "=", "the text is hidden after stop")
	audio.queue_free()
	player.queue_free()
	_events.clear()


## An AudioStreamPlayer in the tree with 10 seconds of silence that has the given subtitle_path.
func _audio_player(subtitle_path: String) -> AudioStreamPlayer:
	var audio := AudioStreamPlayer.new()
	audio.stream = _silence(subtitle_path)
	add_child(audio)
	return audio


## A logged SubtitlePlayer in the tree that follows sync.
func _synced_player(sync: Node) -> SubtitlePlayer:
	var player := SubtitlePlayer.new()
	_log(player)
	add_child(player)
	player.sync_node = player.get_path_to(sync)
	return player


func _silence(subtitle_path: String) -> AudioStreamWAV:
	var stream := AudioStreamWAV.new()
	stream.format = AudioStreamWAV.FORMAT_8_BITS
	stream.mix_rate = 8000
	var data := PackedByteArray()
	data.resize(80000)
	stream.data = data
	stream.subtitle_path = subtitle_path
	return stream


func _segment(start: float, end: float, text: String) -> SubtitleSegment:
	var segment := SubtitleSegment.new()
	segment.start_time = start
	segment.end_time = end
	segment.text = text
	return segment


## 0-1 "A", 2-4 "B", 3-5 "C" (overlaps B).
func _three_segments() -> Subtitle:
	var subtitle := Subtitle.new()
	subtitle.add_segment(_segment(0.0, 1.0, "A"))
	subtitle.add_segment(_segment(2.0, 4.0, "B"))
	subtitle.add_segment(_segment(3.0, 5.0, "C"))
	return subtitle


## The text of the first segment of the subtitle in use.
func _first_text(player: SubtitlePlayer) -> String:
	return player.get_playback().get_subtitle().get_segment(0).text


func _log(source: Object) -> void:
	source.segment_changed.connect(_on_segment_changed)
	source.segment_started.connect(func(segment: SubtitleSegment) -> void: _events.append("+" + segment.text))
	source.segment_ended.connect(func(segment: SubtitleSegment) -> void: _events.append("-" + segment.text))


## Returns the logged signals as one line and clears the log.
func _take() -> String:
	var result: String = " ".join(_events)
	_events.clear()
	return result


func _frames(count: int) -> void:
	for i: int in count:
		await get_tree().process_frame


func _on_segment_changed(segment: SubtitleSegment) -> void:
	if segment == null:
		_events.append("=")
		return
	_events.append("=" + segment.text)
