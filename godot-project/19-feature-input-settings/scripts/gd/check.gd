extends FeatureCheck
## GDScript checks of 19-feature-input-settings. Check the same things as scripts/cs/Issue19FeatureInputSettingsCheck.cs.
##
## The restart check starts the project again as its own process (desktop only) and reads what the input
## settings domain applied before the main scene was ready.

const PROBE_SCENE := "res://19-feature-input-settings/scenes/restart_probe.tscn"
const INPUT_FILE := "user://settings/input.ini"


func _get_check_name() -> String:
	return "19-feature-input-settings"


func _run_checks() -> void:
	await _check_typed_signals()
	expect(Engine.has_singleton("InputSettingsServer"), "InputSettingsServer is a registered singleton")
	expect(Engine.get_singleton("InputSettingsServer") is SettingsServer, "InputSettingsServer is a SettingsServer")
	expect_equal(SettingsServerManager.get_server(&"input"), Engine.get_singleton("InputSettingsServer"), "the domain is registered as \"input\"")
	expect(InputSettingsServer.is_started(), "the domain was started before the main scene")
	expect_equal(InputSettingsServer.get_file_path(), INPUT_FILE, "file path")
	expect(InputSettingsServer.get_setting_names().has("mouse_sensitivity"), "setting names contain mouse_sensitivity")
	expect((InputSettingsServer.get_setting_flags(&"mouse_sensitivity") & SettingsServer.SETTING_FLAG_NO_ENGINE_TARGET) != 0, "mouse_sensitivity has no engine target")

	InputSettingsServer.reset_to_defaults()
	_check_defaults()
	_check_validation()
	_check_helpers()
	_check_signal()
	_check_engine_options()
	await _check_toggle_actions()
	_check_mouse_capture()
	_check_resource()
	_check_restart()

	# Leave no player files behind for later runs.
	InputSettingsServer.reset_to_defaults()
	await get_tree().process_frame
	DirAccess.remove_absolute(INPUT_FILE)


func _check_defaults() -> void:
	for device in [InputSettingsServer.DEVICE_KEYBOARD, InputSettingsServer.DEVICE_MOUSE, InputSettingsServer.DEVICE_GAMEPAD]:
		expect_equal(InputSettingsServer.get_sensitivity(device), 1.0, "default sensitivity of device %d" % device)
		expect(not InputSettingsServer.is_x_inverted(device), "default: x not inverted, device %d" % device)
		expect(not InputSettingsServer.is_y_inverted(device), "default: y not inverted, device %d" % device)
	expect(is_equal_approx(InputSettingsServer.get_gamepad_deadzone(), 0.2), "default deadzone 0.2")
	expect(InputSettingsServer.is_vibration_enabled(), "default: vibration on")
	expect_equal(InputSettingsServer.get_vibration_strength(), 1.0, "default vibration strength")
	expect_equal(InputSettingsServer.get_mouse_smoothing(), 0.0, "default: no smoothing")
	expect_equal(InputSettingsServer.get_toggle_actions(), PackedStringArray(), "default: no toggle actions")
	expect_equal(InputSettingsServer.get_first_player_device_id(), -1, "default: any device")
	expect(InputSettingsServer.is_keyboard_allowed_for_current_player(), "default: keyboard allowed")


func _check_validation() -> void:
	InputSettingsServer.set_sensitivity(InputSettingsServer.DEVICE_MOUSE, 50.0)
	expect_equal(InputSettingsServer.get_sensitivity(InputSettingsServer.DEVICE_MOUSE), 10.0, "sensitivity is clamped to 10")
	InputSettingsServer.set_gamepad_deadzone(3.0)
	expect(is_equal_approx(InputSettingsServer.get_gamepad_deadzone(), 0.95), "deadzone is clamped to 0.95")
	InputSettingsServer.set_toggle_actions(PackedStringArray(["crouch", "", "aim ", "crouch"]))
	expect_equal(InputSettingsServer.get_toggle_actions(), PackedStringArray(["crouch", "aim"]), "toggle actions without empty names and duplicates")
	InputSettingsServer.reset_to_defaults()


