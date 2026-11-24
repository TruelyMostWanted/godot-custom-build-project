extends FeatureCheck
## GDScript checks of 191-feature-input-action-rebinder. Check the same things as scripts/cs/Issue191FeatureInputActionRebinderCheck.cs.
##
## The project defines r191_jump (key G, joypad Paddle1) and r191_fire (left mouse button) in project.godot;
## every check starts from these bindings (reset_all()). Keys and buttons are chosen so that no built-in ui_*
## action uses them; the built-in actions are ignored except in the check of the built-in conflict.

const JUMP := &"r191_jump"
const FIRE := &"r191_fire"
const SAVE_PATH := "user://checks/191_input_map.ini"

var events_log: Array[String] = []


func _get_check_name() -> String:
	return "191-feature-input-action-rebinder"


func _run_checks() -> void:
	_check_class()
	_setup_actions()
	_check_filtering()
	_setup_actions()
	_check_reject()
	_setup_actions()
	_check_swap_order()
	_setup_actions()
	_check_allow()
	_setup_actions()
	_check_ask()
	_setup_actions()
	_check_two_rebinders()
	_setup_actions()
	_check_builtin_conflict()
	_setup_actions()
	_check_save_load()
	InputMapSettingsServer.reset_all()


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


func _last() -> String:
	return events_log[-1] if not events_log.is_empty() else ""


func _on_action_events_changed(action: StringName) -> void:
	if action == JUMP or action == FIRE:
		events_log.append("changed:%s" % action)


func _check_class() -> void:
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


func _check_filtering() -> void:
	var rebinder := _make_rebinder(InputActionRebinder.DEVICE_KEYBOARD_MOUSE, InputActionRebinder.CONFLICT_REJECT)
	expect_equal(rebinder.begin(JUMP, 0), OK, "begin()")
	expect_equal(_last(), "started:r191_jump:0", "started is emitted")
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
	expect_equal(_last(), "completed:r191_jump:0:Keyboard:Ctrl+H", "completed with Ctrl+H")
	expect_equal(events_text(JUMP), "Keyboard:Ctrl+H; Gamepad:Paddle1", "slot 0 has Ctrl+H")
	expect(InputMap.event_is_action(ctrl_h, JUMP), "Ctrl+H works at once")

	expect_equal(rebinder.begin(JUMP, 1), OK, "begin() a new slot")
	var shift := pressed_key(KEY_SHIFT)
	shift.shift_pressed = true
	expect(rebinder.update(shift), "Shift pressed is used")
	expect(rebinder.update(pressed_key(KEY_SHIFT, false)), "Shift released alone is used")
	expect_equal(_last(), "completed:r191_jump:1:Keyboard:Shift", "a modifier released alone is bound")
	expect_equal(events_text(JUMP), "Keyboard:Ctrl+H; Gamepad:Paddle1; Keyboard:Shift", "slot 1 is added at the end")

	rebinder.device_filter = InputActionRebinder.DEVICE_GAMEPAD
	rebinder.gamepad_device = 1
	expect_equal(rebinder.begin(JUMP, 0), OK, "begin() the gamepad slot")
	expect(not rebinder.update(pressed_key(KEY_J)), "a key is outside DEVICE_GAMEPAD")
	expect(not rebinder.update(joy_motion(JOY_AXIS_RIGHT_X, 0.3, 1)), "a weak stick move is ignored")
	expect(not rebinder.update(joy_motion(JOY_AXIS_RIGHT_X, 0.9, 0)), "another pad is ignored")
	expect(rebinder.update(joy_motion(JOY_AXIS_RIGHT_X, 0.9, 1)), "a strong move of the pad is used")
	expect_equal(_last(), "completed:r191_jump:0:GamepadAxis:RightStickRight", "the direction is part of the binding")
	expect(InputMap.event_is_action(joy_motion(JOY_AXIS_RIGHT_X, 1.0, 3), JUMP), "the binding works for every pad")

	expect_equal(rebinder.begin(&"r191_no_such_action", 0), ERR_DOES_NOT_EXIST, "begin() of an unknown action")
	expect_equal(rebinder.begin(JUMP, -1), ERR_INVALID_PARAMETER, "begin() with a negative index")


