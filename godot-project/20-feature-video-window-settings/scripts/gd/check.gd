extends FeatureCheck
## GDScript checks of 20-feature-video-window-settings. Check the same things as scripts/cs/Issue20FeatureVideoWindowSettingsCheck.cs.
##
## Window mode, size and screen go through the confirmation of SettingsServer, so the checks confirm or cancel
## them. On the headless display server there is no window: the domain keeps the values it was given. The
## restart checks start the project again as its own processes (desktop only) and read what the domain loaded
## before the main scene was ready, and the state a second window got when it entered the tree.

const PROBE_SCENE := "res://20-feature-video-window-settings/scenes/restart_probe.tscn"
const VIDEO_WINDOW_FILE := "user://settings/video_window.ini"
const PROBE_WINDOW_KEY := "/root/RestartProbe/Inventory"


func _get_check_name() -> String:
	return "20-feature-video-window-settings"


func _run_checks() -> void:
	expect(Engine.has_singleton("VideoWindowSettingsServer"), "VideoWindowSettingsServer is a registered singleton")
	expect(Engine.get_singleton("VideoWindowSettingsServer") is SettingsServer, "VideoWindowSettingsServer is a SettingsServer")
	expect_equal(SettingsServerManager.get_server(&"video_window"), Engine.get_singleton("VideoWindowSettingsServer"), "the domain is registered as \"video_window\"")
	expect(VideoWindowSettingsServer.is_started(), "the domain was started before the main scene")
	expect_equal(VideoWindowSettingsServer.get_apply_phase(), SettingsServer.APPLY_PHASE_PRE_DISPLAY, "applied before the display server exists")
	expect_equal(VideoWindowSettingsServer.get_file_path(), VIDEO_WINDOW_FILE, "file path")
	var names := VideoWindowSettingsServer.get_setting_names()
	for setting in ["window_mode", "window_size", "screen", "window_centered", "window_position", "borderless", "always_on_top", "unresizable", "transparent", "vsync_mode", "content_scale_mode", "content_scale_aspect", "content_scale_stretch", "content_scale_factor", "hdr_output_requested", "hdr_reference_luminance", "hdr_max_luminance", "window_states"]:
		expect(names.has(setting), "setting \"%s\" exists" % setting)

	_check_defaults()
	_check_confirmation()
	_check_vsync()
	_check_content_scale()
	_check_window_state()
	_check_validation()
	_check_resource()
	await _check_restart()

	# Leave no player files behind for later runs.
	_reset()
	await get_tree().process_frame
	DirAccess.remove_absolute(VIDEO_WINDOW_FILE)


func _reset() -> void:
	VideoWindowSettingsServer.reset_to_defaults()
	VideoWindowSettingsServer.confirm_pending()


func _is_headless() -> bool:
	return DisplayServer.get_name() == "headless"


func _check_defaults() -> void:
	_reset()
	expect_equal(VideoWindowSettingsServer.get_default_value(&"window_mode"), ProjectSettings.get_setting("display/window/size/mode"), "default mode is the project setting")
	expect_equal(VideoWindowSettingsServer.get_default_value(&"window_size"), Vector2i(ProjectSettings.get_setting("display/window/size/viewport_width"), ProjectSettings.get_setting("display/window/size/viewport_height")), "default size is the project viewport size")
	expect_equal(VideoWindowSettingsServer.get_default_value(&"vsync_mode"), ProjectSettings.get_setting("display/window/vsync/vsync_mode"), "default V-Sync mode is the project setting")
	expect_equal(VideoWindowSettingsServer.get_default_value(&"screen"), DisplayServer.SCREEN_PRIMARY, "default screen is the primary screen")
	expect_equal(VideoWindowSettingsServer.get_default_value(&"window_centered"), true, "the window is centered by default")
	expect_equal(VideoWindowSettingsServer.get_default_value(&"hdr_reference_luminance"), -1.0, "HDR luminance: automatic by default")
	expect(VideoWindowSettingsServer.get_command_line_options().is_empty(), "no window options on the command line of this run")
	expect(VideoWindowSettingsServer.is_value_supported(&"content_scale_factor"), "content scale is supported everywhere")
	if _is_headless():
		expect(not VideoWindowSettingsServer.is_value_supported(&"window_size"), "headless: the window size is reported as unsupported")


