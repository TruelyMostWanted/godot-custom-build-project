extends FeatureCheck
## GDScript checks of 233-feature-subtitle-formats. Check the same things as scripts/cs/Issue233FeatureSubtitleFormatsCheck.cs.

const DIR := "res://233-feature-subtitle-formats/resources/"


func _get_check_name() -> String:
	return "233-feature-subtitle-formats"


func _run_checks() -> void:
	_check_enum()
	_check_webvtt()
	_check_ttml()
	_check_ass()
	_check_sbv()
	_check_round_trips()


func _check_enum() -> void:
	expect_equal(Subtitle.FORMAT_MAX, 5, "five formats")
	var extensions := ResourceLoader.get_recognized_extensions_for_type("Subtitle")
	for extension in ["srt", "vtt", "ttml", "dfxp", "ass", "ssa", "sbv"]:
		expect(extensions.has(extension), "ResourceLoader knows .%s" % extension)


func _check_webvtt() -> void:
	var subtitle := load(DIR + "interview.vtt") as Subtitle
	expect(subtitle != null and subtitle.get_segment_count() == 4, "interview.vtt: four cues (NOTE and STYLE skipped)")
	if subtitle == null or subtitle.get_segment_count() != 4:
		return
	expect_equal(subtitle.get_segment(0).speaker, &"Host", "<v Host> is the speaker")
	expect_equal(subtitle.get_segment(0).text, "Welcome to the show\n& thanks for coming.", "entities decoded, two lines")
	expect_equal(subtitle.get_segment(1).kind, SubtitleSegment.KIND_SOUND, "[Applause] is a sound")
	expect_equal(subtitle.get_segment(1).placement, SubtitleSegment.PLACEMENT_TOP, "line:0 is the top")
	expect(subtitle.get_segment(2).off_screen and subtitle.get_segment(2).speaker == &"Guest", "voice and italics")
	expect_equal(subtitle.get_segment(3).kind, SubtitleSegment.KIND_MUSIC, "music notes")


func _check_ttml() -> void:
	var subtitle := load(DIR + "broadcast.ttml") as Subtitle
	expect(subtitle != null and subtitle.get_segment_count() == 4, "broadcast.ttml: four paragraphs")
	if subtitle == null or subtitle.get_segment_count() != 4:
		return
	expect_equal(subtitle.language, "de", "xml:lang is the language")
	expect_equal(subtitle.get_segment(0).text, "Guten Abend\nund willkommen.", "<br/> and whitespace")
	expect(is_equal_approx(subtitle.get_segment(1).end_time, 4.48), "frames at 25 fps")
	expect(is_equal_approx(subtitle.get_segment(2).end_time, 6.5), "offset and dur")
	expect(subtitle.get_segment(2).off_screen, "italic span is off-screen")
	expect_equal(subtitle.get_segment(3).speaker, &"WACHE", "speaker in capitals")
	expect_equal(subtitle.get_segment(3).text, "Halt & stehen bleiben!", "entity decoded")


func _check_ass() -> void:
	var subtitle := load(DIR + "fansub.ass") as Subtitle
	expect(subtitle != null and subtitle.get_segment_count() == 4, "fansub.ass: four dialog lines (comment skipped)")
	if subtitle == null or subtitle.get_segment_count() != 4:
		return
	expect_equal(subtitle.get_segment(0).speaker, &"Guard", "Name field is the speaker")
	expect_equal(subtitle.get_segment(0).text, "Who is there,\nfriend?", "\\N is a line break, commas stay")
	expect_equal(subtitle.get_segment(1).placement, SubtitleSegment.PLACEMENT_TOP, "{\\an8} is the top")
	expect(subtitle.get_segment(2).off_screen, "{\\i1} is italics")
	expect_equal(subtitle.get_segment(3).text, "Styled text", "override tags dropped, \\h is a space")


func _check_sbv() -> void:
	var subtitle := load(DIR + "youtube.sbv") as Subtitle
	expect(subtitle != null and subtitle.get_segment_count() == 3, "youtube.sbv: three cues")
	if subtitle == null or subtitle.get_segment_count() != 3:
		return
	expect_equal(subtitle.get_segment(1).kind, SubtitleSegment.KIND_SOUND, "[Laughs] is a sound")
	expect_equal(subtitle.get_segment(2).speaker, &"HOST", "HOST: is the speaker")


func _check_round_trips() -> void:
	var source := load(DIR + "interview.vtt") as Subtitle
	if source == null:
		return
	for extension: String in ["srt", "vtt", "ttml", "dfxp", "ass", "ssa", "sbv"]:
		var path := "user://check_233_gd." + extension
		var saved := ResourceSaver.save(source, path) == OK
		var loaded := ResourceLoader.load(path, "", ResourceLoader.CACHE_MODE_IGNORE) as Subtitle
		var ok := saved and loaded != null and loaded.get_segment_count() == source.get_segment_count()
		if ok:
			for i in source.get_segment_count():
				var a := source.get_segment(i)
				var b := loaded.get_segment(i)
				ok = ok and is_equal_approx(a.start_time, b.start_time) and is_equal_approx(a.end_time, b.end_time) and a.text == b.text and a.kind == b.kind
		expect(ok, "round trip through .%s keeps times, texts and kinds" % extension)
	var copy := Subtitle.new()
	expect_equal(copy.load_from_text(source.to_text(Subtitle.FORMAT_ASS), Subtitle.FORMAT_ASS), OK, "to_text() and load_from_text() with FORMAT_ASS")
	expect_equal(copy.get_segment(0).speaker, &"Host", "ASS keeps the speaker as written")
