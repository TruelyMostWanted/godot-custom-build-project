extends FeatureTest
## Full API test of Subtitles (Subtitle, SubtitleSegment, SubtitleValidation, SubtitleServer) and the five text formats.
## Same tests as scripts/cs/Test.cs.

const RES := "res://subtitles/resources/"
const EXTENSIONS: Array[String] = ["srt", "vtt", "ttml", "dfxp", "ass", "ssa", "sbv"]
const NOTE := "♪"

var _events: Array[String] = []
var _clock: float = 0.0


func _run_tests() -> void:
	SubtitleServer.track_started.connect(func(_id: int, _subtitle: Subtitle) -> void: _events.append("track_started"))
	SubtitleServer.segment_started.connect(func(_id: int, _segment: SubtitleSegment, text: String) -> void: _events.append("segment_started " + text))
	SubtitleServer.segment_ended.connect(func(_id: int, segment: SubtitleSegment) -> void: _events.append("segment_ended " + segment.text))
	SubtitleServer.text_changed.connect(func(_id: int, text: String) -> void: _events.append("text_changed " + text.replace("\n", "|")))
	SubtitleServer.track_paused.connect(func(_id: int) -> void: _events.append("track_paused"))
	SubtitleServer.track_resumed.connect(func(_id: int) -> void: _events.append("track_resumed"))
	SubtitleServer.track_seeked.connect(func(_id: int, time: float) -> void: _events.append("track_seeked %s" % time))
	SubtitleServer.track_finished.connect(func(_id: int) -> void: _events.append("track_finished"))
	SubtitleServer.track_canceled.connect(func(_id: int) -> void: _events.append("track_canceled"))
	_test_constants()
	_test_segment()
	_test_segment_list()
	_test_validation()
	_test_srt()
	_test_srt_round_trip()
	_test_webvtt()
	_test_ttml()
	_test_ass()
	_test_sbv()
	_test_format_round_trips()
	_test_sample_resource()
	_test_display_text()
	await _test_subtitles_channel()
	_test_translatable_strings()
	_test_keys_srt()
	_test_own_clock()
	_test_settings()
	await _test_time_source()
	_test_dock_tools()


func _test_constants() -> void:
	expect_equal(SubtitleSegment.KIND_MAX, 7, "seven segment kinds")
	expect_equal(SubtitleSegment.PLACEMENT_MAX, 3, "three placements")
	expect_equal(Subtitle.FORMAT_MAX, 5, "five formats")
	expect_equal(SubtitleServer.INVALID_TRACK, 0, "INVALID_TRACK is 0")
	var extensions: PackedStringArray = ResourceLoader.get_recognized_extensions_for_type("Subtitle")
	for extension: String in EXTENSIONS:
		expect(extensions.has(extension), "ResourceLoader knows .%s" % extension)


func _test_segment() -> void:
	var segment: SubtitleSegment = _segment(1.0, 3.0, "Hello")
	expect_equal(segment.get_duration(), 2.0, "get_duration is end_time - start_time")
	expect(segment.contains_time(1.0), "contains_time includes the start")
	expect(not segment.contains_time(3.0), "contains_time excludes the end")
	expect(not segment.is_caption(), "dialog is not a caption")
	for kind: SubtitleSegment.Kind in [SubtitleSegment.KIND_SOUND, SubtitleSegment.KIND_REACTION, SubtitleSegment.KIND_MUSIC, SubtitleSegment.KIND_LYRICS]:
		segment.kind = kind
		expect(segment.is_caption(), "kind %d is a caption" % kind)


