extends FeatureCheck
## GDScript checks of 18-feature-key-bindings. Check the same things as scripts/cs/Issue18FeatureKeyBindingsCheck.cs.
##
## The project defines two actions in project.godot: kb18_jump (Space, joypad A, deadzone 0.5) and kb18_fire
## (left mouse button, right trigger). The restart check starts the project again as its own process (desktop
## only) and reads what the key bindings domain applied to InputMap before the main scene was ready.

const PROBE_SCENE := "res://18-feature-key-bindings/scenes/restart_probe.tscn"
const BINDINGS_FILE := "user://settings/key_bindings.ini"
const JUMP := &"kb18_jump"
const FIRE := &"kb18_fire"


func _get_check_name() -> String:
	return "18-feature-key-bindings"


func _run_checks() -> void:
	var server = Engine.get_singleton("KeyBindingSettingsServer")
	expect(Engine.has_singleton("KeyBindingSettingsServer"), "KeyBindingSettingsServer is a registered singleton")
	expect(server is ListSettingsServer, "KeyBindingSettingsServer is a ListSettingsServer")
	expect_equal(SettingsServerManager.get_server(&"key_bindings"), server, "the domain is registered as \"key_bindings\"")
	expect(KeyBindingSettingsServer.is_started(), "the domain was started before the main scene")
	expect_equal(KeyBindingSettingsServer.get_file_path(), BINDINGS_FILE, "file path")
	expect(KeyBindingSettingsServer.get_default_settings() is KeyBindingList, "the settings resource is a KeyBindingList")

	KeyBindingSettingsServer.reset_all()
	_check_text_form()
	_check_defaults()
	_check_rebind()
	_check_conflicts()
	_check_reset()
	_check_formats()
	_check_restart()

	# Leave no player files behind for later runs.
	KeyBindingSettingsServer.reset_all()
	await get_tree().process_frame
	DirAccess.remove_absolute(BINDINGS_FILE)


static func pressed_key(keycode: Key) -> InputEventKey:
	var event := InputEventKey.new()
	event.keycode = keycode
	event.physical_keycode = keycode
	event.key_label = keycode
	event.pressed = true
	return event


func _check_text_form() -> void:
	for text in ["Key:Ctrl+S", "PhysicalKey:W", "KeyLabel:Shift+A", "Key:CmdOrCtrl+Z", "MouseButton:Left", "MouseButton:Ctrl+WheelUp",
			"JoypadButton:A", "JoypadButton:LeftShoulder", "JoypadMotion:LeftX+", "JoypadMotion:TriggerRight+"]:
		var event := KeyBinding.text_to_event(text)
		expect(event != null, "text_to_event(\"%s\") gives an event" % text)
		if event:
			expect_equal(KeyBinding.event_to_text(event), text, "round trip of \"%s\"" % text)
	expect(KeyBinding.text_to_event("Key:Hyper+S") == null, "an unknown modifier is not valid")
	expect(KeyBinding.text_to_event("Joypad:A") == null, "an unknown type is not valid")
	expect_equal(KeyBinding.event_to_text(pressed_key(KEY_W)), "PhysicalKey:W", "a pressed key is stored by its physical keycode")
	expect_equal(KeyBinding.event_to_text(InputEventScreenTouch.new()), "", "a touch can't be bound")
	expect_equal(KeyBinding.get_event_device(InputEventScreenTouch.new()), KeyBinding.INPUT_DEVICE_MAX, "a touch has no device")
	expect_equal(KeyBinding.get_event_device(InputEventJoypadMotion.new()), KeyBinding.INPUT_DEVICE_GAMEPAD, "a joypad axis is a gamepad event")
	expect_equal(KeyBinding.event_to_label(KeyBinding.text_to_event("Key:Ctrl+S")), "Ctrl+S", "label of Ctrl+S")
	expect_equal(KeyBinding.event_to_label(KeyBinding.text_to_event("MouseButton:Right")), "Right Mouse Button", "label of a mouse button")
	expect_equal(KeyBindingSettingsServer.get_event_label(KeyBinding.text_to_event("JoypadMotion:LeftX+")), "Left Stick Right", "label of a stick direction")

	var row := KeyBinding.new()
	row.set_input_event(KeyBinding.text_to_event("JoypadButton:B"))
	expect_equal(row.device, KeyBinding.INPUT_DEVICE_GAMEPAD, "set_input_event() sets the device")
	expect_equal(row.event, "JoypadButton:B", "set_input_event() sets the text")
	expect(row.get_input_event() is InputEventJoypadButton, "get_input_event() parses the text")


