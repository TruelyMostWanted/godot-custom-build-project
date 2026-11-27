extends FeatureTest
## Full API test of Input Map Settings: InputMapSettingsServer, InputActionRebinder, InputAction, InputActions, the InputMap layers
## and the file records. Same tests as scripts/cs/Test.cs.
##
## The project defines the actions jump (Space, pad A, deadzone 0.5), fire (left mouse button, right trigger), interact (E, pad X),
## test_jump (G, Paddle1), test_fire (left mouse button) and test_attack (F, deadzone 0.3) in project.godot.
## Keys and buttons of the rebinder tests are chosen so that no built-in ui_* action uses them.

const PROBE_SCENE := "res://input-map-settings/scenes/restart_probe.tscn"
const INPUT_MAP_FILE := "user://settings/input_map.ini"
const DIR := "user://input-map-settings-test-gd"
const JUMP := &"jump"
const FIRE := &"fire"
const ATTACK := &"test_attack"
const REB_JUMP := &"test_jump"
const REB_FIRE := &"test_fire"
const SAVE_PATH := "user://input-map-settings-test-gd/input_map.ini"
const BULK_COUNT := 100
const BULK_FIRST_KEY := 0x4E00 # Unicode keycodes no other action uses.

var events_log: Array[String] = []


func _run_tests() -> void:
	DirAccess.make_dir_recursive_absolute(DIR)
	InputMapSettingsServer.reset_all()
	_test_registration()
	_test_text_form()
	_test_input_action()
	_test_input_actions()
	_test_resource_files()
	_test_layers()
	_test_set_input_action()
	_test_parse_cost()
	_test_settings_server()
	InputMapSettingsServer.reset_all()
	_test_server_api()
	_test_defaults()
	_test_rebind()
	_test_conflicts()
	_test_reset()
	_test_formats()
	_test_file_layout()
	_test_records()
	_test_class()
	_setup_actions()
	_test_filtering()
	_setup_actions()
	_test_reject()
	_setup_actions()
	_test_swap_order()
	_setup_actions()
	_test_allow()
	_setup_actions()
	_test_ask()
	_setup_actions()
	_test_two_rebinders()
	_setup_actions()
	_test_builtin_conflict()
	_setup_actions()
	_test_save_load()
	_test_restart()
	InputMapSettingsServer.reset_all()
	await get_tree().process_frame
	DirAccess.remove_absolute(INPUT_MAP_FILE)
	DirAccess.remove_absolute("user://demo_actions.tres")
	DirAccess.remove_absolute("user://demo_actions.res")
	for file_name: String in DirAccess.get_files_at(DIR):
		DirAccess.remove_absolute(DIR.path_join(file_name))
	DirAccess.remove_absolute(DIR)


func _test_registration() -> void:
	expect(Engine.has_singleton("InputMapSettingsServer"), "InputMapSettingsServer is a registered singleton")
	expect(Engine.get_singleton("InputMapSettingsServer") is ListSettingsServer, "InputMapSettingsServer is a ListSettingsServer")
	expect_equal(SettingsServerManager.get_server(&"input_map"), InputMapSettingsServer, "the domain is registered as input_map")
	expect(InputMapSettingsServer.is_started(), "the domain was started before the main scene")
	expect_equal(InputMapSettingsServer.file_path, INPUT_MAP_FILE, "file path")
	expect(InputMapSettingsServer.get_default_settings() is InputActions, "the settings resource is an InputActions")
	expect_equal(InputMapSettingsServer.SAVE_MODE_CUSTOM_ONLY, 0, "SAVE_MODE_CUSTOM_ONLY is 0")
	expect_equal(InputMapSettingsServer.SAVE_MODE_ALL, 1, "SAVE_MODE_ALL is 1")
	expect_equal(InputAction.INPUT_DEVICE_KEYBOARD, 0, "INPUT_DEVICE_KEYBOARD is 0")
	expect_equal(InputAction.INPUT_DEVICE_MOUSE, 1, "INPUT_DEVICE_MOUSE is 1")
	expect_equal(InputAction.INPUT_DEVICE_GAMEPAD, 2, "INPUT_DEVICE_GAMEPAD is 2")
	expect_equal(InputAction.INPUT_DEVICE_MAX, 3, "INPUT_DEVICE_MAX is 3")
	expect_equal(InputMap.INPUT_ACTION_LAYER_BUILTIN, 0, "INPUT_ACTION_LAYER_BUILTIN is 0")
	expect_equal(InputMap.INPUT_ACTION_LAYER_CUSTOM, 1, "INPUT_ACTION_LAYER_CUSTOM is 1")
	expect_equal(InputActionRebinder.DEVICE_ANY, 0, "DEVICE_ANY is 0")
	expect_equal(InputActionRebinder.DEVICE_KEYBOARD_MOUSE, 1, "DEVICE_KEYBOARD_MOUSE is 1")
	expect_equal(InputActionRebinder.DEVICE_GAMEPAD, 2, "DEVICE_GAMEPAD is 2")
	expect_equal(InputActionRebinder.CONFLICT_ASK, 0, "CONFLICT_ASK is 0")
	expect_equal(InputActionRebinder.CONFLICT_REJECT, 1, "CONFLICT_REJECT is 1")
	expect_equal(InputActionRebinder.CONFLICT_SWAP, 2, "CONFLICT_SWAP is 2")
	expect_equal(InputActionRebinder.CONFLICT_ALLOW, 3, "CONFLICT_ALLOW is 3")


func _test_server_api() -> void:
	var names: PackedStringArray = InputMapSettingsServer.get_action_names()
	expect(names.has("jump") and names.has("fire") and names.has("interact"), "get_action_names lists the project actions")
	expect(InputMapSettingsServer.has_action(JUMP), "has_action for a project action")
	expect(not InputMapSettingsServer.has_action(&"no_such_action"), "has_action for an unknown action")
	var added: InputEvent = pressed_key(KEY_Z)
	expect_equal(InputMapSettingsServer.add_event(JUMP, added), OK, "add_event adds a binding")
	expect_equal(InputMapSettingsServer.get_events(JUMP).size(), 3, "jump has three events")
	expect(InputMap.event_is_action(pressed_key(KEY_Z), JUMP), "the added key works at once")
	expect_equal(InputMapSettingsServer.remove_event(JUMP, added), OK, "remove_event removes the binding")
	expect_equal(InputMapSettingsServer.get_events(JUMP).size(), 2, "jump has two events again")
	expect(not InputMap.event_is_action(pressed_key(KEY_Z), JUMP), "the removed key no longer works")
	expect_equal(InputMapSettingsServer.add_event(&"no_such_action", added), ERR_DOES_NOT_EXIST, "add_event to an unknown action fails")
	var copy: InputAction = InputMapSettingsServer.get_input_action(JUMP)
	expect_equal(copy.action, JUMP, "get_input_action returns the action")
	expect(copy != InputMap.get_input_action(JUMP), "get_input_action is a copy of the pending settings")
	expect(InputMapSettingsServer.get_input_action(&"no_such_action") == null, "get_input_action of an unknown action is null")
	expect_equal(InputMapSettingsServer.get_slot_count(JUMP, InputAction.INPUT_DEVICE_MOUSE), 0, "jump has no mouse slot")
	InputMapSettingsServer.set_action_deadzone(JUMP, 0.8)
	expect(is_equal_approx(InputMapSettingsServer.get_action_deadzone(JUMP), 0.8), "get_action_deadzone")
	InputMapSettingsServer.reset_all()


