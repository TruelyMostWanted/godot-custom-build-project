class_name SensitivitySlider
extends HSlider


func _ready() -> void:
	min_value = 0.1
	max_value = 5.0
	step = 0.1
	value = InputSettingsServer.get_sensitivity(InputSettingsServer.DEVICE_MOUSE) # before connecting: no signal
	value_changed.connect(_on_value_changed)
	drag_ended.connect(_on_drag_ended)
	InputSettingsServer.mouse_sensitivity_changed.connect(_on_mouse_sensitivity_changed)


func _on_value_changed(new_value: float) -> void:
	InputSettingsServer.set_sensitivity(InputSettingsServer.DEVICE_MOUSE, new_value)


func _on_drag_ended(_value_changed: bool) -> void:
	InputSettingsServer.save()


func _on_mouse_sensitivity_changed(sensitivity: float) -> void:
	# A reset or a loaded file changed the sensitivity.
	set_value_no_signal(sensitivity) # no value_changed, no loop
