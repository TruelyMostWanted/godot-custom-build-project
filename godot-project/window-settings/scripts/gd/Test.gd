extends FeatureTest
## Full API test of Window Settings (WindowSettingsServer, WindowSettings). Same tests as scripts/cs/Test.cs.
## The tests run in APPLY_MODE_MANUAL, so no confirmation timer starts and no window changes.

const PROBE_SCENE := "res://window-settings/scenes/restart_probe.tscn"
const WINDOW_FILE := "user://settings/window.ini"
const INVENTORY_KEY := "/root/RestartProbe/Inventory"
const POPUP_SIZE := Vector2i(320, 240)
const POPUP_POSITION := Vector2i(50, 40)
const SAVED_SIZE := Vector2i(1920, 1080)
const MISSING_SCREEN := 5

var _received: Dictionary = {}


func _run_tests() -> void:
	WindowSettingsServer.apply_mode = SettingsServer.APPLY_MODE_MANUAL
	_test_defaults()
	_test_values()
	_test_validation()
	_test_signals()
	_test_content_scale()
	_test_window_state()
	_test_command_line_options()
	_test_resource()
	_test_restart()
	_reset()
	WindowSettingsServer.apply_mode = SettingsServer.APPLY_MODE_IMMEDIATE
	DirAccess.remove_absolute(WINDOW_FILE)


func _reset() -> void:
	WindowSettingsServer.reset_to_defaults()
	WindowSettingsServer.apply()
	WindowSettingsServer.confirm_changes()


func _test_defaults() -> void:
	_reset()
	var project_mode: int = ProjectSettings.get_setting("display/window/size/mode")
	var project_width: int = ProjectSettings.get_setting("display/window/size/viewport_width")
	var project_height: int = ProjectSettings.get_setting("display/window/size/viewport_height")
	var project_vsync: int = ProjectSettings.get_setting("display/window/vsync/vsync_mode")
	expect_equal(WindowSettingsServer.get_window_mode(), project_mode, "the default mode is the project setting")
	expect_equal(WindowSettingsServer.get_window_size(), Vector2i(project_width, project_height), "the default size is the project viewport size")
	expect_equal(WindowSettingsServer.get_vsync_mode(), project_vsync, "the default V-Sync mode is the project setting")
	expect_equal(WindowSettingsServer.get_screen(), DisplayServer.SCREEN_PRIMARY, "the default screen is the primary screen")
	expect(WindowSettingsServer.is_window_centered(), "the window is centered by default")
	expect_equal(WindowSettingsServer.get_hdr_reference_luminance(), -1.0, "the HDR reference luminance is automatic by default")
	expect_equal(WindowSettingsServer.get_hdr_max_luminance(), -1.0, "the HDR maximum luminance is automatic by default")


