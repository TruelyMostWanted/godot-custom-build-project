extends FeatureTest
## Full API test of Accessibility Settings (AccessibilitySettingsServer, AccessibilitySettings, AccessibilityColorScheme).
## Same tests as scripts/cs/Test.cs. UI scale, font size, color scheme and focus outline are checked on the root window
## and its overlay theme. The restart test starts the project again as its own process (desktop only).

const PROBE_SCENE := "res://accessibility-settings/scenes/restart_probe.tscn"
const ACCESSIBILITY_FILE := "user://settings/accessibility.ini"
const SCHEME_FILE := "user://accessibility_scheme.tres"
const DARK := AccessibilitySettingsServer.COLOR_SCHEME_HIGH_CONTRAST_DARK
const CUSTOM_TEXT := Color(0.9, 0.9, 0.2)

var _received: Dictionary = {}


func _run_tests() -> void:
	AccessibilitySettingsServer.reset_to_defaults()
	_test_defaults()
	_test_values()
	_test_validation()
	_test_ui_scale()
	await _test_theme()
	_test_helpers()
	_test_speech()
	_test_signals()
	_test_resources()
	_test_restart()
	AccessibilitySettingsServer.reset_to_defaults()
	await get_tree().process_frame
	DirAccess.remove_absolute(ACCESSIBILITY_FILE)


func _test_defaults() -> void:
	expect_equal(AccessibilitySettingsServer.get_ui_scale(), 1.0, "the default UI scale is 1")
	expect_equal(AccessibilitySettingsServer.get_font_size_factor(), 1.0, "the default font size factor is 1")
	expect_equal(AccessibilitySettingsServer.get_focus_outline_width(), 0, "the default focus outline is the one of the theme")
	expect(not AccessibilitySettingsServer.is_speaking_focused_controls(), "speaking focused controls is off by default")
	expect_equal(AccessibilitySettingsServer.get_colorblind_mode(), AccessibilitySettingsServer.COLORBLIND_MODE_NONE, "the default colorblind mode is none")
	var reduce_animation: int = DisplayServer.accessibility_should_reduce_animation()
	expect_equal(AccessibilitySettingsServer.is_reducing_motion(), reduce_animation == 1, "reduced motion follows the OS")
	var increase_contrast: int = DisplayServer.accessibility_should_increase_contrast()
	var expected_scheme: int = AccessibilitySettingsServer.COLOR_SCHEME_DEFAULT
	if increase_contrast == 1:
		expected_scheme = DARK
	expect_equal(AccessibilitySettingsServer.get_color_scheme(), expected_scheme, "the color scheme follows the OS contrast")
	var reduce_transparency: int = DisplayServer.accessibility_should_reduce_transparency()
	expect_equal(AccessibilitySettingsServer.is_reducing_transparency(), reduce_transparency == 1, "reduced transparency follows the OS")
	expect(not AccessibilitySettingsServer.is_subtitles_enabled(), "subtitles are off by default")
	expect_equal(AccessibilitySettingsServer.get_subtitle_scale(), 1.0, "the default subtitle scale is 1")
	expect_equal(AccessibilitySettingsServer.get_custom_color_scheme_path(), "", "no custom scheme by default")
	expect_equal(AccessibilitySettingsServer.get_speech_persona(), "", "no speech persona by default")
	var screen_reader: bool = AccessibilitySettingsServer.is_screen_reader_active()
	expect_equal(screen_reader, DisplayServer.accessibility_screen_reader_active() == 1, "is_screen_reader_active follows the OS")