func _test_records() -> void:
	var event := InputMapSettingsEvent.new()
	expect_equal(event.device, InputMapSettingsEvent.DEVICE_KEYBOARD, "InputMapSettingsEvent: default device")
	expect_equal(event.key_mode, InputMapSettingsEvent.KEY_MODE_PHYSICAL, "InputMapSettingsEvent: default key mode")
	expect_equal(event.device_id, "Any", "InputMapSettingsEvent: default device_id")
	expect_equal(InputMapSettingsEvent.DEVICE_MOUSE, 1, "DEVICE_MOUSE is 1")
	expect_equal(InputMapSettingsEvent.KEY_MODE_LABEL, 1, "KEY_MODE_LABEL is 1")
	expect_equal(InputMapSettingsEvent.KEY_MODE_PRINTED, 2, "KEY_MODE_PRINTED is 2")
	event.input = "S"
	event.modifiers = "Ctrl"
	event.key_mode = InputMapSettingsEvent.KEY_MODE_LABEL
	expect_equal(event.to_text(), "KeyboardLabel:Ctrl+S", "to_text of a label key")
	var restored: InputEvent = event.to_event()
	expect(restored is InputEventKey, "to_event gives a key event")
	expect_equal(restored.keycode, KEY_S, "to_event: the keycode")
	expect(restored.ctrl_pressed, "to_event: the modifier")
	expect(InputMapSettingsEvent.new().to_event() == null, "a record without input is not valid")
	var sample: InputMapSettingsEvent = load("res://input-map-settings/resources/gamepad_axis_event.tres")
	expect_equal(sample.to_text(), "Device1:GamepadAxis:RightStickDown", "the sample InputMapSettingsEvent loads")
	var action := InputMapSettingsAction.new()
	expect_equal(action.layer, InputMapSettingsAction.LAYER_CUSTOM, "InputMapSettingsAction: default layer")
	expect_equal(InputMapSettingsAction.LAYER_BUILT_IN, 0, "LAYER_BUILT_IN is 0")
	expect_equal(InputMapSettingsAction.LAYER_CUSTOM, 1, "LAYER_CUSTOM is 1")
	var sample_action: InputMapSettingsAction = load("res://input-map-settings/resources/settings_action.tres")
	expect_equal(sample_action.action, &"jump", "the sample InputMapSettingsAction loads")
	expect_equal(sample_action.input_events.size(), 2, "the sample action has two event records")
	expect_equal(sample_action.layer, InputMapSettingsAction.LAYER_CUSTOM, "the sample action is on the custom layer")
	var actions := InputMapSettingsActions.new()
	actions.actions.append(sample_action)
	expect_equal(actions.actions.size(), 1, "InputMapSettingsActions holds actions")
	var row: InputMapSettingsEventRow = load("res://input-map-settings/resources/settings_event_row.tres")
	expect_equal(row.input, "B", "the sample InputMapSettingsEventRow loads")
	expect_equal(row.device, InputMapSettingsEvent.DEVICE_GAMEPAD, "the event row has a device")
	expect_equal(row.index, 0, "the event row has an index")
	expect_equal(row.key_mode, InputMapSettingsEvent.KEY_MODE_PHYSICAL, "the event row has a key mode")
	expect_equal(row.modifiers, "", "the event row has modifiers")
	expect_equal(row.device_id, "Any", "the event row has a device_id")
	expect(is_equal_approx(row.deadzone, 0.2), "the event row has a deadzone")
	var rows := InputMapSettingsEventRows.new()
	rows.actions.append(row)
	expect_equal(rows.actions.size(), 1, "InputMapSettingsEventRows holds rows")
	var text_row: InputMapSettingsRow = load("res://input-map-settings/resources/settings_row.tres")
	expect_equal(text_row.bindings, "Keyboard:W; Gamepad:B", "the sample InputMapSettingsRow loads")
	expect(is_equal_approx(text_row.deadzone, 0.2), "the row has a deadzone")
	var text_rows := InputMapSettingsRows.new()
	text_rows.actions.append(text_row)
	expect_equal(text_rows.actions.size(), 1, "InputMapSettingsRows holds rows")
	var player: InputActions = load("res://input-map-settings/resources/player_actions.tres")
	expect(player.get_action_count() > 0, "the sample InputActions loads")
	var jump: InputAction = load("res://input-map-settings/resources/jump_action.tres")
	expect_equal(jump.action, &"jump", "the sample InputAction loads")
	var interact: InputAction = load("res://input-map-settings/resources/interact_action.tres")
	expect_equal(interact.action, &"interact", "the second sample InputAction loads")
	var defaults: InputActions = load("res://input-map-settings/resources/default_bindings.tres")
	expect(defaults.has_action(&"move_left"), "the default bindings sample loads")


static func pressed_key(keycode: Key, pressed := true) -> InputEventKey:
	var event := InputEventKey.new()
	event.keycode = keycode
	event.physical_keycode = keycode
	event.key_label = keycode
	event.pressed = pressed
	return event


static func mouse_button(button: MouseButton, pressed := true) -> InputEventMouseButton:
	var event := InputEventMouseButton.new()
	event.button_index = button
	event.pressed = pressed
	return event


static func joy_button(button: JoyButton, device: int) -> InputEventJoypadButton:
	var event := InputEventJoypadButton.new()
	event.button_index = button
	event.device = device
	event.pressed = true
	return event


static func joy_motion(axis: JoyAxis, value: float, device: int) -> InputEventJoypadMotion:
	var event := InputEventJoypadMotion.new()
	event.axis = axis
	event.axis_value = value
	event.device = device
	return event


static func events_text(action: StringName) -> String:
	return InputMapSettingsServer.events_to_text(InputMapSettingsServer.get_events(action))


static func builtin_actions() -> PackedStringArray:
	var result := PackedStringArray()
	for action in InputMap.get_actions():
		if InputMap.is_builtin_input_action(action):
			result.append(action)
	return result


func _setup_actions() -> void:
	InputMapSettingsServer.reset_all()
	events_log.clear()


func _make_rebinder(filter: InputActionRebinder.DeviceFilter, mode: InputActionRebinder.ConflictMode) -> InputActionRebinder:
	var rebinder := InputActionRebinder.new()
	rebinder.device_filter = filter
	rebinder.conflict_mode = mode
	rebinder.ignored_actions = builtin_actions()
	rebinder.started.connect(func(action: StringName, index: int): events_log.append("started:%s:%d" % [action, index]))
	rebinder.canceled.connect(func(action: StringName, index: int): events_log.append("canceled:%s:%d" % [action, index]))
	rebinder.conflict.connect(func(source: StringName, source_index: int, dest: StringName, dest_index: int): events_log.append("conflict:%s:%d:%s:%d" % [source, source_index, dest, dest_index]))
	rebinder.completed.connect(func(action: StringName, index: int, event: InputEvent): events_log.append("completed:%s:%d:%s" % [action, index, InputAction.event_to_text(event)]))
	return rebinder


func _on_action_events_changed(action: StringName) -> void:
	if action == REB_JUMP or action == REB_FIRE:
		events_log.append("changed:%s" % action)


func _last() -> String:
	if events_log.is_empty():
		return ""
	return events_log[-1]


func _test_text_form() -> void:
	for text in ["KeyboardLabel:Ctrl+S", "Keyboard:W", "KeyboardPrinted:Shift+A", "KeyboardLabel:CmdOrCtrl+Z", "Mouse:Left", "Mouse:Ctrl+WheelUp",
			"Gamepad:A", "Gamepad:LeftShoulder", "GamepadAxis:LeftStickRight", "GamepadAxis:RightTrigger"]:
		var event := InputAction.text_to_event(text)
		expect(event != null, "text_to_event(\"%s\") gives an event" % text)
		if event:
			expect_equal(InputAction.event_to_text(event), text, "round trip of \"%s\"" % text)
	# The text form [Device<N>:]<Type>:[<Modifiers>+]<Input>: devices, special keys, modifiers, all ten axis names.
	for text in ["Device3:Gamepad:A", "Device3:GamepadAxis:LeftStickLeft", "Keyboard:Colon", "Keyboard:Ctrl+Plus", "Keyboard:Ctrl+Shift+S", "Keyboard:CmdOrCtrl+Z", "Mouse:Ctrl+Left", "Keyboard:Shift", "Gamepad:LeftStickPress", "GamepadAxis:LeftStickLeft", "GamepadAxis:LeftStickRight", "GamepadAxis:LeftStickUp", "GamepadAxis:LeftStickDown", "GamepadAxis:RightStickLeft", "GamepadAxis:RightStickRight", "GamepadAxis:RightStickUp", "GamepadAxis:RightStickDown", "GamepadAxis:LeftTrigger", "GamepadAxis:RightTrigger"]:
		var event := InputAction.text_to_event(text)
		expect(event != null, "text_to_event(\"%s\") gives an event" % text)
		if event:
			expect_equal(InputAction.event_to_text(event), text, "round trip of \"%s\"" % text)
	expect_equal(InputAction.get_event_device(InputAction.text_to_event("Device3:Gamepad:A")), InputAction.INPUT_DEVICE_GAMEPAD, "Device3:Gamepad:A is a gamepad event")
	expect_equal(InputAction.text_to_event("Device3:Gamepad:A").device, 3, "Device3:Gamepad:A reacts to pad 3")
	expect_equal(InputAction.event_to_text(InputAction.text_to_event("Keyboard:Shift+Ctrl+S")), "Keyboard:Ctrl+Shift+S", "the modifiers are written in a fixed order")
	expect_equal(InputAction.event_to_text(InputAction.text_to_event("GamepadAxis:LeftX-")), "GamepadAxis:LeftStickLeft", "the old axis name is read and written new")
	expect_equal(InputAction.event_to_text(InputAction.text_to_event("JoypadMotion:TriggerRight+")), "GamepadAxis:RightTrigger", "the old type JoypadMotion is read")
	expect_equal(InputAction.event_to_text(InputAction.text_to_event("JoypadButton:LeftStick@2")), "Device2:Gamepad:LeftStickPress", "the old button and device names are read")
	for text in ["Gamepad:Ctrl+A", "Device:Gamepad:A", "DeviceX:Gamepad:A", "Keyboard:", "Keyboard:Ctrl+", "GamepadAxis:LeftStickX-"]:
		expect(InputAction.text_to_event(text) == null, "\"%s\" is not valid" % text)
	expect(InputAction.text_to_event("KeyboardLabel:Hyper+S") == null, "an unknown modifier is not valid")
	expect(InputAction.text_to_event("Joypad:A") == null, "an unknown type is not valid")
	expect_equal(InputAction.event_to_text(pressed_key(KEY_W)), "Keyboard:W", "a pressed key is stored by its physical keycode")
	expect_equal(InputAction.event_to_text(InputEventScreenTouch.new()), "", "a touch can't be bound")
	expect_equal(InputAction.get_event_device(InputEventScreenTouch.new()), InputAction.INPUT_DEVICE_MAX, "a touch has no device")
	expect_equal(InputAction.get_event_device(InputEventJoypadMotion.new()), InputAction.INPUT_DEVICE_GAMEPAD, "a joypad axis is a gamepad event")
	expect_equal(InputMapSettingsServer.get_event_label(InputAction.text_to_event("KeyboardLabel:Ctrl+S")), "Ctrl+S", "label of Ctrl+S")
	expect_equal(InputMapSettingsServer.get_event_label(InputAction.text_to_event("Mouse:Right")), "Right Mouse Button", "label of a mouse button")
	expect_equal(InputMapSettingsServer.get_event_label(InputAction.text_to_event("GamepadAxis:LeftStickRight")), "Left Stick Right", "label of a stick direction")

	var entry := InputAction.new()
	entry.events = InputMapSettingsServer.text_to_events("Gamepad:B; KeyboardLabel:Space")
	expect_equal(entry.get_event_count(), 2, "the text gives one event per entry")
	expect_equal(entry.get_device_events(InputAction.INPUT_DEVICE_GAMEPAD).size(), 1, "the gamepad event is found by device")
	expect(entry.events[0] is InputEventJoypadButton, "the first event is parsed from the text")
	expect_equal(InputMapSettingsServer.events_to_text(entry.events), "Gamepad:B; KeyboardLabel:Space", "text round trip")