func _check_confirmation() -> void:
	var flags := VideoWindowSettingsServer.get_setting_flags(&"window_mode")
	expect(flags & SettingsServer.SETTING_FLAG_CONFIRM != 0, "window_mode needs a confirmation")
	expect(VideoWindowSettingsServer.get_setting_flags(&"window_size") & SettingsServer.SETTING_FLAG_CONFIRM != 0, "window_size needs a confirmation")
	expect(VideoWindowSettingsServer.get_setting_flags(&"screen") & SettingsServer.SETTING_FLAG_CONFIRM != 0, "screen needs a confirmation")
	expect(VideoWindowSettingsServer.get_setting_flags(&"vsync_mode") & SettingsServer.SETTING_FLAG_CONFIRM == 0, "vsync_mode is applied without a confirmation")

	var old_size := VideoWindowSettingsServer.get_window_size()
	VideoWindowSettingsServer.set_window_size(Vector2i(1024, 600))
	expect(VideoWindowSettingsServer.is_confirmation_pending(), "a new window size waits for the confirmation")
	VideoWindowSettingsServer.cancel_pending()
	expect_equal(VideoWindowSettingsServer.get_window_size(), old_size, "cancel_pending() gives back the old size")

	VideoWindowSettingsServer.set_window_size(Vector2i(1024, 600))
	VideoWindowSettingsServer.confirm_pending()
	expect_equal(VideoWindowSettingsServer.get_applied_value(&"window_size"), Vector2i(1024, 600), "confirm_pending() keeps the new size")
	if not _is_headless() and DisplayServer.window_get_mode() == DisplayServer.WINDOW_MODE_WINDOWED and VideoWindowSettingsServer.is_value_supported(&"window_size"):
		expect_equal(DisplayServer.window_get_size(), Vector2i(1024, 600), "the main window has the new size")
	_reset()


func _check_vsync() -> void:
	VideoWindowSettingsServer.set_vsync_mode(DisplayServer.VSYNC_DISABLED)
	expect_equal(VideoWindowSettingsServer.get_vsync_mode(), DisplayServer.VSYNC_DISABLED, "get_vsync_mode()")
	expect_equal(VideoWindowSettingsServer.get_applied_value(&"vsync_mode"), DisplayServer.VSYNC_DISABLED, "V-Sync is applied at once")
	if not _is_headless() and VideoWindowSettingsServer.is_value_supported(&"vsync_mode"):
		expect_equal(DisplayServer.window_get_vsync_mode(), DisplayServer.VSYNC_DISABLED, "the display server has the V-Sync mode")
	VideoWindowSettingsServer.retrieve()
	expect_equal(VideoWindowSettingsServer.get_vsync_mode(), DisplayServer.VSYNC_DISABLED, "retrieve() reads the same V-Sync mode back")
	_reset()


func _check_content_scale() -> void:
	var root := get_tree().root
	VideoWindowSettingsServer.set_content_scale_mode(Window.CONTENT_SCALE_MODE_CANVAS_ITEMS)
	VideoWindowSettingsServer.set_content_scale_aspect(Window.CONTENT_SCALE_ASPECT_EXPAND)
	VideoWindowSettingsServer.set_content_scale_stretch(Window.CONTENT_SCALE_STRETCH_INTEGER)
	VideoWindowSettingsServer.set_content_scale_factor(1.5)
	expect_equal(root.content_scale_mode, Window.CONTENT_SCALE_MODE_CANVAS_ITEMS, "the root window gets the content scale mode")
	expect_equal(root.content_scale_aspect, Window.CONTENT_SCALE_ASPECT_EXPAND, "the root window gets the content scale aspect")
	expect_equal(root.content_scale_stretch, Window.CONTENT_SCALE_STRETCH_INTEGER, "the root window gets the content scale stretch")
	expect_equal(root.content_scale_factor, 1.5, "the root window gets the content scale factor")
	_reset()
	expect_equal(root.content_scale_factor, VideoWindowSettingsServer.get_default_value(&"content_scale_factor"), "reset_to_defaults() gives back the project factor")


func _check_window_state() -> void:
	var popup := Window.new()
	popup.name = "Check20Popup"
	popup.visible = false
	add_child(popup)
	popup.size = Vector2i(320, 240)
	popup.position = Vector2i(50, 40)
	VideoWindowSettingsServer.save_window_state(popup)
	var key := str(popup.get_path())
	expect(VideoWindowSettingsServer.has_window_state(key), "the state is stored under the node path")
	remove_child(popup)
	popup.size = Vector2i(100, 100)
	add_child(popup)
	expect_equal(popup.size, Vector2i(320, 240), "the window gets its size back when it enters the tree")
	var other := Window.new()
	expect(VideoWindowSettingsServer.restore_window_state(other, key), "restore_window_state() with a key, before add_child()")
	expect_equal(other.size, Vector2i(320, 240), "restored size")
	expect(not VideoWindowSettingsServer.restore_window_state(other, "missing"), "no state for an unknown key")
	VideoWindowSettingsServer.clear_window_state(key)
	expect(not VideoWindowSettingsServer.has_window_state(key), "clear_window_state()")
	other.free()
	popup.queue_free()
	_reset()


func _check_validation() -> void:
	VideoWindowSettingsServer.apply_mode = SettingsServer.APPLY_MODE_MANUAL
	VideoWindowSettingsServer.set_window_size(Vector2i(0, -10))
	expect_equal(VideoWindowSettingsServer.get_window_size(), Vector2i(1, 1), "the window size is at least 1x1")
	VideoWindowSettingsServer.set_hdr_max_luminance(-50.0)
	expect_equal(VideoWindowSettingsServer.get_hdr_max_luminance(), -1.0, "a negative luminance means automatic")
	VideoWindowSettingsServer.set_content_scale_factor(0.0)
	expect_equal(VideoWindowSettingsServer.get_content_scale_factor(), 0.1, "the content scale factor is at least 0.1")
	VideoWindowSettingsServer.revert()
	VideoWindowSettingsServer.apply_mode = SettingsServer.APPLY_MODE_IMMEDIATE


