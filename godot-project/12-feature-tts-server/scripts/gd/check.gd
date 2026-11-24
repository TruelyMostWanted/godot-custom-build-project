extends FeatureCheck
## GDScript checks of 12-feature-tts-server. Check the same things as scripts/cs/Issue12FeatureTtsServerCheck.cs.
##
## Most checks work without voices (headless, or with "audio/general/text_to_speech" off).
## Real speech is only checked when TTSServer.is_available() and the system has voices.

const DIR := "user://12-feature-tts-server"
const SPEECH_TIMEOUT_MSEC := 15000

var _events: Array = []


func _get_check_name() -> String:
	return "12-feature-tts-server"


func _on_started(id: int) -> void:
	_events.append(["started", id])


func _on_ended(id: int) -> void:
	_events.append(["ended", id])


func _on_canceled(id: int) -> void:
	_events.append(["canceled", id])


func _on_boundary(id: int, char_pos: int) -> void:
	_events.append(["boundary", id, char_pos])


func _make_persona(persona_name: String, voice: String, language: String) -> TTSPersona:
	var persona := TTSPersona.new()
	persona.persona_name = persona_name
	persona.voice = voice
	persona.language = language
	return persona


static func _base_language(language: String) -> String:
	return language.replace("-", "_").to_lower().get_slice("_", 0)


func _run_checks() -> void:
	expect(Engine.has_singleton("TTSServer"), "TTSServer is a registered singleton")
	expect(ClassDB.class_exists("TTSPersona"), "TTSPersona is a registered class")
	expect(not ClassDB.can_instantiate("TTSServer"), "TTSServer cannot be instantiated")
	TTSServer.clear_personas()

	_check_persona_values()
	_check_persona_save_load()
	_check_persona_list()
	_check_availability()
	await _check_signals()
	await _check_real_speech()

	TTSServer.clear_personas()
	DirAccess.remove_absolute(DIR.path_join("narrator.tres"))
	DirAccess.remove_absolute(DIR)


func _check_persona_values() -> void:
	var persona := TTSPersona.new()
	expect_equal(persona.persona_name, &"", "default persona_name is empty")
	expect_equal(persona.voice, "", "default voice is empty")
	expect_equal(persona.language, "", "default language is empty")
	expect_equal(persona.volume, 50, "default volume is 50")
	expect(is_equal_approx(persona.pitch, 1.0), "default pitch is 1.0")
	expect(is_equal_approx(persona.rate, 1.0), "default rate is 1.0")
	expect(not persona.interrupt, "default interrupt is false")

	persona.volume = 250
	expect_equal(persona.volume, 100, "volume is clamped to 100")
	persona.volume = -3
	expect_equal(persona.volume, 0, "volume is clamped to 0")
	persona.pitch = 9.0
	expect(is_equal_approx(persona.pitch, 2.0), "pitch is clamped to 2.0")
	persona.rate = 0.0
	expect(is_equal_approx(persona.rate, 0.1), "rate is clamped to 0.1")
	persona.rate = 99.0
	expect(is_equal_approx(persona.rate, 10.0), "rate is clamped to 10.0")

	var changed := [false]
	persona.changed.connect(func() -> void: changed[0] = true)
	persona.language = "de"
	expect(changed[0], "setting a property emits changed")


func _check_persona_save_load() -> void:
	DirAccess.make_dir_recursive_absolute(DIR)
	var persona := _make_persona("narrator", "Some Voice", "en_US")
	persona.volume = 80
	persona.pitch = 1.25
	persona.rate = 0.75
	persona.interrupt = true
	var path := DIR.path_join("narrator.tres")
	expect_equal(ResourceSaver.save(persona, path), OK, "persona saves as .tres")

	var loaded := ResourceLoader.load(path, "", ResourceLoader.CACHE_MODE_IGNORE) as TTSPersona
	expect(loaded != null, "persona loads again as TTSPersona")
	if loaded == null:
		return
	expect(loaded != persona, "loaded persona is a new object")
	expect_equal(loaded.persona_name, &"narrator", "loaded persona_name")
	expect_equal(loaded.voice, "Some Voice", "loaded voice")
	expect_equal(loaded.language, "en_US", "loaded language")
	expect_equal(loaded.volume, 80, "loaded volume")
	expect(is_equal_approx(loaded.pitch, 1.25), "loaded pitch")
	expect(is_equal_approx(loaded.rate, 0.75), "loaded rate")
	expect(loaded.interrupt, "loaded interrupt")


