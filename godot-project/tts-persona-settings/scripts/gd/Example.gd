extends Node
## Minimal example of TTS Persona Settings: the snippets of the wiki guide, in order.
## Wiki: https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/TTS-Persona-Settings


func _ready() -> void:
	# The personas of the game: add them to TTSServer as usual.
	var narrator := TTSPersona.new()
	narrator.persona_name = &"narrator"
	TTSServer.add_persona(narrator)

	# Using the API: set the value of a game persona, and the default persona of the player.
	TTSPersonaSettingsServer.set_persona_value(&"narrator", &"rate", 1.5)
	TTSPersonaSettingsServer.set_default_persona(&"narrator")

	# Using the API: get the value the player hears, and the persona TTSServer uses.
	print(TTSPersonaSettingsServer.get_persona_value(&"narrator", &"rate"))
	print(TTSServer.get_persona(&"narrator").rate)
	print(TTSServer.get_base_persona(&"narrator").rate)
	print(TTSPersonaSettingsServer.get_default_persona())

	# Using the API: apply, save and load.
	TTSPersonaSettingsServer.apply()
	TTSPersonaSettingsServer.save()
	TTSPersonaSettingsServer.load()
	TTSPersonaSettingsServer.apply()

	# A persona button: needs a player who picks a persona, so it is not run here.

	# Leave no player file behind.
	TTSPersonaSettingsServer.reset_to_defaults()
	TTSPersonaSettingsServer.save()
	TTSServer.clear_personas()
	get_tree().quit()
