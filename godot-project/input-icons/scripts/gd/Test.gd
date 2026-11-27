extends FeatureTest
## Full API test of Input Icons: InputIconServer and InputIconSet. Same tests as scripts/cs/Test.cs.
## The icons are made at runtime (solid colors, CC0): the size tells them apart.

const DIR := "user://input-icons-test-gd"
const SAVE_PATH := "user://input-icons-test-gd/icons.tres"
const EXAMPLE_ICONS := "res://input-icons/resources/example_icons.tres"


func _make_icon(size: int) -> Texture2D:
	var image := Image.create_empty(size, size, false, Image.FORMAT_RGBA8)
	image.fill(Color(float(size % 7) / 7.0, 0.5, 1.0))
	return ImageTexture.create_from_image(image)


func _key(keycode: Key) -> InputEventKey:
	var event := InputEventKey.new()
	event.physical_keycode = keycode
	return event


func _joy_button(button: JoyButton) -> InputEventJoypadButton:
	var event := InputEventJoypadButton.new()
	event.button_index = button
	event.device = -1
	return event


func _joy_axis(axis: JoyAxis, value: float) -> InputEventJoypadMotion:
	var event := InputEventJoypadMotion.new()
	event.axis = axis
	event.axis_value = value
	event.device = -1
	return event


func _run_tests() -> void:
	_test_classes()
	_test_set_lookup()
	_test_text_and_inspector()
	_test_save_load()
	_test_server()
	_test_device_family()
	_test_project_settings()
	_test_example_resource()
	_reset_server()
	for file_name: String in DirAccess.get_files_at(DIR):
		DirAccess.remove_absolute(DIR.path_join(file_name))
	DirAccess.remove_absolute(DIR)


func _test_classes() -> void:
	DirAccess.make_dir_recursive_absolute(DIR)
	expect(ClassDB.class_exists("InputIconSet"), "InputIconSet class exists")
	expect(ClassDB.is_parent_class("InputIconSet", "Resource"), "InputIconSet is a Resource")
	expect(Engine.has_singleton("InputIconServer"), "InputIconServer singleton exists")
	expect(ProjectSettings.has_setting("input_devices/icons/xbox"), "project setting input_devices/icons/xbox exists")
	expect(InputIconServer.INPUT_ICON_FAMILY_AUTO == -1, "INPUT_ICON_FAMILY_AUTO is -1")
	expect(InputIconServer.INPUT_ICON_FAMILY_MAX == 5, "five icon families")


func _test_set_lookup() -> void:
	var icons := InputIconSet.new()
	var space := _make_icon(10)
	var left := _make_icon(11)
	var a := _make_icon(12)
	var stick_left := _make_icon(13)
	var trigger := _make_icon(14)
	var fallback := _make_icon(15)
	icons.set_key_icon(KEY_SPACE, space)
	icons.set_mouse_button_icon(MOUSE_BUTTON_LEFT, left)
	icons.set_joy_button_icon(JOY_BUTTON_A, a)
	icons.set_joy_axis_icon(JOY_AXIS_LEFT_X, -1.0, stick_left)
	icons.set_joy_axis_icon(JOY_AXIS_TRIGGER_RIGHT, 1.0, trigger)
	icons.fallback_joy_button_icon = fallback

	expect(icons.get_key_icon(KEY_SPACE) == space, "key icon per Key")
	expect(icons.get_mouse_button_icon(MOUSE_BUTTON_LEFT) == left, "icon per MouseButton")
	expect(icons.get_joy_button_icon(JOY_BUTTON_A) == a, "icon per JoyButton")
	expect(icons.get_joy_axis_icon(JOY_AXIS_LEFT_X, -0.4) == stick_left, "axis icon per direction (negative)")
	expect(icons.get_joy_axis_icon(JOY_AXIS_LEFT_X, 1.0) == null, "other direction has no icon")
	expect(icons.get_icon(_key(KEY_SPACE)) == space, "get_icon of a key event")
	var ctrl_space := _key(KEY_SPACE)
	ctrl_space.ctrl_pressed = true
	expect(icons.get_icon(ctrl_space) == space, "modifiers are ignored")
	expect(icons.get_icon(_joy_axis(JOY_AXIS_LEFT_X, -1.0)) == stick_left, "get_icon of an axis event")
	expect(icons.get_icon(_joy_axis(JOY_AXIS_TRIGGER_RIGHT, 1.0)) == trigger, "get_icon of a trigger")
	expect(icons.get_icon(_joy_button(JOY_BUTTON_X)) == fallback, "fallback icon of the event type")
	expect(icons.get_icon(_joy_button(JOY_BUTTON_X), false) == null, "no fallback with use_fallback = false")
	expect(icons.has_icon(_joy_button(JOY_BUTTON_A)), "has_icon for a set button")
	expect(not icons.has_icon(_joy_button(JOY_BUTTON_X)), "has_icon ignores fallbacks")
	icons.clear()
	expect(icons.get_icon(_key(KEY_SPACE)) == null, "clear removes all icons")