func _test_segment_list() -> void:
	var subtitle: Subtitle = _three_segments()
	var first: SubtitleSegment = _segment(-1.0, -0.5, "Z")
	subtitle.add_segment(first)
	expect(subtitle.get_segment(0) == first, "add_segment keeps the list sorted by start time")
	subtitle.remove_segment(0)
	expect_equal(subtitle.get_segment_count(), 3, "remove_segment removes a segment")
	expect_equal(subtitle.get_start_time(), 0.0, "get_start_time is the start of the first segment")
	expect_equal(subtitle.get_end_time(), 5.0, "get_end_time is the latest end")
	expect_equal(subtitle.find_segment_at(2.5).text, "B", "find_segment_at finds the visible segment")
	expect(subtitle.find_segment_at(1.5) == null, "find_segment_at returns null in a gap")
	expect_equal(subtitle.get_segments_at(3.5).size(), 2, "get_segments_at returns overlapping segments")
	subtitle.shift_times(10.0)
	expect_equal(subtitle.get_start_time(), 10.0, "shift_times moves every segment")
	var unsorted: Array[SubtitleSegment] = [_segment(5.0, 6.0, "late"), _segment(1.0, 2.0, "early")]
	subtitle.segments = unsorted
	subtitle.sort_segments()
	expect_equal(subtitle.get_segment(0).text, "early", "sort_segments sorts by start time")
	subtitle.clear_segments()
	expect_equal(subtitle.get_segment_count(), 0, "clear_segments removes all segments")


func _test_validation() -> void:
	var subtitle := Subtitle.new()
	subtitle.add_segment(_segment(0.0, 2.0, "Good."))
	subtitle.add_segment(_segment(3.0, 3.0, "No duration."))
	subtitle.add_segment(_segment(4.0, 6.0, ""))
	subtitle.add_segment(_segment(7.0, 9.0, "Overlaps."))
	subtitle.add_segment(_segment(8.0, 8.5, "Far too much text for half a second."))
	var validation: SubtitleValidation = subtitle.validate()
	expect(not validation.valid, "validate finds the problems")
	expect_equal(validation.invalid_time_segments, PackedInt32Array([1]), "invalid_time_segments")
	expect_equal(validation.empty_text_segments, PackedInt32Array([2]), "empty_text_segments")
	expect_equal(validation.overlapping_segments, PackedInt32Array([3]), "overlapping_segments")
	expect_equal(validation.fast_reading_segments, PackedInt32Array([4]), "fast_reading_segments")
	expect_equal(validation.get_problem_count(), 4, "get_problem_count")
	expect_equal(subtitle.validate(100.0).fast_reading_segments.size(), 0, "validate takes the characters per second")


func _test_srt() -> void:
	var subtitle: Subtitle = load(RES + "video_editor_export.srt")
	expect_equal(subtitle.get_segment_count(), 6, "SubRip: six numbered cues with CRLF line endings")
	expect_equal(subtitle.get_segment(0).start_time, 3600.5, "SubRip: 01:00:00,500 is 3600.5 s")
	expect_equal(subtitle.get_segment(0).text, "Willkommen bei den Untertiteln\nder Truely-Engine.", "SubRip: a two-line text")
	expect_equal(subtitle.get_segment(1).kind, SubtitleSegment.KIND_SOUND, "SubRip: [Applaus] is a sound")
	expect_equal(subtitle.get_segment(1).text, "Applaus", "SubRip: the brackets are not part of the text")
	expect_equal(subtitle.get_segment(2).kind, SubtitleSegment.KIND_MUSIC, "SubRip: music notes mark music")
	expect_equal(subtitle.get_segment(2).text, "ruhige Musik", "SubRip: the notes are not part of the text")
	expect_equal(subtitle.get_segment(3).speaker, &"WACHE", "SubRip: WACHE: is the speaker")
	expect_equal(subtitle.get_segment(3).text, "Wer ist da?", "SubRip: the speaker is not part of the text")
	expect(subtitle.get_segment(4).off_screen, "SubRip: <i> around the whole text is off-screen")
	expect_equal(subtitle.get_segment(5).text, "Fett und oben", "SubRip: <b> and {\\an8} are removed")
	subtitle.time_offset = -3600.0
	expect_equal(subtitle.get_start_time(), 0.5, "time_offset moves the start")
	expect_equal(subtitle.find_segment_at(4.0).text, "Applaus", "find_segment_at uses time_offset")
	subtitle.time_offset = 0.0


