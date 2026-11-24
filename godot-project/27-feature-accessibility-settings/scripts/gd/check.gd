extends FeatureCheck
## GDScript checks of 27-feature-accessibility-settings. Check the same things as scripts/cs/Issue27FeatureAccessibilitySettingsCheck.cs.
##
## The domain is reached through Engine.get_singleton(), so this script also parses with engines without it.
## UI scale, font size, color scheme and focus outline are checked on the root window and its overlay theme.
## The restart check starts the project again as its own process (desktop only) and reads what the root window
## had before the main scene was ready.

const PROBE_SCENE := "res://27-feature-accessibility-settings/scenes/restart_probe.tscn"
const ACCESSIBILITY_FILE := "user://settings/accessibility.ini"

# AccessibilitySettingsServer.ColorScheme and ColorblindMode.
const COLOR_SCHEME_DEFAULT := 0
const COLOR_SCHEME_HIGH_CONTRAST_DARK := 1
const COLOR_SCHEME_HIGH_CONTRAST_LIGHT := 2
const COLOR_SCHEME_CUSTOM := 3
const COLORBLIND_MODE_NONE := 0
const COLORBLIND_MODE_DEUTERANOPIA := 2

var server: Object


func _get_check_name() -> String:
	return "27-feature-accessibility-settings"


func _run_checks() -> void:
	await _check_typed_signals()
	expect(Engine.has_singleton("AccessibilitySettingsServer"), "AccessibilitySettingsServer is a registered singleton")
	if not Engine.has_singleton("AccessibilitySettingsServer"):
		return
	server = Engine.get_singleton("AccessibilitySettingsServer")
	expect(server is SettingsServer, "AccessibilitySettingsServer is a SettingsServer")
	expect_equal(SettingsServerManager.get_server(&"accessibility"), server, "the domain is registered as \"accessibility\"")
	expect(server.is_started(), "the domain was started before the main scene")
	expect_equal(server.get_apply_phase(), SettingsServer.APPLY_PHASE_SCENE, "applied after the root window exists, before the main scene")
	expect_equal(server.get_file_path(), ACCESSIBILITY_FILE, "file path")
	for setting in ["ui_scale", "font_size_factor", "color_scheme", "custom_color_scheme_path", "focus_outline_width", "speak_focused_controls", "speech_persona"]:
		expect(server.is_setting_bound(setting), "\"%s\" has an engine target" % setting)
	for setting in ["reduce_motion", "reduce_transparency", "colorblind_mode", "subtitles_enabled", "subtitle_scale"]:
		expect(server.get_setting_flags(setting) & SettingsServer.SETTING_FLAG_NO_ENGINE_TARGET != 0, "\"%s\" is game-side" % setting)

	server.reset_to_defaults()
	_check_defaults()
	_check_validation()
	_check_ui_scale()
	await _check_theme()
	_check_helpers()
	_check_speech()
	_check_resources()
	_check_restart()

	# Leave no player files behind for later runs.
	server.reset_to_defaults()
	await get_tree().process_frame
	DirAccess.remove_absolute(ACCESSIBILITY_FILE)


func _check_defaults() -> void:
	expect_equal(server.get_ui_scale(), 1.0, "default UI scale")
	expect_equal(server.get_font_size_factor(), 1.0, "default font size factor")
	expect_equal(server.get_focus_outline_width(), 0, "default focus outline: the theme's own")
	expect(not server.is_speaking_focused_controls(), "speaking focused controls is off by default")
	expect_equal(server.get_colorblind_mode(), COLORBLIND_MODE_NONE, "default colorblind mode")
	# The OS preferences are the defaults; unknown (-1) keeps them off.
	var reduce_animation := DisplayServer.accessibility_should_reduce_animation()
	expect_equal(server.get_default_value(&"reduce_motion"), reduce_animation == 1, "default reduced motion follows the OS (%d)" % reduce_animation)
	var increase_contrast := DisplayServer.accessibility_should_increase_contrast()
	expect_equal(server.get_default_value(&"color_scheme"), COLOR_SCHEME_HIGH_CONTRAST_DARK if increase_contrast == 1 else COLOR_SCHEME_DEFAULT, "default color scheme follows the OS contrast (%d)" % increase_contrast)
	var reduce_transparency := DisplayServer.accessibility_should_reduce_transparency()
	expect_equal(server.get_default_value(&"reduce_transparency"), reduce_transparency == 1, "default reduced transparency follows the OS (%d)" % reduce_transparency)
	print("  INFO  OS: reduce animation %d, increase contrast %d, reduce transparency %d, screen reader %d" % [reduce_animation, increase_contrast, reduce_transparency, DisplayServer.accessibility_screen_reader_active()])


