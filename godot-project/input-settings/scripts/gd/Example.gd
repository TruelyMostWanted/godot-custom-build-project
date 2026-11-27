extends Node
## Minimal example of Input Settings: the snippets of the wiki guide, in order.
## Wiki: https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Input-Settings


func _ready() -> void:
	# Using the API
	var input := InputSettingsServer

	# Set: sensitivity and inversion per device, and actions the player toggles instead of holding.
	input.set_sensitivity(InputSettingsServer.DEVICE_MOUSE, 1.5)
	input.set_y_inverted(InputSettingsServer.DEVICE_MOUSE, true)
	input.set_toggle_actions(["aim"])
	input.set_vibration_strength(0.5)

	# Get: the pending values.
	print(input.get_sensitivity(InputSettingsServer.DEVICE_MOUSE)) # 1.5
	print(input.is_y_inverted(InputSettingsServer.DEVICE_MOUSE)) # true

	# Use: your input code passes its input through the helpers.
	print(input.apply_to_mouse_motion(Vector2(10, 4))) # (15.0, -6.0)

	# In APPLY_MODE_MANUAL, changes wait for apply().
	input.apply()
	# Autosave writes the file at the end of the frame; save() writes it now.
	input.save()
	# load() reads the file into the pending values; apply() makes them the values in use.
	input.load()
	input.apply()

	# A mouse sensitivity slider
	var slider := SensitivitySlider.new()
	add_child(slider)
	print("Slider: ", slider.value)
	input.set_sensitivity(InputSettingsServer.DEVICE_MOUSE, 2.0)
	input.reset_to_defaults()
	print("Slider after the reset: ", slider.value)

	input.save()
	get_tree().quit()
