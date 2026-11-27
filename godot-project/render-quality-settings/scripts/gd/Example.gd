extends Node
## Minimal example of Render Quality Settings: the snippets of the wiki guide, in order.
## Wiki: https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Render-Quality-Settings


func _ready() -> void:
	# Using the API: set, pushed to the root viewport at once.
	RenderQualitySettingsServer.set_scaling_3d_scale(0.75)
	RenderQualitySettingsServer.set_msaa_3d(Viewport.MSAA_4X)
	RenderQualitySettingsServer.set_screen_space_aa(Viewport.SCREEN_SPACE_AA_FXAA)

	# Using the API: get the pending values.
	print(RenderQualitySettingsServer.get_scaling_3d_scale())
	print(RenderQualitySettingsServer.get_msaa_3d())

	# Using the API: values the renderer can't use are stored but not applied.
	print(RenderQualitySettingsServer.is_value_supported(&"use_taa"))

	# Using the API: apply, save and load.
	RenderQualitySettingsServer.apply()
	RenderQualitySettingsServer.save()
	RenderQualitySettingsServer.load()
	RenderQualitySettingsServer.apply()

	# A render scale slider: needs a player who drags it, so it is not run here.

	# Leave no player file behind.
	RenderQualitySettingsServer.reset_to_defaults()
	RenderQualitySettingsServer.save()
	get_tree().quit()
