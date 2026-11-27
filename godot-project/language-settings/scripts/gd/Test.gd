extends FeatureTest
## Full API test of Language Settings: LanguageSettingsServer and LanguageSettings. Same tests as scripts/cs/Test.cs.
## The project has two translations (resources/hello_de.translation and hello_fr.translation) with the key CHECK17_HELLO.

const PROBE_SCENE := "res://language-settings/scenes/restart_probe.tscn"
const LANGUAGE_FILE := "user://settings/language.ini"
const HELLO_KEY := "CHECK17_HELLO"
const HELLO_DE := "Hallo"
const HELLO_FR := "Bonjour"

var _locales: Array[String] = []
var _pseudo: Array[bool] = []
var _directions: Array[int] = []
var _modes: Array[int] = []


func _run_tests() -> void:
	_test_registration()
	_test_defaults()
	_test_locale()
	_test_invalid_locale()
	_test_channels()
	_test_manual_mode()
	_test_pseudolocalization()
	_test_layout_direction()
	_test_auto_translate_mode()
	await _test_signals()
	_test_resource()
	await _test_restart()
	LanguageSettingsServer.reset_to_defaults()
	await get_tree().process_frame
	DirAccess.remove_absolute(LANGUAGE_FILE)


func _project_locale() -> String:
	return TranslationServer.standardize_locale(LanguageSettingsServer.get_project_locale())


func _test_registration() -> void:
	expect(Engine.has_singleton("LanguageSettingsServer"), "LanguageSettingsServer is a registered singleton")
	expect(Engine.get_singleton("LanguageSettingsServer") is SettingsServer, "LanguageSettingsServer is a SettingsServer")
	expect_equal(SettingsServerManager.get_server(&"language"), LanguageSettingsServer, "the domain is registered as language")
	expect(LanguageSettingsServer.is_started(), "the domain was started before the main scene")
	expect_equal(LanguageSettingsServer.file_path, LANGUAGE_FILE, "file path")
	expect_equal(LanguageSettingsServer.get_setting_names(), PackedStringArray(["locale", "pseudolocalization_enabled", "layout_direction", "root_auto_translate_mode", "interface_locale", "audio_locale", "subtitles_locale"]), "setting names")


func _test_defaults() -> void:
	LanguageSettingsServer.reset_to_defaults()
	expect_equal(LanguageSettingsServer.get_default_value(&"locale"), "", "default locale follows the OS")
	expect_equal(LanguageSettingsServer.get_default_value(&"pseudolocalization_enabled"), false, "default: no pseudolocalization")
	expect_equal(LanguageSettingsServer.get_default_value(&"layout_direction"), Window.LAYOUT_DIRECTION_INHERITED, "default layout direction is inherited")
	expect_equal(LanguageSettingsServer.get_default_value(&"root_auto_translate_mode"), Node.AUTO_TRANSLATE_MODE_ALWAYS, "default auto-translate mode is ALWAYS (project setting)")
	expect_equal(LanguageSettingsServer.get_locale(), "", "locale is empty after reset")
	expect_equal(TranslationServer.get_locale(), _project_locale(), "an empty locale gives the project (test or OS) locale")
	expect_equal(LanguageSettingsServer.get_available_locales(), PackedStringArray(["de", "fr"]), "available locales: the two project translations, sorted")
	expect_equal(LanguageSettingsServer.get_command_line_locale(), "", "no --language argument in this run")


func _test_locale() -> void:
	LanguageSettingsServer.set_locale("de")
	expect_equal(TranslationServer.get_locale(), "de", "set_locale reaches TranslationServer at once")
	expect_equal(LanguageSettingsServer.get_locale(), "de", "get_locale")
	expect_equal(tr(HELLO_KEY), HELLO_DE, "the German text is used")
	LanguageSettingsServer.set_locale("fr")
	expect_equal(tr(HELLO_KEY), HELLO_FR, "the French text is used")
	LanguageSettingsServer.set_locale("de-DE")
	expect_equal(LanguageSettingsServer.get_locale(), "de_DE", "the locale is standardized (de-DE becomes de_DE)")
	expect_equal(tr(HELLO_KEY), HELLO_DE, "a regional variant uses the translation of its language")
	LanguageSettingsServer.set_locale("")
	expect_equal(TranslationServer.get_locale(), _project_locale(), "an empty locale follows the OS (or the test locale) again")