func _check_resource() -> void:
	var settings := VideoWindowSettings.new()
	expect_equal(settings.window_mode, DisplayServer.WINDOW_MODE_WINDOWED, "VideoWindowSettings: default mode")
	expect_equal(settings.window_size, Vector2i(1152, 648), "VideoWindowSettings: default size")
	expect_equal(settings.vsync_mode, DisplayServer.VSYNC_ENABLED, "VideoWindowSettings: default V-Sync mode")
	expect_equal(settings.content_scale_factor, 1.0, "VideoWindowSettings: default content scale factor")
	settings.window_mode = DisplayServer.WINDOW_MODE_FULLSCREEN
	expect_equal(settings.window_mode, DisplayServer.WINDOW_MODE_FULLSCREEN, "VideoWindowSettings: window_mode is a plain property")


func _check_restart() -> void:
	if not OS.has_feature("pc"):
		print("  SKIP  restart check (needs a desktop platform to start a second process)")
		return

	# 1) Fullscreen and 1920x1080 are saved and loaded before the main scene; a second window gets its state back.
	VideoWindowSettingsServer.apply_mode = SettingsServer.APPLY_MODE_MANUAL
	VideoWindowSettingsServer.set_window_mode(DisplayServer.WINDOW_MODE_FULLSCREEN)
	VideoWindowSettingsServer.set_window_size(Vector2i(1920, 1080))
	VideoWindowSettingsServer.set_screen(5) # A screen that does not exist.
	VideoWindowSettingsServer.set_borderless(true)
	expect_equal(VideoWindowSettingsServer.apply(), OK, "apply() in manual mode")
	VideoWindowSettingsServer.confirm_pending()
	VideoWindowSettingsServer.apply_mode = SettingsServer.APPLY_MODE_IMMEDIATE
	var inventory := Window.new()
	inventory.size = Vector2i(400, 300)
	inventory.position = Vector2i(60, 70)
	VideoWindowSettingsServer.save_window_state(inventory, PROBE_WINDOW_KEY)
	inventory.free()
	expect_equal(VideoWindowSettingsServer.save(), OK, "save() writes the player file")
	expect(FileAccess.file_exists(VIDEO_WINDOW_FILE), "the player file is in user://settings/")

	var probe := _run_probe([])
	expect(probe.get("started", false), "restart: the domain was started")
	expect_equal(int(probe.get("window_mode", -1)), DisplayServer.WINDOW_MODE_FULLSCREEN, "restart: the saved mode is loaded")
	expect_equal(str(probe.get("window_size", "")), str(Vector2i(1920, 1080)), "restart: the saved size is loaded")
	expect_equal(int(probe.get("screen", -9)), 5, "restart: the saved screen stays stored even if it is missing")
	expect(probe.get("borderless", false), "restart: the saved flag is loaded")
	expect_equal(str(probe.get("inventory_size", "")), str(Vector2i(400, 300)), "restart: the second window gets its size back")
	expect_equal(str(probe.get("inventory_position", "")), str(Vector2i(60, 70)), "restart: the second window gets its position back")
	expect_equal(int(probe.get("ready_frame", -1)), 0, "restart: loaded before the first frame")

	# 2) Command line options are session overrides: active, but never saved.
	probe = _run_probe(["--windowed", "--resolution", "800x600"])
	var options: Dictionary = probe.get("command_line_options", {})
	expect_equal(int(options.get("window_mode", -1)), DisplayServer.WINDOW_MODE_WINDOWED, "--windowed is read")
	expect(probe.get("window_size_overridden", false), "--resolution is a session override")
	expect(probe.get("window_mode_overridden", false), "--windowed is a session override")
	expect_equal(str(probe.get("window_size", "")), str(Vector2i(1920, 1080)), "--resolution does not change the stored size")
	expect(FileAccess.get_file_as_string(VIDEO_WINDOW_FILE).contains("1920"), "--resolution is not saved: the file still has 1920x1080")


func _run_probe(extra_args: Array) -> Dictionary:
	var output := []
	var args := PackedStringArray(["--headless"])
	args.append_array(PackedStringArray(extra_args))
	args.append_array(PackedStringArray(["--path", ProjectSettings.globalize_path("res://"), PROBE_SCENE]))
	var code := OS.execute(OS.get_executable_path(), args, output, true)
	expect_equal(code, 0, "restart: the probe process ran")
	for chunk in output:
		for line in str(chunk).split("\n"):
			if line.begins_with("CHECK20_PROBE "):
				var parsed = JSON.parse_string(line.trim_prefix("CHECK20_PROBE "))
				if parsed is Dictionary:
					return parsed
	expect(false, "restart: the probe printed its result")
	return {}