func _test_values() -> void:
	AccessibilitySettingsServer.set_ui_scale(1.5)
	expect_equal(AccessibilitySettingsServer.get_ui_scale(), 1.5, "ui_scale round trip")
	AccessibilitySettingsServer.set_font_size_factor(1.25)
	expect_equal(AccessibilitySettingsServer.get_font_size_factor(), 1.25, "font_size_factor round trip")
	AccessibilitySettingsServer.set_color_scheme(DARK)
	expect_equal(AccessibilitySettingsServer.get_color_scheme(), DARK, "color_scheme round trip")
	AccessibilitySettingsServer.set_custom_color_scheme_path("res://accessibility-settings/resources/yellow_on_dark_scheme.tres")
	expect_equal(AccessibilitySettingsServer.get_custom_color_scheme_path(), "res://accessibility-settings/resources/yellow_on_dark_scheme.tres", "custom_color_scheme_path round trip")
	AccessibilitySettingsServer.set_focus_outline_width(4)
	expect_equal(AccessibilitySettingsServer.get_focus_outline_width(), 4, "focus_outline_width round trip")
	AccessibilitySettingsServer.set_reduce_motion(true)
	expect(AccessibilitySettingsServer.is_reducing_motion(), "reduce_motion round trip")
	AccessibilitySettingsServer.set_reduce_transparency(true)
	expect(AccessibilitySettingsServer.is_reducing_transparency(), "reduce_transparency round trip")
	AccessibilitySettingsServer.set_colorblind_mode(AccessibilitySettingsServer.COLORBLIND_MODE_DEUTERANOPIA)
	expect_equal(AccessibilitySettingsServer.get_colorblind_mode(), AccessibilitySettingsServer.COLORBLIND_MODE_DEUTERANOPIA, "colorblind_mode round trip")
	AccessibilitySettingsServer.set_subtitles_enabled(true)
	expect(AccessibilitySettingsServer.is_subtitles_enabled(), "subtitles_enabled round trip")
	AccessibilitySettingsServer.set_subtitle_scale(1.5)
	expect_equal(AccessibilitySettingsServer.get_subtitle_scale(), 1.5, "subtitle_scale round trip")
	AccessibilitySettingsServer.set_speech_persona("narrator")
	expect_equal(AccessibilitySettingsServer.get_speech_persona(), "narrator", "speech_persona round trip")
	AccessibilitySettingsServer.set_speak_focused_controls(true)
	expect(AccessibilitySettingsServer.is_speaking_focused_controls(), "speak_focused_controls round trip")
	AccessibilitySettingsServer.reset_to_defaults()


func _test_validation() -> void:
	AccessibilitySettingsServer.set_ui_scale(10.0)
	expect_equal(AccessibilitySettingsServer.get_ui_scale(), 4.0, "the UI scale is clamped to 4")
	AccessibilitySettingsServer.set_font_size_factor(0.1)
	expect_equal(AccessibilitySettingsServer.get_font_size_factor(), 0.5, "the font size factor is clamped to 0.5")
	AccessibilitySettingsServer.set_focus_outline_width(20)
	expect_equal(AccessibilitySettingsServer.get_focus_outline_width(), 8, "the focus outline is clamped to 8")
	AccessibilitySettingsServer.set_value(&"color_scheme", 42)
	expect_equal(AccessibilitySettingsServer.get_color_scheme(), AccessibilitySettingsServer.get_default_value(&"color_scheme"), "an unknown color scheme keeps the current one")
	AccessibilitySettingsServer.reset_to_defaults()


func _test_ui_scale() -> void:
	var root: Window = get_tree().root
	var base_factor: float = root.content_scale_factor
	AccessibilitySettingsServer.set_ui_scale(1.5)
	expect(is_equal_approx(root.content_scale_factor, base_factor * 1.5), "UI scale 150 % multiplies the root content scale factor")
	expect(is_equal_approx(WindowSettingsServer.get_content_scale_factor(), base_factor), "the window factor stays separate")
	AccessibilitySettingsServer.set_ui_scale(1.0)
	expect(is_equal_approx(root.content_scale_factor, base_factor), "UI scale 100 % gives the factor back")


