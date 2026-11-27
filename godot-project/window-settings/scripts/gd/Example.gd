extends Node
## Minimal example of Window Settings: the snippets of the wiki guide, in order.
## Wiki: https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Window-Settings


func _ready() -> void:
	# Using the API: set the values, mode and size wait for confirm_changes().
	WindowSettingsServer.set_window_mode(DisplayServer.WINDOW_MODE_FULLSCREEN)
	WindowSettingsServer.set_window_size(Vector2i(1920, 1080))
	WindowSettingsServer.set_vsync_mode(DisplayServer.VSYNC_ADAPTIVE)
	WindowSettingsServer.set_content_scale_factor(1.25)

	# Using the API: get the pending values.
	print(WindowSettingsServer.get_window_mode())
	print(WindowSettingsServer.get_window_size())

	# Using the API: keep, apply, save and load.
	WindowSettingsServer.confirm_changes()
	WindowSettingsServer.apply()
	WindowSettingsServer.save()
	WindowSettingsServer.load()
	WindowSettingsServer.apply()

	# A window mode button: needs a window and a player who clicks, so it is not run here.

	# Leave no player file behind.
	WindowSettingsServer.reset_to_defaults()
	WindowSettingsServer.confirm_changes()
	WindowSettingsServer.save()
	get_tree().quit()
