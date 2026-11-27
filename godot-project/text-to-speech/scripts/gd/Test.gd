extends FeatureTest
## Full API test of Text to Speech (TTSServer, TTSPersona). Same tests as scripts/cs/Test.cs.
## Most tests work without voices (headless, or with "audio/general/text_to_speech" off); the events are simulated
## through DisplayServer like the OS callbacks. Real speech is only tested when TTSServer.is_available() and the system has voices.

const SPEECH_TIMEOUT_MSEC := 15000

var _events: Array = []


func _run_tests() -> void:
	TTSServer.clear_personas()
	TTSServer.utterance_started.connect(func(id: int) -> void: _events.append(["started", id]))
	TTSServer.utterance_ended.connect(func(id: int) -> void: _events.append(["ended", id]))
	TTSServer.utterance_canceled.connect(func(id: int) -> void: _events.append(["canceled", id]))
	TTSServer.utterance_boundary.connect(func(id: int, char_pos: int) -> void: _events.append(["boundary", id, char_pos]))
	_test_persona_values()
	_test_persona_save_load()
	_test_persona_list()
	_test_persona_overrides()
	_test_unavailable()
	await _test_signals()
	await _test_real_speech()
	TTSServer.clear_personas()


func _test_persona_values() -> void:
	var persona := TTSPersona.new()
	expect_equal(persona.persona_name, &"", "persona_name is empty by default")
	expect_equal(persona.voice, "", "voice is empty by default")
	expect_equal(persona.language, "", "language is empty by default")
	expect_equal(persona.volume, 50, "volume is 50 by default")
	expect_equal(persona.pitch, 1.0, "pitch is 1 by default")
	expect_equal(persona.rate, 1.0, "rate is 1 by default")
	expect(not persona.interrupt, "interrupt is false by default")
	persona.volume = 250
	expect_equal(persona.volume, 100, "volume is clamped to 100")
	persona.volume = -3
	expect_equal(persona.volume, 0, "volume is clamped to 0")
	persona.pitch = 9.0
	expect_equal(persona.pitch, 2.0, "pitch is clamped to 2")
	persona.rate = 0.0
	expect(is_equal_approx(persona.rate, 0.1), "rate is clamped to 0.1")
	persona.rate = 99.0
	expect_equal(persona.rate, 10.0, "rate is clamped to 10")
	var changed: Array[bool] = []
	persona.changed.connect(func() -> void: changed.append(true))
	persona.language = "de"
	expect_equal(changed, [true], "setting a property emits changed")


func _test_persona_save_load() -> void:
	var persona: TTSPersona = _persona("narrator", "en_US")
	persona.voice = "Some Voice"
	persona.volume = 80
	persona.pitch = 1.25
	persona.rate = 0.75
	persona.interrupt = true
	var path := "user://text_to_speech_narrator.tres"
	expect_equal(ResourceSaver.save(persona, path), OK, "a persona saves as .tres")
	var loaded: TTSPersona = ResourceLoader.load(path, "", ResourceLoader.CACHE_MODE_IGNORE)
	expect_equal(loaded.persona_name, &"narrator", "persona_name survives saving")
	expect_equal(loaded.voice, "Some Voice", "voice survives saving")
	expect_equal(loaded.language, "en_US", "language survives saving")
	expect_equal(loaded.volume, 80, "volume survives saving")
	expect_equal(loaded.pitch, 1.25, "pitch survives saving")
	expect_equal(loaded.rate, 0.75, "rate survives saving")
	expect(loaded.interrupt, "interrupt survives saving")