func _test_values() -> void:
	WindowSettingsServer.set_window_mode(DisplayServer.WINDOW_MODE_MAXIMIZED)
	expect_equal(WindowSettingsServer.get_window_mode(), DisplayServer.WINDOW_MODE_MAXIMIZED, "window_mode round trip")
	WindowSettingsServer.set_window_size(Vector2i(1024, 600))
	expect_equal(WindowSettingsServer.get_window_size(), Vector2i(1024, 600), "window_size round trip")
	WindowSettingsServer.set_screen(1)
	expect_equal(WindowSettingsServer.get_screen(), 1, "screen round trip")
	WindowSettingsServer.set_window_centered(false)
	expect(not WindowSettingsServer.is_window_centered(), "window_centered round trip")
	WindowSettingsServer.set_window_position(Vector2i(30, 20))
	expect_equal(WindowSettingsServer.get_window_position(), Vector2i(30, 20), "window_position round trip")
	WindowSettingsServer.set_borderless(true)
	expect(WindowSettingsServer.is_borderless(), "borderless round trip")
	WindowSettingsServer.set_always_on_top(true)
	expect(WindowSettingsServer.is_always_on_top(), "always_on_top round trip")
	WindowSettingsServer.set_unresizable(true)
	expect(WindowSettingsServer.is_unresizable(), "unresizable round trip")
	WindowSettingsServer.set_transparent(true)
	expect(WindowSettingsServer.is_transparent(), "transparent round trip")
	WindowSettingsServer.set_vsync_mode(DisplayServer.VSYNC_DISABLED)
	expect_equal(WindowSettingsServer.get_vsync_mode(), DisplayServer.VSYNC_DISABLED, "vsync_mode round trip")
	WindowSettingsServer.set_hdr_output_requested(true)
	expect(WindowSettingsServer.is_hdr_output_requested(), "hdr_output_requested round trip")
	WindowSettingsServer.set_hdr_reference_luminance(200.0)
	expect_equal(WindowSettingsServer.get_hdr_reference_luminance(), 200.0, "hdr_reference_luminance round trip")
	WindowSettingsServer.set_hdr_max_luminance(1000.0)
	expect_equal(WindowSettingsServer.get_hdr_max_luminance(), 1000.0, "hdr_max_luminance round trip")
	WindowSettingsServer.revert()


func _test_validation() -> void:
	WindowSettingsServer.set_window_size(Vector2i(0, -10))
	expect_equal(WindowSettingsServer.get_window_size(), Vector2i(1, 1), "the window size is at least 1x1")
	WindowSettingsServer.set_hdr_max_luminance(-50.0)
	expect_equal(WindowSettingsServer.get_hdr_max_luminance(), -1.0, "a negative luminance means automatic")
	WindowSettingsServer.set_content_scale_factor(0.0)
	var factor: float = WindowSettingsServer.get_content_scale_factor()
	expect(is_equal_approx(factor, 0.1), "the content scale factor is at least 0.1")
	WindowSettingsServer.revert()


func _test_signals() -> void:
	WindowSettingsServer.window_mode_changed.connect(func(value: int) -> void: _received["window_mode"] = value)
	WindowSettingsServer.window_size_changed.connect(func(value: Vector2i) -> void: _received["window_size"] = value)
	WindowSettingsServer.screen_changed.connect(func(value: int) -> void: _received["screen"] = value)
	WindowSettingsServer.borderless_changed.connect(func(value: bool) -> void: _received["borderless"] = value)
	WindowSettingsServer.vsync_mode_changed.connect(func(value: int) -> void: _received["vsync_mode"] = value)
	WindowSettingsServer.hdr_output_requested_changed.connect(func(value: bool) -> void: _received["hdr_output_requested"] = value)
	WindowSettingsServer.content_scale_mode_changed.connect(func(value: int) -> void: _received["content_scale_mode"] = value)
	WindowSettingsServer.content_scale_aspect_changed.connect(func(value: int) -> void: _received["content_scale_aspect"] = value)
	WindowSettingsServer.content_scale_factor_changed.connect(func(value: float) -> void: _received["content_scale_factor"] = value)
	WindowSettingsServer.set_window_mode(DisplayServer.WINDOW_MODE_FULLSCREEN)
	WindowSettingsServer.set_window_size(SAVED_SIZE)
	WindowSettingsServer.set_screen(0)
	WindowSettingsServer.set_borderless(true)
	WindowSettingsServer.set_vsync_mode(DisplayServer.VSYNC_ADAPTIVE)
	WindowSettingsServer.set_hdr_output_requested(true)
	WindowSettingsServer.set_content_scale_mode(Window.CONTENT_SCALE_MODE_VIEWPORT)
	WindowSettingsServer.set_content_scale_aspect(Window.CONTENT_SCALE_ASPECT_EXPAND)
	WindowSettingsServer.set_content_scale_factor(2.0)
	expect(_received.is_empty(), "no typed signal fires for pending changes in manual mode")
	WindowSettingsServer.apply()
	WindowSettingsServer.confirm_changes()
	expect_equal(_received.get("window_mode"), DisplayServer.WINDOW_MODE_FULLSCREEN, "window_mode_changed passes the mode")
	expect_equal(_received.get("window_size"), SAVED_SIZE, "window_size_changed passes the size")
	expect_equal(_received.get("screen"), 0, "screen_changed passes the screen")
	expect_equal(_received.get("borderless"), true, "borderless_changed passes the flag")
	expect_equal(_received.get("vsync_mode"), DisplayServer.VSYNC_ADAPTIVE, "vsync_mode_changed passes the mode")
	expect_equal(_received.get("hdr_output_requested"), true, "hdr_output_requested_changed passes the flag")
	expect_equal(_received.get("content_scale_mode"), Window.CONTENT_SCALE_MODE_VIEWPORT, "content_scale_mode_changed passes the mode")
	expect_equal(_received.get("content_scale_aspect"), Window.CONTENT_SCALE_ASPECT_EXPAND, "content_scale_aspect_changed passes the aspect")
	expect_equal(_received.get("content_scale_factor"), 2.0, "content_scale_factor_changed passes the factor")
	_reset()


