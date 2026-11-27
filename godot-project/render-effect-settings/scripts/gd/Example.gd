extends Node
## Minimal example of Render Effect Settings: the snippets of the wiki guide, in order.
## Wiki: https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Render-Effect-Settings


func _ready() -> void:
	# Using the API: set, applied to every environment at once.
	RenderEffectSettingsServer.set_allow_volumetric_fog(false)
	RenderEffectSettingsServer.set_ssao_quality(RenderingServer.ENV_SSAO_QUALITY_HIGH)
	RenderEffectSettingsServer.set_brightness(1.2)

	# Using the API: get the pending values.
	print(RenderEffectSettingsServer.is_volumetric_fog_allowed())
	print(RenderEffectSettingsServer.get_ssao_quality())

	# Using the API: apply, save and load.
	RenderEffectSettingsServer.apply()
	RenderEffectSettingsServer.save()
	RenderEffectSettingsServer.load()
	RenderEffectSettingsServer.apply()

	# An SSAO check box: needs a player who clicks it, so it is not run here.

	# Leave no player file behind.
	RenderEffectSettingsServer.reset_to_defaults()
	RenderEffectSettingsServer.save()
	get_tree().quit()
