extends FeatureCheck
## GDScript checks of 29-feature-tts-persona-settings. Check the same things as scripts/cs/Issue29FeatureTtsPersonaSettingsCheck.cs.
##
## The checks work without voices (headless, or with "audio/general/text_to_speech" off): personas are stored
## and applied to TTSServer even when nothing can be spoken. The restart check starts the project a second time
## as its own process (desktop only) and reads the personas TTSServer has before the main scene is ready.

const DIR := "user://29-feature-tts-persona-settings"
const PROBE_SCENE := "res://29-feature-tts-persona-settings/scenes/restart_probe.tscn"
const PERSONAS_FILE := "user://settings/tts_personas.ini"


func _get_check_name() -> String:
	return "29-feature-tts-persona-settings"


func _make_persona(persona_name: StringName, voice: String, language: String, rate: float) -> TTSPersona:
	var persona := TTSPersona.new()
	persona.persona_name = persona_name
	persona.voice = voice
	persona.language = language
	persona.rate = rate
	return persona


func _run_checks() -> void:
	DirAccess.make_dir_recursive_absolute(DIR)
	expect(Engine.has_singleton("TTSPersonaSettingsServer"), "TTSPersonaSettingsServer is a registered singleton")
	expect(Engine.get_singleton("TTSPersonaSettingsServer") is ListSettingsServer, "TTSPersonaSettingsServer is a ListSettingsServer")
	expect(ClassDB.class_exists("TTSPersonaSettings"), "TTSPersonaSettings is a registered class")
	expect(ClassDB.class_exists("TTSPersonaListSettings"), "TTSPersonaListSettings is a registered class")
	expect_equal(SettingsServerManager.get_server(&"tts_personas"), Engine.get_singleton("TTSPersonaSettingsServer"), "the domain is registered as \"tts_personas\"")
	expect(TTSPersonaSettingsServer.is_started(), "the domain was started before the main scene")
	expect_equal(TTSPersonaSettingsServer.get_apply_phase(), SettingsServer.APPLY_PHASE_SERVERS, "applied in APPLY_PHASE_SERVERS")
	expect_equal(TTSPersonaSettingsServer.get_file_path(), PERSONAS_FILE, "file path")
	TTSServer.clear_personas()
	TTSPersonaSettingsServer.reset_to_defaults()

	_check_entry()
	_check_overrides()
	_check_default_persona()
	_check_csv()
	_check_missing_voices()
	_check_restart()

	# Leave no player files behind for later runs.
	TTSPersonaSettingsServer.file_path = ""
	TTSPersonaSettingsServer.reset_to_defaults()
	TTSServer.clear_personas()
	await get_tree().process_frame
	TTSPersonaSettingsServer.flush_autosave()
	DirAccess.remove_absolute(PERSONAS_FILE)
	for file_name in DirAccess.get_files_at(DIR):
		DirAccess.remove_absolute(DIR.path_join(file_name))
	DirAccess.remove_absolute(DIR)


func _check_entry() -> void:
	var entry := TTSPersonaSettings.new()
	expect_equal(entry.persona_name, &"", "default persona_name is empty")
	expect_equal(entry.volume, 50, "default volume is 50")
	expect(not entry.default, "default flag is false")
	entry.volume = 300
	expect_equal(entry.volume, 100, "entry volume is clamped like TTSPersona")
	entry.copy_from_persona(_make_persona(&"guard", "Voice", "de", 0.8))
	var persona := entry.to_persona()
	expect_equal(persona.persona_name, &"guard", "to_persona() keeps the name")
	expect(is_equal_approx(persona.rate, 0.8), "to_persona() keeps the rate")
	persona.rate = 2.0
	expect(is_equal_approx(entry.rate, 0.8), "to_persona() returns a copy")


func _check_overrides() -> void:
	var game_narrator := _make_persona(&"narrator", "", "en", 1.0)
	TTSServer.add_persona(game_narrator)

	TTSPersonaSettingsServer.add_persona(_make_persona(&"helper", "Some Voice", "fr", 1.5))
	expect(TTSPersonaSettingsServer.has_persona(&"helper"), "add_persona() stores the player's persona")
	expect(TTSServer.has_persona(&"helper"), "the player's persona reaches TTSServer at once")
	expect(TTSServer.has_persona_override(&"helper"), "it is a persona override")
	expect(is_equal_approx(TTSServer.get_persona(&"helper").rate, 1.5), "TTSServer has the player's rate")

	TTSPersonaSettingsServer.set_persona_value(&"narrator", &"rate", 2.0)
	expect(TTSPersonaSettingsServer.has_persona(&"narrator"), "changing a game persona creates the player's version")
	expect(is_equal_approx(TTSServer.get_persona(&"narrator").rate, 2.0), "TTSServer uses the player's narrator")
	expect_equal(TTSServer.get_persona(&"narrator").language, "en", "the other values come from the game's persona")
	expect(TTSServer.get_base_persona(&"narrator") == game_narrator, "the game's persona stays the base persona")
	expect(is_equal_approx(game_narrator.rate, 1.0), "the game's persona is unchanged")

	TTSServer.add_persona(_make_persona(&"narrator", "", "en", 1.0))
	expect(is_equal_approx(TTSServer.get_persona(&"narrator").rate, 2.0), "the game adding its persona again does not hide the player's version")

	TTSPersonaSettingsServer.set_persona_value(&"narrator", &"volume", 500)
	expect_equal(TTSPersonaSettingsServer.get_persona_value(&"narrator", &"volume"), 100, "volume is clamped")

	TTSPersonaSettingsServer.remove_persona(&"narrator")
	expect(is_equal_approx(TTSServer.get_persona(&"narrator").rate, 1.0), "removing the player's version brings the game's persona back")
	expect_equal(TTSPersonaSettingsServer.get_persona_names(), PackedStringArray(["helper"]), "the player's persona names")