func _test_content_scale() -> void:
	var root: Window = get_tree().root
	WindowSettingsServer.set_content_scale_mode(Window.CONTENT_SCALE_MODE_CANVAS_ITEMS)
	WindowSettingsServer.set_content_scale_aspect(Window.CONTENT_SCALE_ASPECT_EXPAND)
	WindowSettingsServer.set_content_scale_stretch(Window.CONTENT_SCALE_STRETCH_INTEGER)
	WindowSettingsServer.set_content_scale_factor(2.0)
	expect_equal(WindowSettingsServer.get_content_scale_stretch(), Window.CONTENT_SCALE_STRETCH_INTEGER, "content_scale_stretch round trip")
	WindowSettingsServer.apply()
	expect_equal(root.content_scale_mode, Window.CONTENT_SCALE_MODE_CANVAS_ITEMS, "the root window gets the content scale mode")
	expect_equal(root.content_scale_aspect, Window.CONTENT_SCALE_ASPECT_EXPAND, "the root window gets the content scale aspect")
	expect_equal(root.content_scale_stretch, Window.CONTENT_SCALE_STRETCH_INTEGER, "the root window gets the content scale stretch")
	expect_equal(root.content_scale_factor, 2.0, "the root window gets the content scale factor")
	_reset()
	expect_equal(root.content_scale_factor, 1.0, "reset_to_defaults gives the project factor back")


func _test_window_state() -> void:
	var popup := Window.new()
	popup.name = "Popup"
	popup.visible = false
	add_child(popup)
	popup.size = POPUP_SIZE
	popup.position = POPUP_POSITION
	WindowSettingsServer.save_window_state(popup)
	var key: String = str(popup.get_path())
	expect(WindowSettingsServer.has_window_state(key), "save_window_state stores the state under the node path")
	remove_child(popup)
	popup.size = Vector2i(100, 100)
	add_child(popup)
	expect_equal(popup.size, POPUP_SIZE, "the window gets its size back when it enters the tree")
	var other := Window.new()
	var restored: bool = WindowSettingsServer.restore_window_state(other, key)
	expect(restored, "restore_window_state with a key, before add_child")
	expect_equal(other.size, POPUP_SIZE, "restore_window_state restores the size")
	var missing: bool = WindowSettingsServer.restore_window_state(other, "missing")
	expect(not missing, "restore_window_state returns false for an unknown key")
	WindowSettingsServer.clear_window_state(key)
	expect(not WindowSettingsServer.has_window_state(key), "clear_window_state removes the state")
	other.free()
	popup.queue_free()


func _test_command_line_options() -> void:
	var options: Dictionary = WindowSettingsServer.get_command_line_options()
	expect(options.is_empty(), "no window options on the command line of this run")
	# Does nothing without free windows; it must not fail.
	WindowSettingsServer.place_main_window()