func _test_text_and_inspector() -> void:
	var icons := InputIconSet.new()
	var press := _make_icon(20)
	var up := _make_icon(21)
	var wheel := _make_icon(22)
	icons.joy_button_icons = {"LeftStickPress": press, JOY_BUTTON_B: up}
	expect(icons.get_joy_button_icon(JOY_BUTTON_LEFT_STICK) == press, "dictionary keyed by text name")
	expect(icons.get_joy_button_icon(JOY_BUTTON_B) == up, "dictionary keyed by enum value")
	expect(icons.joy_button_icons.has("LeftStickPress") and icons.joy_button_icons.has("B"), "dictionary is read back with text names")
	expect(icons.get_icon_for_text("Gamepad:LeftStickPress") == press, "get_icon_for_text")
	expect(icons.get_icon_for_text("no event") == null, "invalid text gives null")
	icons.set("mouse/WheelUp", wheel)
	expect(icons.get_mouse_button_icon(MOUSE_BUTTON_WHEEL_UP) == wheel, "Inspector property mouse/WheelUp")
	icons.set("gamepad_axes/LeftStickUp", up)
	expect(icons.get_joy_axis_icon(JOY_AXIS_LEFT_Y, -1.0) == up, "Inspector property gamepad_axes/LeftStickUp")
	var names := {}
	for property in icons.get_property_list():
		names[property.name] = true
	expect(names.has("keyboard/Space"), "Inspector lists keyboard/Space")
	expect(names.has("gamepad_buttons/LeftStickPress"), "Inspector lists gamepad_buttons/LeftStickPress")
	expect(names.has("gamepad_axes/RightTrigger"), "Inspector lists gamepad_axes/RightTrigger")


func _test_save_load() -> void:
	DirAccess.make_dir_recursive_absolute(DIR)
	var icons := InputIconSet.new()
	icons.set_key_icon(KEY_ESCAPE, _make_icon(30))
	icons.set_joy_axis_icon(JOY_AXIS_RIGHT_X, 1.0, _make_icon(31))
	expect(ResourceSaver.save(icons, SAVE_PATH) == OK, "InputIconSet saves as .tres")
	var loaded := ResourceLoader.load(SAVE_PATH, "", ResourceLoader.CACHE_MODE_IGNORE) as InputIconSet
	expect(loaded != null, "InputIconSet loads from .tres")
	if loaded == null:
		return
	var escape := loaded.get_key_icon(KEY_ESCAPE)
	expect(escape != null and escape.get_width() == 30, "loaded key icon")
	var stick := loaded.get_joy_axis_icon(JOY_AXIS_RIGHT_X, 1.0)
	expect(stick != null and stick.get_width() == 31, "loaded axis icon")


