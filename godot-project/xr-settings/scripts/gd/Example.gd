extends Node
## Minimal example of XR Settings: the snippets of the wiki guide, in order.
## Wiki: https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/XR-Settings


func _ready() -> void:
	# Using the API: set, pushed to the OpenXR interface at once, or stored until the interface starts.
	XRSettingsServer.set_foveation_level(XRSettings.FOVEATION_LEVEL_MEDIUM)
	XRSettingsServer.set_render_target_size_multiplier(1.5)

	# Using the API: get the pending values.
	print(XRSettingsServer.get_foveation_level())
	print(XRSettingsServer.get_render_target_size_multiplier())
	# Using the API: without a headset, these values wait for the OpenXR interface.
	print(XRSettingsServer.get_deferred_setting_names())

	# Using the API: apply, save and load.
	XRSettingsServer.apply()
	XRSettingsServer.save()
	XRSettingsServer.load()
	XRSettingsServer.apply()

	# A foveation option: needs a player who picks it, so it is not run here.

	# Leave no player file behind.
	XRSettingsServer.reset_to_defaults()
	XRSettingsServer.save()
	get_tree().quit()