func _test_input_action() -> void:
	var action := InputAction.new()
	expect_equal(action.action, &"", "a new InputAction has no name")
	expect(is_equal_approx(action.deadzone, 0.2), "the default deadzone is 0.2")
	expect_equal(action.events.size(), 0, "a new InputAction has no events")
	action.action = &"demo_demo"
	action.deadzone = 0.4
	expect_equal(action.action, &"demo_demo", "action name")
	expect(is_equal_approx(action.deadzone, 0.4), "deadzone")

	action.events = InputMapSettingsServer.text_to_events("KeyboardLabel:Ctrl+S; Gamepad:A")
	expect_equal(action.get_event_count(), 2, "the text form gives two events")
	expect(action.events[0] is InputEventKey, "the first event is a key")
	expect(action.events[1] is InputEventJoypadButton, "the second event is a joypad button")
	expect_equal(InputMapSettingsServer.events_to_text(action.events), "KeyboardLabel:Ctrl+S; Gamepad:A", "bindings round trip")
	expect_equal(action.get_device_events(InputAction.INPUT_DEVICE_KEYBOARD).size(), 1, "one keyboard event")
	expect_equal(action.get_device_events(InputAction.INPUT_DEVICE_GAMEPAD).size(), 1, "one gamepad event")
	expect_equal(action.get_device_events(InputAction.INPUT_DEVICE_MOUSE).size(), 0, "no mouse event")

	var mouse := InputAction.text_to_event("Mouse:Left")
	action.add_event(mouse)
	expect_equal(action.get_event_count(), 3, "add_event() adds an event")
	expect(action.has_event(mouse), "has_event()")
	action.add_event(InputAction.text_to_event("Mouse:Left"))
	expect_equal(action.get_event_count(), 3, "add_event() ignores an equal event")
	expect(action.remove_event(mouse), "remove_event() finds the event")
	expect(not action.remove_event(mouse), "remove_event() of a missing event is false")
	action.events = [pressed_key(KEY_W)]
	expect_equal(InputMapSettingsServer.events_to_text(action.events), "Keyboard:W", "setting events updates the bindings text")
	action.clear_events()
	expect_equal(action.get_event_count(), 0, "clear_events()")
	expect_equal(InputMapSettingsServer.events_to_text(action.events), "", "no events, no bindings text")

	# Touch has no text form: kept in events, left out of bindings.
	action.add_event(InputEventScreenTouch.new())
	action.add_event(InputAction.text_to_event("KeyboardLabel:Space"))
	expect_equal(action.get_event_count(), 2, "touch and key are both events")
	expect_equal(InputMapSettingsServer.events_to_text(action.events), "KeyboardLabel:Space", "touch is not part of the bindings text")

	for text in ["KeyboardLabel:Ctrl+S", "Keyboard:W", "Mouse:Ctrl+WheelUp", "Gamepad:LeftShoulder", "GamepadAxis:LeftStickRight"]:
		var event := InputAction.text_to_event(text)
		expect(event != null, "text_to_event(\"%s\") gives an event" % text)
		if event:
			expect_equal(InputAction.event_to_text(event), text, "round trip of \"%s\"" % text)
	expect(InputAction.text_to_event("KeyboardLabel:Hyper+S") == null, "an unknown modifier is not valid")
	expect_equal(InputAction.event_to_text(InputEventScreenTouch.new()), "", "a touch has no text")
	expect_equal(InputAction.get_event_device(pressed_key(KEY_A)), InputAction.INPUT_DEVICE_KEYBOARD, "key device")
	expect_equal(InputAction.get_event_device(InputAction.text_to_event("Mouse:Left")), InputAction.INPUT_DEVICE_MOUSE, "mouse device")
	expect_equal(InputAction.get_event_device(InputAction.text_to_event("GamepadAxis:LeftStickRight")), InputAction.INPUT_DEVICE_GAMEPAD, "joypad axis device")
	expect_equal(InputAction.get_event_device(InputEventScreenTouch.new()), InputAction.INPUT_DEVICE_MAX, "touch has no device")


func _test_input_actions() -> void:
	var list := InputActions.new()
	expect_equal(list.get_action_count(), 0, "a new InputActions is empty")
	var jump := InputAction.new()
	jump.action = &"demo_jump"
	jump.events = InputMapSettingsServer.text_to_events("KeyboardLabel:Space")
	var fire := InputAction.new()
	fire.action = &"demo_fire"
	fire.events = InputMapSettingsServer.text_to_events("Mouse:Left")
	list.add_action(jump)
	list.add_action(fire)
	expect_equal(list.get_action_count(), 2, "two actions")
	expect(list.has_action(&"demo_jump") and list.has_action(&"demo_fire"), "has_action()")
	expect(not list.has_action(&"demo_none"), "has_action() of a missing name is false")
	expect_equal(list.get_action(&"demo_jump"), jump, "get_action() returns the same resource")
	expect(list.get_action(&"demo_none") == null, "get_action() of a missing name is null")
	expect_equal(list.get_action_names(), [&"demo_jump", &"demo_fire"], "names in insertion order")

	var jump2 := InputAction.new()
	jump2.action = &"demo_jump"
	jump2.events = InputMapSettingsServer.text_to_events("KeyboardLabel:W")
	list.add_action(jump2)
	expect_equal(list.get_action_count(), 2, "adding a name again replaces the action")
	expect_equal(list.get_action(&"demo_jump"), jump2, "the new action replaced the old one")
	expect_equal(list.get_action_names(), [&"demo_jump", &"demo_fire"], "the replaced action keeps its place")

	# A rename of an action in the list is found by the new name.
	fire.action = &"demo_shoot"
	expect(list.has_action(&"demo_shoot") and not list.has_action(&"demo_fire"), "renaming an action updates the lookup")

	expect(list.remove_action(&"demo_shoot"), "remove_action()")
	expect(not list.remove_action(&"demo_shoot"), "remove_action() of a missing name is false")
	expect_equal(list.get_action_count(), 1, "one action left")
	list.clear()
	expect_equal(list.get_action_count(), 0, "clear()")
	list.actions = [jump, jump2]
	expect_equal(list.get_action_count(), 2, "set_actions(): both entries of a name are kept (#398)")
	expect_equal(list.get_action(&"demo_jump"), jump2, "set_actions(): the later action of a name wins")