func _test_srt_round_trip() -> void:
	var subtitle := Subtitle.new()
	subtitle.add_segment(_segment(1.0, 2.5, "Hallo"))
	subtitle.add_segment(_segment(3.0, 4.0, "Applaus", SubtitleSegment.KIND_SOUND))
	var spoken: SubtitleSegment = _segment(5.0, 6.0, "Wer ist da?")
	spoken.speaker = &"WACHE"
	spoken.off_screen = true
	subtitle.add_segment(spoken)
	var text: String = subtitle.to_text(Subtitle.FORMAT_SRT)
	expect(text.begins_with("1\n00:00:01,000 --> 00:00:02,500\nHallo\n"), "to_text writes SubRip")
	expect(text.contains("[Applaus]"), "to_text writes a sound in brackets")
	expect(text.contains("<i>WACHE: Wer ist da?</i>"), "to_text writes the speaker and italics")
	var copy := Subtitle.new()
	expect_equal(copy.load_from_text(text, Subtitle.FORMAT_SRT), OK, "load_from_text reads SubRip")
	expect_equal(copy.get_segment(1).kind, SubtitleSegment.KIND_SOUND, "the kind survives the round trip")
	expect_equal(copy.get_segment(2).speaker, &"WACHE", "the speaker survives the round trip")
	expect(copy.get_segment(2).off_screen, "off_screen survives the round trip")
	expect_equal(Subtitle.new().load_from_text("only text", Subtitle.FORMAT_SRT), ERR_PARSE_ERROR, "text without cues is a parse error")
	var path := "user://subtitles_test.srt"
	expect_equal(subtitle.save_to_file(path), OK, "save_to_file writes the file")
	var from_file := Subtitle.new()
	expect_equal(from_file.load_from_file(path), OK, "load_from_file reads the file")
	expect_equal(from_file.get_segment(0).end_time, 2.5, "the times survive save_to_file and load_from_file")


func _test_webvtt() -> void:
	var subtitle: Subtitle = load(RES + "interview.vtt")
	expect_equal(subtitle.get_segment_count(), 4, "WebVTT: four cues, NOTE and STYLE skipped")
	expect_equal(subtitle.get_segment(0).speaker, &"Host", "WebVTT: <v Host> is the speaker")
	expect_equal(subtitle.get_segment(0).text, "Welcome to the show\n& thanks for coming.", "WebVTT: entities decoded, two lines")
	expect_equal(subtitle.get_segment(1).kind, SubtitleSegment.KIND_SOUND, "WebVTT: [Applause] is a sound")
	expect_equal(subtitle.get_segment(1).placement, SubtitleSegment.PLACEMENT_TOP, "WebVTT: line:0 is the top")
	expect(subtitle.get_segment(2).off_screen, "WebVTT: <i> is off-screen")
	expect_equal(subtitle.get_segment(2).speaker, &"Guest", "WebVTT: the voice inside italics is the speaker")
	expect_equal(subtitle.get_segment(3).kind, SubtitleSegment.KIND_MUSIC, "WebVTT: music notes mark music")


func _test_ttml() -> void:
	var subtitle: Subtitle = load(RES + "broadcast.ttml")
	expect_equal(subtitle.get_segment_count(), 4, "TTML: four paragraphs")
	expect_equal(subtitle.language, "de", "TTML: xml:lang is the language")
	expect_equal(subtitle.get_segment(0).text, "Guten Abend\nund willkommen.", "TTML: <br/> is a line break, whitespace collapsed")
	expect_equal(subtitle.get_segment(1).end_time, 4.48, "TTML: frames at 25 fps")
	expect_equal(subtitle.get_segment(2).end_time, 6.5, "TTML: an offset with dur")
	expect(subtitle.get_segment(2).off_screen, "TTML: an italic span is off-screen")
	expect_equal(subtitle.get_segment(3).speaker, &"WACHE", "TTML: a speaker in capitals")
	expect_equal(subtitle.get_segment(3).text, "Halt & stehen bleiben!", "TTML: entities decoded")


func _test_ass() -> void:
	var subtitle: Subtitle = load(RES + "fansub.ass")
	expect_equal(subtitle.get_segment_count(), 4, "ASS: four dialog lines, the comment skipped")
	expect_equal(subtitle.get_segment(0).speaker, &"Guard", "ASS: the Name field is the speaker")
	expect_equal(subtitle.get_segment(0).text, "Who is there,\nfriend?", "ASS: \\N is a line break, commas stay")
	expect_equal(subtitle.get_segment(1).placement, SubtitleSegment.PLACEMENT_TOP, "ASS: {\\an8} is the top")
	expect(subtitle.get_segment(2).off_screen, "ASS: {\\i1} is off-screen")
	expect_equal(subtitle.get_segment(3).text, "Styled text", "ASS: override tags dropped, \\h is a space")


