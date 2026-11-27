extends FeatureTest
## Full API test of Input Settings: InputSettingsServer and InputSettings. Same tests as scripts/cs/Test.cs.

const PROBE_SCENE := "res://input-settings/scenes/restart_probe.tscn"
const INPUT_FILE := "user://settings/input.ini"
const DIR := "user://input-settings-test-gd"
const MAX_SENSITIVITY := 10.0
const MAX_DEADZONE := 0.95
const TYPED_SIGNALS: Array[StringName] = [
	&"mouse_sensitivity_changed", &"keyboard_sensitivity_changed", &"gamepad_sensitivity_changed",
	&"mouse_x_inverted_changed", &"mouse_y_inverted_changed", &"keyboard_x_inverted_changed", &"keyboard_y_inverted_changed",
	&"gamepad_x_inverted_changed", &"gamepad_y_inverted_changed", &"gamepad_deadzone_changed", &"mouse_smoothing_changed",
	&"vibration_enabled_changed", &"vibration_strength_changed", &"toggle_actions_changed", &"first_player_device_id_changed",
]

var _events: Array = []


func _run_tests() -> void:
	DirAccess.make_dir_recursive_absolute(DIR)
	_test_registration()
	_test_constants()
	InputSettingsServer.reset_to_defaults()
	_test_defaults()
	_test_validation()
	_test_devices()
	_test_helpers()
	_test_smoothing()
	_test_player_events()
	_test_vibration()
	_test_float_precision()
	_test_signals()
	_test_engine_options()
	await _test_toggle_actions()
	_test_mouse_capture()
	_test_resource()
	_test_restart()
	InputSettingsServer.reset_to_defaults()
	await get_tree().process_frame
	DirAccess.remove_absolute(INPUT_FILE)
	_remove_dir(DIR)


func _record(name: String) -> Callable:
	return func(...args: Array) -> void: _events.append([name] + args)


func _test_registration() -> void:
	expect(Engine.has_singleton("InputSettingsServer"), "InputSettingsServer is a registered singleton")
	expect(Engine.get_singleton("InputSettingsServer") is SettingsServer, "InputSettingsServer is a SettingsServer")
	expect_equal(SettingsServerManager.get_server(&"input"), InputSettingsServer, "the domain is registered as input")
	expect(InputSettingsServer.is_started(), "the domain was started before the main scene")
	expect_equal(InputSettingsServer.file_path, INPUT_FILE, "file path")
	expect(InputSettingsServer.get_setting_names().has("mouse_sensitivity"), "setting names contain mouse_sensitivity")
	var flags: int = InputSettingsServer.get_setting_flags(&"mouse_sensitivity")
	expect((flags & SettingsServer.SETTING_FLAG_NO_ENGINE_TARGET) != 0, "mouse_sensitivity has no engine target")
	for signal_name in TYPED_SIGNALS:
		expect(InputSettingsServer.has_signal(signal_name), "InputSettingsServer has the signal %s" % signal_name)


func _test_constants() -> void:
	expect_equal(InputSettingsServer.DEVICE_KEYBOARD, 0, "DEVICE_KEYBOARD is 0")
	expect_equal(InputSettingsServer.DEVICE_MOUSE, 1, "DEVICE_MOUSE is 1")
	expect_equal(InputSettingsServer.DEVICE_GAMEPAD, 2, "DEVICE_GAMEPAD is 2")
	expect_equal(InputSettingsServer.DEVICE_MAX, 3, "DEVICE_MAX is 3")