func _check_reject() -> void:
	var rebinder := _make_rebinder(InputActionRebinder.DEVICE_KEYBOARD_MOUSE, InputActionRebinder.CONFLICT_REJECT)
	rebinder.begin(FIRE, 0)
	expect(rebinder.update(pressed_key(KEY_G)), "REJECT: the conflicting key is used")
	expect_equal(_last(), "conflict:r191_fire:0:r191_jump:0", "REJECT: conflict names both slots")
	expect(rebinder.is_active(), "REJECT: the session waits for another key")
	expect_equal(InputAction.event_to_text(rebinder.get_conflict_event()), "Keyboard:G", "get_conflict_event()")
	expect_equal(events_text(FIRE), "Mouse:Left", "REJECT: fire is unchanged")
	expect(rebinder.update(pressed_key(KEY_K)), "REJECT: another key")
	expect_equal(_last(), "completed:r191_fire:0:Keyboard:K", "REJECT: the free key is bound")


func _check_swap_order() -> void:
	var rebinder := _make_rebinder(InputActionRebinder.DEVICE_KEYBOARD_MOUSE, InputActionRebinder.CONFLICT_SWAP)
	InputMapSettingsServer.action_events_changed.connect(_on_action_events_changed)
	rebinder.begin(FIRE, 0)
	events_log.clear()
	expect(rebinder.update(pressed_key(KEY_G)), "SWAP: the key is used")
	InputMapSettingsServer.action_events_changed.disconnect(_on_action_events_changed)
	expect_equal(events_text(FIRE), "Keyboard:G", "SWAP: fire has G")
	expect_equal(events_text(JUMP), "Mouse:Left; Gamepad:Paddle1", "SWAP: jump has the old event of fire")
	expect(InputMap.event_is_action(mouse_button(MOUSE_BUTTON_LEFT), JUMP), "SWAP: the left button jumps")
	expect(not InputMap.event_is_action(pressed_key(KEY_G), JUMP), "SWAP: G no longer jumps")
	expect_equal(events_log.size(), 3, "SWAP: two server signals and completed")
	if events_log.size() == 3:
		expect(events_log.has("changed:r191_fire") and events_log.has("changed:r191_jump"), "SWAP: the server announces both actions")
		expect_equal(events_log[2], "completed:r191_fire:0:Keyboard:G", "SWAP: completed comes after the server signals")


func _check_allow() -> void:
	var rebinder := _make_rebinder(InputActionRebinder.DEVICE_ANY, InputActionRebinder.CONFLICT_ALLOW)
	rebinder.begin(FIRE, 5)
	expect(rebinder.update(pressed_key(KEY_G)), "ALLOW: the key is used")
	expect_equal(_last(), "completed:r191_fire:1:Keyboard:G", "ALLOW: added behind the last event")
	expect_equal(events_text(FIRE), "Mouse:Left; Keyboard:G", "ALLOW: fire has G")
	expect(InputMap.event_is_action(pressed_key(KEY_G), JUMP) and InputMap.event_is_action(pressed_key(KEY_G), FIRE), "ALLOW: G works for both")


func _check_ask() -> void:
	var rebinder := _make_rebinder(InputActionRebinder.DEVICE_KEYBOARD_MOUSE, InputActionRebinder.CONFLICT_ASK)
	expect_equal(rebinder.resolve_conflict_allow(), ERR_UNCONFIGURED, "ASK: nothing to resolve")
	rebinder.begin(FIRE, 0)
	expect(rebinder.update(pressed_key(KEY_G)), "ASK: the key is used")
	expect(rebinder.is_conflict_pending(), "ASK: the conflict waits")
	expect(not rebinder.update(pressed_key(KEY_K)), "ASK: no input while the menu asks")
	rebinder.cancel()
	expect_equal(_last(), "canceled:r191_fire:0", "ASK: cancel() emits canceled")
	expect_equal(events_text(FIRE), "Mouse:Left", "ASK: cancel changes nothing")
	expect_equal(events_text(JUMP), "KeyboardLabel:G; Gamepad:Paddle1", "ASK: jump unchanged")

	rebinder.begin(FIRE, 0)
	rebinder.update(pressed_key(KEY_G))
	expect_equal(rebinder.resolve_conflict_allow(), OK, "ASK: resolve_conflict_allow()")
	expect_equal(events_text(FIRE), "Keyboard:G", "ASK allow: fire has G")
	expect(InputMap.event_is_action(pressed_key(KEY_G), JUMP), "ASK allow: jump keeps G")

	InputMapSettingsServer.reset_action(FIRE)
	expect_equal(events_text(FIRE), "Mouse:Left", "fire is back on the left button")
	rebinder.begin(FIRE, 0)
	rebinder.update(pressed_key(KEY_G))
	expect_equal(rebinder.resolve_conflict_swap(), OK, "ASK: resolve_conflict_swap()")
	expect_equal(_last(), "completed:r191_fire:0:Keyboard:G", "ASK swap: completed")
	expect_equal(events_text(JUMP), "Mouse:Left; Gamepad:Paddle1", "ASK swap: jump has the left button")