func _check_defaults() -> void:
	var actions := KeyBindingSettingsServer.get_action_names()
	expect(actions.has("kb18_jump") and actions.has("kb18_fire"), "the project actions are managed")
	expect(not actions.has("ui_accept"), "built-in ui_* actions are left out by default")
	expect_equal(KeyBinding.event_to_text(KeyBindingSettingsServer.get_binding(JUMP, KeyBinding.INPUT_DEVICE_KEYBOARD, 0)), "Key:Space", "jump: keyboard slot 0 is Space")
	expect_equal(KeyBinding.event_to_text(KeyBindingSettingsServer.get_binding(JUMP, KeyBinding.INPUT_DEVICE_GAMEPAD, 0)), "JoypadButton:A", "jump: gamepad slot 0 is A")
	expect_equal(KeyBinding.event_to_text(KeyBindingSettingsServer.get_binding(FIRE, KeyBinding.INPUT_DEVICE_GAMEPAD, 0)), "JoypadMotion:TriggerRight+", "fire: gamepad slot 0 is the right trigger")
	expect(is_equal_approx(KeyBindingSettingsServer.get_action_deadzone(JUMP), 0.5), "jump: deadzone 0.5 from the project")
	expect_equal(KeyBindingSettingsServer.get_events(JUMP).size(), 2, "jump has two events")


func _check_rebind() -> void:
	expect_equal(KeyBindingSettingsServer.rebind(JUMP, pressed_key(KEY_W)), OK, "rebind() of keyboard slot 0")
	expect_equal(KeyBindingSettingsServer.rebind(JUMP, pressed_key(KEY_UP), 1), OK, "rebind() of keyboard slot 1")
	expect(InputMap.event_is_action(pressed_key(KEY_W), JUMP), "the new key works at once")
	expect(InputMap.event_is_action(pressed_key(KEY_UP), JUMP), "the second slot works")
	expect(not InputMap.event_is_action(pressed_key(KEY_SPACE), JUMP), "the old key no longer works")
	expect_equal(KeyBindingSettingsServer.get_slot_count(JUMP, KeyBinding.INPUT_DEVICE_KEYBOARD), 2, "two keyboard slots")

	KeyBindingSettingsServer.clear_binding(JUMP, KeyBinding.INPUT_DEVICE_KEYBOARD, 1)
	expect(not InputMap.event_is_action(pressed_key(KEY_UP), JUMP), "a cleared slot no longer works")
	expect(KeyBindingSettingsServer.get_binding(JUMP, KeyBinding.INPUT_DEVICE_KEYBOARD, 1) == null, "a cleared slot has no event")

	KeyBindingSettingsServer.set_action_deadzone(JUMP, 0.7)
	expect(is_equal_approx(InputMap.action_get_deadzone(JUMP), 0.7), "the deadzone reaches InputMap")
	expect_equal(KeyBindingSettingsServer.rebind(JUMP, InputEventScreenTouch.new()), ERR_INVALID_PARAMETER, "a touch can't be bound")
	expect_equal(KeyBindingSettingsServer.rebind(&"kb18_no_such_action", pressed_key(KEY_Q)), ERR_DOES_NOT_EXIST, "an unknown action can't be bound")


func _check_conflicts() -> void:
	KeyBindingSettingsServer.reset_all()
	expect_equal(KeyBindingSettingsServer.find_conflicts(pressed_key(KEY_SPACE)), PackedStringArray(["kb18_jump"]), "Space is used by jump")
	expect(KeyBindingSettingsServer.find_conflicts(pressed_key(KEY_SPACE), JUMP).is_empty(), "the ignored action is not a conflict")
	expect(KeyBindingSettingsServer.find_conflicts(pressed_key(KEY_K)).is_empty(), "K is free")
	KeyBindingSettingsServer.rebind(FIRE, pressed_key(KEY_SPACE))
	expect_equal(KeyBindingSettingsServer.find_conflicts(pressed_key(KEY_SPACE)).size(), 2, "after rebinding fire, Space is used twice")