func _test_defaults() -> void:
	for device in [InputSettingsServer.DEVICE_KEYBOARD, InputSettingsServer.DEVICE_MOUSE, InputSettingsServer.DEVICE_GAMEPAD]:
		expect_equal(InputSettingsServer.get_sensitivity(device), 1.0, "default sensitivity of device %d" % device)
		expect(not InputSettingsServer.is_x_inverted(device), "default: x not inverted, device %d" % device)
		expect(not InputSettingsServer.is_y_inverted(device), "default: y not inverted, device %d" % device)
	expect_equal(InputSettingsServer.get_value(&"gamepad_deadzone"), 0.2, "default deadzone is 0.2, not 0.20000000298023224")
	expect(InputSettingsServer.is_vibration_enabled(), "default: vibration on")
	expect_equal(InputSettingsServer.get_vibration_strength(), 1.0, "default vibration strength")
	expect_equal(InputSettingsServer.get_mouse_smoothing(), 0.0, "default: no smoothing")
	expect_equal(InputSettingsServer.get_toggle_actions(), PackedStringArray(), "default: no toggle actions")
	expect_equal(InputSettingsServer.get_first_player_device_id(), -1, "default: any device")
	expect(InputSettingsServer.is_keyboard_allowed_for_current_player(), "default: keyboard allowed")
	expect(not InputSettingsServer.is_mouse_confined(), "default: mouse not confined")


func _test_validation() -> void:
	InputSettingsServer.set_sensitivity(InputSettingsServer.DEVICE_MOUSE, 50.0)
	expect_equal(InputSettingsServer.get_sensitivity(InputSettingsServer.DEVICE_MOUSE), MAX_SENSITIVITY, "sensitivity is clamped to 10")
	InputSettingsServer.set_sensitivity(InputSettingsServer.DEVICE_MOUSE, 0.0)
	expect_equal(InputSettingsServer.get_value(&"mouse_sensitivity"), 0.01, "sensitivity is clamped to 0.01")
	InputSettingsServer.set_gamepad_deadzone(3.0)
	expect_equal(InputSettingsServer.get_value(&"gamepad_deadzone"), MAX_DEADZONE, "deadzone is clamped to 0.95")
	InputSettingsServer.set_mouse_smoothing(3.0)
	expect_equal(InputSettingsServer.get_value(&"mouse_smoothing"), MAX_DEADZONE, "smoothing is clamped to 0.95")
	InputSettingsServer.set_vibration_strength(3.0)
	expect_equal(InputSettingsServer.get_vibration_strength(), 1.0, "vibration strength is clamped to 1")
	InputSettingsServer.set_toggle_actions(PackedStringArray(["crouch", "", "aim ", "crouch"]))
	expect_equal(InputSettingsServer.get_toggle_actions(), PackedStringArray(["crouch", "aim"]), "toggle actions without empty names and duplicates")
	InputSettingsServer.reset_to_defaults()


func _test_devices() -> void:
	for device in [InputSettingsServer.DEVICE_KEYBOARD, InputSettingsServer.DEVICE_MOUSE, InputSettingsServer.DEVICE_GAMEPAD]:
		InputSettingsServer.set_sensitivity(device, 2.0)
		InputSettingsServer.set_x_inverted(device, true)
		InputSettingsServer.set_y_inverted(device, true)
		expect_equal(InputSettingsServer.get_sensitivity(device), 2.0, "set_sensitivity, device %d" % device)
		expect(InputSettingsServer.is_x_inverted(device), "set_x_inverted, device %d" % device)
		expect(InputSettingsServer.is_y_inverted(device), "set_y_inverted, device %d" % device)
	expect_equal(InputSettingsServer.get_value(&"keyboard_sensitivity"), 2.0, "the keyboard values are the settings keyboard_*")
	expect_equal(InputSettingsServer.get_value(&"gamepad_x_inverted"), true, "the gamepad values are the settings gamepad_*")
	InputSettingsServer.reset_to_defaults()