func _test_resource_files() -> void:
	# `events` is a stored property: .tres and .res keep the events as sub-resources.
	var stored := false
	for property in InputAction.new().get_property_list():
		if property.name == "events":
			stored = (property.usage & PROPERTY_USAGE_STORAGE) != 0 and (property.usage & PROPERTY_USAGE_EDITOR) != 0
		expect(property.name != "bindings", "InputAction has no bindings property")
	expect(stored, "events is stored and shown in the Inspector")

	var list := InputActions.new()
	var jump := InputAction.new()
	jump.action = &"demo_file_jump"
	jump.deadzone = 0.5
	jump.events = InputMapSettingsServer.text_to_events("KeyboardLabel:Space; Keyboard:Ctrl+W; Mouse:Right; Device1:Gamepad:A; GamepadAxis:LeftStickUp")
	var touch := InputEventScreenTouch.new()
	touch.index = 2
	jump.add_event(touch)
	list.add_action(jump)
	var empty := InputAction.new()
	empty.action = &"demo_file_empty"
	list.add_action(empty)
	for path in ["user://demo_actions.tres", "user://demo_actions.res"]:
		expect_equal(ResourceSaver.save(list, path), OK, "save %s" % path)
		var loaded := ResourceLoader.load(path, "", ResourceLoader.CACHE_MODE_IGNORE) as InputActions
		expect(loaded != null and loaded != list, "load %s" % path)
		if loaded == null:
			continue
		expect_equal(loaded.get_action_names(), [&"demo_file_jump", &"demo_file_empty"], "%s: action names" % path)
		var read := loaded.get_action(&"demo_file_jump")
		expect(is_equal_approx(read.deadzone, 0.5), "%s: deadzone" % path)
		expect_equal(read.get_event_count(), 6, "%s: all events, touch too" % path)
		expect_equal(InputMapSettingsServer.events_to_text(read.events), InputMapSettingsServer.events_to_text(jump.events), "%s: the same events" % path)
		var read_touch := read.events[5] as InputEventScreenTouch
		expect(read_touch != null and read_touch.index == 2, "%s: the touch event" % path)
		expect_equal(loaded.get_action(&"demo_file_empty").get_event_count(), 0, "%s: an action without events" % path)
	var text := FileAccess.get_file_as_string("user://demo_actions.tres")
	expect(text.contains("events = Array[InputEvent]([SubResource("), ".tres: events as sub-resources")
	expect(text.contains("deadzone = 0.5"), ".tres: the deadzone that is not the default")
	expect(not text.contains("bindings"), ".tres: no text form")

	# The text form of InputMapSettingsServer: one pad with a Device<N>: prefix, events without text left out.
	expect_equal(InputMapSettingsServer.events_to_text(jump.events), "KeyboardLabel:Space; Keyboard:Ctrl+W; Mouse:Right; Device1:Gamepad:A; GamepadAxis:LeftStickUp", "events_to_text()")
	expect_equal(InputMapSettingsServer.text_to_events("KeyboardLabel:Space; NotAType:X").size(), 1, "text_to_events() skips invalid items")
	expect_equal(InputMapSettingsServer.text_to_events("Device1:Gamepad:A")[0].device, 1, "text_to_events(): the pad")


func _test_layers() -> void:
	expect(InputMap.is_builtin_input_action(&"ui_accept"), "ui_accept is built-in")
	expect(not InputMap.is_custom_input_action(&"ui_accept"), "ui_accept is not custom")
	expect(not InputMap.is_custom_input_action(&"ui_text_backspace_word.macos"), "a platform variant of a built-in action is never custom")
	expect(InputMap.is_custom_input_action(ATTACK), "a project action is custom")
	expect(not InputMap.is_builtin_input_action(ATTACK), "a project action is not built-in")
	expect(not InputMap.is_custom_input_action(&"demo_not_there"), "a missing action is not custom")
	expect_equal(InputMap.get_input_action_layer(&"ui_accept"), InputMap.INPUT_ACTION_LAYER_BUILTIN, "layer of ui_accept")
	expect_equal(InputMap.get_input_action_layer(ATTACK), InputMap.INPUT_ACTION_LAYER_CUSTOM, "layer of the project action")

	var builtin := InputMap.get_input_actions(InputMap.INPUT_ACTION_LAYER_BUILTIN)
	var custom := InputMap.get_input_actions(InputMap.INPUT_ACTION_LAYER_CUSTOM)
	expect(builtin.has_action(&"ui_accept"), "the built-in layer has ui_accept")
	expect(not builtin.has_action(ATTACK), "the built-in layer has no custom action")
	expect(custom.has_action(ATTACK), "the custom layer has the project action")
	expect(not custom.has_action(&"ui_accept"), "the custom layer has no built-in action")
	expect_equal(builtin.get_action_count() + custom.get_action_count(), InputMap.get_actions().size(), "the layers together are all actions")
	expect(InputMap.get_input_actions(InputMap.INPUT_ACTION_LAYER_BUILTIN) == builtin, "the built-in layer is the same object every time")
	expect(InputMap.get_input_actions(InputMap.INPUT_ACTION_LAYER_CUSTOM) == custom, "the custom layer is the same object every time")
	for builtin_name in builtin.get_action_names():
		if not InputMap.is_builtin_input_action(builtin_name):
			expect(false, "%s is in the built-in layer but not built-in" % builtin_name)
			break

	var attack := InputMap.get_input_action(ATTACK)
	expect_equal(attack.action, ATTACK, "get_input_action() name")
	expect(is_equal_approx(attack.deadzone, 0.3), "get_input_action() deadzone from the project")
	expect_equal(InputMapSettingsServer.events_to_text(attack.events), "KeyboardLabel:F", "get_input_action() events as text")
	expect(attack == custom.get_action(ATTACK), "get_input_action() is the action of the custom layer")
	var key_h := InputAction.text_to_event("KeyboardLabel:H")
	attack.add_event(key_h)
	expect(InputMap.event_is_action(pressed_key(KEY_H), ATTACK), "an event added to the returned action works")
	attack.remove_event(key_h)
	expect(not InputMap.event_is_action(pressed_key(KEY_H), ATTACK), "an event removed from the returned action no longer works")
	expect_equal(InputMap.get_input_action(ATTACK).get_event_count(), 1, "one event again")

	# Direct edits of a layer reach the InputMap.
	var direct := InputAction.new()
	direct.action = &"demo_direct"
	direct.events = InputMapSettingsServer.text_to_events("KeyboardLabel:F9")
	custom.add_action(direct)
	expect(InputMap.has_action(&"demo_direct"), "an action added to the custom layer is in the InputMap")
	expect(InputMap.event_is_action(pressed_key(KEY_F9), &"demo_direct"), "its key works")
	expect(custom.remove_action(&"demo_direct"), "remove it from the layer")
	expect(not InputMap.has_action(&"demo_direct"), "an action removed from the layer is gone from the InputMap")


func _test_set_input_action() -> void:
	var original := InputMap.get_input_action(ATTACK)
	var changed := InputAction.new()
	changed.action = ATTACK
	changed.deadzone = 0.6
	changed.events = InputMapSettingsServer.text_to_events("KeyboardLabel:G")
	InputMap.set_input_action(changed)
	expect(InputMap.event_is_action(pressed_key(KEY_G), ATTACK), "set_input_action(): the new key works")
	expect(not InputMap.event_is_action(pressed_key(KEY_F), ATTACK), "set_input_action(): the old key no longer works")
	expect(is_equal_approx(InputMap.action_get_deadzone(ATTACK), 0.6), "set_input_action(): the deadzone is replaced")
	InputMap.set_input_action(original)
	expect(InputMap.event_is_action(pressed_key(KEY_F), ATTACK), "restoring the action brings back the old key")
	expect(not InputMap.event_is_action(pressed_key(KEY_G), ATTACK), "restoring removes the new key")

	# A new name creates a custom action; set_input_actions() takes a list.
	var extra := InputActions.new()
	for n in 2:
		var action := InputAction.new()
		action.action = StringName("demo_extra_%d" % n)
		action.events = InputMapSettingsServer.text_to_events("KeyboardLabel:F%d" % (n + 5))
		extra.add_action(action)
	InputMap.set_input_actions(extra)
	expect(InputMap.has_action(&"demo_extra_0") and InputMap.has_action(&"demo_extra_1"), "set_input_actions() adds new actions")
	expect(InputMap.is_custom_input_action(&"demo_extra_1"), "a new action is custom")
	expect(InputMap.event_is_action(pressed_key(KEY_F6), &"demo_extra_1"), "the new action reacts to its key")
	InputMap.erase_action(&"demo_extra_0")
	InputMap.erase_action(&"demo_extra_1")


func _test_parse_cost() -> void:
	for i in BULK_COUNT:
		var bulk := StringName("demo_bulk_%d" % i)
		InputMap.add_action(bulk)
		InputMap.action_add_event(bulk, pressed_key((BULK_FIRST_KEY + i) as Key))
	expect(InputMap.has_action(&"demo_bulk_99"), "100 actions were added")

	for i in [0, 37, 99]:
		var bulk := StringName("demo_bulk_%d" % i)
		Input.parse_input_event(pressed_key((BULK_FIRST_KEY + i) as Key))
		Input.flush_buffered_events()
		expect(Input.is_action_pressed(bulk), "%s is pressed after parse_input_event()" % bulk)
		var other := StringName("demo_bulk_%d" % ((i + 1) % BULK_COUNT))
		expect(not Input.is_action_pressed(other), "%s is not pressed by the key of %s" % [other, bulk])
		Input.parse_input_event(pressed_key((BULK_FIRST_KEY + i) as Key, false))
		Input.flush_buffered_events()
		expect(not Input.is_action_pressed(bulk), "%s is released again" % bulk)

	var event := pressed_key((BULK_FIRST_KEY + 50) as Key)
	var start := Time.get_ticks_usec()
	for i in 1000:
		Input.parse_input_event(event)
	var elapsed := Time.get_ticks_usec() - start
	Input.parse_input_event(pressed_key((BULK_FIRST_KEY + 50) as Key, false))
	Input.flush_buffered_events()
	print("  INFO  1000 x parse_input_event() with 100 extra actions: %.2f ms" % (elapsed / 1000.0))
	expect(not Input.is_action_pressed(&"demo_bulk_50"), "the bulk action is released at the end")

	for i in BULK_COUNT:
		InputMap.erase_action(StringName("demo_bulk_%d" % i))
	expect(not InputMap.has_action(&"demo_bulk_0"), "the bulk actions were removed")
	expect(not InputMap.event_is_action(pressed_key(BULK_FIRST_KEY as Key), &"demo_bulk_0"), "a removed action no longer matches")


