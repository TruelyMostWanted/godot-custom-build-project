class_name OutputDeviceButton
extends OptionButton


func _ready() -> void:
	# Android, iOS and the Web only have "Default": the system or browser picks the device.
	disabled = not AudioDeviceSettingsServer.is_device_selection_supported()
	for device: String in AudioServer.get_output_device_list():
		add_item(device)
	_select_device(AudioDeviceSettingsServer.get_output_device())
	item_selected.connect(_on_item_selected)
	AudioDeviceSettingsServer.output_device_changed.connect(_select_device)


func _on_item_selected(index: int) -> void:
	AudioDeviceSettingsServer.set_output_device(get_item_text(index))
	AudioDeviceSettingsServer.save()


func _select_device(device: String) -> void:
	# Also called when a reset or a loaded file changed the device.
	for i in item_count:
		if get_item_text(i) == device:
			select(i) # select() doesn't emit item_selected: no loop
			return