func _test_persona_list() -> void:
	var builtin: TTSPersona = TTSServer.get_default_persona()
	expect(builtin != null, "a built-in default persona exists")
	expect_equal(TTSServer.get_default_persona_name(), &"", "there is no default persona name at the start")
	var narrator: TTSPersona = _persona("narrator", "en")
	var guard: TTSPersona = _persona("guard", "de")
	TTSServer.add_persona(narrator)
	TTSServer.add_persona(guard)
	expect(TTSServer.has_persona("guard"), "has_persona finds an added persona")
	expect(not TTSServer.has_persona("king"), "has_persona is false for an unknown name")
	expect(TTSServer.get_persona("narrator") == narrator, "get_persona returns the added persona")
	expect(TTSServer.get_persona("king") == null, "get_persona returns null for an unknown name")
	expect_equal(TTSServer.get_personas(), [narrator, guard], "get_personas returns the personas in order")
	expect_equal(TTSServer.get_persona_names(), PackedStringArray(["narrator", "guard"]), "get_persona_names keeps the order")
	TTSServer.add_persona(null)
	TTSServer.add_persona(_persona("", ""))
	expect_equal(TTSServer.get_personas().size(), 2, "a null persona or one without a name is not added")
	TTSServer.set_default_persona("king")
	expect_equal(TTSServer.get_default_persona_name(), &"", "an unknown default persona is rejected")
	TTSServer.set_default_persona("guard")
	expect_equal(TTSServer.get_default_persona_name(), &"guard", "set_default_persona sets the name")
	expect(TTSServer.get_default_persona() == guard, "get_default_persona returns that persona")
	var guard2: TTSPersona = _persona("guard", "fr")
	TTSServer.add_persona(guard2)
	expect_equal(TTSServer.get_personas().size(), 2, "a persona with the same name replaces the old one")
	expect(TTSServer.get_default_persona() == guard2, "the default persona follows the replacement")
	TTSServer.remove_persona("guard")
	expect(not TTSServer.has_persona("guard"), "remove_persona removes the persona")
	expect(TTSServer.get_default_persona() == builtin, "removing the default persona brings back the built-in one")
	TTSServer.clear_personas()
	expect(TTSServer.get_personas().is_empty(), "clear_personas removes all personas")


func _test_persona_overrides() -> void:
	var narrator: TTSPersona = _persona("narrator", "en")
	TTSServer.add_persona(narrator)
	var faster: TTSPersona = _persona("narrator", "en")
	faster.rate = 1.5
	TTSServer.set_persona_override(faster)
	expect(TTSServer.has_persona_override("narrator"), "has_persona_override finds the override")
	expect(TTSServer.get_persona_override("narrator") == faster, "get_persona_override returns the override")
	expect(TTSServer.get_persona("narrator") == faster, "get_persona returns the override first")
	expect(TTSServer.get_base_persona("narrator") == narrator, "get_base_persona returns the persona of the game")
	var own: TTSPersona = _persona("own", "de")
	TTSServer.set_persona_override(own)
	expect_equal(TTSServer.get_persona_overrides(), [faster, own], "get_persona_overrides returns the overrides in order")
	TTSServer.set_default_persona_override(&"own")
	expect_equal(TTSServer.get_default_persona_override(), &"own", "get_default_persona_override returns the player's default")
	expect(TTSServer.get_default_persona() == own, "the player's default persona comes first")
	TTSServer.remove_persona_override("narrator")
	expect(TTSServer.get_persona("narrator") == narrator, "remove_persona_override brings back the persona of the game")
	TTSServer.clear_persona_overrides()
	expect(TTSServer.get_persona_overrides().is_empty(), "clear_persona_overrides removes all overrides")
	expect_equal(TTSServer.get_default_persona_override(), &"", "clear_persona_overrides removes the player's default")
	TTSServer.clear_personas()


func _test_unavailable() -> void:
	if TTSServer.is_available():
		expect(DisplayServer.has_feature(DisplayServer.FEATURE_TEXT_TO_SPEECH), "is_available needs a display server with speech")
		return
	expect(TTSServer.get_voices().is_empty(), "get_voices is empty when unavailable")
	expect(not TTSServer.is_speaking(), "is_speaking is false when unavailable")
	TTSServer.pause()
	expect(not TTSServer.is_paused(), "is_paused stays false when unavailable")
	TTSServer.resume()
	TTSServer.stop()
	expect_equal(TTSServer.speak("Hello"), -1, "speak returns -1 when unavailable")
	expect_equal(TTSServer.speak_with_persona("Hello", &"unknown"), -1, "speak_with_persona returns -1 when unavailable")
	expect(TTSServer.get_pending_utterances().is_empty(), "nothing is pending when unavailable")
	var persona: TTSPersona = _persona("x", "en")
	persona.voice = "Some Voice"
	expect_equal(TTSServer.resolve_voice_id(persona), "Some Voice", "without voices, resolve_voice_id passes the voice on")


