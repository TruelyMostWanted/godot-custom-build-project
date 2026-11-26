extends FeatureCheck
## GDScript checks of 227-feature-subtitles. Check the same things as scripts/cs/Issue227FeatureSubtitlesCheck.cs.

const DIR := "res://227-feature-subtitles/resources/"
const NOTE := "♪"

var _events: Array[String] = []
var _clock := 0.0


func _get_check_name() -> String:
	return "227-feature-subtitles"


func _run_checks() -> void:
	_check_classes()
	_check_srt_from_video_editor()
	_check_round_trip()
	_check_example_resource()
	_check_validation()
	_check_display_text()
	_check_own_clock()
	await _check_time_source()


func _segment(start: float, end: float, text: String, kind := SubtitleSegment.KIND_DIALOG) -> SubtitleSegment:
	var segment := SubtitleSegment.new()
	segment.start_time = start
	segment.end_time = end
	segment.text = text
	segment.kind = kind
	return segment


func _three_segments() -> Subtitle:
	var subtitle := Subtitle.new()
	subtitle.add_segment(_segment(0.0, 1.0, "A"))
	subtitle.add_segment(_segment(2.0, 4.0, "B"))
	subtitle.add_segment(_segment(3.0, 5.0, "C"))
	return subtitle


func _check_classes() -> void:
	for name in ["Subtitle", "SubtitleSegment", "SubtitleValidation", "SubtitleServer"]:
		expect(ClassDB.class_exists(name), "class %s exists" % name)
	expect(Engine.has_singleton("SubtitleServer"), "SubtitleServer is a singleton")
	expect_equal(SubtitleSegment.KIND_MAX, 7, "seven segment kinds")
	expect_equal(SubtitleServer.INVALID_TRACK, 0, "INVALID_TRACK is 0")
	expect(ResourceLoader.get_recognized_extensions_for_type("Subtitle").has("srt"), "ResourceLoader knows .srt for Subtitle")


func _check_srt_from_video_editor() -> void:
	var subtitle := load(DIR + "video_editor_export.srt") as Subtitle
	expect(subtitle != null, "load() returns a Subtitle for .srt")
	if subtitle == null:
		return
	expect_equal(subtitle.get_segment_count(), 6, "six cues (CRLF, numbered)")
	var first := subtitle.get_segment(0)
	expect(is_equal_approx(first.start_time, 3600.5), "first cue starts at 01:00:00,500")
	expect_equal(first.text, "Willkommen bei den Untertiteln\nder Truely-Engine.", "two-line text")
	expect_equal(subtitle.get_segment(1).kind, SubtitleSegment.KIND_SOUND, "[Applaus] is a sound")
	expect_equal(subtitle.get_segment(1).text, "Applaus", "the brackets are not part of the text")
	expect_equal(subtitle.get_segment(2).kind, SubtitleSegment.KIND_MUSIC, "music notes mark music")
	expect_equal(subtitle.get_segment(2).text, "ruhige Musik", "the notes are not part of the text")
	expect_equal(subtitle.get_segment(3).speaker, &"WACHE", "WACHE: is the speaker")
	expect_equal(subtitle.get_segment(3).text, "Wer ist da?", "the speaker is not part of the text")
	expect(subtitle.get_segment(4).off_screen, "<i> around the whole text is off-screen")
	expect_equal(subtitle.get_segment(5).text, "Fett und oben", "<b> and {\\an8} are removed")

	subtitle.time_offset = -3600.0
	expect(is_equal_approx(subtitle.get_start_time(), 0.5), "time_offset moves the start to 0.5 s")
	var at_four := subtitle.find_segment_at(4.0)
	expect(at_four != null and at_four.text == "Applaus", "find_segment_at() uses the offset")
	subtitle.time_offset = 0.0