func _check_persona_list() -> void:
	var builtin := TTSServer.get_default_persona()
	expect(builtin != null, "a built-in default persona exists")
	expect_equal(TTSServer.get_default_persona_name(), "", "no default persona name at start")

	var narrator := _make_persona("narrator", "", "en")
	var guard := _make_persona("guard", "", "de")
	TTSServer.add_persona(narrator)
	TTSServer.add_persona(guard)
	expect(TTSServer.has_persona("narrator") and TTSServer.has_persona("guard"), "has_persona knows both personas")
	expect(not TTSServer.has_persona("king"), "has_persona is false for an unknown name")
	expect(TTSServer.get_persona("narrator") == narrator, "get_persona returns the added persona")
	expect(TTSServer.get_persona("king") == null, "get_persona returns null for an unknown name")
	expect_equal(TTSServer.get_personas().size(), 2, "get_personas returns both personas")
	expect_equal(Array(TTSServer.get_persona_names()), ["narrator", "guard"], "get_persona_names keeps the order")

	# Errors are printed, nothing crashes.
	TTSServer.add_persona(null)
	TTSServer.add_persona(_make_persona("", "", ""))
	TTSServer.set_default_persona("king")
	expect_equal(TTSServer.get_personas().size(), 2, "invalid personas are not added")
	expect_equal(TTSServer.get_default_persona_name(), "", "an unknown default persona is rejected")

	TTSServer.set_default_persona("guard")
	expect_equal(TTSServer.get_default_persona_name(), "guard", "set_default_persona sets the name")
	expect(TTSServer.get_default_persona() == guard, "get_default_persona returns the persona")

	var guard2 := _make_persona("guard", "", "fr")
	TTSServer.add_persona(guard2)
	expect_equal(TTSServer.get_personas().size(), 2, "a persona with the same name replaces the old one")
	expect(TTSServer.get_default_persona() == guard2, "the default persona follows the replacement")

	TTSServer.remove_persona("guard")
	expect(not TTSServer.has_persona("guard"), "remove_persona removes the persona")
	expect_equal(TTSServer.get_default_persona_name(), "", "removing the default persona resets the default")
	expect(TTSServer.get_default_persona() == builtin, "the built-in persona is the default again")
	TTSServer.remove_persona("does_not_exist")

	TTSServer.clear_personas()
	expect(TTSServer.get_personas().is_empty(), "clear_personas removes all personas")


func _check_availability() -> void:
	var enabled: bool = ProjectSettings.get_setting("audio/general/text_to_speech", false)
	if enabled and DisplayServer.has_feature(DisplayServer.FEATURE_TEXT_TO_SPEECH):
		expect(TTSServer.is_available(), "is_available is true with the setting and a TTS display server")
		return
	expect(not TTSServer.is_available(), "is_available is false without the setting or TTS support")
	expect(TTSServer.get_voices().is_empty(), "get_voices is empty when unavailable")
	expect(not TTSServer.is_speaking(), "is_speaking is false when unavailable")
	expect(not TTSServer.is_paused(), "is_paused is false when unavailable")
	TTSServer.pause()
	TTSServer.resume()
	TTSServer.stop()
	# A clear error, no crash.
	expect_equal(TTSServer.speak("Hello"), -1, "speak returns -1 when unavailable")
	expect_equal(TTSServer.speak("Hello", _make_persona("x", "Voice", "en")), -1, "speak with a persona returns -1 when unavailable")
	expect(TTSServer.get_pending_utterances().is_empty(), "nothing is pending when unavailable")
	expect_equal(TTSServer.resolve_voice_id(_make_persona("x", "Some Voice", "en")), "Some Voice", "without voices, resolve_voice_id passes the voice on")