func _test_sbv() -> void:
	var subtitle: Subtitle = load(RES + "youtube.sbv")
	expect_equal(subtitle.get_segment_count(), 3, "SBV: three cues")
	expect_equal(subtitle.get_segment(1).kind, SubtitleSegment.KIND_SOUND, "SBV: [Laughs] is a sound")
	expect_equal(subtitle.get_segment(2).speaker, &"HOST", "SBV: HOST: is the speaker")


func _test_format_round_trips() -> void:
	var source: Subtitle = load(RES + "interview.vtt")
	for extension: String in EXTENSIONS:
		var path: String = "user://subtitles_test." + extension
		ResourceSaver.save(source, path)
		var loaded: Subtitle = ResourceLoader.load(path, "", ResourceLoader.CACHE_MODE_IGNORE)
		var same: bool = loaded.get_segment_count() == source.get_segment_count()
		for i: int in source.get_segment_count():
			var a: SubtitleSegment = source.get_segment(i)
			var b: SubtitleSegment = loaded.get_segment(i)
			same = same and is_equal_approx(a.start_time, b.start_time) and is_equal_approx(a.end_time, b.end_time) and a.text == b.text and a.kind == b.kind
		expect(same, "a round trip through .%s keeps times, texts and kinds" % extension)
	var copy := Subtitle.new()
	copy.load_from_text(source.to_text(Subtitle.FORMAT_ASS), Subtitle.FORMAT_ASS)
	expect_equal(copy.get_segment(0).speaker, &"Host", "ASS keeps the speaker as written")


func _test_sample_resource() -> void:
	var story: Subtitle = load(RES + "village_story.tres")
	expect_equal(story.language, "de", "village_story.tres: language")
	expect_equal(story.get_segment_count(), 8, "village_story.tres: eight segments")
	var kinds: Dictionary = {}
	for segment: SubtitleSegment in story.segments:
		kinds[segment.kind] = true
	expect_equal(kinds.size(), SubtitleSegment.KIND_MAX, "village_story.tres uses every kind")
	expect_equal(story.get_segment(7).placement, SubtitleSegment.PLACEMENT_TOP, "village_story.tres: placement is stored")


func _test_display_text() -> void:
	var subtitle := Subtitle.new()
	var sound: SubtitleSegment = _segment(0.0, 1.0, "Applaus", SubtitleSegment.KIND_SOUND)
	expect_equal(SubtitleServer.get_display_text(subtitle, sound), "[Applaus]", "get_display_text puts a sound in brackets")
	expect_equal(SubtitleServer.get_display_text(subtitle, sound, true), "[lb]Applaus[rb]", "get_display_text with bbcode escapes the brackets")
	var music: SubtitleSegment = _segment(0.0, 1.0, "Musik", SubtitleSegment.KIND_MUSIC)
	expect_equal(SubtitleServer.get_display_text(subtitle, music), NOTE + " Musik " + NOTE, "get_display_text puts music between notes")
	var narration: SubtitleSegment = _segment(0.0, 1.0, "Es war einmal", SubtitleSegment.KIND_NARRATION)
	expect_equal(SubtitleServer.get_display_text(subtitle, narration, true), "[i]Es war einmal[/i]", "get_display_text with bbcode puts narration in italics")
	var translation := Translation.new()
	translation.locale = TranslationServer.get_locale()
	translation.add_message("Hallo", "Hello", &"greeting")
	var domain: TranslationDomain = TranslationServer.get_or_add_domain(&"subtitles")
	domain.add_translation(translation)
	var greeting: SubtitleSegment = _segment(0.0, 1.0, "Hallo")
	greeting.translation_context = &"greeting"
	expect_equal(SubtitleServer.get_display_text(subtitle, greeting), "Hallo", "without auto_translate the text stays")
	subtitle.auto_translate = true
	expect_equal(SubtitleServer.get_display_text(subtitle, greeting), "Hello", "auto_translate translates with translation_context")
	domain.remove_translation(translation)


