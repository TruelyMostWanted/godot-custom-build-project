extends FeatureCheck
## GDScript checks of 172-feature-input-map-layers. Check the same things as scripts/cs/Issue172FeatureInputMapLayersCheck.cs.
##
## The project defines the action l172_attack (key F, deadzone 0.3) in project.godot. Other actions are
## created at runtime and removed again.

const ATTACK := &"l172_attack"
const BULK_COUNT := 100
const BULK_FIRST_KEY := 0x4E00 # Unicode keycodes no other action uses.


func _get_check_name() -> String:
	return "172-feature-input-map-layers"


func _run_checks() -> void:
	_check_input_action()
	_check_input_actions()
	_check_layers()
	_check_set_input_action()
	_check_parse_cost()
	_check_settings_server()


static func pressed_key(keycode: Key, pressed := true) -> InputEventKey:
	var event := InputEventKey.new()
	event.keycode = keycode
	event.physical_keycode = keycode
	event.key_label = keycode
	event.pressed = pressed
	return event


func _check_input_action() -> void:
	var action := InputAction.new()
	expect_equal(action.action, &"", "a new InputAction has no name")
	expect(is_equal_approx(action.deadzone, 0.2), "the default deadzone is 0.2")
	expect_equal(action.events.size(), 0, "a new InputAction has no events")
	action.action = &"l172_demo"
	action.deadzone = 0.4
	expect_equal(action.action, &"l172_demo", "action name")
	expect(is_equal_approx(action.deadzone, 0.4), "deadzone")

	action.bindings = "Key:Ctrl+S; JoypadButton:A"
	expect_equal(action.get_event_count(), 2, "bindings text gives two events")
	expect(action.events[0] is InputEventKey, "the first event is a key")
	expect(action.events[1] is InputEventJoypadButton, "the second event is a joypad button")
	expect_equal(action.bindings, "Key:Ctrl+S; JoypadButton:A", "bindings round trip")
	expect_equal(action.get_device_events(InputAction.INPUT_DEVICE_KEYBOARD).size(), 1, "one keyboard event")
	expect_equal(action.get_device_events(InputAction.INPUT_DEVICE_GAMEPAD).size(), 1, "one gamepad event")
	expect_equal(action.get_device_events(InputAction.INPUT_DEVICE_MOUSE).size(), 0, "no mouse event")

	var mouse := InputAction.text_to_event("MouseButton:Left")
	action.add_event(mouse)
	expect_equal(action.get_event_count(), 3, "add_event() adds an event")
	expect(action.has_event(mouse), "has_event()")
	action.add_event(InputAction.text_to_event("MouseButton:Left"))
	expect_equal(action.get_event_count(), 3, "add_event() ignores an equal event")
	expect(action.remove_event(mouse), "remove_event() finds the event")
	expect(not action.remove_event(mouse), "remove_event() of a missing event is false")
	action.events = [pressed_key(KEY_W)]
	expect_equal(action.bindings, "PhysicalKey:W", "setting events updates the bindings text")
	action.clear_events()
	expect_equal(action.get_event_count(), 0, "clear_events()")
	expect_equal(action.bindings, "", "no events, no bindings text")

	# Touch has no text form: kept in events, left out of bindings.
	action.add_event(InputEventScreenTouch.new())
	action.add_event(InputAction.text_to_event("Key:Space"))
	expect_equal(action.get_event_count(), 2, "touch and key are both events")
	expect_equal(action.bindings, "Key:Space", "touch is not part of the bindings text")

	for text in ["Key:Ctrl+S", "PhysicalKey:W", "MouseButton:Ctrl+WheelUp", "JoypadButton:LeftShoulder", "JoypadMotion:LeftX+"]:
		var event := InputAction.text_to_event(text)
		expect(event != null, "text_to_event(\"%s\") gives an event" % text)
		if event:
			expect_equal(InputAction.event_to_text(event), text, "round trip of \"%s\"" % text)
	expect(InputAction.text_to_event("Key:Hyper+S") == null, "an unknown modifier is not valid")
	expect_equal(InputAction.event_to_text(InputEventScreenTouch.new()), "", "a touch has no text")
	expect_equal(InputAction.get_event_device(pressed_key(KEY_A)), InputAction.INPUT_DEVICE_KEYBOARD, "key device")
	expect_equal(InputAction.get_event_device(InputAction.text_to_event("MouseButton:Left")), InputAction.INPUT_DEVICE_MOUSE, "mouse device")
	expect_equal(InputAction.get_event_device(InputAction.text_to_event("JoypadMotion:LeftX+")), InputAction.INPUT_DEVICE_GAMEPAD, "joypad axis device")
	expect_equal(InputAction.get_event_device(InputEventScreenTouch.new()), InputAction.INPUT_DEVICE_MAX, "touch has no device")