func _test_server() -> void:
	var keyboard := InputIconSet.new()
	var generic := InputIconSet.new()
	var playstation := InputIconSet.new()
	var key_w := _make_icon(40)
	var generic_a := _make_icon(41)
	var generic_b := _make_icon(42)
	var ps_a := _make_icon(43)
	var ps_fallback := _make_icon(44)
	keyboard.set_key_icon(KEY_W, key_w)
	generic.set_joy_button_icon(JOY_BUTTON_A, generic_a)
	generic.set_joy_button_icon(JOY_BUTTON_B, generic_b)
	playstation.set_joy_button_icon(JOY_BUTTON_A, ps_a)
	playstation.fallback_joy_button_icon = ps_fallback

	var changed: Array[int] = []
	var on_changed := func(family: int) -> void: changed.append(family)
	InputIconServer.icon_set_changed.connect(on_changed)
	InputIconServer.set_icon_set(InputIconServer.INPUT_ICON_FAMILY_KEYBOARD_MOUSE, keyboard)
	InputIconServer.set_icon_set(InputIconServer.INPUT_ICON_FAMILY_GENERIC_GAMEPAD, generic)
	InputIconServer.set_icon_set(InputIconServer.INPUT_ICON_FAMILY_PLAYSTATION, playstation)
	expect(changed.size() == 3 and changed[2] == InputIconServer.INPUT_ICON_FAMILY_PLAYSTATION, "icon_set_changed on set_icon_set")
	playstation.set_joy_button_icon(JOY_BUTTON_Y, _make_icon(45))
	expect(changed.size() == 4, "icon_set_changed when a set is edited")
	InputIconServer.icon_set_changed.disconnect(on_changed)

	expect(InputIconServer.get_icon_set(InputIconServer.INPUT_ICON_FAMILY_PLAYSTATION) == playstation, "get_icon_set")
	expect(InputIconServer.get_icon(_key(KEY_W)) == key_w, "keys use KEYBOARD_MOUSE")
	expect(InputIconServer.get_event_family(_key(KEY_W)) == InputIconServer.INPUT_ICON_FAMILY_KEYBOARD_MOUSE, "family of a key")
	expect(InputIconServer.get_icon(_joy_button(JOY_BUTTON_A), InputIconServer.INPUT_ICON_FAMILY_PLAYSTATION) == ps_a, "specific icon of the family")
	expect(InputIconServer.get_icon(_joy_button(JOY_BUTTON_B), InputIconServer.INPUT_ICON_FAMILY_PLAYSTATION) == generic_b, "then the icon of GENERIC_GAMEPAD")
	expect(InputIconServer.get_icon(_joy_button(JOY_BUTTON_X), InputIconServer.INPUT_ICON_FAMILY_PLAYSTATION) == ps_fallback, "then the fallback icon")
	expect(InputIconServer.get_icon_for_text("Gamepad:A", InputIconServer.INPUT_ICON_FAMILY_PLAYSTATION) == ps_a, "get_icon_for_text with family")

	InputIconServer.gamepad_family = InputIconServer.INPUT_ICON_FAMILY_PLAYSTATION
	expect(InputIconServer.get_icon(_joy_button(JOY_BUTTON_A)) == ps_a, "gamepad_family forces the family")
	InputIconServer.gamepad_family = InputIconServer.INPUT_ICON_FAMILY_AUTO

	expect(InputIconServer.get_family_for_joy_name("Xbox Wireless Controller") == InputIconServer.INPUT_ICON_FAMILY_XBOX, "Xbox name")
	expect(InputIconServer.get_family_for_joy_name("DualSense Wireless Controller") == InputIconServer.INPUT_ICON_FAMILY_PLAYSTATION, "DualSense name")
	expect(InputIconServer.get_family_for_joy_name("Wireless Controller", 0x054C) == InputIconServer.INPUT_ICON_FAMILY_PLAYSTATION, "Sony vendor id")
	expect(InputIconServer.get_family_for_joy_name("057e-2009-Pro Controller") == InputIconServer.INPUT_ICON_FAMILY_NINTENDO, "browser Gamepad.id")
	expect(InputIconServer.get_family_for_joy_name("Some Pad") == InputIconServer.INPUT_ICON_FAMILY_GENERIC_GAMEPAD, "unknown name")

	InputIconServer.reset_icon_set(InputIconServer.INPUT_ICON_FAMILY_PLAYSTATION)
	expect(not InputIconServer.has_icon_set_override(InputIconServer.INPUT_ICON_FAMILY_PLAYSTATION), "reset_icon_set drops the override")
	expect(InputIconServer.get_icon_set(InputIconServer.INPUT_ICON_FAMILY_PLAYSTATION) == null, "no project setting: no set")