func _test_invalid_locale() -> void:
	LanguageSettingsServer.set_locale("zz_ZZ")
	expect_equal(LanguageSettingsServer.get_locale(), "zz_ZZ", "a locale without translation is kept as the stored value")
	expect_equal(TranslationServer.get_locale(), _project_locale(), "no translation for the locale: the project locale is used, without an error")
	LanguageSettingsServer.set_locale("fr")
	expect_equal(TranslationServer.get_locale(), "fr", "a valid locale works again afterwards")
	LanguageSettingsServer.set_locale("")


func _test_channels() -> void:
	LanguageSettingsServer.set_locale("fr")
	expect_equal(LanguageSettingsServer.get_locale_for(&"interface"), "", "a channel is the same as the language by default")
	expect_equal(LanguageSettingsServer.get_locale_for(&""), "fr", "an empty channel is the language")
	LanguageSettingsServer.set_locale_for(&"audio", "de")
	expect_equal(LanguageSettingsServer.get_locale_for(&"audio"), "de", "set_locale_for stores the audio locale")
	expect_equal(LanguageSettingsServer.get_value(&"audio_locale"), "de", "the audio locale is the setting audio_locale")
	expect_equal(TranslationServer.get_locale_for(&"audio"), "de", "the audio locale is in use")
	expect_equal(TranslationServer.get_locale_for(&"interface"), "fr", "the interface follows the language")
	LanguageSettingsServer.set_locale_for(&"subtitles", "de-DE")
	expect_equal(LanguageSettingsServer.get_locale_for(&"subtitles"), "de_DE", "a channel locale is standardized")
	LanguageSettingsServer.set_locale_for(&"interface", "de")
	expect_equal(LanguageSettingsServer.get_value(&"interface_locale"), "de", "the interface locale is the setting interface_locale")
	expect_equal(tr(HELLO_KEY), HELLO_DE, "the interface channel changes the translated text")
	LanguageSettingsServer.set_locale_for(&"", "fr")
	expect_equal(LanguageSettingsServer.get_locale(), "fr", "an empty channel in set_locale_for is set_locale")
	LanguageSettingsServer.set_locale_for(&"video", "de")
	expect_equal(LanguageSettingsServer.get_locale_for(&"video"), "", "an unknown channel is refused (an error is printed)")
	expect_equal(LanguageSettingsServer.get_available_locales_for(&"interface"), PackedStringArray(["de", "fr"]), "get_available_locales_for: the loaded translations for interface")
	expect_equal(LanguageSettingsServer.get_available_locales_for(&"audio"), LanguageSettingsServer.get_available_locales(), "a channel without catalogs lists all locales")
	expect_equal(LanguageSettingsServer.get_available_locales_for(&""), LanguageSettingsServer.get_available_locales(), "an empty channel lists all locales")
	LanguageSettingsServer.reset_to_defaults()
	expect_equal(LanguageSettingsServer.get_locale_for(&"audio"), "", "reset_to_defaults clears the channels")


func _test_manual_mode() -> void:
	LanguageSettingsServer.apply_mode = SettingsServer.APPLY_MODE_MANUAL
	LanguageSettingsServer.set_locale("fr")
	expect_equal(TranslationServer.get_locale(), _project_locale(), "manual mode: not applied yet")
	expect(LanguageSettingsServer.is_dirty(), "manual mode: dirty")
	LanguageSettingsServer.revert()
	expect_equal(LanguageSettingsServer.get_locale(), "", "revert gives back the applied locale")
	LanguageSettingsServer.set_locale("fr")
	expect_equal(LanguageSettingsServer.apply(), OK, "apply in manual mode")
	expect_equal(TranslationServer.get_locale(), "fr", "apply reaches TranslationServer")
	LanguageSettingsServer.apply_mode = SettingsServer.APPLY_MODE_IMMEDIATE
	LanguageSettingsServer.reset_to_defaults()


func _test_pseudolocalization() -> void:
	LanguageSettingsServer.set_locale("fr")
	expect(not LanguageSettingsServer.is_pseudolocalization_enabled(), "pseudolocalization is off by default")
	LanguageSettingsServer.set_pseudolocalization_enabled(true)
	expect(TranslationServer.pseudolocalization_enabled, "set_pseudolocalization_enabled reaches TranslationServer")
	expect(LanguageSettingsServer.is_pseudolocalization_enabled(), "is_pseudolocalization_enabled")
	var text: String = tr(HELLO_KEY)
	expect(text != HELLO_FR, "the text is pseudolocalized (changed)")
	expect(text.length() >= HELLO_FR.length(), "the pseudolocalized text is not shorter")
	LanguageSettingsServer.set_pseudolocalization_enabled(false)
	expect_equal(tr(HELLO_KEY), HELLO_FR, "pseudolocalization off gives back the translation")
	LanguageSettingsServer.reset_to_defaults()


