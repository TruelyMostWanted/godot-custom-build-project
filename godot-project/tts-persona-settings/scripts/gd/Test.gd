extends FeatureTest
## Full API test of TTS Persona Settings (TTSPersonaSettingsServer, TTSPersonaSettings, TTSPersonaListSettings).
## Same tests as scripts/cs/Test.cs. The tests work without voices (headless, or with text to speech off): personas
## are stored and applied to TTSServer even when nothing can be spoken. The restart test starts the project a second
## time as its own process (desktop only) and reads the personas TTSServer has before the main scene is ready.

const TEST_DIR := "user://tts-persona-settings"
const PROBE_SCENE := "res://tts-persona-settings/scenes/restart_probe.tscn"
const PERSONAS_FILE := "user://settings/tts_personas.ini"
const PERSONAS_CSV := "user://settings/tts_personas.csv"
const MISSING_VOICE := "A Voice Not On This Device"


func _run_tests() -> void:
	DirAccess.make_dir_recursive_absolute(TEST_DIR)
	TTSServer.clear_personas()
	TTSPersonaSettingsServer.reset_to_defaults()
	_test_entry()
	_test_overrides()
	_test_default_persona()
	_test_csv()
	_test_missing_voices()
	_test_list_resource()
	_test_restart()
	TTSPersonaSettingsServer.file_path = ""
	TTSPersonaSettingsServer.reset_to_defaults()
	TTSServer.clear_personas()
	DirAccess.remove_absolute(PERSONAS_FILE)
	for file_name: String in DirAccess.get_files_at(TEST_DIR):
		DirAccess.remove_absolute(TEST_DIR.path_join(file_name))
	DirAccess.remove_absolute(TEST_DIR)


func _make_persona(persona_name: StringName, voice: String, language: String, rate: float) -> TTSPersona:
	var persona := TTSPersona.new()
	persona.persona_name = persona_name
	persona.voice = voice
	persona.language = language
	persona.rate = rate
	return persona


func _test_entry() -> void:
	var entry := TTSPersonaSettings.new()
	expect_equal(entry.persona_name, &"", "TTSPersonaSettings: the persona_name is empty by default")
	expect_equal(entry.volume, 50, "TTSPersonaSettings: the default volume is 50")
	expect_equal(entry.rate, 1.0, "TTSPersonaSettings: the default rate is 1")
	expect_equal(entry.pitch, 1.0, "TTSPersonaSettings: the default pitch is 1")
	expect(not entry.default, "TTSPersonaSettings: default is false")
	expect(not entry.interrupt, "TTSPersonaSettings: interrupt is false")
	entry.volume = 300
	expect_equal(entry.volume, 100, "TTSPersonaSettings: the volume is clamped like TTSPersona")
	entry.copy_from_persona(_make_persona(&"guard", "Voice", "de", 0.8))
	var persona: TTSPersona = entry.to_persona()
	expect_equal(persona.persona_name, &"guard", "to_persona keeps the name")
	expect_equal(persona.voice, "Voice", "to_persona keeps the voice")
	expect_equal(persona.language, "de", "to_persona keeps the language")
	expect(is_equal_approx(persona.rate, 0.8), "to_persona keeps the rate")
	persona.rate = 2.0
	expect(is_equal_approx(entry.rate, 0.8), "to_persona returns a copy")


func _test_overrides() -> void:
	var game_narrator: TTSPersona = _make_persona(&"narrator", "", "en", 1.0)
	TTSServer.add_persona(game_narrator)
	TTSPersonaSettingsServer.add_persona(_make_persona(&"helper", "Some Voice", "fr", 1.5))
	expect(TTSPersonaSettingsServer.has_persona(&"helper"), "add_persona stores the persona of the player")
	expect(TTSServer.has_persona(&"helper"), "the persona of the player reaches TTSServer at once")
	expect(TTSServer.has_persona_override(&"helper"), "it is a persona override")
	expect(is_equal_approx(TTSServer.get_persona(&"helper").rate, 1.5), "TTSServer has the rate of the player")
	expect_equal(TTSPersonaSettingsServer.get_persona(&"helper").language, "fr", "get_persona returns the persona of the player")
	TTSPersonaSettingsServer.set_persona_value(&"narrator", &"rate", 2.0)
	expect(TTSPersonaSettingsServer.has_persona(&"narrator"), "changing a game persona creates the version of the player")
	expect(is_equal_approx(TTSServer.get_persona(&"narrator").rate, 2.0), "TTSServer uses the narrator of the player")
	expect_equal(TTSServer.get_persona(&"narrator").language, "en", "the other values come from the persona of the game")
	expect(TTSServer.get_base_persona(&"narrator") == game_narrator, "the persona of the game stays the base persona")
	expect(is_equal_approx(game_narrator.rate, 1.0), "the persona of the game is unchanged")
	expect(is_equal_approx(TTSPersonaSettingsServer.get_persona_value(&"narrator", &"rate"), 2.0), "get_persona_value returns the value the player hears")
	TTSServer.add_persona(_make_persona(&"narrator", "", "en", 1.0))
	expect(is_equal_approx(TTSServer.get_persona(&"narrator").rate, 2.0), "the game adding its persona again does not hide the version of the player")
	TTSPersonaSettingsServer.set_persona_value(&"narrator", &"volume", 500)
	expect_equal(TTSPersonaSettingsServer.get_persona_value(&"narrator", &"volume"), 100, "the volume is clamped")
	TTSPersonaSettingsServer.remove_persona(&"narrator")
	expect(is_equal_approx(TTSServer.get_persona(&"narrator").rate, 1.0), "removing the version of the player brings the persona of the game back")
	expect_equal(TTSPersonaSettingsServer.get_persona_names(), PackedStringArray(["helper"]), "get_persona_names lists the personas of the player")