func _test_helpers() -> void:
	InputSettingsServer.set_sensitivity(InputSettingsServer.DEVICE_MOUSE, 2.5)
	InputSettingsServer.set_y_inverted(InputSettingsServer.DEVICE_MOUSE, true)
	expect_equal(InputSettingsServer.apply_to_mouse_motion(Vector2(2, 4)), Vector2(5, -10), "apply_to_mouse_motion: sensitivity and Y inversion")
	var motion := InputEventMouseMotion.new()
	motion.screen_relative = Vector2(2, 4)
	expect_equal(InputSettingsServer.get_look_vector(motion), Vector2(5, -10), "get_look_vector of a mouse motion")
	expect_equal(InputSettingsServer.get_look_vector(InputEventKey.new()), Vector2(), "get_look_vector of a key event is zero")
	InputSettingsServer.set_sensitivity(InputSettingsServer.DEVICE_KEYBOARD, 2.0)
	InputSettingsServer.set_x_inverted(InputSettingsServer.DEVICE_KEYBOARD, true)
	expect_equal(InputSettingsServer.apply_to_vector(InputSettingsServer.DEVICE_KEYBOARD, Vector2(1, 1)), Vector2(-2, 2), "apply_to_vector for the keyboard")
	expect_equal(InputSettingsServer.apply_to_stick(Vector2(0.1, 0.1)), Vector2(), "apply_to_stick: inside the deadzone")
	expect(InputSettingsServer.apply_to_stick(Vector2(0, 0.6)).is_equal_approx(Vector2(0, 0.5)), "apply_to_stick: rescaled outside the deadzone")
	InputSettingsServer.set_y_inverted(InputSettingsServer.DEVICE_GAMEPAD, true)
	InputSettingsServer.set_sensitivity(InputSettingsServer.DEVICE_GAMEPAD, 3.0)
	expect(InputSettingsServer.apply_to_stick(Vector2(0, 0.6)).is_equal_approx(Vector2(0, -1.5)), "apply_to_stick: gamepad sensitivity and inversion")
	InputSettingsServer.reset_to_defaults()


func _test_smoothing() -> void:
	InputSettingsServer.set_mouse_smoothing(0.5)
	InputSettingsServer.reset_mouse_smoothing()
	expect(InputSettingsServer.apply_to_mouse_motion(Vector2(4, 0)).is_equal_approx(Vector2(2, 0)), "smoothing: the first motion is blended with zero")
	expect(InputSettingsServer.apply_to_mouse_motion(Vector2(4, 0)).is_equal_approx(Vector2(3, 0)), "smoothing: the second motion is blended with the first")
	InputSettingsServer.reset_mouse_smoothing()
	expect(InputSettingsServer.apply_to_mouse_motion(Vector2(4, 0)).is_equal_approx(Vector2(2, 0)), "reset_mouse_smoothing forgets the previous result")
	InputSettingsServer.reset_to_defaults()


func _test_player_events() -> void:
	var pad := InputEventJoypadButton.new()
	pad.device = 1
	expect(InputSettingsServer.is_player_event(pad), "any joypad belongs to the first player by default")
	InputSettingsServer.set_first_player_device_id(0)
	expect_equal(InputSettingsServer.get_first_player_device_id(), 0, "get_first_player_device_id")
	expect(not InputSettingsServer.is_player_event(pad), "another joypad is not the first player")
	InputSettingsServer.set_allow_keyboard_for_current_player(false)
	expect(not InputSettingsServer.is_keyboard_allowed_for_current_player(), "is_keyboard_allowed_for_current_player")
	expect(not InputSettingsServer.is_player_event(InputEventKey.new()), "keyboard not allowed for the first player")
	InputSettingsServer.reset_to_defaults()


func _test_vibration() -> void:
	InputSettingsServer.set_vibration_strength(0.5)
	InputSettingsServer.vibrate(0, 1.0, 0.8, 0.2)
	expect(Input.get_joy_vibration_strength(0).is_equal_approx(Vector2(0.5, 0.4)), "vibrate scales by the strength")
	Input.stop_joy_vibration(0)
	InputSettingsServer.set_vibration_enabled(false)
	expect(not InputSettingsServer.is_vibration_enabled(), "is_vibration_enabled")
	InputSettingsServer.vibrate(0, 1.0, 1.0, 0.2)
	expect_equal(Input.get_joy_vibration_strength(0), Vector2(), "vibrate does nothing when vibration is off")
	InputSettingsServer.reset_to_defaults()