func _connect_signals() -> void:
	_events.clear()
	TTSServer.utterance_started.connect(_on_started)
	TTSServer.utterance_ended.connect(_on_ended)
	TTSServer.utterance_canceled.connect(_on_canceled)
	TTSServer.utterance_boundary.connect(_on_boundary)


func _disconnect_signals() -> void:
	TTSServer.utterance_started.disconnect(_on_started)
	TTSServer.utterance_ended.disconnect(_on_ended)
	TTSServer.utterance_canceled.disconnect(_on_canceled)
	TTSServer.utterance_boundary.disconnect(_on_boundary)


func _check_signals() -> void:
	# Simulate the platform: the events go through DisplayServer, like the OS callbacks.
	_connect_signals()
	DisplayServer.call("_tts_post_utterance_event", DisplayServer.TTS_UTTERANCE_STARTED, 9001, 0)
	DisplayServer.call("_tts_post_utterance_event", DisplayServer.TTS_UTTERANCE_BOUNDARY, 9001, 4)
	DisplayServer.call("_tts_post_utterance_event", DisplayServer.TTS_UTTERANCE_ENDED, 9001, 0)
	DisplayServer.call("_tts_post_utterance_event", DisplayServer.TTS_UTTERANCE_CANCELED, 9002, 0)
	expect(_events.is_empty(), "signals are deferred, not emitted right away")
	await get_tree().process_frame
	await get_tree().process_frame
	expect_equal(_events, [["started", 9001], ["boundary", 9001, 4], ["ended", 9001], ["canceled", 9002]], "start, boundary, end and cancel arrive in order")
	_disconnect_signals()


func _wait_for_event(event_names: Array, id: int) -> void:
	var start := Time.get_ticks_msec()
	while Time.get_ticks_msec() - start < SPEECH_TIMEOUT_MSEC:
		await get_tree().process_frame
		for event_name: String in event_names:
			if _events.has([event_name, id]):
				return


func _check_real_speech() -> void:
	if not TTSServer.is_available():
		print("  SKIP  real speech: text to speech is not available (enable audio/general/text_to_speech)")
		return
	var voices := TTSServer.get_voices()
	if voices.is_empty():
		print("  SKIP  real speech: the system has no voices")
		return

	var first: Dictionary = voices[0]
	var first_id: String = first["id"]
	var first_name: String = first["name"]
	var language: String = first["language"]
	expect(not TTSServer.get_voices(language).is_empty(), "get_voices filters by language")
	expect_equal(TTSServer.resolve_voice_id(_make_persona("x", first_id, "")), first_id, "resolve_voice_id keeps an existing voice ID")
	expect_equal(TTSServer.resolve_voice_id(_make_persona("x", first_name, "")), first_id, "resolve_voice_id finds a voice by name")

	# A missing voice falls back to a voice of the same language.
	var missing := _make_persona("missing", "voice-that-does-not-exist", language)
	var resolved := TTSServer.resolve_voice_id(missing)
	var resolved_language := ""
	for voice: Dictionary in voices:
		if voice["id"] == resolved:
			resolved_language = voice["language"]
	expect_equal(_base_language(resolved_language), _base_language(language), "a missing voice falls back to the same language")

	_connect_signals()
	missing.volume = 10
	var id := TTSServer.speak("Hello", missing)
	expect(id > 0, "speak returns a positive utterance ID")
	expect(TTSServer.get_pending_utterances().has(id), "the utterance is pending")
	await _wait_for_event(["ended", "canceled"], id)
	expect(_events.has(["started", id]), "utterance_started arrives for real speech")
	expect(_events.has(["ended", id]), "utterance_ended arrives for real speech")
	expect(not TTSServer.get_pending_utterances().has(id), "the utterance is no longer pending")

	# Stop cancels the current utterance.
	var long_id := TTSServer.speak("This is a long sentence that is stopped before it ends.", missing)
	await get_tree().create_timer(0.3).timeout
	TTSServer.stop()
	await _wait_for_event(["canceled", "ended"], long_id)
	expect(_events.has(["canceled", long_id]), "utterance_canceled arrives after stop")
	_disconnect_signals()