func _check_helpers() -> void:
	InputSettingsServer.set_sensitivity(InputSettingsServer.DEVICE_MOUSE, 2.5)
	InputSettingsServer.set_y_inverted(InputSettingsServer.DEVICE_MOUSE, true)
	expect_equal(InputSettingsServer.apply_to_mouse_motion(Vector2(2, 4)), Vector2(5, -10), "apply_to_mouse_motion(): sensitivity and Y inversion")
	var motion := InputEventMouseMotion.new()
	motion.screen_relative = Vector2(2, 4)
	expect_equal(InputSettingsServer.get_look_vector(motion), Vector2(5, -10), "get_look_vector() of a mouse motion")
	expect_equal(InputSettingsServer.get_look_vector(InputEventKey.new()), Vector2(), "get_look_vector() of a key event is zero")

	InputSettingsServer.set_sensitivity(InputSettingsServer.DEVICE_KEYBOARD, 2.0)
	InputSettingsServer.set_x_inverted(InputSettingsServer.DEVICE_KEYBOARD, true)
	expect_equal(InputSettingsServer.apply_to_vector(InputSettingsServer.DEVICE_KEYBOARD, Vector2(1, 1)), Vector2(-2, 2), "apply_to_vector() for the keyboard")

	expect_equal(InputSettingsServer.apply_to_stick(Vector2(0.1, 0.1)), Vector2(), "apply_to_stick(): inside the deadzone")
	expect(InputSettingsServer.apply_to_stick(Vector2(0, 0.6)).is_equal_approx(Vector2(0, 0.5)), "apply_to_stick(): rescaled outside the deadzone")
	InputSettingsServer.set_y_inverted(InputSettingsServer.DEVICE_GAMEPAD, true)
	InputSettingsServer.set_sensitivity(InputSettingsServer.DEVICE_GAMEPAD, 3.0)
	expect(InputSettingsServer.apply_to_stick(Vector2(0, 0.6)).is_equal_approx(Vector2(0, -1.5)), "apply_to_stick(): gamepad sensitivity and inversion")

	InputSettingsServer.set_mouse_smoothing(0.5)
	InputSettingsServer.set_sensitivity(InputSettingsServer.DEVICE_MOUSE, 1.0)
	InputSettingsServer.set_y_inverted(InputSettingsServer.DEVICE_MOUSE, false)
	InputSettingsServer.reset_mouse_smoothing()
	expect(InputSettingsServer.apply_to_mouse_motion(Vector2(4, 0)).is_equal_approx(Vector2(2, 0)), "smoothing: first motion is blended with zero")
	expect(InputSettingsServer.apply_to_mouse_motion(Vector2(4, 0)).is_equal_approx(Vector2(3, 0)), "smoothing: second motion is blended with the first")

	var pad := InputEventJoypadButton.new()
	pad.device = 1
	expect(InputSettingsServer.is_player_event(pad), "any joypad belongs to the first player by default")
	InputSettingsServer.set_first_player_device_id(0)
	expect(not InputSettingsServer.is_player_event(pad), "another joypad is not the first player")
	InputSettingsServer.set_allow_keyboard_for_current_player(false)
	expect(not InputSettingsServer.is_player_event(InputEventKey.new()), "keyboard not allowed for the first player")

	InputSettingsServer.set_vibration_strength(0.5)
	InputSettingsServer.vibrate(0, 1.0, 0.8, 0.2)
	expect(Input.get_joy_vibration_strength(0).is_equal_approx(Vector2(0.5, 0.4)), "vibrate() scales by the strength")
	Input.stop_joy_vibration(0)
	InputSettingsServer.set_vibration_enabled(false)
	InputSettingsServer.vibrate(0, 1.0, 1.0, 0.2)
	expect_equal(Input.get_joy_vibration_strength(0), Vector2(), "vibrate() does nothing when vibration is off")
	InputSettingsServer.reset_to_defaults()


func _check_signal() -> void:
	var changes := []
	var on_changed := func(name: StringName, value: Variant) -> void: changes.append([name, value])
	InputSettingsServer.value_changed.connect(on_changed)
	InputSettingsServer.set_y_inverted(InputSettingsServer.DEVICE_GAMEPAD, true)
	InputSettingsServer.value_changed.disconnect(on_changed)
	expect_equal(changes, [[&"gamepad_y_inverted", true]], "value_changed for a game-side value")
	InputSettingsServer.reset_to_defaults()


