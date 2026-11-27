extends Node
## Minimal example of Input Map Settings: the snippets of the wiki guide, in order.
## Wiki: https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Input-Map-Settings


func _ready() -> void:
	# Using the API
	var input_map := InputMapSettingsServer

	# Set: binds an event to a slot of its device, applied to the InputMap at once.
	var w_key := InputEventKey.new()
	w_key.physical_keycode = KEY_W
	input_map.rebind(&"jump", w_key, 0) # Keyboard slot 0: W instead of Space.
	input_map.rebind(&"jump", InputAction.text_to_event("Gamepad:B"), 0) # The text form of an event.
	input_map.clear_event(&"interact", InputAction.INPUT_DEVICE_GAMEPAD, 0)

	# Get: the event of one slot, and a label for the UI.
	var event := input_map.get_event(&"jump", InputAction.INPUT_DEVICE_KEYBOARD, 0)
	print(input_map.get_event_label(event)) # W
	print(InputMap.action_has_event(&"jump", event)) # true

	# reset_action() gives one action the bindings of the project back.
	input_map.reset_action(&"fire")

	# In APPLY_MODE_MANUAL, changes wait for apply().
	input_map.apply()
	# Autosave writes the file at the end of the frame; save() writes it now.
	input_map.save()
	# load() reads the file into the pending values; apply() pushes them to the InputMap.
	input_map.load()
	input_map.apply()

	# A rebind button
	var button := RebindButton.new()
	add_child(button)
	print("Button: ", button.text)
	button.pressed.emit()
	print("Button waiting: ", button.text)
	var pressed_key := InputEventKey.new()
	pressed_key.keycode = KEY_Q
	pressed_key.physical_keycode = KEY_Q
	pressed_key.pressed = true
	Input.parse_input_event(pressed_key)
	await get_tree().process_frame
	print("Button after the key: ", button.text)

	# Ask before taking a key away (the dialog is left out: it needs a window).

	input_map.reset_all()
	input_map.save()
	get_tree().quit()