func _test_subtitles_channel() -> void:
	# Voices in English, subtitles in German: the subtitles channel has its own locale.
	var catalog := TextLocalizationCatalog.new()
	catalog.channel = &"subtitles"
	catalog.set_text("LINE_1", "en", "Where are we?")
	catalog.set_text("LINE_1", "de", "Wo sind wir?")
	catalog.set_text("LINE_1", "de", "Wo bin ich?", &"alone")
	TextLocalizationServer.add_catalog(catalog)
	var locale: String = TranslationServer.get_locale()
	TranslationServer.set_locale("en")
	await get_tree().process_frame
	var subtitle := Subtitle.new()
	subtitle.auto_translate = true
	var line: SubtitleSegment = _segment(0.0, 1.0, "LINE_1")
	expect_equal(SubtitleServer.get_display_text(subtitle, line), "Where are we?", "subtitles follow the general locale")
	TranslationServer.set_locale_for(&"subtitles", "de")
	expect_equal(TranslationServer.get_locale_for(&"interface"), "en", "the interface stays English")
	expect_equal(SubtitleServer.get_display_text(subtitle, line), "Wo sind wir?", "subtitles follow the subtitles channel")
	var alone: SubtitleSegment = _segment(0.0, 1.0, "LINE_1")
	alone.translation_context = &"alone"
	expect_equal(SubtitleServer.get_display_text(subtitle, alone), "Wo bin ich?", "translation_context selects the context")
	expect_equal(tr("LINE_1"), "LINE_1", "the key is not in the main domain")
	subtitle.auto_translate = false
	expect_equal(SubtitleServer.get_display_text(subtitle, line), "LINE_1", "auto_translate off shows the key")
	TranslationServer.clear_locale_for(&"subtitles")
	TextLocalizationServer.remove_catalog(catalog)
	TranslationServer.set_locale(locale)


func _test_translatable_strings() -> void:
	var subtitle := Subtitle.new()
	subtitle.add_segment(_segment(0.0, 1.0, "Hello"))
	var spoken: SubtitleSegment = _segment(0.0, 1.0, "Run!")
	spoken.translation_context = &"chase"
	spoken.speaker = &"Anna"
	subtitle.add_segment(spoken)
	subtitle.add_segment(_segment(0.0, 1.0, ""))
	var strings: Array = subtitle.get_translatable_strings()
	expect_equal(strings.size(), 3, "get_translatable_strings skips segments without text, adds one entry per speaker")
	expect_equal(strings[0], PackedStringArray(["Hello", "", "", ""]), "entry: text, empty context")
	expect_equal(strings[1], PackedStringArray(["Run!", "chase", "", "Speaker: Anna"]), "entry: text, context, speaker comment")


func _test_keys_srt() -> void:
	var subtitle: Subtitle = load(RES + "line_keys.srt")
	expect(subtitle != null, "line_keys.srt loads as Subtitle")
	expect(not subtitle.auto_translate, "a loaded .srt starts with auto_translate off")
	var strings: Array = subtitle.get_translatable_strings()
	expect_equal(strings.size(), 2, "line_keys.srt gives one entry per cue")
	expect_equal(strings[1][0], "LINE_2", "cue text of line_keys.srt")


func _test_own_clock() -> void:
	var subtitle: Subtitle = _three_segments()
	var id: int = SubtitleServer.play(subtitle, Callable(), 2.5)
	expect(SubtitleServer.is_playing(id), "play starts a track")
	expect_equal(_take(), "track_started, segment_started B, text_changed B", "play emits track_started and the visible segment")
	expect_equal(SubtitleServer.get_track_ids(), PackedInt32Array([id]), "get_track_ids lists the track")
	expect(SubtitleServer.get_track_subtitle(id) == subtitle, "get_track_subtitle returns the subtitle")
	SubtitleServer.seek(id, 3.5)
	expect_equal(_take(), "track_seeked 3.5, segment_started C, text_changed B|C", "seek into an overlap")
	expect_equal(SubtitleServer.get_active_segments(id).size(), 2, "get_active_segments returns both segments")
	expect_equal(SubtitleServer.get_track_text(id), "B\nC", "get_track_text has one line per segment")
	SubtitleServer.pause(id)
	expect(SubtitleServer.is_paused(id), "pause pauses the track")
	SubtitleServer.resume(id)
	expect_equal(_take(), "track_paused, track_resumed", "pause and resume emit their signals")
	SubtitleServer.stop(id)
	expect_equal(_take(), "segment_ended B, segment_ended C, text_changed , track_canceled", "stop ends the visible segments and cancels")
	expect(not SubtitleServer.is_playing(id), "a stopped track is gone")
	SubtitleServer.play(subtitle)
	SubtitleServer.play(subtitle)
	SubtitleServer.stop_all()
	expect(SubtitleServer.get_track_ids().is_empty(), "stop_all stops every track")
	_events.clear()


