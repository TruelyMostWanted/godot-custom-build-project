extends Node
## Started as a separate process by Test.gd: prints the personas that the TTS persona settings domain
## applied to TTSServer before the main scene was ready, then quits.


func _ready() -> void:
	var names := []
	for persona: TTSPersona in TTSServer.get_personas():
		names.append(String(persona.persona_name))
	var helper := TTSServer.get_persona(&"helper")
	var probe := {
		"started": TTSPersonaSettingsServer.is_started(),
		"persona_names": names,
		"helper_rate": helper.rate if helper != null else 0.0,
		"default_persona": String(TTSServer.get_default_persona_name()),
	}
	print("PROBE " + JSON.stringify(probe))
	get_tree().quit(0)
