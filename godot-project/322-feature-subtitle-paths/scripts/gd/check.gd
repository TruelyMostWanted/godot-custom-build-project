extends FeatureCheck
## GDScript checks of 322-feature-subtitle-paths. Check the same things as scripts/cs/Issue322FeatureSubtitlePathsCheck.cs.

const TRACK_A := "res://322-feature-subtitle-paths/track_a.srt"
const TRACK_B := "res://322-feature-subtitle-paths/track_b.srt"

var _events: Array[bool] = []


func _get_check_name() -> String:
	return "322-feature-subtitle-paths"


func _run_checks() -> void:
	_check_properties()
	await _check_playing_changed()
	await _check_stream_path()
	await _check_player_override()
	await _check_explicit_wins()
	await _check_stream_switch()
	await _check_crossfade()
	await _check_hidden_when_stopped()


func _silence(seconds: float) -> AudioStreamWAV:
	var stream := AudioStreamWAV.new()
	stream.format = AudioStreamWAV.FORMAT_16_BITS
	stream.mix_rate = 8000
	var data := PackedByteArray()
	data.resize(int(seconds * 8000) * 2)
	stream.data = data
	return stream


func _frames(count := 3) -> void:
	for i in count:
		await get_tree().process_frame


func _text(player: SubtitlePlayer) -> String:
	var subtitle := player.get_playback().get_subtitle()
	if subtitle == null or subtitle.get_segment_count() == 0:
		return ""
	return subtitle.get_segment(0).text


# An AudioStreamPlayer with a 10 s stream with the subtitle path `path`, and a synced SubtitlePlayer.
func _make_pair(path: String) -> Array:
	var audio := AudioStreamPlayer.new()
	var stream := _silence(10.0)
	stream.subtitle_path = path
	audio.stream = stream
	add_child(audio)
	var player := SubtitlePlayer.new()
	add_child(player)
	player.sync_node = player.get_path_to(audio)
	return [audio, player]


func _check_properties() -> void:
	var stream := AudioStreamWAV.new()
	expect_equal(stream.subtitle_path, "", "AudioStream.subtitle_path is empty by default")
	stream.subtitle_path = TRACK_A
	expect_equal(stream.subtitle_path, TRACK_A, "AudioStream.subtitle_path round trip")
	expect(ClassDB.class_has_method("VideoStream", "get_subtitle_path"), "VideoStream has get_subtitle_path()")
	expect(ClassDB.class_has_method("VideoStream", "set_subtitle_path"), "VideoStream has set_subtitle_path()")
	var names := []
	for property in ClassDB.class_get_property_list("VideoStream"):
		names.append(property.name)
	expect(names.has("subtitle_path"), "VideoStream has the property subtitle_path")
	if ClassDB.can_instantiate("VideoStream"):
		var video: VideoStream = ClassDB.instantiate("VideoStream")
		video.subtitle_path = TRACK_B
		expect_equal(video.subtitle_path, TRACK_B, "VideoStream.subtitle_path round trip")
	var players: Array[Node] = [AudioStreamPlayer.new(), AudioStreamPlayer2D.new(), AudioStreamPlayer3D.new(), VideoStreamPlayer.new()]
	for player in players:
		expect_equal(player.subtitle_path, "", "%s.subtitle_path is empty by default" % player.get_class())
		player.subtitle_path = TRACK_B
		expect_equal(player.subtitle_path, TRACK_B, "%s.subtitle_path round trip" % player.get_class())
		expect(player.has_signal("playing_changed"), "%s has the signal playing_changed" % player.get_class())
		player.free()
	expect(ClassDB.class_has_signal("VideoStreamPlayer", "stream_changed"), "VideoStreamPlayer has the signal stream_changed")