func _test_float_precision() -> void:
	_events.clear()
	var on_changed: Callable = _record("value_changed")
	InputSettingsServer.value_changed.connect(on_changed)
	InputSettingsServer.set_gamepad_deadzone(0.123456)
	InputSettingsServer.value_changed.disconnect(on_changed)
	expect_equal(InputSettingsServer.get_value(&"gamepad_deadzone"), 0.12, "set_gamepad_deadzone snaps to the 0.01 step")
	expect_equal(_events, [["value_changed", &"gamepad_deadzone", 0.12]], "value_changed carries 0.12")
	InputSettingsServer.set_sensitivity(InputSettingsServer.DEVICE_MOUSE, 1.23456)
	expect_equal(InputSettingsServer.get_value(&"mouse_sensitivity"), 1.23, "mouse_sensitivity snaps to 0.01")
	InputSettingsServer.set_gamepad_deadzone(0.3)
	InputSettingsServer.set_sensitivity(InputSettingsServer.DEVICE_MOUSE, 1.7)
	InputSettingsServer.apply()
	for extension: String in ["json", "ini"]:
		InputSettingsServer.file_path = DIR.path_join("input." + extension)
		expect_equal(InputSettingsServer.save(), OK, "save as %s" % extension)
		var text: String = FileAccess.get_file_as_string(InputSettingsServer.file_path)
		expect(text.contains("0.3") and text.contains("1.7"), "%s: the file holds 0.3 and 1.7" % extension)
		expect(not text.contains("0000000") and not text.contains("9999999"), "%s: no float noise" % extension)
		InputSettingsServer.reset_to_defaults()
		expect_equal(InputSettingsServer.load(), OK, "load as %s" % extension)
		expect_equal(InputSettingsServer.get_value(&"gamepad_deadzone"), 0.3, "%s: the deadzone is 0.3 again" % extension)
		InputSettingsServer.set_gamepad_deadzone(0.3)
		InputSettingsServer.set_sensitivity(InputSettingsServer.DEVICE_MOUSE, 1.7)
		InputSettingsServer.apply()
	InputSettingsServer.file_path = ""
	InputSettingsServer.reset_to_defaults()


func _test_signals() -> void:
	_events.clear()
	var sensitivity: Callable = _record("mouse_sensitivity_changed")
	var inverted: Callable = _record("mouse_y_inverted_changed")
	var deadzone: Callable = _record("gamepad_deadzone_changed")
	var strength: Callable = _record("vibration_strength_changed")
	var toggles: Callable = _record("toggle_actions_changed")
	var first: Callable = _record("first_player_device_id_changed")
	InputSettingsServer.mouse_sensitivity_changed.connect(sensitivity)
	InputSettingsServer.mouse_y_inverted_changed.connect(inverted)
	InputSettingsServer.gamepad_deadzone_changed.connect(deadzone)
	InputSettingsServer.vibration_strength_changed.connect(strength)
	InputSettingsServer.toggle_actions_changed.connect(toggles)
	InputSettingsServer.first_player_device_id_changed.connect(first)
	InputSettingsServer.apply_mode = SettingsServer.APPLY_MODE_MANUAL
	InputSettingsServer.set_sensitivity(InputSettingsServer.DEVICE_MOUSE, 2.0)
	InputSettingsServer.set_y_inverted(InputSettingsServer.DEVICE_MOUSE, true)
	InputSettingsServer.set_gamepad_deadzone(0.4)
	InputSettingsServer.set_vibration_strength(0.5)
	InputSettingsServer.set_toggle_actions(PackedStringArray(["aim"]))
	InputSettingsServer.set_first_player_device_id(2)
	expect(_events.is_empty(), "the typed signals do not fire for pending changes in manual mode")
	InputSettingsServer.apply()
	expect_equal(_events.size(), 6, "every typed signal fires once on apply")
	expect(_events.has(["mouse_sensitivity_changed", 2.0]), "mouse_sensitivity_changed carries the value")
	expect(_events.has(["mouse_y_inverted_changed", true]), "mouse_y_inverted_changed carries the value")
	expect(_events.has(["gamepad_deadzone_changed", 0.4]), "gamepad_deadzone_changed carries the value")
	expect(_events.has(["vibration_strength_changed", 0.5]), "vibration_strength_changed carries the value")
	expect(_events.has(["toggle_actions_changed", PackedStringArray(["aim"])]), "toggle_actions_changed carries the actions")
	expect(_events.has(["first_player_device_id_changed", 2]), "first_player_device_id_changed carries the device")
	InputSettingsServer.apply()
	expect_equal(_events.size(), 6, "the typed signals do not fire when nothing changed")
	InputSettingsServer.apply_mode = SettingsServer.APPLY_MODE_IMMEDIATE
	InputSettingsServer.reset_to_defaults()
	expect_equal(_events.size(), 12, "the typed signals fire again when the defaults are applied")
	InputSettingsServer.mouse_sensitivity_changed.disconnect(sensitivity)
	InputSettingsServer.mouse_y_inverted_changed.disconnect(inverted)
	InputSettingsServer.gamepad_deadzone_changed.disconnect(deadzone)
	InputSettingsServer.vibration_strength_changed.disconnect(strength)
	InputSettingsServer.toggle_actions_changed.disconnect(toggles)
	InputSettingsServer.first_player_device_id_changed.disconnect(first)


