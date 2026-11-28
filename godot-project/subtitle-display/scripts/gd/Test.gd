extends FeatureTest
## Full API test of Subtitle Display (SubtitleLabel): defaults, speakers, accessibility, captions, reparenting. Same tests as scripts/cs/Test.cs.




func _run_tests() -> void:
	_test_defaults()
	_test_player_and_speakers()
	_test_accessibility()
	await _test_changes()
	_test_table_label()


func _segment(start: float, end: float, text: String, speaker: StringName = &"", kind := SubtitleSegment.KIND_DIALOG) -> SubtitleSegment:
	var segment := SubtitleSegment.new()
	segment.start_time = start
	segment.end_time = end
	segment.text = text
	segment.speaker = speaker
	segment.kind = kind
	return segment


# 0-1 "Hello" (Anna), 2-4 "Applause" (sound), 3-4 "Run!" (Ben).
func _make_pair() -> Array:
	var subtitle := Subtitle.new()
	subtitle.add_segment(_segment(0.0, 1.0, "Hello", &"Anna"))
	subtitle.add_segment(_segment(2.0, 4.0, "Applause", &"", SubtitleSegment.KIND_SOUND))
	subtitle.add_segment(_segment(3.0, 4.0, "Run!", &"Ben"))
	var player := SubtitlePlayer.new()
	player.subtitle = subtitle
	add_child(player)
	var label := SubtitleLabel.new()
	add_child(label)
	label.player = label.get_path_to(player)
	return [player, label]


func _sized(label: SubtitleLabel, text: String) -> String:
	return "[font_size=%d]%s[/font_size]" % [label.get_display_font_size(), text]


func _test_defaults() -> void:
	var label := SubtitleLabel.new()
	expect(label.is_class("RichTextLabel"), "SubtitleLabel is a RichTextLabel")
	expect(label.bbcode_enabled, "bbcode_enabled is on")
	expect(label.fit_content, "fit_content is on")
	expect_equal(label.horizontal_alignment, HORIZONTAL_ALIGNMENT_CENTER, "centered")
	expect_equal(label.mouse_filter, Control.MOUSE_FILTER_IGNORE, "ignores the mouse")
	expect_equal(label.font_size, 32, "font_size is 32")
	expect(label.show_speaker_names, "show_speaker_names is on")
	expect(label.follow_accessibility_settings, "follow_accessibility_settings is on")
	expect(label.speaker_colors.is_empty(), "speaker_colors is empty")
	expect_equal(label.auto_translate_mode, Node.AUTO_TRANSLATE_MODE_DISABLED, "translated in the subtitles domain only")
	expect_equal(label.get_subtitle_text(), "", "no player: no text")
	label.free()


func _test_player_and_speakers() -> void:
	var pair := _make_pair()
	var player: SubtitlePlayer = pair[0]
	var label: SubtitleLabel = pair[1]
	label.follow_accessibility_settings = false
	player.play(0.5)
	expect_equal(label.text, _sized(label, "Anna: Hello"), "shows the line with the speaker")
	expect(label.visible, "visible while a line is shown")
	label.speaker_colors = {&"Anna": Color(1, 0, 0)}
	expect_equal(label.text, _sized(label, "[color=#ff0000ff]Anna: Hello[/color]"), "colors the speaker")
	label.show_speaker_names = false
	expect_equal(label.text, _sized(label, "[color=#ff0000ff]Hello[/color]"), "without the speaker name")
	label.show_speaker_names = true
	player.seek(1.5)
	expect_equal(label.text, "", "no line between segments")
	expect(not label.visible, "hidden while no line is shown")
	player.seek(3.5)
	expect_equal(label.text, _sized(label, "[lb]Applause[rb]\nBen: Run!"), "two lines, the caption without speaker")
	SubtitleServer.captions_enabled = false
	expect_equal(label.text, _sized(label, "Ben: Run!"), "captions_enabled off drops the caption at once")
	SubtitleServer.captions_enabled = true
	SubtitleServer.enabled = false
	expect_equal(label.text, "", "SubtitleServer.enabled off hides the line at once")
	SubtitleServer.enabled = true
	expect_equal(label.text, _sized(label, "[lb]Applause[rb]\nBen: Run!"), "SubtitleServer.enabled on shows it again")
	label.font_size = 20
	expect_equal(label.get_display_font_size(), 20, "font_size without the accessibility scale")
	player.stop()
	expect_equal(label.text, "", "stop clears the text")
	label.queue_free()
	player.queue_free()


func _test_accessibility() -> void:
	var enabled := AccessibilitySettingsServer.is_subtitles_enabled()
	var scale := AccessibilitySettingsServer.get_subtitle_scale()
	AccessibilitySettingsServer.set_subtitles_enabled(true)
	AccessibilitySettingsServer.set_subtitle_scale(1.0)
	var pair := _make_pair()
	var player: SubtitlePlayer = pair[0]
	var label: SubtitleLabel = pair[1]
	player.play(0.5)
	AccessibilitySettingsServer.set_subtitle_scale(2.0)
	expect_equal(label.get_display_font_size(), AccessibilitySettingsServer.scale_subtitle_font_size(32), "font size follows subtitle_scale")
	expect_equal(label.text, _sized(label, "Anna: Hello"), "the text updates on subtitle_scale_changed")
	AccessibilitySettingsServer.set_subtitles_enabled(false)
	expect_equal(label.text, "", "subtitles_enabled off hides the text")
	label.follow_accessibility_settings = false
	expect_equal(label.text, "[font_size=32]Anna: Hello[/font_size]", "not following: shown, unscaled")
	label.follow_accessibility_settings = true
	AccessibilitySettingsServer.set_subtitles_enabled(true)
	expect_equal(label.text, _sized(label, "Anna: Hello"), "subtitles_enabled on shows it again")
	label.queue_free()
	player.queue_free()
	AccessibilitySettingsServer.set_subtitles_enabled(enabled)
	AccessibilitySettingsServer.set_subtitle_scale(scale)


