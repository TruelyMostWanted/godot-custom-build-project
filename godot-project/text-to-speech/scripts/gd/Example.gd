extends Node
## Minimal example of Text to Speech: the snippets of the wiki guide, in order.
## Wiki: https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Text-to-Speech
## Headless runs have no speech: speak() would print an error, so every speak() is behind is_available().

const RES := "res://text-to-speech/resources/"


func _ready() -> void:
	# Give characters their own voice.
	TTSServer.add_persona(load(RES + "narrator.tres"))
	TTSServer.add_persona(load(RES + "guard.tres"))
	TTSServer.set_default_persona("narrator")
	if TTSServer.is_available():
		TTSServer.speak("Welcome!") # narrator
		TTSServer.speak("Halt!", TTSServer.get_persona("guard")) # guard
	print(TTSServer.get_persona_names()) # ["narrator", "guard"]

	# Speak a line.
	if not TTSServer.is_available():
		push_warning("Text to speech is off or not supported here.")
	var narrator := TTSPersona.new()
	narrator.persona_name = "narrator"
	narrator.language = "en"
	narrator.volume = 70
	narrator.rate = 0.9
	ResourceSaver.save(narrator, "user://narrator.tres")
	var id: int = -1
	if TTSServer.is_available():
		id = TTSServer.speak("Once upon a time...", narrator)
	if id == -1:
		print("Not spoken.")

	# Choose a voice.
	for voice: Dictionary in TTSServer.get_voices("de"):
		print(voice["name"], " (", voice["language"], ")")
	var guard := TTSPersona.new()
	guard.voice = "Microsoft Hedda Desktop" # Missing on Linux ...
	guard.language = "de" # ... so a German voice is used there.
	print(TTSServer.resolve_voice_id(guard))

	# Highlight the spoken word.
	TTSServer.utterance_boundary.connect(_on_boundary)
	TTSServer.utterance_ended.connect(func(utterance_id: int) -> void: print("Done: ", utterance_id))

	# Lower the music while speaking (the mixer channel "music" is created first).
	var channel := AudioMixerChannel.new()
	channel.channel_name = &"music"
	AudioMixerServer.add_channel(channel)
	var music: AudioMixerChannel = AudioMixerServer.get_channel(&"music")
	music.duck_on_tts = true
	music.duck_volume_db = -12.0
	if TTSServer.is_available():
		TTSServer.speak("The bridge is out. Take the north road.")
	print("Music ducks on speech: ", AudioMixerServer.get_channel(&"music").duck_on_tts)

	# Let the player change the voices.
	TTSServer.add_persona(narrator) # The game's persona.
	TTSPersonaSettingsServer.set_persona_value(&"narrator", &"rate", 1.5) # The player's choice.
	if TTSServer.is_available():
		TTSServer.speak_with_persona("Welcome!", &"narrator") # Rate 1.5.
		TTSServer.speak_with_persona("Halt!", &"unknown") # Warning, default persona.
	print("Player's rate: ", TTSServer.get_persona(&"narrator").rate, ", game's rate: ", TTSServer.get_base_persona(&"narrator").rate)

	# Clean up: the player's change is saved with the settings.
	TTSPersonaSettingsServer.remove_persona(&"narrator")
	TTSServer.clear_personas()
	await get_tree().process_frame
	get_tree().quit()


func _on_boundary(_id: int, char_pos: int) -> void:
	$Subtitle.visible_characters = char_pos