func _check_input_actions() -> void:
	var list := InputActions.new()
	expect_equal(list.get_action_count(), 0, "a new InputActions is empty")
	var jump := InputAction.new()
	jump.action = &"l172_jump"
	jump.bindings = "Key:Space"
	var fire := InputAction.new()
	fire.action = &"l172_fire"
	fire.bindings = "MouseButton:Left"
	list.add_action(jump)
	list.add_action(fire)
	expect_equal(list.get_action_count(), 2, "two actions")
	expect(list.has_action(&"l172_jump") and list.has_action(&"l172_fire"), "has_action()")
	expect(not list.has_action(&"l172_none"), "has_action() of a missing name is false")
	expect_equal(list.get_action(&"l172_jump"), jump, "get_action() returns the same resource")
	expect(list.get_action(&"l172_none") == null, "get_action() of a missing name is null")
	expect_equal(list.get_action_names(), [&"l172_jump", &"l172_fire"], "names in insertion order")

	var jump2 := InputAction.new()
	jump2.action = &"l172_jump"
	jump2.bindings = "Key:W"
	list.add_action(jump2)
	expect_equal(list.get_action_count(), 2, "adding a name again replaces the action")
	expect_equal(list.get_action(&"l172_jump"), jump2, "the new action replaced the old one")
	expect_equal(list.get_action_names(), [&"l172_jump", &"l172_fire"], "the replaced action keeps its place")

	# A rename of an action in the list is found by the new name.
	fire.action = &"l172_shoot"
	expect(list.has_action(&"l172_shoot") and not list.has_action(&"l172_fire"), "renaming an action updates the lookup")

	expect(list.remove_action(&"l172_shoot"), "remove_action()")
	expect(not list.remove_action(&"l172_shoot"), "remove_action() of a missing name is false")
	expect_equal(list.get_action_count(), 1, "one action left")
	list.clear()
	expect_equal(list.get_action_count(), 0, "clear()")
	list.actions = [jump, jump2]
	expect_equal(list.get_action_count(), 1, "set_actions(): the later action of a name wins")
	expect_equal(list.get_action(&"l172_jump"), jump2, "set_actions(): the later one is kept")


func _check_layers() -> void:
	expect(InputMap.is_builtin_input_action(&"ui_accept"), "ui_accept is built-in")
	expect(not InputMap.is_custom_input_action(&"ui_accept"), "ui_accept is not custom")
	expect(not InputMap.is_builtin_input_action(&"ui_text_backspace_word.macos"), "a platform variant is not an action of the built-in layer")
	expect(InputMap.is_custom_input_action(ATTACK), "a project action is custom")
	expect(not InputMap.is_builtin_input_action(ATTACK), "a project action is not built-in")
	expect(not InputMap.is_custom_input_action(&"l172_not_there"), "a missing action is not custom")
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
	expect_equal(attack.bindings, "Key:F", "get_input_action() events as text")
	expect(attack == custom.get_action(ATTACK), "get_input_action() is the action of the custom layer")
	var key_h := InputAction.text_to_event("Key:H")
	attack.add_event(key_h)
	expect(InputMap.event_is_action(pressed_key(KEY_H), ATTACK), "an event added to the returned action works")
	attack.remove_event(key_h)
	expect(not InputMap.event_is_action(pressed_key(KEY_H), ATTACK), "an event removed from the returned action no longer works")
	expect_equal(InputMap.get_input_action(ATTACK).get_event_count(), 1, "one event again")

	# Direct edits of a layer reach the InputMap.
	var direct := InputAction.new()
	direct.action = &"l172_direct"
	direct.bindings = "Key:F9"
	custom.add_action(direct)
	expect(InputMap.has_action(&"l172_direct"), "an action added to the custom layer is in the InputMap")
	expect(InputMap.event_is_action(pressed_key(KEY_F9), &"l172_direct"), "its key works")
	expect(custom.remove_action(&"l172_direct"), "remove it from the layer")
	expect(not InputMap.has_action(&"l172_direct"), "an action removed from the layer is gone from the InputMap")


func _check_set_input_action() -> void:
	var original := InputMap.get_input_action(ATTACK)
	var changed := InputAction.new()
	changed.action = ATTACK
	changed.deadzone = 0.6
	changed.bindings = "Key:G"
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
		action.action = StringName("l172_extra_%d" % n)
		action.bindings = "Key:F%d" % (n + 5)
		extra.add_action(action)
	InputMap.set_input_actions(extra)
	expect(InputMap.has_action(&"l172_extra_0") and InputMap.has_action(&"l172_extra_1"), "set_input_actions() adds new actions")
	expect(InputMap.is_custom_input_action(&"l172_extra_1"), "a new action is custom")
	expect(InputMap.event_is_action(pressed_key(KEY_F6), &"l172_extra_1"), "the new action reacts to its key")
	InputMap.erase_action(&"l172_extra_0")
	InputMap.erase_action(&"l172_extra_1")


