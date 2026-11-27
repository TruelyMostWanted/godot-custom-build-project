extends Node
## Minimal example of Input Icons: the snippets of the wiki guide, in order.
## Wiki: https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Input-Icons

var prompt := TextureRect.new()


func _ready() -> void:
	# Create an icon set (the example uses icon.svg for every button instead of the prompt images).
	var xbox := InputIconSet.new()
	var prompt_icon: Texture2D = preload("res://icon.svg")
	xbox.set_joy_button_icon(JOY_BUTTON_A, prompt_icon)
	xbox.set_joy_axis_icon(JOY_AXIS_LEFT_X, -1.0, prompt_icon)
	xbox.joy_button_icons = {"B": prompt_icon, "LeftStickPress": prompt_icon}
	DirAccess.make_dir_recursive_absolute("user://input-icons-example")
	ResourceSaver.save(xbox, "user://input-icons-example/xbox.tres")
	print("Gamepad button icons: ", xbox.joy_button_icons.keys())

	# Register the sets in the project settings (here at runtime).
	InputIconServer.set_icon_set(InputIconServer.INPUT_ICON_FAMILY_PLAYSTATION, xbox)
	print("Override: ", InputIconServer.has_icon_set_override(InputIconServer.INPUT_ICON_FAMILY_PLAYSTATION))
	InputIconServer.reset_icon_set(InputIconServer.INPUT_ICON_FAMILY_PLAYSTATION)

	# Show the prompt of an action
	add_child(prompt)
	InputIconServer.icon_set_changed.connect(func(_family: int) -> void: _update_prompt())
	InputIconServer.set_icon_set(InputIconServer.INPUT_ICON_FAMILY_KEYBOARD_MOUSE, xbox)
	_update_prompt()
	print("Prompt: ", prompt.texture)

	# Let the player choose the gamepad icons
	InputIconServer.gamepad_family = InputIconServer.INPUT_ICON_FAMILY_NINTENDO
	print("Gamepad family: ", InputIconServer.gamepad_family)

	# Fallback icons: a button without an icon gets the fallback of its type.
	xbox.fallback_joy_button_icon = prompt_icon
	var pad_x := InputEventJoypadButton.new()
	pad_x.button_index = JOY_BUTTON_X
	print("Fallback icon: ", xbox.get_icon(pad_x))

	InputIconServer.reset_icon_set(InputIconServer.INPUT_ICON_FAMILY_KEYBOARD_MOUSE)
	InputIconServer.gamepad_family = InputIconServer.INPUT_ICON_FAMILY_AUTO
	DirAccess.remove_absolute("user://input-icons-example/xbox.tres")
	DirAccess.remove_absolute("user://input-icons-example")
	get_tree().quit()


func _update_prompt() -> void:
	var events: Array[InputEvent] = InputMap.action_get_events("jump")
	if events.is_empty():
		prompt.texture = null
	else:
		prompt.texture = InputIconServer.get_icon(events[0])
