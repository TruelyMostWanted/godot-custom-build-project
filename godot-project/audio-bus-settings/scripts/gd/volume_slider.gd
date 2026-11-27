class_name VolumeSlider
extends HSlider

## Name of the bus in the bus layout.
@export var bus: StringName = &"Master"


func _ready() -> void:
	min_value = 0.0
	max_value = 1.0
	step = 0.01
	value = AudioBusSettingsServer.get_bus_volume_linear(bus) # before connecting: no signal
	value_changed.connect(_on_value_changed)
	drag_ended.connect(_on_drag_ended)
	AudioBusSettingsServer.bus_volume_changed.connect(_on_bus_volume_changed)


func _on_value_changed(new_value: float) -> void:
	AudioBusSettingsServer.set_bus_volume_linear(bus, new_value)


func _on_drag_ended(_value_changed: bool) -> void:
	AudioBusSettingsServer.save()


func _on_bus_volume_changed(bus_name: StringName, volume_db: float) -> void:
	# Another slider, a reset or a loaded file changed the volume.
	if bus_name == bus:
		set_value_no_signal(db_to_linear(volume_db)) # no value_changed, no loop