func _reset_server() -> void:
	for family in InputIconServer.INPUT_ICON_FAMILY_MAX:
		InputIconServer.reset_icon_set(family)
	InputIconServer.gamepad_family = InputIconServer.INPUT_ICON_FAMILY_AUTO


func _test_device_family() -> void:
	var family: int = InputIconServer.get_device_family(0)
	expect(family >= InputIconServer.INPUT_ICON_FAMILY_GENERIC_GAMEPAD, "get_device_family gives a gamepad family")
	expect(family < InputIconServer.INPUT_ICON_FAMILY_MAX, "get_device_family is below INPUT_ICON_FAMILY_MAX")
	expect_equal(InputIconServer.get_event_family(_joy_button(JOY_BUTTON_A)), InputIconServer.get_device_family(-1), "get_event_family of a gamepad event follows the device")
	InputIconServer.gamepad_family = InputIconServer.INPUT_ICON_FAMILY_NINTENDO
	expect_equal(InputIconServer.get_event_family(_joy_button(JOY_BUTTON_A)), InputIconServer.INPUT_ICON_FAMILY_NINTENDO, "gamepad_family forces the family of a gamepad event")
	expect_equal(InputIconServer.gamepad_family, InputIconServer.INPUT_ICON_FAMILY_NINTENDO, "gamepad_family property")
	InputIconServer.gamepad_family = InputIconServer.INPUT_ICON_FAMILY_AUTO


func _test_project_settings() -> void:
	for family_name: String in ["keyboard_mouse", "generic_gamepad", "xbox", "playstation", "nintendo"]:
		expect(ProjectSettings.has_setting("input_devices/icons/" + family_name), "project setting input_devices/icons/%s exists" % family_name)
	var changed: Array[int] = []
	var on_changed := func(family: int) -> void: changed.append(family)
	InputIconServer.icon_set_changed.connect(on_changed)
	ProjectSettings.set_setting("input_devices/icons/xbox", EXAMPLE_ICONS)
	var xbox: InputIconSet = InputIconServer.get_icon_set(InputIconServer.INPUT_ICON_FAMILY_XBOX)
	expect(xbox != null, "the set of the project setting is loaded")
	expect(not InputIconServer.has_icon_set_override(InputIconServer.INPUT_ICON_FAMILY_XBOX), "a project set is no override")
	expect(xbox.get_key_icon(KEY_SPACE) != null, "the set of the project setting has the example icons")
	ProjectSettings.set_setting("input_devices/icons/xbox", "")
	InputIconServer.icon_set_changed.disconnect(on_changed)


func _test_example_resource() -> void:
	var icons: InputIconSet = load(EXAMPLE_ICONS)
	expect(icons.get_key_icon(KEY_ESCAPE) != null, "the example set has an Escape icon")
	expect(icons.get_mouse_button_icon(MOUSE_BUTTON_LEFT) != null, "the example set has a left mouse button icon")
	expect(icons.get_joy_button_icon(JOY_BUTTON_A) != null, "the example set has an A button icon")
	expect(icons.get_joy_axis_icon(JOY_AXIS_LEFT_Y, -1.0) != null, "the example set has a left stick up icon")
	expect(icons.fallback_key_icon != null, "the example set has a fallback key icon")
	expect(icons.fallback_mouse_button_icon == null, "the example set has no fallback mouse icon")
	expect(icons.fallback_joy_button_icon == null, "the example set has no fallback joy button icon")
	expect(icons.fallback_joy_axis_icon == null, "the example set has no fallback joy axis icon")
	expect(icons.get_fallback_icon(_key(KEY_A)) == icons.fallback_key_icon, "get_fallback_icon of a key")
	expect(icons.get_fallback_icon(_joy_button(JOY_BUTTON_A)) == null, "get_fallback_icon without a fallback is null")
	expect(icons.key_icons.has("Space"), "key_icons is keyed by names")
	expect(icons.mouse_button_icons.has("Left"), "mouse_button_icons is keyed by names")
	expect(icons.joy_button_icons.has("A"), "joy_button_icons is keyed by names")
	expect(icons.joy_axis_icons.has("LeftStickUp"), "joy_axis_icons is keyed by names")