func _test_default_persona() -> void:
	TTSServer.set_default_persona(&"narrator")
	expect_equal(TTSServer.get_default_persona_name(), &"narrator", "the default persona of the game")
	TTSPersonaSettingsServer.set_default_persona(&"helper")
	expect_equal(TTSPersonaSettingsServer.get_default_persona(), &"helper", "the default persona of the player is stored")
	expect_equal(TTSServer.get_default_persona_name(), &"helper", "the default persona of the player wins over the one of the game")
	TTSServer.set_default_persona(&"narrator")
	expect_equal(TTSServer.get_default_persona_name(), &"helper", "also when the game sets its default later")
	TTSPersonaSettingsServer.set_default_persona(&"")
	expect_equal(TTSServer.get_default_persona_name(), &"narrator", "an empty name brings the default of the game back")
	TTSPersonaSettingsServer.set_default_persona(&"helper")


func _test_csv() -> void:
	ProjectSettings.set_setting("settings/general/default_format", ResourceTextSerializer.FORMAT_CSV)
	expect_equal(TTSPersonaSettingsServer.get_file_path(), PERSONAS_CSV, "the personas can be stored as CSV")
	ProjectSettings.set_setting("settings/general/default_format", ResourceTextSerializer.FORMAT_INI)
	ProjectSettings.set_setting("settings/tts_personas/override_format", true)
	ProjectSettings.set_setting("settings/tts_personas/format", ResourceTextSerializer.FORMAT_CSV)
	expect_equal(TTSPersonaSettingsServer.get_file_path(), PERSONAS_CSV, "override_format stores only this domain as CSV")
	ProjectSettings.set_setting("settings/tts_personas/override_format", false)
	ProjectSettings.set_setting("settings/tts_personas/format", ResourceTextSerializer.FORMAT_INI)
	expect_equal(TTSPersonaSettingsServer.get_file_path(), PERSONAS_FILE, "without override_format the default format is used")
	var path: String = TEST_DIR.path_join("personas.csv")
	TTSPersonaSettingsServer.add_persona(_make_persona(&"guard", "", "de", 0.8))
	TTSPersonaSettingsServer.apply()
	TTSPersonaSettingsServer.file_path = path
	expect_equal(TTSPersonaSettingsServer.save(), OK, "save writes the CSV")
	var lines: PackedStringArray = FileAccess.get_file_as_string(path).strip_edges().split("\n")
	expect_equal(lines.size(), 3, "CSV: a header and one row per persona")
	TTSPersonaSettingsServer.remove_persona(&"guard")
	TTSPersonaSettingsServer.set_persona_value(&"helper", &"rate", 3.0)
	expect_equal(TTSPersonaSettingsServer.load(), OK, "load reads the CSV")
	TTSPersonaSettingsServer.apply()
	expect(TTSServer.has_persona(&"guard"), "CSV: the persona is back in TTSServer")
	expect(is_equal_approx(TTSServer.get_persona(&"helper").rate, 1.5), "CSV: the stored rate is applied")
	expect_equal(TTSServer.get_default_persona_name(), &"helper", "CSV: the default persona is a column")
	TTSPersonaSettingsServer.file_path = ""


func _test_missing_voices() -> void:
	TTSPersonaSettingsServer.set_persona_value(&"guard", &"voice", MISSING_VOICE)
	var missing: PackedStringArray = TTSPersonaSettingsServer.get_missing_voice_personas()
	if TTSServer.get_voices().is_empty():
		expect(missing.is_empty(), "without a voice list no voice is reported missing")
	else:
		expect(missing.has("guard"), "a voice missing on this device is reported")
	expect(TTSServer.has_persona(&"guard"), "a persona with a missing voice is kept")


func _test_list_resource() -> void:
	var list := TTSPersonaListSettings.new()
	expect(list.personas.is_empty(), "TTSPersonaListSettings: no personas by default")
	var entry := TTSPersonaSettings.new()
	entry.persona_name = &"narrator"
	list.personas.append(entry)
	expect_equal(list.personas.size(), 1, "TTSPersonaListSettings: personas holds the entries")
	expect(TTSPersonaSettingsServer.get_settings() is TTSPersonaListSettings, "the settings resource is a TTSPersonaListSettings")


func _test_restart() -> void:
	if not OS.has_feature("pc"):
		print("  SKIP  restart test (needs a desktop platform to start a second process)")
		return
	TTSPersonaSettingsServer.apply()
	expect_equal(TTSPersonaSettingsServer.save(), OK, "save writes the player file")
	var args := PackedStringArray(["--headless", "--path", ProjectSettings.globalize_path("res://"), PROBE_SCENE])
	var output: Array = []
	var code: int = OS.execute(OS.get_executable_path(), args, output, true)
	expect_equal(code, 0, "restart: the probe process ran")
	var probe: Dictionary = {}
	for line: String in str(output[0]).split("\n"):
		if line.begins_with("PROBE "):
			probe = JSON.parse_string(line.trim_prefix("PROBE "))
	var names: Array = probe.get("persona_names", [])
	expect(names.has("helper"), "restart: the saved helper persona is in TTSServer")
	expect(names.has("guard"), "restart: the saved guard persona is in TTSServer")
	expect_equal(probe.get("helper_rate"), 1.5, "restart: the saved rate is applied")
	expect_equal(probe.get("default_persona"), "helper", "restart: the saved default persona is applied")