func _check_engine_options() -> void:
	var accumulated := Input.use_accumulated_input
	var touch := Input.emulate_touch_from_mouse
	var mouse := Input.emulate_mouse_from_touch
	expect_equal(InputSettingsServer.is_using_accumulated_input(), accumulated, "the default of use_accumulated_input comes from Input")
	InputSettingsServer.set_use_accumulated_input(not accumulated)
	expect_equal(Input.use_accumulated_input, not accumulated, "use_accumulated_input takes effect at once")
	InputSettingsServer.set_emulate_touch_from_mouse(not touch)
	expect_equal(Input.emulate_touch_from_mouse, not touch, "emulate_touch_from_mouse takes effect at once")
	InputSettingsServer.set_emulate_mouse_from_touch(not mouse)
	expect_equal(Input.emulate_mouse_from_touch, not mouse, "emulate_mouse_from_touch takes effect at once")
	InputSettingsServer.reset_to_defaults()
	expect_equal(Input.use_accumulated_input, accumulated, "reset gives back use_accumulated_input")
	expect_equal(Input.emulate_touch_from_mouse, touch, "reset gives back emulate_touch_from_mouse")
	expect_equal(Input.emulate_mouse_from_touch, mouse, "reset gives back emulate_mouse_from_touch")


func _check_toggle_actions() -> void:
	InputMap.add_action(&"check19_crouch")
	InputMap.add_action(&"check19_sprint")
	InputSettingsServer.set_toggle_actions(PackedStringArray(["check19_crouch"]))
	expect(InputSettingsServer.is_toggle_action(&"check19_crouch"), "is_toggle_action()")

	Input.action_press(&"check19_crouch")
	Input.action_press(&"check19_sprint")
	expect(InputSettingsServer.is_action_active(&"check19_crouch"), "toggle: active after the press")
	expect(InputSettingsServer.is_action_active(&"check19_sprint"), "hold: active while pressed")
	Input.action_release(&"check19_crouch")
	Input.action_release(&"check19_sprint")
	await get_tree().process_frame
	expect(InputSettingsServer.is_action_active(&"check19_crouch"), "toggle: still active after the release")
	expect(not InputSettingsServer.is_action_active(&"check19_sprint"), "hold: inactive after the release")
	# A new frame, as in a game: the press arrives before the frame is processed.
	await get_tree().process_frame
	Input.action_press(&"check19_crouch")
	expect(not InputSettingsServer.is_action_active(&"check19_crouch"), "toggle: the second press switches it off")
	Input.action_release(&"check19_crouch")

	InputSettingsServer.reset_to_defaults()
	InputMap.erase_action(&"check19_crouch")
	InputMap.erase_action(&"check19_sprint")


func _check_mouse_capture() -> void:
	if DisplayServer.get_name() == "headless":
		print("  SKIP  mouse capture check (the headless display server does not keep the mouse mode)")
		return
	InputSettingsServer.set_mouse_confined(true)
	InputSettingsServer.set_mouse_captured(false)
	expect_equal(Input.mouse_mode, Input.MOUSE_MODE_CONFINED, "set_mouse_captured(false) with mouse_confined: confined")
	InputSettingsServer.set_mouse_confined(false)
	InputSettingsServer.set_mouse_captured(false)
	expect_equal(Input.mouse_mode, Input.MOUSE_MODE_VISIBLE, "set_mouse_captured(false): visible")
	InputSettingsServer.reset_to_defaults()


func _check_resource() -> void:
	var settings := InputSettings.new()
	expect_equal(settings.mouse_sensitivity, 1.0, "InputSettings: default mouse sensitivity")
	expect_equal(settings.mouse_y_inverted, false, "InputSettings: default Y inversion")
	settings.mouse_y_inverted = true
	expect(settings.mouse_y_inverted, "InputSettings: mouse_y_inverted is a plain property")