func _check_validation() -> void:
	server.set_ui_scale(10.0)
	expect_equal(server.get_ui_scale(), 4.0, "UI scale is clamped to 4")
	server.set_font_size_factor(0.1)
	expect_equal(server.get_font_size_factor(), 0.5, "font size factor is clamped to 0.5")
	server.set_focus_outline_width(20)
	expect_equal(server.get_focus_outline_width(), 8, "focus outline is clamped to 8")
	server.set_value(&"color_scheme", 42)
	expect_equal(server.get_color_scheme(), server.get_default_value(&"color_scheme"), "an unknown color scheme keeps the current one")
	server.reset_to_defaults()


func _check_ui_scale() -> void:
	var root := get_tree().root
	var base_factor := root.content_scale_factor
	server.set_ui_scale(1.5)
	expect(is_equal_approx(root.content_scale_factor, base_factor * 1.5), "UI scale 150 %% multiplies the root content scale factor (%f)" % root.content_scale_factor)
	var video_window = Engine.get_singleton("VideoWindowSettingsServer")
	expect(is_equal_approx(video_window.get_content_scale_factor(), base_factor), "the video window factor stays separate")
	server.set_ui_scale(1.0)
	expect(is_equal_approx(root.content_scale_factor, base_factor), "UI scale 100 % gives the factor back")


func _check_theme() -> void:
	var root := get_tree().root
	var previous_theme := root.theme
	expect(server.get_overlay_theme() == null, "no overlay theme with the defaults")

	var changes := []
	server.color_scheme_changed.connect(func(scheme): changes.append(scheme))
	server.set_color_scheme(COLOR_SCHEME_HIGH_CONTRAST_DARK)
	var overlay: Theme = server.get_overlay_theme()
	expect(overlay != null and root.theme == overlay, "a high-contrast scheme sets the overlay theme on the root window")
	expect_equal(changes.size(), 1, "color_scheme_changed is emitted once")
	expect(server.get_active_color_scheme() is AccessibilityColorScheme, "the active scheme is an AccessibilityColorScheme")
	if overlay != null:
		expect_equal(overlay.get_color(&"font_color", &"Button"), Color.WHITE, "button text is white")
		var focus := overlay.get_stylebox(&"focus", &"Button") as StyleBoxFlat
		expect(focus != null and focus.border_color == server.get_active_color_scheme().focus_color, "the focus outline has the scheme's focus color")
	# A button in the tree really uses the scheme.
	var button := Button.new()
	button.text = "Play"
	add_child(button)
	# Controls see a changed root theme from the next frame (before the first draw).
	await get_tree().process_frame
	expect_equal(button.get_theme_color(&"font_color"), Color.WHITE, "a button in the tree gets the scheme's text color")
	expect_equal(server.get_scheme_color(&"problem", Color.MAGENTA), server.get_active_color_scheme().problem_color, "get_scheme_color() returns the role color")

	var font_size_before := button.get_theme_font_size(&"font_size")
	server.set_font_size_factor(1.5)
	overlay = server.get_overlay_theme()
	expect(overlay.has_default_font_size(), "the font size factor sets the default font size of the overlay")
	await get_tree().process_frame
	expect_equal(button.get_theme_font_size(&"font_size"), roundi(font_size_before * 1.5), "a button in the tree gets the bigger font")
	server.set_focus_outline_width(4)
	var focus_box := server.get_overlay_theme().get_stylebox(&"focus", &"Button") as StyleBoxFlat
	expect(focus_box != null and focus_box.border_width_left == 4, "the focus outline width reaches the focus style box")
	button.queue_free()

	server.reset_to_defaults()
	expect(server.get_overlay_theme() == null, "back at the defaults: no overlay theme")
	expect(root.theme == previous_theme, "the root window gets its previous theme back")
	expect_equal(changes.size(), 2, "color_scheme_changed is emitted when the scheme goes away")