func _test_engine_options() -> void:
	var accumulated: bool = Input.use_accumulated_input
	var touch: bool = Input.emulate_touch_from_mouse
	var mouse: bool = Input.emulate_mouse_from_touch
	expect_equal(InputSettingsServer.is_using_accumulated_input(), accumulated, "the default of use_accumulated_input comes from Input")
	InputSettingsServer.set_use_accumulated_input(not accumulated)
	expect_equal(Input.use_accumulated_input, not accumulated, "use_accumulated_input takes effect at once")
	InputSettingsServer.set_emulate_touch_from_mouse(not touch)
	expect_equal(Input.emulate_touch_from_mouse, not touch, "emulate_touch_from_mouse takes effect at once")
	expect_equal(InputSettingsServer.is_emulating_touch_from_mouse(), not touch, "is_emulating_touch_from_mouse")
	InputSettingsServer.set_emulate_mouse_from_touch(not mouse)
	expect_equal(Input.emulate_mouse_from_touch, not mouse, "emulate_mouse_from_touch takes effect at once")
	expect_equal(InputSettingsServer.is_emulating_mouse_from_touch(), not mouse, "is_emulating_mouse_from_touch")
	InputSettingsServer.reset_to_defaults()
	expect_equal(Input.use_accumulated_input, accumulated, "reset gives back use_accumulated_input")
	expect_equal(Input.emulate_touch_from_mouse, touch, "reset gives back emulate_touch_from_mouse")
	expect_equal(Input.emulate_mouse_from_touch, mouse, "reset gives back emulate_mouse_from_touch")


func _test_toggle_actions() -> void:
	InputMap.add_action(&"test_crouch")
	InputMap.add_action(&"test_sprint")
	InputSettingsServer.set_toggle_actions(PackedStringArray(["test_crouch"]))
	expect(InputSettingsServer.is_toggle_action(&"test_crouch"), "is_toggle_action for a toggle action")
	expect(not InputSettingsServer.is_toggle_action(&"test_sprint"), "is_toggle_action for a hold action")
	Input.action_press(&"test_crouch")
	Input.action_press(&"test_sprint")
	expect(InputSettingsServer.is_action_active(&"test_crouch"), "toggle: active after the press")
	expect(InputSettingsServer.is_action_active(&"test_sprint"), "hold: active while pressed")
	Input.action_release(&"test_crouch")
	Input.action_release(&"test_sprint")
	await get_tree().process_frame
	expect(InputSettingsServer.is_action_active(&"test_crouch"), "toggle: still active after the release")
	expect(not InputSettingsServer.is_action_active(&"test_sprint"), "hold: inactive after the release")
	await get_tree().process_frame
	Input.action_press(&"test_crouch")
	expect(not InputSettingsServer.is_action_active(&"test_crouch"), "toggle: the second press switches it off")
	Input.action_release(&"test_crouch")
	await get_tree().process_frame
	Input.action_press(&"test_crouch")
	expect(InputSettingsServer.is_action_active(&"test_crouch"), "toggle: the third press switches it on")
	Input.action_release(&"test_crouch")
	InputSettingsServer.reset_action_states()
	await get_tree().process_frame
	expect(not InputSettingsServer.is_action_active(&"test_crouch"), "reset_action_states switches the toggle actions off")
	InputSettingsServer.reset_to_defaults()
	InputMap.erase_action(&"test_crouch")
	InputMap.erase_action(&"test_sprint")