func _test_theme() -> void:
	var root: Window = get_tree().root
	var previous_theme: Theme = root.theme
	expect(AccessibilitySettingsServer.get_overlay_theme() == null, "no overlay theme with the defaults")
	AccessibilitySettingsServer.set_color_scheme(DARK)
	var overlay: Theme = AccessibilitySettingsServer.get_overlay_theme()
	expect(root.theme == overlay, "a high-contrast scheme sets the overlay theme on the root window")
	var scheme: AccessibilityColorScheme = AccessibilitySettingsServer.get_active_color_scheme()
	expect_equal(overlay.get_color(&"font_color", &"Button"), Color.WHITE, "button text is white")
	var focus: StyleBoxFlat = overlay.get_stylebox(&"focus", &"Button")
	expect_equal(focus.border_color, scheme.focus_color, "the focus outline has the focus color of the scheme")
	var button := Button.new()
	button.text = "Play"
	add_child(button)
	await get_tree().process_frame
	expect_equal(button.get_theme_color(&"font_color"), Color.WHITE, "a button in the tree gets the text color of the scheme")
	expect_equal(AccessibilitySettingsServer.get_scheme_color(&"problem", Color.MAGENTA), scheme.problem_color, "get_scheme_color returns the role color")
	var font_size_before: int = button.get_theme_font_size(&"font_size")
	AccessibilitySettingsServer.set_font_size_factor(1.5)
	await get_tree().process_frame
	expect_equal(button.get_theme_font_size(&"font_size"), roundi(font_size_before * 1.5), "a button in the tree gets the bigger font")
	AccessibilitySettingsServer.set_focus_outline_width(4)
	var focus_box: StyleBoxFlat = AccessibilitySettingsServer.get_overlay_theme().get_stylebox(&"focus", &"Button")
	expect_equal(focus_box.border_width_left, 4, "the focus outline width reaches the focus style box")
	button.queue_free()
	AccessibilitySettingsServer.reset_to_defaults()
	expect(AccessibilitySettingsServer.get_overlay_theme() == null, "back at the defaults there is no overlay theme")
	expect(root.theme == previous_theme, "the root window gets its previous theme back")


func _test_helpers() -> void:
	AccessibilitySettingsServer.set_reduce_motion(true)
	expect_equal(AccessibilitySettingsServer.get_animation_duration(0.3), 0.0, "reduced motion: no animation duration")
	expect_equal(AccessibilitySettingsServer.get_motion_scale(), 0.0, "reduced motion: motion scale 0")
	AccessibilitySettingsServer.set_reduce_motion(false)
	expect_equal(AccessibilitySettingsServer.get_animation_duration(0.3), 0.3, "no reduced motion: the duration stays")
	AccessibilitySettingsServer.set_font_size_factor(2.0)
	expect_equal(AccessibilitySettingsServer.scale_font_size(16), 32, "scale_font_size multiplies by the factor")
	AccessibilitySettingsServer.set_subtitle_scale(1.5)
	expect_equal(AccessibilitySettingsServer.scale_subtitle_font_size(10), 30, "scale_subtitle_font_size multiplies by the subtitle scale")
	AccessibilitySettingsServer.reset_to_defaults()


func _test_speech() -> void:
	var button := Button.new()
	button.text = "Play"
	expect_equal(AccessibilitySettingsServer.get_control_speech_text(button), "Play", "the text of a button is spoken")
	button.accessibility_name = "Start the game"
	expect_equal(AccessibilitySettingsServer.get_control_speech_text(button), "Start the game", "accessibility_name wins over the text")
	var field := LineEdit.new()
	field.placeholder_text = "Player name"
	expect_equal(AccessibilitySettingsServer.get_control_speech_text(field), "Player name", "an empty field speaks its placeholder")
	field.free()
	var root: Window = get_tree().root
	var before: int = root.get_signal_connection_list(&"gui_focus_changed").size()
	AccessibilitySettingsServer.set_speak_focused_controls(true)
	expect_equal(root.get_signal_connection_list(&"gui_focus_changed").size(), before + 1, "speaking focused controls connects to the root window")
	AccessibilitySettingsServer.set_speak_focused_controls(false)
	expect_equal(root.get_signal_connection_list(&"gui_focus_changed").size(), before, "turning it off disconnects")
	if TTSServer.is_available() and not AccessibilitySettingsServer.is_screen_reader_active():
		var persona := TTSPersona.new()
		persona.persona_name = &"test_narrator"
		TTSServer.add_persona(persona)
		AccessibilitySettingsServer.set_speech_persona("test_narrator")
		var utterance: int = AccessibilitySettingsServer.speak_control(button)
		expect(utterance >= 0, "speak_control returns an utterance id")
		TTSServer.stop()
		TTSServer.remove_persona(&"test_narrator")
	else:
		print("  SKIP  speak_control with a voice (no TTS voice or a screen reader is active)")
		expect_equal(AccessibilitySettingsServer.speak_control(button), -1, "without a voice nothing is spoken")
	button.free()
	AccessibilitySettingsServer.reset_to_defaults()