func _test_layout_direction() -> void:
	var root: Window = get_tree().root
	LanguageSettingsServer.set_layout_direction(Window.LAYOUT_DIRECTION_RTL)
	expect_equal(root.get_layout_direction(), Window.LAYOUT_DIRECTION_RTL, "the root window gets the layout direction")
	expect_equal(LanguageSettingsServer.get_layout_direction(), Window.LAYOUT_DIRECTION_RTL, "get_layout_direction")
	LanguageSettingsServer.set_layout_direction(Window.LAYOUT_DIRECTION_LTR)
	expect_equal(root.get_layout_direction(), Window.LAYOUT_DIRECTION_LTR, "left-to-right")
	LanguageSettingsServer.reset_to_defaults()
	expect_equal(root.get_layout_direction(), Window.LAYOUT_DIRECTION_INHERITED, "reset_to_defaults gives back the inherited direction")


func _test_auto_translate_mode() -> void:
	var root: Window = get_tree().root
	LanguageSettingsServer.set_root_auto_translate_mode(Node.AUTO_TRANSLATE_MODE_DISABLED)
	expect_equal(root.auto_translate_mode, Node.AUTO_TRANSLATE_MODE_DISABLED, "the root window gets the auto-translate mode")
	expect_equal(LanguageSettingsServer.get_root_auto_translate_mode(), Node.AUTO_TRANSLATE_MODE_DISABLED, "get_root_auto_translate_mode")
	LanguageSettingsServer.reset_to_defaults()
	expect_equal(root.auto_translate_mode, Node.AUTO_TRANSLATE_MODE_ALWAYS, "reset_to_defaults gives back ALWAYS")


func _test_signals() -> void:
	var on_locale := func(channel: StringName, locale: String, _previous_locale: String) -> void:
		if channel == &"":
			_locales.append(locale)
	var on_pseudo := func(enabled: bool) -> void: _pseudo.append(enabled)
	var on_direction := func(direction: int) -> void: _directions.append(direction)
	var on_mode := func(mode: int) -> void: _modes.append(mode)
	TranslationServer.locale_changed.connect(on_locale)
	LanguageSettingsServer.pseudolocalization_enabled_changed.connect(on_pseudo)
	LanguageSettingsServer.layout_direction_changed.connect(on_direction)
	LanguageSettingsServer.root_auto_translate_mode_changed.connect(on_mode)
	LanguageSettingsServer.apply_mode = SettingsServer.APPLY_MODE_MANUAL
	LanguageSettingsServer.set_locale("fr")
	LanguageSettingsServer.set_pseudolocalization_enabled(true)
	LanguageSettingsServer.set_layout_direction(Window.LAYOUT_DIRECTION_RTL)
	LanguageSettingsServer.set_root_auto_translate_mode(Node.AUTO_TRANSLATE_MODE_DISABLED)
	expect(_locales.is_empty() and _pseudo.is_empty(), "the typed signals do not fire for pending changes in manual mode")
	LanguageSettingsServer.apply()
	expect_equal(_locales, ["fr"], "TranslationServer.locale_changed fires once on apply with the locale")
	expect_equal(_pseudo, [true], "pseudolocalization_enabled_changed fires once on apply")
	expect_equal(_directions, [Window.LAYOUT_DIRECTION_RTL], "layout_direction_changed fires once on apply")
	expect_equal(_modes, [Node.AUTO_TRANSLATE_MODE_DISABLED], "root_auto_translate_mode_changed fires once on apply")
	LanguageSettingsServer.apply()
	expect_equal(_locales.size(), 1, "locale_changed does not fire when nothing changed")
	LanguageSettingsServer.reset_to_defaults()
	LanguageSettingsServer.apply()
	expect_equal(_locales.size(), 2, "locale_changed fires again when the default is applied")
	LanguageSettingsServer.apply_mode = SettingsServer.APPLY_MODE_IMMEDIATE
	TranslationServer.locale_changed.disconnect(on_locale)
	LanguageSettingsServer.pseudolocalization_enabled_changed.disconnect(on_pseudo)
	LanguageSettingsServer.layout_direction_changed.disconnect(on_direction)
	LanguageSettingsServer.root_auto_translate_mode_changed.disconnect(on_mode)
	await get_tree().process_frame