func _test_settings() -> void:
	var subtitle := Subtitle.new()
	subtitle.add_segment(_segment(0.0, 5.0, "Hello"))
	subtitle.add_segment(_segment(0.0, 5.0, "Applause", SubtitleSegment.KIND_SOUND))
	var id: int = SubtitleServer.play(subtitle, Callable(), 1.0)
	SubtitleServer.captions_enabled = false
	expect_equal(SubtitleServer.get_track_text(id), "Hello", "captions_enabled = false hides sounds and keeps speech")
	SubtitleServer.captions_enabled = true
	SubtitleServer.enabled = false
	expect_equal(SubtitleServer.get_track_text(id), "", "enabled = false hides every segment")
	SubtitleServer.enabled = true
	expect_equal(SubtitleServer.get_track_text(id), "Hello\n[Applause]", "enabled and captions_enabled show everything again")
	SubtitleServer.stop(id)
	_events.clear()


func _test_time_source() -> void:
	_clock = 0.5
	var id: int = SubtitleServer.play(_three_segments(), func() -> float: return _clock)
	expect_equal(_take(), "track_started, segment_started A, text_changed A", "a time source starts at its time")
	_clock = 2.5
	await get_tree().process_frame
	await get_tree().process_frame
	expect_equal(_take(), "segment_ended A, segment_started B, text_changed B", "the frame update follows the time source")
	expect_equal(SubtitleServer.get_track_time(id), 2.5, "get_track_time is the source time")
	_clock = 10.0
	await get_tree().process_frame
	await get_tree().process_frame
	expect_equal(_take(), "segment_ended B, text_changed , track_finished", "past the end the track finishes")
	expect(not SubtitleServer.is_playing(id), "a finished track is gone")


func _segment(start: float, end: float, text: String, kind: SubtitleSegment.Kind = SubtitleSegment.KIND_DIALOG) -> SubtitleSegment:
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


## Returns the logged signals as one line and clears the log.
func _take() -> String:
	var result: String = ", ".join(_events)
	_events.clear()
	return result


func _test_dock_tools() -> void:
	_test_methods()
	_example()
	_test_shift_and_sync()
	_test_split()
	_test_merge()


func _segment_dock_tools(start: float, end: float, text: String) -> SubtitleSegment:
	var segment := SubtitleSegment.new()
	segment.start_time = start
	segment.end_time = end
	segment.text = text
	return segment


func _test_methods() -> void:
	expect(ClassDB.class_has_method("Subtitle", "sync_times"), "Subtitle.sync_times() exists")
	expect(ClassDB.class_has_method("Subtitle", "split_segment"), "Subtitle.split_segment() exists")
	expect(ClassDB.class_has_method("Subtitle", "merge_segments"), "Subtitle.merge_segments() exists")


# The snippet of the wiki guide "Subtitles dock", section "From scripts".
func _example() -> void:
	var subtitle := Subtitle.new()
	subtitle.add_segment(_segment_dock_tools(1.5, 3.0, "Hello there"))
	subtitle.add_segment(_segment_dock_tools(60.0, 61.0, "Bye"))
	subtitle.shift_times(-0.5)
	subtitle.sync_times(1.0, 2.0, 59.5, 62.5)
	var second := subtitle.split_segment(0, 2.5)
	subtitle.merge_segments(0)
	expect_equal(second, 1, "example: split_segment() returns the index of the new segment")
	expect_equal(subtitle.get_segment_count(), 2, "example: merge_segments() undoes the split")
	expect(is_equal_approx(subtitle.get_segment(1).start_time, 62.5), "example: the last line starts at its sync target")