func _check_helpers() -> void:
	server.set_reduce_motion(true)
	expect_equal(server.get_animation_duration(0.3), 0.0, "reduced motion: no animation duration")
	expect_equal(server.get_motion_scale(), 0.0, "reduced motion: motion scale 0")
	server.set_reduce_motion(false)
	expect_equal(server.get_animation_duration(0.3), 0.3, "no reduced motion: the duration stays")
	server.set_font_size_factor(2.0)
	expect_equal(server.scale_font_size(16), 32, "scale_font_size()")
	server.set_subtitle_scale(1.5)
	expect_equal(server.scale_subtitle_font_size(10), 30, "scale_subtitle_font_size()")
	server.set_colorblind_mode(COLORBLIND_MODE_DEUTERANOPIA)
	expect_equal(server.get_colorblind_mode(), COLORBLIND_MODE_DEUTERANOPIA, "colorblind mode is stored")
	server.reset_to_defaults()


func _check_speech() -> void:
	var button := Button.new()
	button.text = "Play"
	expect_equal(server.get_control_speech_text(button), "Play", "the text of a button is spoken")
	button.accessibility_name = "Start the game"
	expect_equal(server.get_control_speech_text(button), "Start the game", "accessibility_name wins over the text")
	var field := LineEdit.new()
	field.placeholder_text = "Player name"
	expect_equal(server.get_control_speech_text(field), "Player name", "an empty field speaks its placeholder")
	field.free()

	var root := get_tree().root
	var before := root.get_signal_connection_list(&"gui_focus_changed").size()
	server.set_speak_focused_controls(true)
	expect_equal(root.get_signal_connection_list(&"gui_focus_changed").size(), before + 1, "speaking focused controls connects to the root window")
	if TTSServer.is_available() and not server.is_screen_reader_active():
		var persona := TTSPersona.new()
		persona.persona_name = &"check_narrator"
		TTSServer.add_persona(persona)
		server.set_speech_persona("check_narrator")
		add_child(button)
		button.grab_focus()
		expect(TTSServer.get_pending_utterances().size() > 0 or TTSServer.is_speaking(), "moving the focus speaks the control")
		TTSServer.stop()
		TTSServer.remove_persona(&"check_narrator")
		button.queue_free()
	else:
		print("  SKIP  spoken focus (no TTS voice or a screen reader is active)")
		expect_equal(server.speak_control(button), -1, "without a voice nothing is spoken")
		button.free()
	server.set_speak_focused_controls(false)
	expect_equal(root.get_signal_connection_list(&"gui_focus_changed").size(), before, "turning it off disconnects")
	server.reset_to_defaults()


func _check_resources() -> void:
	var dark := AccessibilityColorScheme.create_high_contrast_dark()
	var light := AccessibilityColorScheme.create_high_contrast_light()
	expect_equal(dark.text_color, Color.WHITE, "dark scheme: white text")
	expect_equal(light.surface_color, Color.WHITE, "light scheme: white surfaces")
	expect_equal(dark.get_role_color(&"focus"), dark.focus_color, "get_role_color()")
	expect(server.get_settings() is AccessibilitySettings, "the settings resource is an AccessibilitySettings")

	# A custom scheme from a file.
	var custom := AccessibilityColorScheme.new()
	custom.text_color = Color(0.9, 0.9, 0.2)
	var path := "user://check_27_scheme.tres"
	expect_equal(ResourceSaver.save(custom, path), OK, "a custom scheme can be saved")
	server.set_custom_color_scheme_path(path)
	server.set_color_scheme(COLOR_SCHEME_CUSTOM)
	var overlay: Theme = server.get_overlay_theme()
	expect(overlay != null and overlay.get_color(&"font_color", &"Label").is_equal_approx(Color(0.9, 0.9, 0.2)), "the custom scheme reaches the overlay theme")
	server.reset_to_defaults()
	DirAccess.remove_absolute(path)