func _check_two_rebinders() -> void:
	var keyboard := _make_rebinder(InputActionRebinder.DEVICE_KEYBOARD_MOUSE, InputActionRebinder.CONFLICT_REJECT)
	var pad := _make_rebinder(InputActionRebinder.DEVICE_GAMEPAD, InputActionRebinder.CONFLICT_REJECT)
	keyboard.begin(JUMP, 0)
	keyboard.cancel()
	expect_equal(_last(), "canceled:r191_jump:0", "cancel() before any input")
	expect_equal(events_text(JUMP), "KeyboardLabel:G; Gamepad:Paddle1", "cancel() changes nothing")

	keyboard.begin(JUMP, 0)
	pad.begin(JUMP, 0)
	expect(not keyboard.update(joy_button(JOY_BUTTON_PADDLE3, 0)), "the keyboard rebinder skips the pad")
	expect(pad.update(joy_button(JOY_BUTTON_PADDLE3, 0)), "the pad rebinder takes the pad")
	expect(keyboard.is_active() and not pad.is_active(), "only the pad session ended")
	expect(not pad.update(pressed_key(KEY_L)), "the ended pad rebinder takes nothing")
	expect(keyboard.update(pressed_key(KEY_L)), "the keyboard rebinder takes the key")
	expect_equal(events_text(JUMP), "Keyboard:L; Gamepad:Paddle3", "both sessions changed their own slot")


func _check_builtin_conflict() -> void:
	var rebinder := _make_rebinder(InputActionRebinder.DEVICE_KEYBOARD_MOUSE, InputActionRebinder.CONFLICT_REJECT)
	rebinder.ignored_actions = PackedStringArray()
	expect_equal(rebinder.begin(&"ui_accept", 0), ERR_UNAVAILABLE, "a built-in action can't be rebound in CUSTOM_ONLY")
	rebinder.begin(JUMP, 0)
	rebinder.update(pressed_key(KEY_ENTER))
	var accept_conflict := false
	for entry in events_log:
		accept_conflict = accept_conflict or entry.begins_with("conflict:r191_jump:0:ui_accept:")
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
	expect_equal(_last(), "completed:r191_jump:0:Keyboard:Enter", "ignored actions are no conflict")
	rebinder.cancel()


func _check_save_load() -> void:
	var old_path := InputMapSettingsServer.get_file_path()
	DirAccess.make_dir_recursive_absolute(SAVE_PATH.get_base_dir())
	InputMapSettingsServer.set_file_path(SAVE_PATH)
	var rebinder := _make_rebinder(InputActionRebinder.DEVICE_ANY, InputActionRebinder.CONFLICT_REJECT)
	rebinder.begin(JUMP, 0)
	var ctrl_j := pressed_key(KEY_J)
	ctrl_j.ctrl_pressed = true
	rebinder.update(ctrl_j)
	rebinder.begin(JUMP, 1)
	rebinder.update(joy_motion(JOY_AXIS_TRIGGER_LEFT, 0.8, 2))
	var stored := events_text(JUMP)
	expect_equal(stored, "Keyboard:Ctrl+J; GamepadAxis:LeftTrigger", "the stored form of both events")
	expect_equal(InputMapSettingsServer.events_to_text(InputMapSettingsServer.text_to_events(stored)), stored, "the stored form survives the text round trip")
	expect_equal(InputMapSettingsServer.save(), OK, "save()")
	InputMapSettingsServer.rebind(JUMP, pressed_key(KEY_M))
	expect_equal(InputMapSettingsServer.load(), OK, "load()")
	InputMapSettingsServer.apply()
	expect_equal(events_text(JUMP), stored, "the committed events are what a save and load gives back")
	expect(InputMap.event_is_action(ctrl_j, JUMP), "Ctrl+J works after the load")
	InputMapSettingsServer.set_file_path(old_path)
	DirAccess.remove_absolute(SAVE_PATH)
