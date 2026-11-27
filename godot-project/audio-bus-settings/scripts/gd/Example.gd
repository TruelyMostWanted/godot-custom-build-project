extends Node
## Minimal example of Audio Bus Settings: the snippets of the wiki guide, in order.
## Wiki: https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Audio-Bus-Settings


func _ready() -> void:
	# Using the API
	var buses := AudioBusSettingsServer

	# Set: applied to the AudioServer at once (APPLY_MODE_IMMEDIATE, the default).
	buses.set_bus_volume_db(&"Music", -12.0)
	buses.set_bus_volume_linear(&"Sfx", 0.5)
	buses.set_bus_mute(&"Sfx", true)

	# Get: the pending values.
	print(buses.get_bus_volume_db(&"Music")) # -12.0
	print(buses.is_bus_mute(&"Sfx")) # true

	# In APPLY_MODE_MANUAL, changes wait for apply().
	buses.apply()
	# Autosave writes the file at the end of the frame; save() writes it now.
	buses.save()
	# load() reads the file into the pending values; apply() pushes them to the AudioServer.
	buses.load()
	buses.apply()

	# Presets (the preset is a resource of this folder)
	var night_mode := load("res://audio-bus-settings/resources/night_mode_preset.tres") as AudioBusListSettings
	AudioBusSettingsServer.apply_preset(night_mode)
	print(AudioBusSettingsServer.get_bus_volume_db(&"Music")) # -18.0
	print(AudioBusSettingsServer.get_current_preset() == night_mode) # true
	AudioBusSettingsServer.set_bus_volume_db(&"Music", -10.0)
	print(AudioBusSettingsServer.is_custom()) # true
	print(AudioBusSettingsServer.matches_preset(night_mode)) # false

	# A volume slider
	var slider := VolumeSlider.new()
	slider.bus = &"Music"
	add_child(slider)
	print("Slider: ", slider.value)
	AudioBusSettingsServer.set_bus_volume_linear(&"Music", 0.5)
	print("Slider after the change: ", slider.value)

	buses.reset_to_defaults()
	buses.save()
	get_tree().quit()