func _test_signals() -> void:
	_events.clear()
	DisplayServer.call("_tts_post_utterance_event", DisplayServer.TTS_UTTERANCE_STARTED, 9001, 0)
	DisplayServer.call("_tts_post_utterance_event", DisplayServer.TTS_UTTERANCE_BOUNDARY, 9001, 4)
	DisplayServer.call("_tts_post_utterance_event", DisplayServer.TTS_UTTERANCE_ENDED, 9001, 0)
	DisplayServer.call("_tts_post_utterance_event", DisplayServer.TTS_UTTERANCE_CANCELED, 9002, 0)
	expect(_events.is_empty(), "the utterance signals are deferred to the main thread")
	await get_tree().process_frame
	await get_tree().process_frame
	expect_equal(_events, [["started", 9001], ["boundary", 9001, 4], ["ended", 9001], ["canceled", 9002]], "start, boundary, end and cancel arrive in order")


func _test_real_speech() -> void:
	if not TTSServer.is_available() or TTSServer.get_voices().is_empty():
		print("  SKIP  real speech: text to speech is not available or the system has no voices")
		return
	var first: Dictionary = TTSServer.get_voices()[0]
	var first_id: String = first["id"]
	var language: String = first["language"]
	expect(not TTSServer.get_voices(language).is_empty(), "get_voices filters by language")
	var by_id: TTSPersona = _persona("x", "")
	by_id.voice = first_id
	expect_equal(TTSServer.resolve_voice_id(by_id), first_id, "resolve_voice_id keeps an existing voice ID")
	var by_name: TTSPersona = _persona("x", "")
	by_name.voice = first["name"]
	expect_equal(TTSServer.resolve_voice_id(by_name), first_id, "resolve_voice_id finds a voice by name")
	var missing: TTSPersona = _persona("missing", language)
	missing.voice = "voice-that-does-not-exist"
	missing.volume = 10
	expect(TTSServer.resolve_voice_id(missing) != "", "a missing voice falls back to a voice of the language")
	var music := AudioMixerChannel.new()
	music.channel_name = &"music"
	music.duck_on_tts = true
	AudioMixerServer.add_channel(music)
	_events.clear()
	var id: int = TTSServer.speak("Hello", missing)
	expect(TTSServer.get_pending_utterances().has(id), "speak returns a pending utterance ID")
	await _wait_for_event(["started"], id)
	expect(AudioMixerServer.is_ducked(&"music"), "real speech ducks a mixer channel with duck_on_tts")
	await _wait_for_event(["ended", "canceled"], id)
	expect(_events.has(["ended", id]), "utterance_ended arrives for real speech")
	await get_tree().create_timer(0.1).timeout
	expect(not AudioMixerServer.is_ducked(&"music"), "the channel is restored after real speech")
	var long_id: int = TTSServer.speak("This is a long sentence that is stopped before it ends.", missing)
	await get_tree().create_timer(0.3).timeout
	TTSServer.stop()
	await _wait_for_event(["canceled", "ended"], long_id)
	expect(_events.has(["canceled", long_id]), "stop cancels the current utterance")
	AudioMixerServer.remove_channel(&"music")


func _wait_for_event(event_names: Array[String], id: int) -> void:
	var start: int = Time.get_ticks_msec()
	while Time.get_ticks_msec() - start < SPEECH_TIMEOUT_MSEC:
		await get_tree().process_frame
		for event_name: String in event_names:
			if _events.has([event_name, id]):
				return


func _persona(persona_name: String, language: String) -> TTSPersona:
	var persona := TTSPersona.new()
	persona.persona_name = persona_name
	persona.language = language
	return persona