func _test_resource() -> void:
	var settings := WindowSettings.new()
	expect_equal(settings.window_mode, DisplayServer.WINDOW_MODE_WINDOWED, "WindowSettings: default window_mode")
	expect_equal(settings.window_size, Vector2i(1152, 648), "WindowSettings: default window_size")
	expect_equal(settings.screen, DisplayServer.SCREEN_PRIMARY, "WindowSettings: default screen")
	expect(settings.window_centered, "WindowSettings: window_centered by default")
	expect_equal(settings.vsync_mode, DisplayServer.VSYNC_ENABLED, "WindowSettings: default vsync_mode")
	expect_equal(settings.content_scale_factor, 1.0, "WindowSettings: default content_scale_factor")
	expect(settings.window_states.is_empty(), "WindowSettings: window_states is empty")
	settings.window_mode = DisplayServer.WINDOW_MODE_FULLSCREEN
	expect_equal(settings.window_mode, DisplayServer.WINDOW_MODE_FULLSCREEN, "WindowSettings: window_mode is a plain property")


func _test_restart() -> void:
	if not OS.has_feature("pc"):
		print("  SKIP  restart test (needs a desktop platform to start a second process)")
		return
	WindowSettingsServer.set_window_mode(DisplayServer.WINDOW_MODE_FULLSCREEN)
	WindowSettingsServer.set_window_size(SAVED_SIZE)
	WindowSettingsServer.set_screen(MISSING_SCREEN)
	WindowSettingsServer.set_borderless(true)
	var inventory := Window.new()
	inventory.size = Vector2i(400, 300)
	inventory.position = Vector2i(60, 70)
	WindowSettingsServer.save_window_state(inventory, INVENTORY_KEY)
	inventory.free()
	WindowSettingsServer.apply()
	WindowSettingsServer.confirm_changes()
	WindowSettingsServer.save()
	var probe: Dictionary = _run_probe([])
	expect_equal(probe.get("ready_frame"), 0.0, "restart: the settings are loaded before the first frame")
	expect_equal(probe.get("window_mode"), float(DisplayServer.WINDOW_MODE_FULLSCREEN), "restart: the saved mode is loaded")
	expect_equal(probe.get("window_size"), str(SAVED_SIZE), "restart: the saved size is loaded")
	expect_equal(probe.get("screen"), float(MISSING_SCREEN), "restart: a saved screen that is missing stays stored")
	expect_equal(probe.get("borderless"), true, "restart: the saved flag is loaded")
	expect_equal(probe.get("inventory_size"), str(Vector2i(400, 300)), "restart: the second window gets its size back")
	expect_equal(probe.get("inventory_position"), str(Vector2i(60, 70)), "restart: the second window gets its position back")
	probe = _run_probe(["--windowed", "--resolution", "800x600"])
	var options: Dictionary = probe.get("command_line_options", {})
	expect_equal(options.get("window_mode"), float(DisplayServer.WINDOW_MODE_WINDOWED), "restart: --windowed is read")
	expect_equal(probe.get("window_size_overridden"), true, "restart: --resolution is a session override")
	expect_equal(probe.get("window_size"), str(SAVED_SIZE), "restart: --resolution does not change the stored size")


func _run_probe(extra_args: Array[String]) -> Dictionary:
	var args := PackedStringArray(["--headless"])
	args.append_array(PackedStringArray(extra_args))
	args.append_array(PackedStringArray(["--path", ProjectSettings.globalize_path("res://"), PROBE_SCENE]))
	var output: Array = []
	var code: int = OS.execute(OS.get_executable_path(), args, output, true)
	expect_equal(code, 0, "restart: the probe process ran")
	for line: String in str(output[0]).split("\n"):
		if line.begins_with("PROBE "):
			return JSON.parse_string(line.trim_prefix("PROBE "))
	expect(false, "restart: the probe printed its result")
	return {}