func _test_shift_and_sync() -> void:
	var subtitle := Subtitle.new()
	subtitle.add_segment(_segment_dock_tools(10.0, 12.0, "a"))
	subtitle.add_segment(_segment_dock_tools(20.0, 22.0, "b"))
	subtitle.add_segment(_segment_dock_tools(30.0, 31.0, "c"))
	subtitle.sync_times(10.0, 11.0, 30.0, 51.0)
	expect(is_equal_approx(subtitle.get_segment(0).start_time, 11.0), "sync_times(): the first point moves to its target")
	expect(is_equal_approx(subtitle.get_segment(0).end_time, 15.0), "sync_times(): times are stretched (scale 2)")
	expect(is_equal_approx(subtitle.get_segment(1).start_time, 31.0), "sync_times(): times in between are mapped linearly")
	expect(is_equal_approx(subtitle.get_segment(2).start_time, 51.0), "sync_times(): the second point moves to its target")
	var changes := [0]
	subtitle.changed.connect(func(): changes[0] += 1)
	subtitle.sync_times(11.0, 12.0, 51.0, 52.0)
	expect(changes[0] >= 1, "sync_times(): emits changed")
	expect(is_equal_approx(subtitle.get_segment(2).end_time, 54.0), "sync_times(): equal offsets are a shift")


func _test_split() -> void:
	var subtitle := Subtitle.new()
	var first := _segment_dock_tools(0.0, 4.0, "First line\nSecond line")
	first.speaker = &"Ann"
	first.kind = SubtitleSegment.KIND_NARRATION
	subtitle.add_segment(first)
	subtitle.add_segment(_segment_dock_tools(10.0, 14.0, "one two three four"))
	var index := subtitle.split_segment(0, 1.5)
	expect_equal(index, 1, "split_segment(): returns the index of the second part")
	expect_equal(subtitle.get_segment_count(), 3, "split_segment(): adds one segment")
	expect_equal(subtitle.get_segment(0).end_time, 1.5, "split_segment(): the first part ends at the split")
	expect_equal(subtitle.get_segment(1).start_time, 1.5, "split_segment(): the second part starts at the split")
	expect_equal(subtitle.get_segment(1).end_time, 4.0, "split_segment(): the second part keeps the end")
	expect_equal(subtitle.get_segment(0).text, "First line", "split_segment(): the text is divided at the line break")
	expect_equal(subtitle.get_segment(1).text, "Second line", "split_segment(): the second line goes to the second part")
	expect_equal(subtitle.get_segment(1).speaker, &"Ann", "split_segment(): the speaker is kept")
	expect_equal(subtitle.get_segment(1).kind, SubtitleSegment.KIND_NARRATION, "split_segment(): the kind is kept")
	expect(subtitle.get_segment(1) != first, "split_segment(): the second part is a new segment")
	subtitle.split_segment(2, 13.0)
	expect_equal(subtitle.get_segment(2).text, "one two three", "split_segment(): one line is divided at the nearest space")
	expect_equal(subtitle.get_segment(3).text, "four", "split_segment(): the rest goes to the second part")


func _test_merge() -> void:
	var subtitle := Subtitle.new()
	subtitle.add_segment(_segment_dock_tools(0.0, 1.0, "a"))
	subtitle.add_segment(_segment_dock_tools(1.5, 2.0, "b"))
	subtitle.add_segment(_segment_dock_tools(3.0, 4.0, "c"))
	subtitle.add_segment(_segment_dock_tools(5.0, 6.0, "d"))
	var first := subtitle.get_segment(0)
	expect_equal(subtitle.merge_segments(0), OK, "merge_segments(): merges two segments by default")
	expect_equal(subtitle.get_segment_count(), 3, "merge_segments(): removes the merged segment")
	expect(subtitle.get_segment(0) == first, "merge_segments(): keeps the first segment")
	expect_equal(subtitle.get_segment(0).end_time, 2.0, "merge_segments(): spans to the last end")
	expect_equal(subtitle.get_segment(0).text, "a\nb", "merge_segments(): joins the texts one per line")
	expect_equal(subtitle.merge_segments(0, 3), OK, "merge_segments(): merges a count of segments")
	expect_equal(subtitle.get_segment_count(), 1, "merge_segments(): one segment is left")
	expect_equal(subtitle.get_segment(0).end_time, 6.0, "merge_segments(): the merged segment ends at the last end")
	expect_equal(subtitle.get_segment(0).text, "a\nb\nc\nd", "merge_segments(): all texts are kept")