func _test_changes() -> void:
	var enabled := AccessibilitySettingsServer.is_subtitles_enabled()
	var captions := AccessibilitySettingsServer.is_captions_enabled()
	AccessibilitySettingsServer.set_subtitles_enabled(true)
	AccessibilitySettingsServer.set_captions_enabled(true)
	var pair := _make_pair()
	var player: SubtitlePlayer = pair[0]
	var label: SubtitleLabel = pair[1]
	player.play(3.5)
	expect_equal(label.text, _sized(label, "[lb]Applause[rb]\nBen: Run!"), "caption and speech")
	AccessibilitySettingsServer.set_captions_enabled(false)
	expect_equal(label.text, _sized(label, "Ben: Run!"), "AccessibilitySettings.captions_enabled off drops the caption")
	AccessibilitySettingsServer.set_captions_enabled(true)
	expect_equal(label.text, _sized(label, "[lb]Applause[rb]\nBen: Run!"), "captions_enabled on shows it again")
	var readies := [0]
	label.ready.connect(func() -> void: readies[0] += 1)
	remove_child(label)
	add_child(label)
	expect_equal(readies[0], 0, "reparenting does not run _ready() again")
	expect_equal(label.text, _sized(label, "[lb]Applause[rb]\nBen: Run!"), "still follows the player after reparenting")
	player.free()
	await get_tree().process_frame
	expect_equal(label.text, "", "a freed player clears the line")
	expect(not label.visible, "hidden after the player is freed")
	label.queue_free()
	AccessibilitySettingsServer.set_subtitles_enabled(enabled)
	AccessibilitySettingsServer.set_captions_enabled(captions)


func _test_table_label() -> void:
	_test_preview_properties()
	_test_translatable_speakers()
	_test_translated_speaker()


func _segment_table_label(start: float, end: float, text: String, speaker: StringName = &"", kind := SubtitleSegment.KIND_DIALOG) -> SubtitleSegment:
	var segment := SubtitleSegment.new()
	segment.start_time = start
	segment.end_time = end
	segment.text = text
	segment.speaker = speaker
	segment.kind = kind
	return segment


func _test_preview_properties() -> void:
	var label := SubtitleLabel.new()
	expect_equal(label.preview_speaker, &"Speaker", "preview_speaker default")
	expect_equal(label.preview_text, "The subtitle line appears here.", "preview_text default")
	label.preview_speaker = &"Anna"
	label.preview_text = "Hello"
	expect_equal(label.preview_speaker, &"Anna", "preview_speaker is set")
	expect_equal(label.preview_text, "Hello", "preview_text is set")
	label.refresh()
	expect_equal(label.text, "", "no preview while the game runs")
	label.free()


func _test_translatable_speakers() -> void:
	var subtitle := Subtitle.new()
	subtitle.add_segment(_segment_table_label(0.0, 1.0, "Hello", &"Anna"))
	subtitle.add_segment(_segment_table_label(1.0, 2.0, "Run!", &"Ben"))
	subtitle.add_segment(_segment_table_label(2.0, 3.0, "Again", &"Anna"))
	subtitle.add_segment(_segment_table_label(3.0, 4.0, "Door", &"Door", SubtitleSegment.KIND_SOUND))
	var strings := subtitle.get_translatable_strings()
	expect_equal(strings.size(), 6, "four texts, then two speakers")
	expect_equal(strings[5], PackedStringArray(["Ben", "speaker", "", "Speaker name"]), "speaker entry with the context speaker")
	expect_equal(strings[4][0], "Anna", "each speaker once")
	var speakers := 0
	for entry in strings:
		if entry[1] == "speaker":
			speakers += 1
	expect_equal(speakers, 2, "the speaker of a caption is not listed")


func _test_translated_speaker() -> void:
	var german := Translation.new()
	german.locale = "de"
	german.add_message(&"Anna", &"Annchen", &"speaker")
	var domain := TranslationServer.get_or_add_domain(&"subtitles")
	domain.add_translation(german)
	var subtitle := Subtitle.new()
	var segment := _segment_table_label(0.0, 1.0, "Hello", &"Anna")
	subtitle.add_segment(segment)
	expect_equal(SubtitleServer.get_display_speaker(subtitle, segment), "Anna", "auto_translate off: the name as is")
	subtitle.auto_translate = true
	TranslationServer.set_locale_for(&"subtitles", "de")
	expect_equal(SubtitleServer.get_display_speaker(subtitle, segment), "Annchen", "translated through the subtitles channel")
	var player := SubtitlePlayer.new()
	player.subtitle = subtitle
	add_child(player)
	var label := SubtitleLabel.new()
	add_child(label)
	label.follow_accessibility_settings = false
	label.player = label.get_path_to(player)
	player.play(0.5)
	expect_equal(label.text, "[font_size=%d]Annchen: Hello[/font_size]" % label.get_display_font_size(), "the label shows the translated name")
	TranslationServer.clear_locale_for(&"subtitles")
	expect_equal(SubtitleServer.get_display_speaker(subtitle, segment), "Anna", "back to the source name")
	label.queue_free()
	player.queue_free()
	domain.remove_translation(german)