func _check_playing_changed() -> void:
	var audio := AudioStreamPlayer.new()
	audio.stream = _silence(10.0)
	add_child(audio)
	audio.playing_changed.connect(func(playing: bool): _events.append(playing))
	audio.play()
	await _frames()
	expect_equal(_events, [true], "playing_changed(true) on play()")
	audio.stream_paused = true
	await _frames()
	expect_equal(_events, [true, false], "playing_changed(false) on stream_paused = true")
	audio.stream_paused = false
	await _frames()
	expect_equal(_events, [true, false, true], "playing_changed(true) on resume")
	audio.stop()
	await _frames()
	expect_equal(_events, [true, false, true, false], "playing_changed(false) on stop()")
	audio.queue_free()
	_events.clear()


func _check_stream_path() -> void:
	var pair := _make_pair(TRACK_A)
	var audio: AudioStreamPlayer = pair[0]
	var player: SubtitlePlayer = pair[1]
	expect(player.subtitle == null, "the explicit subtitle stays empty")
	audio.play()
	await _frames()
	expect(player.get_playback().get_subtitle() != null, "the subtitle of the stream path is loaded after play()")
	expect_equal(_text(player), "Track A", "the first segment is Track A")
	expect(player.subtitle == null, "get_subtitle() returns only the explicit subtitle")
	audio.queue_free()
	player.queue_free()


func _check_player_override() -> void:
	var pair := _make_pair(TRACK_A)
	var audio: AudioStreamPlayer = pair[0]
	var player: SubtitlePlayer = pair[1]
	audio.subtitle_path = TRACK_B
	audio.play()
	await _frames()
	expect_equal(_text(player), "Track B", "the subtitle_path of the player wins over the stream's")
	audio.queue_free()
	player.queue_free()


func _check_explicit_wins() -> void:
	var pair := _make_pair(TRACK_A)
	var audio: AudioStreamPlayer = pair[0]
	var player: SubtitlePlayer = pair[1]
	var explicit := Subtitle.new()
	var segment := SubtitleSegment.new()
	segment.start_time = 0.0
	segment.end_time = 5.0
	segment.text = "Explicit"
	explicit.add_segment(segment)
	player.subtitle = explicit
	audio.play()
	await _frames()
	expect(player.subtitle == explicit, "get_subtitle() returns the explicit subtitle")
	expect_equal(_text(player), "Explicit", "the explicit subtitle wins over the paths")
	audio.queue_free()
	player.queue_free()


func _check_stream_switch() -> void:
	var pair := _make_pair(TRACK_A)
	var audio: AudioStreamPlayer = pair[0]
	var player: SubtitlePlayer = pair[1]
	audio.play()
	await _frames()
	expect_equal(_text(player), "Track A", "before the switch: Track A")
	var other := _silence(10.0)
	other.subtitle_path = TRACK_B
	audio.stream = other
	audio.play()
	await _frames()
	expect_equal(_text(player), "Track B", "after switching the stream: Track B")
	audio.queue_free()
	player.queue_free()


func _check_crossfade() -> void:
	var pair := _make_pair(TRACK_A)
	var audio: AudioStreamPlayer = pair[0]
	var player: SubtitlePlayer = pair[1]
	audio.play()
	await _frames()
	var other := _silence(10.0)
	other.subtitle_path = TRACK_B
	audio.crossfade_to(other, 0.1)
	await get_tree().create_timer(0.5).timeout
	await _frames()
	expect_equal(_text(player), "Track B", "after crossfade_to(): Track B")
	audio.queue_free()
	player.queue_free()


func _check_hidden_when_stopped() -> void:
	var pair := _make_pair(TRACK_A)
	var audio: AudioStreamPlayer = pair[0]
	var player: SubtitlePlayer = pair[1]
	var texts: Array[String] = []
	player.segment_changed.connect(func(segment): texts.append(segment.text if segment else ""))
	audio.play()
	await _frames()
	expect_equal(texts.back() if not texts.is_empty() else "<none>", "Track A", "text shown while playing")
	audio.stop()
	await _frames()
	expect_equal(texts.back() if not texts.is_empty() else "<none>", "", "text hidden after stop()")
	audio.queue_free()
	player.queue_free()