func _check_restart() -> void:
	if not OS.has_feature("pc"):
		print("  SKIP  restart check (needs a desktop platform to start a second process)")
		return
	var accumulated := Input.use_accumulated_input
	InputSettingsServer.set_y_inverted(InputSettingsServer.DEVICE_MOUSE, true)
	InputSettingsServer.set_sensitivity(InputSettingsServer.DEVICE_MOUSE, 2.5)
	InputSettingsServer.set_use_accumulated_input(not accumulated)
	expect_equal(InputSettingsServer.save(), OK, "save() writes the player file")
	var text := FileAccess.get_file_as_string(INPUT_FILE)
	expect(text.contains("mouse_y_inverted"), "the INI file contains mouse_y_inverted")
	expect(text.contains("mouse_sensitivity"), "the INI file contains mouse_sensitivity")

	var probe := _run_probe()
	expect(probe.get("started", false), "restart: the domain was started")
	expect(probe.get("y_inverted", false), "restart: Y inversion is restored")
	expect(is_equal_approx(float(probe.get("sensitivity", 0.0)), 2.5), "restart: mouse sensitivity is restored")
	expect_equal(Vector2(float(probe.get("look_x", 0.0)), float(probe.get("look_y", 0.0))), Vector2(5, -10), "restart: the helper returns the changed values")
	expect_equal(probe.get("use_accumulated_input", accumulated), not accumulated, "restart: use_accumulated_input is applied before the main scene")
	InputSettingsServer.reset_to_defaults()


func _run_probe() -> Dictionary:
	var output := []
	var args := PackedStringArray(["--headless", "--path", ProjectSettings.globalize_path("res://"), PROBE_SCENE])
	var code := OS.execute(OS.get_executable_path(), args, output, true)
	expect_equal(code, 0, "restart: the probe process ran")
	for chunk in output:
		for line in str(chunk).split("\n"):
			if line.begins_with("CHECK19_PROBE "):
				var parsed = JSON.parse_string(line.trim_prefix("CHECK19_PROBE "))
				if parsed is Dictionary:
					return parsed
	expect(false, "restart: the probe printed its result")
	return {}


## Issue #173: the typed signal fires once per applied change, not for pending changes in manual mode.
func _check_typed_signals() -> void:
	if not Engine.has_singleton("InputSettingsServer"):
		return
	var s: SettingsServer = Engine.get_singleton("InputSettingsServer")
	expect(s.has_signal(&"mouse_sensitivity_changed"), "InputSettingsServer has the typed signal mouse_sensitivity_changed")
	var arg_info: Dictionary = {}
	for info: Dictionary in s.get_signal_list():
		if info.name == "mouse_sensitivity_changed":
			arg_info = info.args[0]
	expect_equal(arg_info.get("type"), TYPE_FLOAT, "mouse_sensitivity_changed has a typed argument")
	var file_path := s.get_file_path()
	var had_file := FileAccess.file_exists(file_path)
	var previous_mode := s.apply_mode
	s.apply_mode = SettingsServer.APPLY_MODE_IMMEDIATE
	s.reset_value(&"mouse_sensitivity")
	var new_value: Variant = 2.0 if s.get_applied_value(&"mouse_sensitivity") != 2.0 else 3.0
	var received: Array = []
	var on_changed := func(v: Variant) -> void: received.append(v)
	s.connect(&"mouse_sensitivity_changed", on_changed)

	s.apply_mode = SettingsServer.APPLY_MODE_MANUAL
	s.set_value(&"mouse_sensitivity", new_value)
	expect_equal(received.size(), 0, "mouse_sensitivity_changed does not fire for a pending change in manual mode")
	s.apply()
	expect_equal(received, [new_value], "mouse_sensitivity_changed fires once on apply() with the applied value")
	if not received.is_empty():
		expect_equal(typeof(received[0]), TYPE_FLOAT, "mouse_sensitivity_changed passes a typed value")
	s.apply()
	expect_equal(received.size(), 1, "mouse_sensitivity_changed does not fire when nothing changed")
	s.reset_value(&"mouse_sensitivity")
	expect_equal(received.size(), 1, "mouse_sensitivity_changed waits for apply() after a reset in manual mode")
	s.apply()
	expect_equal(received.size(), 2, "mouse_sensitivity_changed fires again when the default is applied")

	s.disconnect(&"mouse_sensitivity_changed", on_changed)
	s.apply_mode = previous_mode
	await get_tree().process_frame
	if not had_file:
		DirAccess.remove_absolute(file_path)