func _test_signals() -> void:
	AccessibilitySettingsServer.color_scheme_changed.connect(func(value: AccessibilityColorScheme) -> void: _received["color_scheme"] = value)
	AccessibilitySettingsServer.colorblind_mode_changed.connect(func(value: int) -> void: _received["colorblind_mode"] = value)
	AccessibilitySettingsServer.focus_outline_width_changed.connect(func(value: int) -> void: _received["focus_outline_width"] = value)
	AccessibilitySettingsServer.font_size_factor_changed.connect(func(value: float) -> void: _received["font_size_factor"] = value)
	AccessibilitySettingsServer.reduce_motion_changed.connect(func(value: bool) -> void: _received["reduce_motion"] = value)
	AccessibilitySettingsServer.reduce_transparency_changed.connect(func(value: bool) -> void: _received["reduce_transparency"] = value)
	AccessibilitySettingsServer.speak_focused_controls_changed.connect(func(value: bool) -> void: _received["speak_focused_controls"] = value)
	AccessibilitySettingsServer.subtitle_scale_changed.connect(func(value: float) -> void: _received["subtitle_scale"] = value)
	AccessibilitySettingsServer.subtitles_enabled_changed.connect(func(value: bool) -> void: _received["subtitles_enabled"] = value)
	AccessibilitySettingsServer.ui_scale_changed.connect(func(value: float) -> void: _received["ui_scale"] = value)
	AccessibilitySettingsServer.reset_to_defaults()
	AccessibilitySettingsServer.set_color_scheme(DARK)
	AccessibilitySettingsServer.set_colorblind_mode(AccessibilitySettingsServer.COLORBLIND_MODE_TRITANOPIA)
	AccessibilitySettingsServer.set_focus_outline_width(3)
	AccessibilitySettingsServer.set_font_size_factor(1.5)
	AccessibilitySettingsServer.set_reduce_motion(true)
	AccessibilitySettingsServer.set_reduce_transparency(true)
	AccessibilitySettingsServer.set_speak_focused_controls(true)
	AccessibilitySettingsServer.set_subtitle_scale(2.0)
	AccessibilitySettingsServer.set_subtitles_enabled(true)
	AccessibilitySettingsServer.set_ui_scale(1.5)
	expect(_received.get("color_scheme") is AccessibilityColorScheme, "color_scheme_changed passes the scheme")
	expect_equal(_received.get("colorblind_mode"), AccessibilitySettingsServer.COLORBLIND_MODE_TRITANOPIA, "colorblind_mode_changed passes the mode")
	expect_equal(_received.get("focus_outline_width"), 3, "focus_outline_width_changed passes the width")
	expect_equal(_received.get("font_size_factor"), 1.5, "font_size_factor_changed passes the factor")
	expect_equal(_received.get("reduce_motion"), true, "reduce_motion_changed passes the flag")
	expect_equal(_received.get("reduce_transparency"), true, "reduce_transparency_changed passes the flag")
	expect_equal(_received.get("speak_focused_controls"), true, "speak_focused_controls_changed passes the flag")
	expect_equal(_received.get("subtitle_scale"), 2.0, "subtitle_scale_changed passes the scale")
	expect_equal(_received.get("subtitles_enabled"), true, "subtitles_enabled_changed passes the flag")
	expect_equal(_received.get("ui_scale"), 1.5, "ui_scale_changed passes the scale")
	AccessibilitySettingsServer.reset_to_defaults()


