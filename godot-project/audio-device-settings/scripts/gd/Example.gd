extends Node
## Minimal example of Audio Device Settings: the snippets of the wiki guide, in order.
## Wiki: https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Audio-Device-Settings


func _ready() -> void:
	# Using the API
	var devices := AudioDeviceSettingsServer

	# Only some drivers can switch the device; elsewhere it stays "Default".
	if AudioDeviceSettingsServer.is_device_selection_supported():
		# Set: a name from AudioServer.get_output_device_list(); "Default" follows the system.
		devices.set_output_device(AudioServer.get_output_device_list()[0])
		devices.set_input_device("Default")

	# Get: the pending values.
	print(devices.get_output_device()) # Default
	print(devices.get_input_device()) # Default

	# In APPLY_MODE_MANUAL, changes wait for apply().
	devices.apply()
	# Autosave writes the file at the end of the frame; save() writes it now.
	devices.save()
	# load() reads the file into the pending values; apply() pushes them to the AudioServer.
	devices.load()
	devices.apply()

	# An output device button
	var button := OutputDeviceButton.new()
	add_child(button)
	print("Items: ", button.item_count)
	print("Disabled: ", button.disabled)

	get_tree().quit()