func _test_settings_server() -> void:
	expect_equal(InputMapSettingsServer.save_mode, InputMapSettingsServer.SAVE_MODE_CUSTOM_ONLY, "save_mode is CUSTOM_ONLY by default")
	expect(InputMapSettingsServer.get_default_settings() is InputActions, "the settings resource is an InputActions")
	expect(InputMapSettingsServer.has_action(ATTACK), "the project action is managed")
	expect(not InputMapSettingsServer.has_action(&"ui_accept"), "built-in actions are not managed in CUSTOM_ONLY")
	expect_equal(InputMapSettingsServer.rebind(&"ui_accept", pressed_key(KEY_Q)), ERR_DOES_NOT_EXIST, "a built-in action can't be rebound in CUSTOM_ONLY")

	InputMapSettingsServer.reset_all()
	expect_equal(InputAction.event_to_text(InputMapSettingsServer.get_event(ATTACK, InputAction.INPUT_DEVICE_KEYBOARD, 0)), "KeyboardLabel:F", "slot 0 is F")
	expect_equal(InputMapSettingsServer.rebind(ATTACK, pressed_key(KEY_X)), OK, "rebind() slot 0")
	expect_equal(InputAction.event_to_text(InputMapSettingsServer.get_event(ATTACK, InputAction.INPUT_DEVICE_KEYBOARD, 0)), "Keyboard:X", "get_event() gives the new key")
	expect(InputMap.event_is_action(pressed_key(KEY_X), ATTACK), "the new key works at once")
	expect(not InputMap.event_is_action(pressed_key(KEY_F), ATTACK), "the old key no longer works")
	expect_equal(InputMapSettingsServer.rebind(ATTACK, pressed_key(KEY_Y), 1), OK, "rebind() slot 1")
	expect_equal(InputMapSettingsServer.get_slot_count(ATTACK, InputAction.INPUT_DEVICE_KEYBOARD), 2, "two keyboard slots")
	expect(InputMapSettingsServer.events_to_text(InputMapSettingsServer.get_input_action(ATTACK).events).contains("Keyboard:Y"), "get_input_action() shows both keys")

	InputMapSettingsServer.clear_event(ATTACK, InputAction.INPUT_DEVICE_KEYBOARD, 0)
	expect_equal(InputMapSettingsServer.get_slot_count(ATTACK, InputAction.INPUT_DEVICE_KEYBOARD), 1, "clear_event() removes the slot")
	expect_equal(InputAction.event_to_text(InputMapSettingsServer.get_event(ATTACK, InputAction.INPUT_DEVICE_KEYBOARD, 0)), "Keyboard:Y", "later slots move up")
	expect(not InputMap.event_is_action(pressed_key(KEY_X), ATTACK), "the cleared key no longer works")
	expect(InputMap.event_is_action(pressed_key(KEY_Y), ATTACK), "the moved key works")

	InputMapSettingsServer.reset_action(ATTACK)
	expect(InputMap.event_is_action(pressed_key(KEY_F), ATTACK), "reset_action(): F works again")
	expect(not InputMap.event_is_action(pressed_key(KEY_Y), ATTACK), "reset_action(): Y no longer works")
	expect_equal(InputMapSettingsServer.events_to_text(InputMapSettingsServer.get_input_action(ATTACK).events), "KeyboardLabel:F", "reset_action(): the project bindings")

	InputMapSettingsServer.rebind(ATTACK, pressed_key(KEY_X))
	InputMapSettingsServer.reset_all()
	expect(InputMap.event_is_action(pressed_key(KEY_F), ATTACK), "reset_all(): F works again")
	expect(not InputMap.event_is_action(pressed_key(KEY_X), ATTACK), "reset_all(): X no longer works")

	# SAVE_MODE_ALL manages the built-in actions too.
	# A copy: the server changes the action of the InputMap itself.
	var saved_accept := InputMap.get_input_action(&"ui_accept").duplicate() as InputAction
	InputMapSettingsServer.save_mode = InputMapSettingsServer.SAVE_MODE_ALL
	expect_equal(InputMapSettingsServer.rebind(&"ui_accept", pressed_key(KEY_Q)), OK, "SAVE_MODE_ALL: a built-in action can be rebound")
	expect(InputMapSettingsServer.has_action(&"ui_accept"), "SAVE_MODE_ALL: ui_accept is managed now")
	InputMapSettingsServer.save_mode = InputMapSettingsServer.SAVE_MODE_CUSTOM_ONLY
	InputMapSettingsServer.reset_all()
	InputMap.set_input_action(saved_accept)
	expect(not InputMap.event_is_action(pressed_key(KEY_Q), &"ui_accept"), "ui_accept is restored")


func _test_defaults() -> void:
	var actions := InputMapSettingsServer.get_action_names()
	expect(actions.has("jump") and actions.has("fire"), "the project actions are managed")
	expect(not actions.has("ui_accept"), "built-in ui_* actions are left out by default")
	expect_equal(InputMapSettingsServer.save_mode, InputMapSettingsServer.SAVE_MODE_CUSTOM_ONLY, "save_mode is CUSTOM_ONLY by default")
	expect_equal(InputAction.event_to_text(InputMapSettingsServer.get_event(JUMP, InputAction.INPUT_DEVICE_KEYBOARD, 0)), "KeyboardLabel:Space", "jump: keyboard slot 0 is Space")
	expect_equal(InputAction.event_to_text(InputMapSettingsServer.get_event(JUMP, InputAction.INPUT_DEVICE_GAMEPAD, 0)), "Gamepad:A", "jump: gamepad slot 0 is A")
	expect_equal(InputAction.event_to_text(InputMapSettingsServer.get_event(FIRE, InputAction.INPUT_DEVICE_GAMEPAD, 0)), "GamepadAxis:RightTrigger", "fire: gamepad slot 0 is the right trigger")
	expect(is_equal_approx(InputMapSettingsServer.get_action_deadzone(JUMP), 0.5), "jump: deadzone 0.5 from the project")
	expect_equal(InputMapSettingsServer.get_events(JUMP).size(), 2, "jump has two events")


func _test_rebind() -> void:
	expect_equal(InputMapSettingsServer.rebind(JUMP, pressed_key(KEY_W)), OK, "rebind() of keyboard slot 0")
	expect_equal(InputMapSettingsServer.rebind(JUMP, pressed_key(KEY_UP), 1), OK, "rebind() of keyboard slot 1")
	expect(InputMap.event_is_action(pressed_key(KEY_W), JUMP), "the new key works at once")
	expect(InputMap.event_is_action(pressed_key(KEY_UP), JUMP), "the second slot works")
	expect(not InputMap.event_is_action(pressed_key(KEY_SPACE), JUMP), "the old key no longer works")
	expect_equal(InputMapSettingsServer.get_slot_count(JUMP, InputAction.INPUT_DEVICE_KEYBOARD), 2, "two keyboard slots")

	InputMapSettingsServer.clear_event(JUMP, InputAction.INPUT_DEVICE_KEYBOARD, 1)
	expect(not InputMap.event_is_action(pressed_key(KEY_UP), JUMP), "a cleared slot no longer works")
	expect(InputMapSettingsServer.get_event(JUMP, InputAction.INPUT_DEVICE_KEYBOARD, 1) == null, "a cleared slot has no event")

	# Clearing a slot removes the event: the later slots of the device move up.
	InputMapSettingsServer.rebind(JUMP, pressed_key(KEY_UP), 1)
	InputMapSettingsServer.clear_event(JUMP, InputAction.INPUT_DEVICE_KEYBOARD, 0)
	expect_equal(InputMapSettingsServer.get_slot_count(JUMP, InputAction.INPUT_DEVICE_KEYBOARD), 1, "clearing slot 0 leaves one keyboard slot")
	expect_equal(InputAction.event_to_text(InputMapSettingsServer.get_event(JUMP, InputAction.INPUT_DEVICE_KEYBOARD, 0)), "Keyboard:Up", "slot 1 moved up to slot 0")
	expect(InputMap.event_is_action(pressed_key(KEY_UP), JUMP), "the moved key still works")
	expect(not InputMap.event_is_action(pressed_key(KEY_W), JUMP), "the cleared key no longer works")

	InputMapSettingsServer.set_action_deadzone(JUMP, 0.7)
	expect(is_equal_approx(InputMap.action_get_deadzone(JUMP), 0.7), "the deadzone reaches InputMap")
	expect_equal(InputMapSettingsServer.rebind(JUMP, InputEventScreenTouch.new()), ERR_INVALID_PARAMETER, "a touch can't be bound")
	expect_equal(InputMapSettingsServer.rebind(&"no_such_action", pressed_key(KEY_Q)), ERR_DOES_NOT_EXIST, "an unknown action can't be bound")