func _check_reset() -> void:
	KeyBindingSettingsServer.rebind(JUMP, pressed_key(KEY_W))
	KeyBindingSettingsServer.reset_action(JUMP)
	expect(InputMap.event_is_action(pressed_key(KEY_SPACE), JUMP), "reset_action(): Space works again")
	expect(not InputMap.event_is_action(pressed_key(KEY_W), JUMP), "reset_action(): W no longer works")
	expect(InputMap.event_is_action(pressed_key(KEY_SPACE), FIRE), "reset_action() keeps the other actions")
	KeyBindingSettingsServer.reset_all()
	expect(not InputMap.event_is_action(pressed_key(KEY_SPACE), FIRE), "reset_all(): fire is back at the project bindings")
	var click := InputEventMouseButton.new()
	click.button_index = MOUSE_BUTTON_LEFT
	expect(InputMap.event_is_action(click, FIRE), "reset_all(): the left mouse button fires again")


func _check_formats() -> void:
	for extension in ["ini", "json", "csv"]:
		var path := "user://kb18_bindings.%s" % extension
		KeyBindingSettingsServer.set_file_path(path)
		KeyBindingSettingsServer.rebind(JUMP, pressed_key(KEY_W))
		KeyBindingSettingsServer.rebind(JUMP, KeyBinding.text_to_event("JoypadMotion:LeftY-"))
		KeyBindingSettingsServer.rebind(FIRE, KeyBinding.text_to_event("MouseButton:Ctrl+Right"))
		expect_equal(KeyBindingSettingsServer.save(), OK, "%s: save()" % extension)
		var text := FileAccess.get_file_as_string(path)
		expect(text.contains("PhysicalKey:W") and text.contains("JoypadMotion:LeftY-") and text.contains("MouseButton:Ctrl+Right"), "%s: the file holds the event texts" % extension)
		KeyBindingSettingsServer.reset_all()
		expect_equal(KeyBindingSettingsServer.load(), OK, "%s: load()" % extension)
		KeyBindingSettingsServer.apply()
		expect(InputMap.event_is_action(pressed_key(KEY_W), JUMP), "%s: key survives the round trip" % extension)
		var stick := InputEventJoypadMotion.new()
		stick.axis = JOY_AXIS_LEFT_Y
		stick.axis_value = -1.0
		expect(InputMap.event_is_action(stick, JUMP), "%s: joypad axis survives the round trip" % extension)
		var ctrl_right := InputEventMouseButton.new()
		ctrl_right.button_index = MOUSE_BUTTON_RIGHT
		ctrl_right.ctrl_pressed = true
		expect(InputMap.event_is_action(ctrl_right, FIRE), "%s: mouse button survives the round trip" % extension)
		var pad_a := InputEventJoypadButton.new()
		pad_a.button_index = JOY_BUTTON_A
		expect(not InputMap.event_is_action(pad_a, JUMP), "%s: the replaced joypad button no longer works" % extension)
		KeyBindingSettingsServer.reset_all()
		DirAccess.remove_absolute(path)
	KeyBindingSettingsServer.set_file_path("")


func _check_restart() -> void:
	if not OS.has_feature("pc"):
		print("  SKIP  restart check (needs a desktop platform to start a second process)")
		return
	KeyBindingSettingsServer.rebind(JUMP, pressed_key(KEY_W))
	# A row of an action that a later game version no longer has.
	var gone := KeyBinding.new()
	gone.action = "kb18_removed"
	gone.event = "Key:G"
	KeyBindingSettingsServer.add_entry(gone)
	expect_equal(KeyBindingSettingsServer.save(), OK, "save() writes the player file")
	var text := FileAccess.get_file_as_string(BINDINGS_FILE)
	expect(text.contains("PhysicalKey:W"), "the INI file contains PhysicalKey:W")
	expect(text.contains("kb18_removed"), "the INI file contains the removed action")

	var probe := _run_probe()
	expect(probe.get("started", false), "restart: the domain was started")
	expect(probe.get("w_jumps", false), "restart: the new key works")
	expect(not probe.get("space_jumps", true), "restart: the old key does not")
	expect(not probe.get("has_removed", true), "restart: the removed action is skipped")
	expect(probe.get("pad_a_jumps", false), "restart: the other slots keep their bindings")
	KeyBindingSettingsServer.reset_all()


func _run_probe() -> Dictionary:
	var output := []
	var args := PackedStringArray(["--headless", "--path", ProjectSettings.globalize_path("res://"), PROBE_SCENE])
	var code := OS.execute(OS.get_executable_path(), args, output, true)
	expect_equal(code, 0, "restart: the probe process ran")
	for chunk in output:
		for line in str(chunk).split("\n"):
			if line.begins_with("CHECK18_PROBE "):
				var parsed = JSON.parse_string(line.trim_prefix("CHECK18_PROBE "))
				if parsed is Dictionary:
					return parsed
	expect(false, "restart: the probe printed its result")
	return {}
