extends FeatureCheck
## GDScript checks of 260-feature-subtitle-player. Check the same things as scripts/cs/Issue260FeatureSubtitlePlayerCheck.cs.

const VIDEO_PATH := "res://196-feature-webm-video/resources/testsrc.webm"

var _events: Array[String] = []


func _get_check_name() -> String:
	return "260-feature-subtitle-player"


func _run_checks() -> void:
	_check_classes()
	_check_playback()
	_check_playback_skip_and_seek()
	await _check_own_clock()
	await _check_animation_sync()
	await _check_audio_sync()
	await _check_video_sync()


func _segment(start: float, end: float, text: String) -> SubtitleSegment:
	var segment := SubtitleSegment.new()
	segment.start_time = start
	segment.end_time = end
	segment.text = text
	return segment


# 0-1 "A", 2-4 "B", 3-5 "C" (overlaps B).
func _three_segments() -> Subtitle:
	var subtitle := Subtitle.new()
	subtitle.add_segment(_segment(0.0, 1.0, "A"))
	subtitle.add_segment(_segment(2.0, 4.0, "B"))
	subtitle.add_segment(_segment(3.0, 5.0, "C"))
	return subtitle


# Logs "+X" for segment_started, "-X" for segment_ended and "=X" for segment_changed ("=" alone for none).
func _log_signals(source: Object) -> void:
	source.segment_changed.connect(func(segment): _events.append("=" + (segment.text if segment else "")))
	source.segment_started.connect(func(segment): _events.append("+" + segment.text))
	source.segment_ended.connect(func(segment): _events.append("-" + segment.text))


func _take() -> String:
	var result := " ".join(_events)
	_events.clear()
	return result


func _frames(count := 2) -> void:
	for i in count:
		await get_tree().process_frame


func _check_classes() -> void:
	expect(ClassDB.class_exists("SubtitlePlayback"), "class SubtitlePlayback exists")
	expect(ClassDB.class_exists("SubtitlePlayer"), "class SubtitlePlayer exists")
	expect(SubtitlePlayback.new() is RefCounted, "SubtitlePlayback is a RefCounted")
	var player := SubtitlePlayer.new()
	expect(player is Node, "SubtitlePlayer is a Node")
	expect(player.get_playback() is SubtitlePlayback, "get_playback() returns the SubtitlePlayback")
	expect(not player.autoplay and player.sync_node.is_empty() and player.time_offset == 0.0, "defaults: no autoplay, no sync node, no offset")
	player.free()


func _check_playback() -> void:
	var playback := SubtitlePlayback.new()
	_log_signals(playback)
	playback.subtitle = _three_segments()

	playback.update(0.5)
	expect_equal(_take(), "+A =A", "update() starts A")
	playback.update(1.0)
	expect_equal(_take(), "-A =", "end times are exclusive")
	playback.update(3.5)
	expect_equal(_take(), "+B +C =C", "overlapping segments start in order, the current one is the latest")
	expect_equal(playback.get_active_segments().size(), 2, "two active segments")
	expect(is_equal_approx(playback.get_time(), 3.5), "get_time() is the last time")
	playback.update(4.5)
	expect_equal(_take(), "-B", "B ends, C stays current")
	expect_equal(playback.get_current_segment().text, "C", "get_current_segment() is C")

	playback.subtitle.time_offset = 10.0
	playback.update(10.5)
	expect_equal(_take(), "-C +A =A", "time_offset of the subtitle moves the segments")

	playback.reset()
	expect_equal(_take(), "-A =", "reset() ends the active segments")
	expect(playback.get_current_segment() == null, "nothing is active after reset()")


func _check_playback_skip_and_seek() -> void:
	var playback := SubtitlePlayback.new()
	_log_signals(playback)
	playback.subtitle = _three_segments()

	playback.update(0.5)
	_take()
	playback.update(10.0)
	expect_equal(_take(), "-A +B -B +C -C =", "update() reports segments skipped by a long frame")
	playback.seek(0.5)
	expect_equal(_take(), "+A =A", "seek() backwards")
	playback.seek(10.0)
	expect_equal(_take(), "-A =", "seek() forward skips silently")

	var unsorted := Subtitle.new()
	var segments: Array[SubtitleSegment] = [_segment(3.0, 4.0, "late"), _segment(0.0, 1.0, "early")]
	unsorted.segments = segments
	playback.subtitle = unsorted
	playback.update(0.5)
	expect_equal(playback.get_current_segment().text, "early", "unsorted segments are found")
	playback.update(3.5)
	expect_equal(playback.get_current_segment().text, "late", "unsorted segments are found in time order")
	_take()