func _test_conflicts() -> void:
	InputMapSettingsServer.reset_all()
	expect_equal(InputMapSettingsServer.find_conflicts(pressed_key(KEY_SPACE)), PackedStringArray(["jump"]), "Space is used by jump")
	expect(InputMapSettingsServer.find_conflicts(pressed_key(KEY_SPACE), JUMP).is_empty(), "the ignored action is not a conflict")
	expect(InputMapSettingsServer.find_conflicts(pressed_key(KEY_K)).is_empty(), "K is free")
	InputMapSettingsServer.rebind(FIRE, pressed_key(KEY_SPACE))
	expect_equal(InputMapSettingsServer.find_conflicts(pressed_key(KEY_SPACE)).size(), 2, "after rebinding fire, Space is used twice")


func _test_reset() -> void:
	InputMapSettingsServer.rebind(JUMP, pressed_key(KEY_W))
	InputMapSettingsServer.reset_action(JUMP)
	expect(InputMap.event_is_action(pressed_key(KEY_SPACE), JUMP), "reset_action(): Space works again")
	expect(not InputMap.event_is_action(pressed_key(KEY_W), JUMP), "reset_action(): W no longer works")
	expect(InputMap.event_is_action(pressed_key(KEY_SPACE), FIRE), "reset_action() keeps the other actions")
	InputMapSettingsServer.reset_all()
	expect(not InputMap.event_is_action(pressed_key(KEY_SPACE), FIRE), "reset_all(): fire is back at the project bindings")
	var click := InputEventMouseButton.new()
	click.button_index = MOUSE_BUTTON_LEFT
	expect(InputMap.event_is_action(click, FIRE), "reset_all(): the left mouse button fires again")


func _test_formats() -> void:
	for extension in ["ini", "json", "csv"]:
		var path := "user://input-map-settings-test-gd/map_bindings.%s" % extension
		InputMapSettingsServer.set_file_path(path)
		InputMapSettingsServer.rebind(JUMP, pressed_key(KEY_W))
		InputMapSettingsServer.rebind(JUMP, InputAction.text_to_event("GamepadAxis:LeftStickUp"))
		InputMapSettingsServer.rebind(FIRE, InputAction.text_to_event("Mouse:Ctrl+Right"))
		expect_equal(InputMapSettingsServer.save(), OK, "%s: save()" % extension)
		var text := FileAccess.get_file_as_string(path)
		expect(text.contains("Keyboard") and text.contains("LeftStickUp") and text.contains("Mouse") and text.contains("Ctrl"), "%s: the file holds the event fields" % extension)
		expect(not text.contains("Keyboard:W") and not text.contains("InputEvent"), "%s: no event texts, no sub-resources" % extension)
		InputMapSettingsServer.reset_all()
		expect_equal(InputMapSettingsServer.load(), OK, "%s: load()" % extension)
		InputMapSettingsServer.apply()
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
		InputMapSettingsServer.reset_all()
		DirAccess.remove_absolute(path)
	InputMapSettingsServer.set_file_path("")


func _test_file_layout() -> void:
	# The text form: physical (Keyboard) or label (KeyboardLabel), one pad as a Device<N> prefix, the axis name, the modifiers.
	var text := "Keyboard:Ctrl+S; KeyboardLabel:E; Mouse:Left; Gamepad:A; GamepadAxis:LeftStickLeft; Device2:Gamepad:B"
	var events := InputMapSettingsServer.text_to_events(text)
	expect_equal(events.size(), 6, "text_to_events(): six events")
	expect_equal(InputMapSettingsServer.events_to_text(events), text, "events_to_text() writes the same names")
	expect_equal(events[0].physical_keycode, KEY_S, "Keyboard: the physical keycode")
	expect_equal(events[0].keycode, KEY_NONE, "Keyboard: no label keycode")
	expect(events[0].ctrl_pressed, "Keyboard:Ctrl+S has the modifier")
	expect_equal(events[1].keycode, KEY_E, "KeyboardLabel: the keycode")
	expect_equal(events[1].physical_keycode, KEY_NONE, "KeyboardLabel: no physical keycode")
	expect_equal(events[3].device, -1, "Gamepad:A reacts to all pads")
	expect_equal(events[5].device, 2, "Device2:Gamepad:B reacts to pad 2")
	expect(events[4] is InputEventJoypadMotion and events[4].axis == JOY_AXIS_LEFT_X and events[4].axis_value < 0.0, "GamepadAxis:LeftStickLeft is the left stick to the left")
	# The names of version 2 files are still read.
	expect_equal(InputMapSettingsServer.events_to_text(InputMapSettingsServer.text_to_events("Key:Ctrl+S; PhysicalKey:W; MouseButton:Left; JoypadButton:A@1; JoypadMotion:LeftY-")),
			"KeyboardLabel:Ctrl+S; Keyboard:W; Mouse:Left; Device1:Gamepad:A; GamepadAxis:LeftStickUp", "the names of version 2 are read")

	var record := InputMapSettingsEvent.from_text("Device1:GamepadAxis:RightStickDown")
	expect(record != null, "from_text() gives a record")
	if record != null:
		expect_equal(record.device, InputMapSettingsEvent.DEVICE_GAMEPAD, "record: device Gamepad")
		expect_equal(record.input, "RightStickDown", "record: input")
		expect_equal(record.device_id, "1", "record: device_id is the pad number")
		expect_equal(record.to_text(), "Device1:GamepadAxis:RightStickDown", "record: text")
	var key_record := InputMapSettingsEvent.from_event(pressed_key(KEY_W))
	expect_equal(key_record.key_mode, InputMapSettingsEvent.KEY_MODE_PHYSICAL, "a pressed key is a Physical key record")
	expect_equal(key_record.device_id, "Any", "a key has DeviceId Any")
	expect(InputMapSettingsEvent.from_text("Keyboard:NoSuchKey") == null, "an unknown key is not valid")

	# The file layout: one record per event in the formats with nesting, one row per event in CSV.
	InputMapSettingsServer.reset_all()
	InputMapSettingsServer.rebind(JUMP, pressed_key(KEY_W))
	InputMapSettingsServer.rebind(JUMP, InputAction.text_to_event("GamepadAxis:LeftStickLeft"))
	InputMapSettingsServer.rebind(FIRE, InputAction.text_to_event("Mouse:Ctrl+Right"))
	for extension in ["json", "ini", "csv", "xml", "toml", "yaml"]:
		if not ResourceTextSerializer.is_format_available(ResourceTextSerializer.get_format_from_extension(extension)):
			continue
		var path := "user://input-map-settings-test-gd/map_layout.%s" % extension
		InputMapSettingsServer.set_file_path(path)
		expect_equal(InputMapSettingsServer.save(), OK, "%s: save()" % extension)
		var file_text := FileAccess.get_file_as_string(path)
		if extension == "csv":
			expect(file_text.begins_with("action,index,device,input,modifiers,key_mode,device_id,deadzone"), "csv: the header names the fields")
			expect(file_text.contains("jump,0,Keyboard,W,,Physical,Any,"), "csv: the physical key W is one row")
			expect(file_text.contains("jump,1,Gamepad,LeftStickLeft,,"), "csv: the axis direction is in the name")
			expect(file_text.contains("fire,0,Mouse,Right,Ctrl,Physical,Any,"), "csv: the mouse button has the modifier Ctrl")
		else:
			expect(file_text.contains("input_events") and file_text.contains("key_mode") and file_text.contains("device_id"), "%s: the events are records with fields" % extension)
		InputMapSettingsServer.reset_all()
		expect_equal(InputMapSettingsServer.load(), OK, "%s: load()" % extension)
		InputMapSettingsServer.apply()
		expect(InputMap.event_is_action(pressed_key(KEY_W), JUMP), "%s: Keyboard:W survives" % extension)
		expect_equal(InputMapSettingsServer.events_to_text(InputMapSettingsServer.get_events(FIRE)), "Mouse:Ctrl+Right; GamepadAxis:RightTrigger", "%s: the mouse event replaces the mouse slot, the trigger stays" % extension)
		DirAccess.remove_absolute(path)

	# A file of version 2: one text cell per action.
	var v2_path := "user://input-map-settings-test-gd/map_v2.json"
	var v2 := FileAccess.open(v2_path, FileAccess.WRITE)
	v2.store_string('{"$type": "InputMapSettingsRows", "metadata/settings_version": 2, "actions": [{"action": "jump", "deadzone": 0.4, "bindings": "Key:K; JoypadButton:B@2"}]}')
	v2.close()
	InputMapSettingsServer.reset_all()
	InputMapSettingsServer.set_file_path(v2_path)
	expect_equal(InputMapSettingsServer.load(), OK, "a version 2 file loads")
	InputMapSettingsServer.apply()
	expect_equal(InputMapSettingsServer.events_to_text(InputMapSettingsServer.get_events(JUMP)), "KeyboardLabel:K; Device2:Gamepad:B", "version 2: the bindings are read")
	expect(is_equal_approx(InputMapSettingsServer.get_action_deadzone(JUMP), 0.4), "version 2: the deadzone is read")
	DirAccess.remove_absolute(v2_path)
	InputMapSettingsServer.reset_all()
	InputMapSettingsServer.set_file_path("")