func _check_parse_cost() -> void:
	for i in BULK_COUNT:
		var bulk := StringName("l172_bulk_%d" % i)
		InputMap.add_action(bulk)
		InputMap.action_add_event(bulk, pressed_key((BULK_FIRST_KEY + i) as Key))
	expect(InputMap.has_action(&"l172_bulk_99"), "100 actions were added")

	for i in [0, 37, 99]:
		var bulk := StringName("l172_bulk_%d" % i)
		Input.parse_input_event(pressed_key((BULK_FIRST_KEY + i) as Key))
		Input.flush_buffered_events()
		expect(Input.is_action_pressed(bulk), "%s is pressed after parse_input_event()" % bulk)
		var other := StringName("l172_bulk_%d" % ((i + 1) % BULK_COUNT))
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
	expect(not Input.is_action_pressed(&"l172_bulk_50"), "the bulk action is released at the end")

	for i in BULK_COUNT:
		InputMap.erase_action(StringName("l172_bulk_%d" % i))
	expect(not InputMap.has_action(&"l172_bulk_0"), "the bulk actions were removed")
	expect(not InputMap.event_is_action(pressed_key(BULK_FIRST_KEY as Key), &"l172_bulk_0"), "a removed action no longer matches")


func _check_settings_server() -> void:
	expect_equal(InputMapSettingsServer.save_mode, InputMapSettingsServer.SAVE_MODE_CUSTOM_ONLY, "save_mode is CUSTOM_ONLY by default")
	expect(InputMapSettingsServer.get_default_settings() is InputActions, "the settings resource is an InputActions")
	expect(InputMapSettingsServer.has_action(ATTACK), "the project action is managed")
	expect(not InputMapSettingsServer.has_action(&"ui_accept"), "built-in actions are not managed in CUSTOM_ONLY")
	expect_equal(InputMapSettingsServer.rebind(&"ui_accept", pressed_key(KEY_Q)), ERR_DOES_NOT_EXIST, "a built-in action can't be rebound in CUSTOM_ONLY")

	InputMapSettingsServer.reset_all()
	expect_equal(InputAction.event_to_text(InputMapSettingsServer.get_event(ATTACK, InputAction.INPUT_DEVICE_KEYBOARD, 0)), "Key:F", "slot 0 is F")
	expect_equal(InputMapSettingsServer.rebind(ATTACK, pressed_key(KEY_X)), OK, "rebind() slot 0")
	expect_equal(InputAction.event_to_text(InputMapSettingsServer.get_event(ATTACK, InputAction.INPUT_DEVICE_KEYBOARD, 0)), "PhysicalKey:X", "get_event() gives the new key")
	expect(InputMap.event_is_action(pressed_key(KEY_X), ATTACK), "the new key works at once")
	expect(not InputMap.event_is_action(pressed_key(KEY_F), ATTACK), "the old key no longer works")
	expect_equal(InputMapSettingsServer.rebind(ATTACK, pressed_key(KEY_Y), 1), OK, "rebind() slot 1")
	expect_equal(InputMapSettingsServer.get_slot_count(ATTACK, InputAction.INPUT_DEVICE_KEYBOARD), 2, "two keyboard slots")
	expect(InputMapSettingsServer.get_input_action(ATTACK).bindings.contains("PhysicalKey:Y"), "get_input_action() shows both keys")

	InputMapSettingsServer.clear_event(ATTACK, InputAction.INPUT_DEVICE_KEYBOARD, 0)
	expect_equal(InputMapSettingsServer.get_slot_count(ATTACK, InputAction.INPUT_DEVICE_KEYBOARD), 1, "clear_event() removes the slot")
	expect_equal(InputAction.event_to_text(InputMapSettingsServer.get_event(ATTACK, InputAction.INPUT_DEVICE_KEYBOARD, 0)), "PhysicalKey:Y", "later slots move up")
	expect(not InputMap.event_is_action(pressed_key(KEY_X), ATTACK), "the cleared key no longer works")
	expect(InputMap.event_is_action(pressed_key(KEY_Y), ATTACK), "the moved key works")

	InputMapSettingsServer.reset_action(ATTACK)
	expect(InputMap.event_is_action(pressed_key(KEY_F), ATTACK), "reset_action(): F works again")
	expect(not InputMap.event_is_action(pressed_key(KEY_Y), ATTACK), "reset_action(): Y no longer works")
	expect_equal(InputMapSettingsServer.get_input_action(ATTACK).bindings, "Key:F", "reset_action(): the project bindings")

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
