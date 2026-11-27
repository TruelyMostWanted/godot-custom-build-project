extends Node
## Minimal example of Accessibility Settings: the snippets of the wiki guide, in order.
## Wiki: https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Accessibility-Settings


func _ready() -> void:
	# Using the API: set, the engine applies these to the root window at once.
	AccessibilitySettingsServer.set_ui_scale(1.25)
	AccessibilitySettingsServer.set_font_size_factor(1.5)
	AccessibilitySettingsServer.set_color_scheme(AccessibilitySettingsServer.COLOR_SCHEME_HIGH_CONTRAST_DARK)
	# Using the API: your game reads this one.
	AccessibilitySettingsServer.set_reduce_motion(true)

	# Using the API: get the pending values, and the root window that uses them.
	print(AccessibilitySettingsServer.get_ui_scale())
	print(get_tree().root.content_scale_factor)
	print(AccessibilitySettingsServer.get_animation_duration(0.4))

	# Using the API: apply, save and load.
	AccessibilitySettingsServer.apply()
	AccessibilitySettingsServer.save()
	AccessibilitySettingsServer.load()
	AccessibilitySettingsServer.apply()

	# A UI scale slider: needs a player who drags it, so it is not run here.

	# Leave no player file behind.
	AccessibilitySettingsServer.reset_to_defaults()
	AccessibilitySettingsServer.save()
	get_tree().quit()