func _check_own_clock() -> void:
	var player := SubtitlePlayer.new()
	player.process_mode = Node.PROCESS_MODE_PAUSABLE
	_log_signals(player)
	player.subtitle = _three_segments()
	add_child(player)
	await _frames()
	expect(not player.is_playing(), "no autoplay: not playing")
	expect_equal(_take(), "", "nothing happens before play()")

	player.play(0.5)
	expect(player.is_playing(), "play() starts the own clock")
	expect_equal(_take(), "+A =A", "play() emits the segment at the start position")
	player.seek(2.5)
	expect_equal(_take(), "-A +B =B", "seek() forward")
	await _frames()
	expect(player.get_playback_position() > 2.5, "the own clock advances (%s)" % player.get_playback_position())
	player.seek(0.25)
	expect_equal(_take(), "-B +A =A", "seek() backwards")

	get_tree().paused = true
	var paused_at := player.get_playback_position()
	await _frames(3)
	expect_equal(player.get_playback_position(), paused_at, "the own clock stands still while the tree is paused")
	get_tree().paused = false

	player.stop()
	expect(not player.is_playing(), "stop() stops")
	expect_equal(_take(), "-A =", "stop() ends the active segment")
	player.queue_free()

	var autoplayer := SubtitlePlayer.new()
	autoplayer.subtitle = _three_segments()
	autoplayer.autoplay = true
	add_child(autoplayer)
	expect(autoplayer.is_playing(), "autoplay starts the own clock when ready")
	var first := autoplayer.get_playback().get_current_segment()
	expect(first != null and first.text == "A", "autoplay starts at 0")
	autoplayer.queue_free()


func _check_animation_sync() -> void:
	var animation := Animation.new()
	animation.length = 10.0
	var library := AnimationLibrary.new()
	library.add_animation(&"talk", animation)
	var animation_player := AnimationPlayer.new()
	animation_player.add_animation_library(&"", library)
	add_child(animation_player)

	var player := SubtitlePlayer.new()
	_log_signals(player)
	player.subtitle = _three_segments()
	add_child(player)
	player.sync_node = player.get_path_to(animation_player)

	animation_player.play(&"talk")
	animation_player.seek(2.5, true)
	await _frames()
	expect(player.is_playing(), "follows a playing AnimationPlayer")
	expect_equal(_take(), "+B =B", "animation sync: B at 2.5 s")
	animation_player.seek(0.5, true)
	await _frames()
	expect_equal(_take(), "-B +A =A", "animation sync: seeking backwards")
	animation_player.stop()
	await _frames()
	expect(not player.is_playing(), "a stopped AnimationPlayer stops the subtitles")
	expect_equal(_take(), "-A =", "animation sync: stop ends the segment")
	player.queue_free()
	animation_player.queue_free()


func _silence(seconds: float) -> AudioStreamWAV:
	var stream := AudioStreamWAV.new()
	stream.format = AudioStreamWAV.FORMAT_8_BITS
	stream.mix_rate = 8000
	var data := PackedByteArray()
	data.resize(int(seconds * 8000))
	stream.data = data
	return stream


func _check_audio_sync() -> void:
	var audio := AudioStreamPlayer.new()
	audio.stream = _silence(6.0)
	add_child(audio)

	var player := SubtitlePlayer.new()
	_log_signals(player)
	player.subtitle = _three_segments()
	add_child(player)
	player.sync_node = player.get_path_to(audio)

	audio.play(2.5)
	await _frames()
	expect(player.is_playing(), "follows a playing AudioStreamPlayer")
	expect_equal(_take(), "+B =B", "audio sync: B at 2.5 s")
	audio.seek(0.5)
	await _frames()
	expect_equal(_take(), "-B +A =A", "audio sync: seeking backwards")
	audio.stop()
	await _frames()
	expect(not player.is_playing(), "a stopped AudioStreamPlayer stops the subtitles")
	expect_equal(_take(), "-A =", "audio sync: stop ends the segment")
	player.queue_free()
	audio.queue_free()


func _check_video_sync() -> void:
	var video := VideoStreamPlayer.new()
	video.stream = load(VIDEO_PATH)
	add_child(video)

	# The clip is about 1 s long: "early" until 0.5 s, then "late".
	var subtitle := Subtitle.new()
	subtitle.add_segment(_segment(0.0, 0.5, "early"))
	subtitle.add_segment(_segment(0.5, 5.0, "late"))
	var player := SubtitlePlayer.new()
	_log_signals(player)
	player.subtitle = subtitle
	add_child(player)
	player.sync_node = player.get_path_to(video)

	video.play()
	await _frames()
	expect(player.is_playing(), "follows a playing VideoStreamPlayer")
	expect_equal(_take(), "+early =early", "video sync: early at the start")
	video.stream_position = 0.7
	await _frames()
	expect_equal(_take(), "-early +late =late", "video sync: seeking forward")
	video.stop()
	await _frames()
	expect(not player.is_playing(), "a stopped VideoStreamPlayer stops the subtitles")
	expect_equal(_take(), "-late =", "video sync: stop ends the segment")
	player.queue_free()
	video.queue_free()