func _check_round_trip() -> void:
	var subtitle := Subtitle.new()
	subtitle.add_segment(_segment(1.0, 2.5, "Hallo"))
	subtitle.add_segment(_segment(3.0, 4.0, "Applaus", SubtitleSegment.KIND_SOUND))
	var spoken := _segment(5.0, 6.0, "Wer ist da?")
	spoken.speaker = &"WACHE"
	spoken.off_screen = true
	subtitle.add_segment(spoken)

	var text := subtitle.to_text(Subtitle.FORMAT_SRT)
	expect(text.begins_with("1\n00:00:01,000 --> 00:00:02,500\nHallo\n"), "to_text() writes SubRip")
	expect(text.contains("[Applaus]") and text.contains("<i>WACHE: Wer ist da?</i>"), "to_text() writes decorations back")

	var copy := Subtitle.new()
	expect_equal(copy.load_from_text(text, Subtitle.FORMAT_SRT), OK, "load_from_text()")
	expect_equal(copy.get_segment_count(), 3, "three segments again")
	expect_equal(copy.get_segment(1).kind, SubtitleSegment.KIND_SOUND, "kind survives the round trip")
	expect(copy.get_segment(2).off_screen and copy.get_segment(2).speaker == &"WACHE", "speaker and off_screen survive")
	expect_equal(Subtitle.new().load_from_text("only text", Subtitle.FORMAT_SRT), ERR_PARSE_ERROR, "text without cues is a parse error")

	var path := "user://check_227_gd.srt"
	expect_equal(ResourceSaver.save(subtitle, path), OK, "ResourceSaver.save() writes .srt")
	var loaded := ResourceLoader.load(path, "", ResourceLoader.CACHE_MODE_IGNORE) as Subtitle
	expect(loaded != null and loaded.get_segment_count() == 3, "the saved .srt loads again")
	if loaded:
		expect(is_equal_approx(loaded.get_segment(0).end_time, 2.5), "times survive")


func _check_example_resource() -> void:
	var story := load(DIR + "village_story.tres") as Subtitle
	expect(story != null, "village_story.tres loads")
	if story == null:
		return
	expect_equal(story.language, "de", "language")
	expect_equal(story.get_segment_count(), 8, "eight segments")
	var kinds := {}
	for segment in story.segments:
		kinds[segment.kind] = true
	expect_equal(kinds.size(), SubtitleSegment.KIND_MAX, "every kind is used")
	expect_equal(story.get_segment(7).placement, SubtitleSegment.PLACEMENT_TOP, "placement is stored")
	expect_equal(story.get_segments_at(6.5).size(), 2, "overlapping sound and reaction")


func _check_validation() -> void:
	var subtitle := Subtitle.new()
	subtitle.add_segment(_segment(0.0, 2.0, "Gut."))
	subtitle.add_segment(_segment(3.0, 3.0, "Ohne Dauer."))
	subtitle.add_segment(_segment(4.0, 6.0, ""))
	subtitle.add_segment(_segment(7.0, 9.0, "Überlappt."))
	subtitle.add_segment(_segment(8.0, 8.5, "Viel zu viel Text für eine halbe Sekunde."))
	var validation := subtitle.validate()
	expect(not validation.valid, "problems are found")
	expect_equal(validation.invalid_time_segments, PackedInt32Array([1]), "invalid time")
	expect_equal(validation.empty_text_segments, PackedInt32Array([2]), "empty text")
	expect_equal(validation.overlapping_segments, PackedInt32Array([3]), "overlap")
	expect_equal(validation.fast_reading_segments, PackedInt32Array([4]), "fast reading")
	expect_equal(validation.get_problem_count(), 4, "four problems")


func _check_display_text() -> void:
	var subtitle := Subtitle.new()
	expect_equal(SubtitleServer.get_display_text(subtitle, _segment(0, 1, "Applaus", SubtitleSegment.KIND_SOUND)), "[Applaus]", "sound in brackets")
	expect_equal(SubtitleServer.get_display_text(subtitle, _segment(0, 1, "Musik", SubtitleSegment.KIND_MUSIC)), NOTE + " Musik " + NOTE, "music between notes")
	expect_equal(SubtitleServer.get_display_text(subtitle, _segment(0, 1, "Applaus", SubtitleSegment.KIND_SOUND), true), "[lb]Applaus[rb]", "BBCode escapes brackets")
	expect_equal(SubtitleServer.get_display_text(subtitle, _segment(0, 1, "Es war einmal", SubtitleSegment.KIND_NARRATION), true), "[i]Es war einmal[/i]", "narration in italics")