func _test_mouse_capture() -> void:
	if DisplayServer.get_name() == "headless":
		print("  SKIP  mouse capture test (the headless display server does not keep the mouse mode)")
		return
	InputSettingsServer.set_mouse_confined(true)
	expect(InputSettingsServer.is_mouse_confined(), "is_mouse_confined")
	InputSettingsServer.set_mouse_captured(false)
	expect_equal(Input.mouse_mode, Input.MOUSE_MODE_CONFINED, "set_mouse_captured(false) with mouse_confined: confined")
	InputSettingsServer.set_mouse_captured(true)
	expect_equal(Input.mouse_mode, Input.MOUSE_MODE_CAPTURED, "set_mouse_captured(true): captured")
	InputSettingsServer.set_mouse_confined(false)
	InputSettingsServer.set_mouse_captured(false)
	expect_equal(Input.mouse_mode, Input.MOUSE_MODE_VISIBLE, "set_mouse_captured(false): visible")
	InputSettingsServer.reset_to_defaults()


func _test_resource() -> void:
	var settings := InputSettings.new()
	expect_equal(settings.mouse_sensitivity, 1.0, "InputSettings: default mouse sensitivity")
	expect_equal(settings.mouse_y_inverted, false, "InputSettings: default Y inversion")
	expect_equal(settings.first_player_device_id, -1, "InputSettings: default first player device")
	settings.mouse_y_inverted = true
	settings.toggle_actions = PackedStringArray(["aim"])
	expect(settings.mouse_y_inverted, "InputSettings: mouse_y_inverted is a plain property")
	expect_equal(settings.toggle_actions, PackedStringArray(["aim"]), "InputSettings: toggle_actions")
	var sample: InputSettings = load("res://input-settings/resources/inverted_mouse_settings.tres")
	expect_equal(sample.mouse_sensitivity, 1.5, "the sample InputSettings loads")
	expect(sample.mouse_y_inverted, "the sample inverts the mouse Y axis")


func _test_restart() -> void:
	if not OS.has_feature("pc"):
		print("  SKIP  restart test (needs a desktop platform to start a second process)")
		return
	var accumulated: bool = Input.use_accumulated_input
	InputSettingsServer.set_y_inverted(InputSettingsServer.DEVICE_MOUSE, true)
	InputSettingsServer.set_sensitivity(InputSettingsServer.DEVICE_MOUSE, 2.5)
	InputSettingsServer.set_use_accumulated_input(not accumulated)
	expect_equal(InputSettingsServer.save(), OK, "save writes the player file")
	var text: String = FileAccess.get_file_as_string(INPUT_FILE)
	expect(text.contains("mouse_y_inverted"), "the INI file contains mouse_y_inverted")
	expect(text.contains("mouse_sensitivity"), "the INI file contains mouse_sensitivity")
	var probe: Dictionary = _run_probe()
	expect(probe.get("started", false), "restart: the domain was started")
	expect(probe.get("y_inverted", false), "restart: Y inversion is restored")
	expect_equal(probe.get("sensitivity", 0.0), 2.5, "restart: mouse sensitivity is restored")
	expect_equal(Vector2(probe.get("look_x", 0.0), probe.get("look_y", 0.0)), Vector2(5, -10), "restart: the helper returns the changed values")
	expect_equal(probe.get("use_accumulated_input", accumulated), not accumulated, "restart: use_accumulated_input is applied before the main scene")
	InputSettingsServer.reset_to_defaults()


func _run_probe() -> Dictionary:
	var output: Array = []
	var args: PackedStringArray = ["--headless", "--path", ProjectSettings.globalize_path("res://"), PROBE_SCENE]
	var code: int = OS.execute(OS.get_executable_path(), args, output, true)
	expect_equal(code, 0, "restart: the probe process ran")
	for chunk in output:
		for line: String in str(chunk).split("\n"):
			if line.begins_with("PROBE "):
				var parsed: Variant = JSON.parse_string(line.trim_prefix("PROBE "))
				return parsed
	expect(false, "restart: the probe printed its result")
	return {}


func _remove_dir(path: String) -> void:
	for file_name: String in DirAccess.get_files_at(path):
		DirAccess.remove_absolute(path.path_join(file_name))
	DirAccess.remove_absolute(path)