func _test_restart() -> void:
	if not OS.has_feature("pc"):
		print("  SKIP  restart check (needs a desktop platform to start a second process)")
		return
	InputMapSettingsServer.rebind(JUMP, pressed_key(KEY_W))
	# A row of an action that a later game version no longer has.
	var gone := InputAction.new()
	gone.action = "removed"
	gone.events = InputMapSettingsServer.text_to_events("KeyboardLabel:G")
	InputMapSettingsServer.add_entry(gone)
	expect_equal(InputMapSettingsServer.save(), OK, "save() writes the player file")
	var text := FileAccess.get_file_as_string(INPUT_MAP_FILE)
	expect(text.contains("Keyboard") and text.contains("input_events"), "the INI file has the event fields (device Keyboard)")
	expect(text.contains("removed"), "the INI file contains the removed action")

	var probe := _run_probe()
	expect(probe.get("started", false), "restart: the domain was started")
	expect(probe.get("w_jumps", false), "restart: the new key works")
	expect(not probe.get("space_jumps", true), "restart: the old key does not")
	expect(not probe.get("has_removed", true), "restart: the removed action is skipped")
	expect(probe.get("pad_a_jumps", false), "restart: the other slots keep their bindings")
	InputMapSettingsServer.reset_all()


func _run_probe() -> Dictionary:
	var output := []
	var args := PackedStringArray(["--headless", "--path", ProjectSettings.globalize_path("res://"), PROBE_SCENE])
	var code := OS.execute(OS.get_executable_path(), args, output, true)
	expect_equal(code, 0, "restart: the probe process ran")
	for chunk in output:
		for line in str(chunk).split("\n"):
			if line.begins_with("PROBE "):
				var parsed = JSON.parse_string(line.trim_prefix("PROBE "))
				if parsed is Dictionary:
					return parsed
	expect(false, "restart: the probe printed its result")
	return {}


func _test_class() -> void:
	expect(ClassDB.class_exists("InputActionRebinder"), "InputActionRebinder is registered")
	expect(ClassDB.is_parent_class("InputActionRebinder", "RefCounted"), "InputActionRebinder is a RefCounted")
	expect(InputMapSettingsServer.has_signal("action_events_changed"), "the server has action_events_changed")
	var rebinder := InputActionRebinder.new()
	expect_equal(rebinder.device_filter, InputActionRebinder.DEVICE_ANY, "device_filter is DEVICE_ANY by default")
	expect_equal(rebinder.conflict_mode, InputActionRebinder.CONFLICT_ASK, "conflict_mode is CONFLICT_ASK by default")
	expect(is_equal_approx(rebinder.axis_threshold, 0.5), "axis_threshold is 0.5 by default")
	expect_equal(rebinder.gamepad_device, -1, "gamepad_device is -1 by default")
	expect_equal(rebinder.ignored_actions.size(), 0, "no ignored actions by default")
	expect(not rebinder.is_active(), "a new rebinder is not active")
	expect_equal(rebinder.get_event_index(), -1, "no event index without a session")
	expect(not rebinder.update(pressed_key(KEY_G)), "update() without a session uses nothing")
	for signal_name in ["started", "canceled", "conflict", "completed"]:
		expect(rebinder.has_signal(signal_name), "signal %s" % signal_name)


func _test_filtering() -> void:
	var rebinder := _make_rebinder(InputActionRebinder.DEVICE_KEYBOARD_MOUSE, InputActionRebinder.CONFLICT_REJECT)
	expect_equal(rebinder.begin(REB_JUMP, 0), OK, "begin()")
	expect_equal(_last(), "started:test_jump:0", "started is emitted")
	expect(rebinder.is_active(), "the session is active")

	var motion := InputEventMouseMotion.new()
	motion.relative = Vector2(12, 0)
	expect(not rebinder.update(motion), "mouse motion is ignored")
	expect(not rebinder.update(pressed_key(KEY_H, false)), "a released key is ignored")
	var echo := pressed_key(KEY_H)
	echo.echo = true
	expect(not rebinder.update(echo), "an echo is ignored")
	expect(not rebinder.update(joy_button(JOY_BUTTON_PADDLE2, 0)), "a joypad button is outside DEVICE_KEYBOARD_MOUSE")
	var touch := InputEventScreenTouch.new()
	touch.pressed = true
	expect(not rebinder.update(touch), "a touch is ignored")

	var ctrl := pressed_key(KEY_CTRL)
	ctrl.ctrl_pressed = true
	expect(rebinder.update(ctrl), "a modifier alone is used")
	expect(rebinder.is_active(), "a modifier alone is not bound yet")
	var ctrl_h := pressed_key(KEY_H)
	ctrl_h.ctrl_pressed = true
	expect(rebinder.update(ctrl_h), "Ctrl+H is used")
	expect(not rebinder.is_active(), "the session ends with the binding")
	expect_equal(_last(), "completed:test_jump:0:Keyboard:Ctrl+H", "completed with Ctrl+H")
	expect_equal(events_text(REB_JUMP), "Keyboard:Ctrl+H; Gamepad:Paddle1", "slot 0 has Ctrl+H")
	expect(InputMap.event_is_action(ctrl_h, REB_JUMP), "Ctrl+H works at once")

	expect_equal(rebinder.begin(REB_JUMP, 1), OK, "begin() a new slot")
	var shift := pressed_key(KEY_SHIFT)
	shift.shift_pressed = true
	expect(rebinder.update(shift), "Shift pressed is used")
	expect(rebinder.update(pressed_key(KEY_SHIFT, false)), "Shift released alone is used")
	expect_equal(_last(), "completed:test_jump:1:Keyboard:Shift", "a modifier released alone is bound")
	expect_equal(events_text(REB_JUMP), "Keyboard:Ctrl+H; Gamepad:Paddle1; Keyboard:Shift", "slot 1 is added at the end")

	rebinder.device_filter = InputActionRebinder.DEVICE_GAMEPAD
	rebinder.gamepad_device = 1
	expect_equal(rebinder.begin(REB_JUMP, 0), OK, "begin() the gamepad slot")
	expect(not rebinder.update(pressed_key(KEY_J)), "a key is outside DEVICE_GAMEPAD")
	expect(not rebinder.update(joy_motion(JOY_AXIS_RIGHT_X, 0.3, 1)), "a weak stick move is ignored")
	expect(not rebinder.update(joy_motion(JOY_AXIS_RIGHT_X, 0.9, 0)), "another pad is ignored")
	expect(rebinder.update(joy_motion(JOY_AXIS_RIGHT_X, 0.9, 1)), "a strong move of the pad is used")
	expect_equal(_last(), "completed:test_jump:0:GamepadAxis:RightStickRight", "the direction is part of the binding")
	expect(InputMap.event_is_action(joy_motion(JOY_AXIS_RIGHT_X, 1.0, 3), REB_JUMP), "the binding works for every pad")

	expect_equal(rebinder.begin(&"no_such_action", 0), ERR_DOES_NOT_EXIST, "begin() of an unknown action")
	expect_equal(rebinder.begin(REB_JUMP, -1), ERR_INVALID_PARAMETER, "begin() with a negative index")


func _test_reject() -> void:
	var rebinder := _make_rebinder(InputActionRebinder.DEVICE_KEYBOARD_MOUSE, InputActionRebinder.CONFLICT_REJECT)
	rebinder.begin(REB_FIRE, 0)
	expect(rebinder.update(pressed_key(KEY_G)), "REJECT: the conflicting key is used")
	expect_equal(_last(), "conflict:test_fire:0:test_jump:0", "REJECT: conflict names both slots")
	expect(rebinder.is_active(), "REJECT: the session waits for another key")
	expect_equal(InputAction.event_to_text(rebinder.get_conflict_event()), "Keyboard:G", "get_conflict_event()")
	expect_equal(events_text(REB_FIRE), "Mouse:Left", "REJECT: fire is unchanged")
	expect(rebinder.update(pressed_key(KEY_K)), "REJECT: another key")
	expect_equal(_last(), "completed:test_fire:0:Keyboard:K", "REJECT: the free key is bound")


