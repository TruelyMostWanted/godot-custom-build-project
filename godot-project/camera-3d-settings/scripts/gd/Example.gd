extends Node
## Minimal example of Camera 3D Settings: the snippets of the wiki guide, in order.
## Wiki: https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Camera-3D-Settings


func _ready() -> void:
	# Using the API: set, every Camera3D in the group camera_3d_settings gets the value at once.
	Camera3DSettingsServer.set_field_of_view(90.0)
	# Using the API: game-side values are only stored.
	Camera3DSettingsServer.set_shake_intensity(0.5)

	# Using the API: get the pending values.
	print(Camera3DSettingsServer.get_field_of_view())
	print(Camera3DSettingsServer.get_shake_intensity())

	# Using the API: apply, save and load.
	Camera3DSettingsServer.apply()
	Camera3DSettingsServer.save()
	Camera3DSettingsServer.load()
	Camera3DSettingsServer.apply()

	# A field of view slider: needs a player who drags it, so it is not run here.

	# Leave no player file behind.
	Camera3DSettingsServer.reset_to_defaults()
	Camera3DSettingsServer.save()
	get_tree().quit()