func _test_resources() -> void:
	var dark: AccessibilityColorScheme = AccessibilityColorScheme.create_high_contrast_dark()
	var light: AccessibilityColorScheme = AccessibilityColorScheme.create_high_contrast_light()
	expect_equal(dark.text_color, Color.WHITE, "the dark scheme has white text")
	expect_equal(light.surface_color, Color.WHITE, "the light scheme has white surfaces")
	expect_equal(dark.get_role_color(&"focus"), dark.focus_color, "get_role_color returns the color of the role")
	expect_equal(dark.get_role_color(&"missing", Color.MAGENTA), Color.MAGENTA, "get_role_color returns the default for an unknown role")
	var scheme := AccessibilityColorScheme.new()
	expect_equal(scheme.border_width, 2, "AccessibilityColorScheme: default border_width")
	expect_equal(scheme.focus_color, Color(1, 0.85, 0, 1), "AccessibilityColorScheme: default focus_color")
	scheme.text_color = CUSTOM_TEXT
	expect_equal(ResourceSaver.save(scheme, SCHEME_FILE), OK, "a custom scheme can be saved")
	AccessibilitySettingsServer.set_custom_color_scheme_path(SCHEME_FILE)
	AccessibilitySettingsServer.set_color_scheme(AccessibilitySettingsServer.COLOR_SCHEME_CUSTOM)
	var overlay: Theme = AccessibilitySettingsServer.get_overlay_theme()
	expect(overlay.get_color(&"font_color", &"Label").is_equal_approx(CUSTOM_TEXT), "the custom scheme reaches the overlay theme")
	AccessibilitySettingsServer.reset_to_defaults()
	DirAccess.remove_absolute(SCHEME_FILE)
	var settings := AccessibilitySettings.new()
	expect_equal(settings.ui_scale, 1.0, "AccessibilitySettings: default ui_scale")
	expect_equal(settings.color_scheme, AccessibilitySettingsServer.COLOR_SCHEME_DEFAULT, "AccessibilitySettings: default color_scheme")
	expect(not settings.reduce_motion, "AccessibilitySettings: reduce_motion is off by default")
	settings.ui_scale = 2.0
	expect_equal(settings.ui_scale, 2.0, "AccessibilitySettings: ui_scale is a plain property")


func _test_restart() -> void:
	if not OS.has_feature("pc"):
		print("  SKIP  restart test (needs a desktop platform to start a second process)")
		return
	AccessibilitySettingsServer.set_ui_scale(1.5)
	AccessibilitySettingsServer.set_color_scheme(DARK)
	AccessibilitySettingsServer.set_reduce_motion(true)
	expect_equal(AccessibilitySettingsServer.save(), OK, "save writes the player file")
	var args := PackedStringArray(["--headless", "--path", ProjectSettings.globalize_path("res://"), PROBE_SCENE])
	var output: Array = []
	var code: int = OS.execute(OS.get_executable_path(), args, output, true)
	expect_equal(code, 0, "restart: the probe process ran")
	var probe: Dictionary = {}
	for line: String in str(output[0]).split("\n"):
		if line.begins_with("PROBE "):
			probe = JSON.parse_string(line.trim_prefix("PROBE "))
	expect_equal(probe.get("ready_frame"), 0.0, "restart: applied before the first frame")
	expect_equal(probe.get("ui_scale"), 1.5, "restart: the saved UI scale is loaded")
	var expected_root: float = float(probe.get("video_factor")) * 1.5
	expect(is_equal_approx(probe.get("root_factor"), expected_root), "restart: the root window is scaled before the main scene is ready")
	expect_equal(probe.get("color_scheme"), float(DARK), "restart: the saved scheme is loaded")
	expect_equal(probe.get("overlay_on_root"), true, "restart: the overlay theme is on the root window before the main scene is ready")
	expect_equal(probe.get("button_font_white"), true, "restart: a button of the main scene has the text color of the scheme")
	expect_equal(probe.get("reduce_motion"), true, "restart: reduced motion is loaded")