func _connect_log() -> void:
	SubtitleServer.track_started.connect(_on_track_started)
	SubtitleServer.segment_started.connect(_on_segment_started)
	SubtitleServer.segment_ended.connect(_on_segment_ended)
	SubtitleServer.text_changed.connect(_on_text_changed)
	SubtitleServer.track_paused.connect(_on_track_paused)
	SubtitleServer.track_resumed.connect(_on_track_resumed)
	SubtitleServer.track_seeked.connect(_on_track_seeked)
	SubtitleServer.track_finished.connect(_on_track_finished)
	SubtitleServer.track_canceled.connect(_on_track_canceled)


func _disconnect_log() -> void:
	SubtitleServer.track_started.disconnect(_on_track_started)
	SubtitleServer.segment_started.disconnect(_on_segment_started)
	SubtitleServer.segment_ended.disconnect(_on_segment_ended)
	SubtitleServer.text_changed.disconnect(_on_text_changed)
	SubtitleServer.track_paused.disconnect(_on_track_paused)
	SubtitleServer.track_resumed.disconnect(_on_track_resumed)
	SubtitleServer.track_seeked.disconnect(_on_track_seeked)
	SubtitleServer.track_finished.disconnect(_on_track_finished)
	SubtitleServer.track_canceled.disconnect(_on_track_canceled)


func _take() -> String:
	var result := ", ".join(_events)
	_events.clear()
	return result


func _check_own_clock() -> void:
	_connect_log()
	var id := SubtitleServer.play(_three_segments(), Callable(), 2.5)
	expect(SubtitleServer.is_playing(id), "play() starts a track")
	expect_equal(_take(), "track_started, segment_started B, text_changed B", "play() emits track_started and the visible segment")

	SubtitleServer.seek(id, 3.5)
	expect_equal(_take(), "track_seeked 3.5, segment_started C, text_changed B|C", "seek() into an overlap")
	expect_equal(SubtitleServer.get_active_segments(id).size(), 2, "two active segments")
	expect_equal(SubtitleServer.get_track_text(id), "B\nC", "track text has both lines")

	SubtitleServer.pause(id)
	SubtitleServer.resume(id)
	expect_equal(_take(), "track_paused, track_resumed", "pause() and resume()")

	SubtitleServer.captions_enabled = false
	SubtitleServer.captions_enabled = true
	expect_equal(_take(), "", "captions_enabled does not touch dialog")

	SubtitleServer.stop(id)
	expect_equal(_take(), "segment_ended B, segment_ended C, text_changed , track_canceled", "stop() ends the visible segments and cancels")
	expect(not SubtitleServer.is_playing(id), "the track is gone")
	_disconnect_log()


func _get_clock() -> float:
	return _clock


func _check_time_source() -> void:
	_connect_log()
	_clock = 0.5
	var id := SubtitleServer.play(_three_segments(), _get_clock)
	expect_equal(_take(), "track_started, segment_started A, text_changed A", "a time source starts at its time")

	_clock = 2.5
	await get_tree().process_frame
	await get_tree().process_frame
	expect_equal(_take(), "segment_ended A, segment_started B, text_changed B", "the frame update follows the time source")
	expect(is_equal_approx(SubtitleServer.get_track_time(id), 2.5), "track time is the source time")

	_clock = 10.0
	await get_tree().process_frame
	await get_tree().process_frame
	expect_equal(_take(), "segment_ended B, text_changed , track_finished", "past the end the track finishes")
	expect(not SubtitleServer.is_playing(id), "the finished track is gone")
	_disconnect_log()


func _on_track_started(_id: int, _subtitle: Subtitle) -> void:
	_events.append("track_started")


func _on_segment_started(_id: int, _segment: SubtitleSegment, text: String) -> void:
	_events.append("segment_started " + text)


func _on_segment_ended(_id: int, segment: SubtitleSegment) -> void:
	_events.append("segment_ended " + segment.text)


func _on_text_changed(_id: int, text: String) -> void:
	_events.append("text_changed " + text.replace("\n", "|"))


func _on_track_paused(_id: int) -> void:
	_events.append("track_paused")


func _on_track_resumed(_id: int) -> void:
	_events.append("track_resumed")


func _on_track_seeked(_id: int, time: float) -> void:
	_events.append("track_seeked %s" % time)


func _on_track_finished(_id: int) -> void:
	_events.append("track_finished")


func _on_track_canceled(_id: int) -> void:
	_events.append("track_canceled")