func _check_restart() -> void:
	if not OS.has_feature("pc"):
		print("  SKIP  restart check (needs a desktop platform to start a second process)")
		return
	server.set_ui_scale(1.5)
	server.set_color_scheme(COLOR_SCHEME_HIGH_CONTRAST_DARK)
	server.set_reduce_motion(true)
	expect_equal(server.save(), OK, "save() writes the player file")
	expect(FileAccess.get_file_as_string(ACCESSIBILITY_FILE).contains("ui_scale"), "the player file is plain INI")

	var probe := _run_probe()
	expect(probe.get("started", false), "restart: the domain was started")
	expect_equal(int(probe.get("ready_frame", -1)), 0, "restart: applied before the first frame")
	expect(is_equal_approx(float(probe.get("ui_scale", 0.0)), 1.5), "restart: the saved UI scale is loaded")
	expect(is_equal_approx(float(probe.get("root_factor", 0.0)), float(probe.get("video_factor", 0.0)) * 1.5), "restart: the root window is scaled before the main scene is ready")
	expect_equal(int(probe.get("color_scheme", -1)), COLOR_SCHEME_HIGH_CONTRAST_DARK, "restart: the saved scheme is loaded")
	expect(probe.get("overlay_on_root", false), "restart: the overlay theme is on the root window before the main scene is ready")
	expect(probe.get("button_font_white", false), "restart: a button of the main scene has the scheme's text color")
	expect(probe.get("reduce_motion", false), "restart: reduced motion is loaded")
	server.reset_to_defaults()


func _run_probe() -> Dictionary:
	var output := []
	var args := PackedStringArray(["--headless", "--path", ProjectSettings.globalize_path("res://"), PROBE_SCENE])
	var code := OS.execute(OS.get_executable_path(), args, output, true)
	expect_equal(code, 0, "restart: the probe process ran")
	for chunk in output:
		for line in str(chunk).split("\n"):
			if line.begins_with("CHECK27_PROBE "):
				var parsed = JSON.parse_string(line.trim_prefix("CHECK27_PROBE "))
				if parsed is Dictionary:
					return parsed
	expect(false, "restart: the probe printed its result")
	return {}


## Issue #173: the typed signal fires once per applied change, not for pending changes in manual mode.
func _check_typed_signals() -> void:
	if not Engine.has_singleton("AccessibilitySettingsServer"):
		return
	var s: SettingsServer = Engine.get_singleton("AccessibilitySettingsServer")
	expect(s.has_signal(&"ui_scale_changed"), "AccessibilitySettingsServer has the typed signal ui_scale_changed")
	var arg_info: Dictionary = {}
	for info: Dictionary in s.get_signal_list():
		if info.name == "ui_scale_changed":
			arg_info = info.args[0]
	expect_equal(arg_info.get("type"), TYPE_FLOAT, "ui_scale_changed has a typed argument")
	var file_path := s.get_file_path()
	var had_file := FileAccess.file_exists(file_path)
	var previous_mode := s.apply_mode
	s.apply_mode = SettingsServer.APPLY_MODE_IMMEDIATE
	s.reset_value(&"ui_scale")
	var new_value: Variant = 1.5 if s.get_applied_value(&"ui_scale") != 1.5 else 2.0
	var received: Array = []
	var on_changed := func(v: Variant) -> void: received.append(v)
	s.connect(&"ui_scale_changed", on_changed)

	s.apply_mode = SettingsServer.APPLY_MODE_MANUAL
	s.set_value(&"ui_scale", new_value)
	expect_equal(received.size(), 0, "ui_scale_changed does not fire for a pending change in manual mode")
	s.apply()
	expect_equal(received, [new_value], "ui_scale_changed fires once on apply() with the applied value")
	if not received.is_empty():
		expect_equal(typeof(received[0]), TYPE_FLOAT, "ui_scale_changed passes a typed value")
	s.apply()
	expect_equal(received.size(), 1, "ui_scale_changed does not fire when nothing changed")
	s.reset_value(&"ui_scale")
	expect_equal(received.size(), 1, "ui_scale_changed waits for apply() after a reset in manual mode")
	s.apply()
	expect_equal(received.size(), 2, "ui_scale_changed fires again when the default is applied")

	s.disconnect(&"ui_scale_changed", on_changed)
	s.apply_mode = previous_mode
	await get_tree().process_frame
	if not had_file:
		DirAccess.remove_absolute(file_path)