func _test_swap_order() -> void:
	var rebinder := _make_rebinder(InputActionRebinder.DEVICE_KEYBOARD_MOUSE, InputActionRebinder.CONFLICT_SWAP)
	InputMapSettingsServer.action_events_changed.connect(_on_action_events_changed)
	rebinder.begin(REB_FIRE, 0)
	events_log.clear()
	expect(rebinder.update(pressed_key(KEY_G)), "SWAP: the key is used")
	InputMapSettingsServer.action_events_changed.disconnect(_on_action_events_changed)
	expect_equal(events_text(REB_FIRE), "Keyboard:G", "SWAP: fire has G")
	expect_equal(events_text(REB_JUMP), "Mouse:Left; Gamepad:Paddle1", "SWAP: jump has the old event of fire")
	expect(InputMap.event_is_action(mouse_button(MOUSE_BUTTON_LEFT), REB_JUMP), "SWAP: the left button jumps")
	expect(not InputMap.event_is_action(pressed_key(KEY_G), REB_JUMP), "SWAP: G no longer jumps")
	expect_equal(events_log.size(), 3, "SWAP: two server signals and completed")
	if events_log.size() == 3:
		expect(events_log.has("changed:test_fire") and events_log.has("changed:test_jump"), "SWAP: the server announces both actions")
		expect_equal(events_log[2], "completed:test_fire:0:Keyboard:G", "SWAP: completed comes after the server signals")


func _test_allow() -> void:
	var rebinder := _make_rebinder(InputActionRebinder.DEVICE_ANY, InputActionRebinder.CONFLICT_ALLOW)
	rebinder.begin(REB_FIRE, 5)
	expect(rebinder.update(pressed_key(KEY_G)), "ALLOW: the key is used")
	expect_equal(_last(), "completed:test_fire:1:Keyboard:G", "ALLOW: added behind the last event")
	expect_equal(events_text(REB_FIRE), "Mouse:Left; Keyboard:G", "ALLOW: fire has G")
	expect(InputMap.event_is_action(pressed_key(KEY_G), REB_JUMP) and InputMap.event_is_action(pressed_key(KEY_G), REB_FIRE), "ALLOW: G works for both")


func _test_ask() -> void:
	var rebinder := _make_rebinder(InputActionRebinder.DEVICE_KEYBOARD_MOUSE, InputActionRebinder.CONFLICT_ASK)
	expect_equal(rebinder.resolve_conflict_allow(), ERR_UNCONFIGURED, "ASK: nothing to resolve")
	rebinder.begin(REB_FIRE, 0)
	expect(rebinder.update(pressed_key(KEY_G)), "ASK: the key is used")
	expect(rebinder.is_conflict_pending(), "ASK: the conflict waits")
	expect(not rebinder.update(pressed_key(KEY_K)), "ASK: no input while the menu asks")
	rebinder.cancel()
	expect_equal(_last(), "canceled:test_fire:0", "ASK: cancel() emits canceled")
	expect_equal(events_text(REB_FIRE), "Mouse:Left", "ASK: cancel changes nothing")
	expect_equal(events_text(REB_JUMP), "KeyboardLabel:G; Gamepad:Paddle1", "ASK: jump unchanged")

	rebinder.begin(REB_FIRE, 0)
	rebinder.update(pressed_key(KEY_G))
	expect_equal(rebinder.resolve_conflict_allow(), OK, "ASK: resolve_conflict_allow()")
	expect_equal(events_text(REB_FIRE), "Keyboard:G", "ASK allow: fire has G")
	expect(InputMap.event_is_action(pressed_key(KEY_G), REB_JUMP), "ASK allow: jump keeps G")

	InputMapSettingsServer.reset_action(REB_FIRE)
	expect_equal(events_text(REB_FIRE), "Mouse:Left", "fire is back on the left button")
	rebinder.begin(REB_FIRE, 0)
	rebinder.update(pressed_key(KEY_G))
	expect_equal(rebinder.resolve_conflict_swap(), OK, "ASK: resolve_conflict_swap()")
	expect_equal(_last(), "completed:test_fire:0:Keyboard:G", "ASK swap: completed")
	expect_equal(events_text(REB_JUMP), "Mouse:Left; Gamepad:Paddle1", "ASK swap: jump has the left button")


func _test_two_rebinders() -> void:
	var keyboard := _make_rebinder(InputActionRebinder.DEVICE_KEYBOARD_MOUSE, InputActionRebinder.CONFLICT_REJECT)
	var pad := _make_rebinder(InputActionRebinder.DEVICE_GAMEPAD, InputActionRebinder.CONFLICT_REJECT)
	keyboard.begin(REB_JUMP, 0)
	keyboard.cancel()
	expect_equal(_last(), "canceled:test_jump:0", "cancel() before any input")
	expect_equal(events_text(REB_JUMP), "KeyboardLabel:G; Gamepad:Paddle1", "cancel() changes nothing")

	keyboard.begin(REB_JUMP, 0)
	pad.begin(REB_JUMP, 0)
	expect(not keyboard.update(joy_button(JOY_BUTTON_PADDLE3, 0)), "the keyboard rebinder skips the pad")
	expect(pad.update(joy_button(JOY_BUTTON_PADDLE3, 0)), "the pad rebinder takes the pad")
	expect(keyboard.is_active() and not pad.is_active(), "only the pad session ended")
	expect(not pad.update(pressed_key(KEY_L)), "the ended pad rebinder takes nothing")
	expect(keyboard.update(pressed_key(KEY_L)), "the keyboard rebinder takes the key")
	expect_equal(events_text(REB_JUMP), "Keyboard:L; Gamepad:Paddle3", "both sessions changed their own slot")


func _test_builtin_conflict() -> void:
	var rebinder := _make_rebinder(InputActionRebinder.DEVICE_KEYBOARD_MOUSE, InputActionRebinder.CONFLICT_REJECT)
	rebinder.ignored_actions = PackedStringArray()
	expect_equal(rebinder.begin(&"ui_accept", 0), ERR_UNAVAILABLE, "a built-in action can't be rebound in CUSTOM_ONLY")
	rebinder.begin(REB_JUMP, 0)
	rebinder.update(pressed_key(KEY_ENTER))
	var accept_conflict := false
	for entry in events_log:
		accept_conflict = accept_conflict or entry.begins_with("conflict:test_jump:0:ui_accept:")
	expect(accept_conflict, "Enter conflicts with the built-in ui_accept")
	expect(rebinder.is_active(), "REJECT keeps waiting")
	rebinder.conflict_mode = InputActionRebinder.CONFLICT_SWAP
	events_log.clear()
	rebinder.update(pressed_key(KEY_ENTER))
	expect(rebinder.is_active(), "SWAP with a built-in action that is not stored is refused")
	expect(InputMap.event_is_action(pressed_key(KEY_ENTER), &"ui_accept"), "ui_accept keeps Enter")
	rebinder.ignored_actions = PackedStringArray(["ui_accept", "ui_text_newline", "ui_text_newline_blank", "ui_text_submit", "ui_text_completion_accept"])
	rebinder.conflict_mode = InputActionRebinder.CONFLICT_REJECT
	events_log.clear()
	rebinder.update(pressed_key(KEY_ENTER))
	if rebinder.is_active():
		# Another built-in action of this project uses Enter too.
		rebinder.ignored_actions = builtin_actions()
		rebinder.update(pressed_key(KEY_ENTER))
	expect_equal(_last(), "completed:test_jump:0:Keyboard:Enter", "ignored actions are no conflict")
	rebinder.cancel()


func _test_save_load() -> void:
	var old_path := InputMapSettingsServer.get_file_path()
	DirAccess.make_dir_recursive_absolute(SAVE_PATH.get_base_dir())
	InputMapSettingsServer.set_file_path(SAVE_PATH)
	var rebinder := _make_rebinder(InputActionRebinder.DEVICE_ANY, InputActionRebinder.CONFLICT_REJECT)
	rebinder.begin(REB_JUMP, 0)
	var ctrl_j := pressed_key(KEY_J)
	ctrl_j.ctrl_pressed = true
	rebinder.update(ctrl_j)
	rebinder.begin(REB_JUMP, 1)
	rebinder.update(joy_motion(JOY_AXIS_TRIGGER_LEFT, 0.8, 2))
	var stored := events_text(REB_JUMP)
	expect_equal(stored, "Keyboard:Ctrl+J; GamepadAxis:LeftTrigger", "the stored form of both events")
	expect_equal(InputMapSettingsServer.events_to_text(InputMapSettingsServer.text_to_events(stored)), stored, "the stored form survives the text round trip")
	expect_equal(InputMapSettingsServer.save(), OK, "save()")
	InputMapSettingsServer.rebind(REB_JUMP, pressed_key(KEY_M))
	expect_equal(InputMapSettingsServer.load(), OK, "load()")
	InputMapSettingsServer.apply()
	expect_equal(events_text(REB_JUMP), stored, "the committed events are what a save and load gives back")
	expect(InputMap.event_is_action(ctrl_j, REB_JUMP), "Ctrl+J works after the load")
	InputMapSettingsServer.set_file_path(old_path)
	DirAccess.remove_absolute(SAVE_PATH)