func _check_default_persona() -> void:
	TTSServer.set_default_persona(&"narrator")
	expect_equal(TTSServer.get_default_persona_name(), &"narrator", "the game's default persona")
	TTSPersonaSettingsServer.set_default_persona(&"helper")
	expect_equal(TTSPersonaSettingsServer.get_default_persona(), &"helper", "the player's default persona is stored")
	expect_equal(TTSServer.get_default_persona_name(), &"helper", "the player's default persona wins over the game's")
	TTSServer.set_default_persona(&"narrator")
	expect_equal(TTSServer.get_default_persona_name(), &"helper", "also when the game sets its default later")
	TTSPersonaSettingsServer.set_default_persona(&"")
	expect_equal(TTSServer.get_default_persona_name(), &"narrator", "an empty name brings the game's default back")
	TTSPersonaSettingsServer.set_default_persona(&"helper")

	# speak_with_persona() uses the player's persona; -1 without text to speech.
	if not TTSServer.is_available():
		expect_equal(TTSServer.speak_with_persona("Hello", &"helper"), -1, "speak_with_persona() returns -1 without text to speech")
	else:
		var id := TTSServer.speak_with_persona("Hello", &"helper")
		expect(id > 0, "speak_with_persona() returns an utterance id")
		TTSServer.stop()


func _check_csv() -> void:
	ProjectSettings.set_setting("settings/storage/format", ResourceTextSerializer.FORMAT_CSV)
	expect_equal(TTSPersonaSettingsServer.get_file_path(), "user://settings/tts_personas.csv", "the personas can be stored as CSV")
	ProjectSettings.set_setting("settings/storage/format", ResourceTextSerializer.FORMAT_INI)

	var path := DIR.path_join("personas.csv")
	TTSPersonaSettingsServer.add_persona(_make_persona(&"guard", "", "de", 0.8))
	TTSPersonaSettingsServer.apply()
	TTSPersonaSettingsServer.file_path = path
	expect_equal(TTSPersonaSettingsServer.save(), OK, "save() as CSV")
	var lines := FileAccess.get_file_as_string(path).strip_edges().split("\n")
	expect_equal(lines.size(), 3, "CSV: a header and one row per persona")
	var rows := {"helper": 0, "guard": 0}
	for line in lines:
		for persona_name in rows:
			if line.contains(persona_name):
				rows[persona_name] += 1
	expect_equal(rows, {"helper": 1, "guard": 1}, "CSV: one row per persona")

	TTSPersonaSettingsServer.remove_persona(&"guard")
	TTSPersonaSettingsServer.set_persona_value(&"helper", &"rate", 3.0)
	expect_equal(TTSPersonaSettingsServer.load(), OK, "load() reads the CSV")
	TTSPersonaSettingsServer.apply()
	expect(TTSServer.has_persona(&"guard"), "CSV: the persona is back in TTSServer")
	expect(is_equal_approx(TTSServer.get_persona(&"helper").rate, 1.5), "CSV: the stored rate is applied")
	expect_equal(TTSServer.get_default_persona_name(), &"helper", "CSV: the default persona is a column")
	TTSPersonaSettingsServer.file_path = ""


func _check_missing_voices() -> void:
	TTSPersonaSettingsServer.set_persona_value(&"guard", &"voice", "A Voice Not On This Device")
	var missing := TTSPersonaSettingsServer.get_missing_voice_personas()
	if TTSServer.get_voices().is_empty():
		expect(missing.is_empty(), "without a voice list no voice is reported missing")
	else:
		expect(missing.has("guard"), "a voice missing on this device is reported")
		expect(not TTSServer.resolve_voice_id(TTSServer.get_persona(&"guard")).is_empty(), "TTSServer falls back to another voice")
	expect(TTSServer.has_persona(&"guard"), "a persona with a missing voice is kept")


func _check_restart() -> void:
	if not OS.has_feature("pc"):
		print("  SKIP  restart check (needs a desktop platform to start a second process)")
		return
	TTSPersonaSettingsServer.apply()
	expect_equal(TTSPersonaSettingsServer.save(), OK, "save() writes the player file")
	expect(FileAccess.file_exists(PERSONAS_FILE), "the player file is in user://settings/")

	var probe := _run_probe()
	expect(probe.get("started", false), "restart: the domain was started")
	var names: Array = probe.get("persona_names", [])
	expect(names.has("helper") and names.has("guard"), "restart: the saved personas are in TTSServer.get_personas()")
	expect(is_equal_approx(probe.get("helper_rate", 0.0), 1.5), "restart: the saved rate is applied")
	expect_equal(probe.get("default_persona", ""), "helper", "restart: the saved default persona is applied")


func _run_probe() -> Dictionary:
	var output := []
	var args := PackedStringArray(["--headless", "--path", ProjectSettings.globalize_path("res://"), PROBE_SCENE])
	var code := OS.execute(OS.get_executable_path(), args, output, true)
	expect_equal(code, 0, "restart: the probe process ran")
	for chunk in output:
		for line in str(chunk).split("\n"):
			if line.begins_with("CHECK29_PROBE "):
				var parsed = JSON.parse_string(line.trim_prefix("CHECK29_PROBE "))
				if parsed is Dictionary:
					return parsed
	expect(false, "restart: the probe printed its result")
	return {}