func _test_resource() -> void:
	var settings := LanguageSettings.new()
	expect_equal(settings.locale, "", "LanguageSettings: default locale")
	expect_equal(settings.pseudolocalization_enabled, false, "LanguageSettings: default pseudolocalization")
	expect_equal(settings.layout_direction, Window.LAYOUT_DIRECTION_INHERITED, "LanguageSettings: default layout direction")
	expect_equal(settings.root_auto_translate_mode, Node.AUTO_TRANSLATE_MODE_ALWAYS, "LanguageSettings: default auto-translate mode")
	settings.locale = "fr"
	settings.interface_locale = "de"
	settings.audio_locale = "de"
	settings.subtitles_locale = "fr"
	expect_equal(settings.locale, "fr", "LanguageSettings.locale")
	expect_equal(settings.interface_locale, "de", "LanguageSettings.interface_locale")
	expect_equal(settings.audio_locale, "de", "LanguageSettings.audio_locale")
	expect_equal(settings.subtitles_locale, "fr", "LanguageSettings.subtitles_locale")
	var sample: LanguageSettings = load("res://language-settings/resources/german_settings.tres")
	expect_equal(sample.locale, "de", "the sample LanguageSettings loads")


func _test_restart() -> void:
	if not OS.has_feature("pc"):
		print("  SKIP  restart test (needs a desktop platform to start a second process)")
		return
	LanguageSettingsServer.set_locale("fr")
	LanguageSettingsServer.set_layout_direction(Window.LAYOUT_DIRECTION_RTL)
	LanguageSettingsServer.set_root_auto_translate_mode(Node.AUTO_TRANSLATE_MODE_DISABLED)
	expect_equal(LanguageSettingsServer.save(), OK, "save writes the player file")
	expect(FileAccess.file_exists(LANGUAGE_FILE), "the player file is in user://settings/")
	expect_equal(_read_stored_locale(), "fr", "the file contains the locale")
	var probe: Dictionary = _run_probe([])
	expect(probe.get("started", false), "restart: the domain was started")
	expect_equal(probe.get("locale", ""), "fr", "restart: the saved locale is active")
	expect_equal(probe.get("hello", ""), HELLO_FR, "restart: the first text is already French")
	expect_equal(int(probe.get("layout_direction", -1)), Window.LAYOUT_DIRECTION_RTL, "restart: the saved layout direction is on the root window")
	expect_equal(int(probe.get("auto_translate_mode", -1)), Node.AUTO_TRANSLATE_MODE_DISABLED, "restart: the saved auto-translate mode is on the root window")
	expect_equal(int(probe.get("ready_frame", -1)), 0, "restart: applied before the first frame")
	probe = _run_probe(["--language", "de"])
	expect_equal(probe.get("command_line_locale", ""), "de", "--language: the argument is read")
	expect_equal(probe.get("locale", ""), "de", "--language wins over the stored locale")
	expect_equal(probe.get("hello", ""), HELLO_DE, "--language: the first text is German")
	expect_equal(_read_stored_locale(), "fr", "--language is not saved: the file still has the stored locale")
	LanguageSettingsServer.set_pseudolocalization_enabled(true)
	LanguageSettingsServer.set_locale("zz_ZZ")
	expect_equal(LanguageSettingsServer.save(), OK, "save with an unknown locale")
	probe = _run_probe([])
	expect(probe.get("pseudolocalization", false), "restart: the saved pseudolocalization is on")
	expect_equal(probe.get("locale", ""), probe.get("project_locale", "?"), "restart: an unknown stored locale falls back to the project locale")


func _read_stored_locale() -> String:
	var text: String = FileAccess.get_file_as_string(LANGUAGE_FILE)
	for line: String in text.split("\n"):
		var stripped: String = line.strip_edges()
		if stripped.begins_with("locale="):
			return stripped.trim_prefix("locale=").trim_prefix("\"").trim_suffix("\"")
	return "<no locale key>"


func _run_probe(extra_args: PackedStringArray) -> Dictionary:
	var output: Array = []
	var args: PackedStringArray = ["--headless"]
	args.append_array(extra_args)
	args.append_array(["--path", ProjectSettings.globalize_path("res://"), PROBE_SCENE])
	var code: int = OS.execute(OS.get_executable_path(), args, output, true)
	expect_equal(code, 0, "restart: the probe process ran")
	for chunk in output:
		for line: String in str(chunk).split("\n"):
			if line.begins_with("PROBE "):
				var parsed: Variant = JSON.parse_string(line.trim_prefix("PROBE "))
				return parsed
	expect(false, "restart: the probe printed its result")
	return {}
