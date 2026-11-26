extends FeatureCheck
## GDScript checks of 17-feature-language-settings. Check the same things as scripts/cs/Issue17FeatureLanguageSettingsCheck.cs.
##
## The project has two translations (resources/hello_de.translation and hello_fr.translation, set in
## internationalization/locale/translations) with the key CHECK17_HELLO. The restart checks start the project
## again as its own processes (desktop only) and read what the domain applied before the main scene was ready.

const PROBE_SCENE := "res://17-feature-language-settings/scenes/restart_probe.tscn"
const LANGUAGE_FILE := "user://settings/language.ini"


func _get_check_name() -> String:
	return "17-feature-language-settings"


func _run_checks() -> void:
	await _check_typed_signals()
	expect(Engine.has_singleton("LanguageSettingsServer"), "LanguageSettingsServer is a registered singleton")
	expect(Engine.get_singleton("LanguageSettingsServer") is SettingsServer, "LanguageSettingsServer is a SettingsServer")
	expect_equal(SettingsServerManager.get_server(&"language"), Engine.get_singleton("LanguageSettingsServer"), "the domain is registered as \"language\"")
	expect(LanguageSettingsServer.is_started(), "the domain was started before the main scene")
	expect_equal(LanguageSettingsServer.get_file_path(), LANGUAGE_FILE, "file path")
	expect_equal(LanguageSettingsServer.get_setting_names(), PackedStringArray(["locale", "pseudolocalization_enabled", "layout_direction", "root_auto_translate_mode", "interface_locale", "audio_locale", "subtitles_locale"]), "setting names")

	_check_defaults()
	_check_locale()
	_check_invalid_locale()
	_check_manual_mode()
	_check_pseudolocalization()
	_check_layout_direction()
	_check_auto_translate_mode()
	_check_resource()
	await _check_restart()

	# Leave no player files behind for later runs.
	LanguageSettingsServer.reset_to_defaults()
	await get_tree().process_frame
	DirAccess.remove_absolute(LANGUAGE_FILE)


func _project_locale() -> String:
	return TranslationServer.standardize_locale(LanguageSettingsServer.get_project_locale())


func _check_defaults() -> void:
	LanguageSettingsServer.reset_to_defaults()
	expect_equal(LanguageSettingsServer.get_default_value(&"locale"), "", "default locale follows the OS")
	expect_equal(LanguageSettingsServer.get_default_value(&"pseudolocalization_enabled"), false, "default: no pseudolocalization")
	expect_equal(LanguageSettingsServer.get_default_value(&"layout_direction"), Window.LAYOUT_DIRECTION_INHERITED, "default layout direction is inherited")
	expect_equal(LanguageSettingsServer.get_default_value(&"root_auto_translate_mode"), Node.AUTO_TRANSLATE_MODE_ALWAYS, "default auto-translate mode is ALWAYS (project setting)")
	expect_equal(LanguageSettingsServer.get_locale(), "", "locale is empty after reset")
	expect_equal(TranslationServer.get_locale(), _project_locale(), "an empty locale gives the project (test or OS) locale")
	var locales := LanguageSettingsServer.get_available_locales()
	expect_equal(locales, PackedStringArray(["de", "fr"]), "available locales: the two project translations, sorted")
	expect_equal(LanguageSettingsServer.get_command_line_locale(), "", "no --language argument in this run")


func _check_locale() -> void:
	LanguageSettingsServer.set_locale("de")
	expect_equal(TranslationServer.get_locale(), "de", "set_locale() reaches TranslationServer at once")
	expect_equal(LanguageSettingsServer.get_locale(), "de", "get_locale()")
	expect_equal(tr("CHECK17_HELLO"), "Hallo", "the German text is used")
	LanguageSettingsServer.set_locale("fr")
	expect_equal(tr("CHECK17_HELLO"), "Bonjour", "the French text is used")
	LanguageSettingsServer.set_locale("de-DE")
	expect_equal(LanguageSettingsServer.get_locale(), "de_DE", "the locale is standardized (de-DE becomes de_DE)")
	expect_equal(tr("CHECK17_HELLO"), "Hallo", "a regional variant uses the translation of its language")
	LanguageSettingsServer.set_locale("")
	expect_equal(TranslationServer.get_locale(), _project_locale(), "an empty locale follows the OS (or the test locale) again")


func _check_invalid_locale() -> void:
	LanguageSettingsServer.set_locale("zz_ZZ")
	expect_equal(LanguageSettingsServer.get_locale(), "zz_ZZ", "a locale without translation is kept as the stored value")
	expect_equal(TranslationServer.get_locale(), _project_locale(), "no translation for the locale: the project locale is used, without an error")
	LanguageSettingsServer.set_locale("fr")
	expect_equal(TranslationServer.get_locale(), "fr", "a valid locale works again afterwards")
	LanguageSettingsServer.set_locale("")


func _check_manual_mode() -> void:
	LanguageSettingsServer.apply_mode = SettingsServer.APPLY_MODE_MANUAL
	LanguageSettingsServer.set_locale("fr")
	expect_equal(TranslationServer.get_locale(), _project_locale(), "manual mode: not applied yet")
	expect(LanguageSettingsServer.is_dirty(), "manual mode: dirty")
	LanguageSettingsServer.revert()
	expect_equal(LanguageSettingsServer.get_locale(), "", "revert() gives back the applied locale")
	LanguageSettingsServer.set_locale("fr")
	expect_equal(LanguageSettingsServer.apply(), OK, "apply() in manual mode")
	expect_equal(TranslationServer.get_locale(), "fr", "apply() reaches TranslationServer")
	LanguageSettingsServer.apply_mode = SettingsServer.APPLY_MODE_IMMEDIATE
	LanguageSettingsServer.reset_to_defaults()


func _check_pseudolocalization() -> void:
	LanguageSettingsServer.set_locale("fr")
	expect(not LanguageSettingsServer.is_pseudolocalization_enabled(), "pseudolocalization is off by default")
	LanguageSettingsServer.set_pseudolocalization_enabled(true)
	expect(TranslationServer.pseudolocalization_enabled, "set_pseudolocalization_enabled() reaches TranslationServer")
	expect(LanguageSettingsServer.is_pseudolocalization_enabled(), "is_pseudolocalization_enabled()")
	var text := tr("CHECK17_HELLO")
	expect(text != "Bonjour" and text.length() >= "Bonjour".length(), "the text is pseudolocalized (changed, not shorter)")
	LanguageSettingsServer.set_pseudolocalization_enabled(false)
	expect_equal(tr("CHECK17_HELLO"), "Bonjour", "pseudolocalization off gives back the translation")
	LanguageSettingsServer.reset_to_defaults()


func _check_layout_direction() -> void:
	var root := get_tree().root
	LanguageSettingsServer.set_layout_direction(Window.LAYOUT_DIRECTION_RTL)
	expect_equal(root.get_layout_direction(), Window.LAYOUT_DIRECTION_RTL, "the root window gets the layout direction")
	expect_equal(LanguageSettingsServer.get_layout_direction(), Window.LAYOUT_DIRECTION_RTL, "get_layout_direction()")
	LanguageSettingsServer.set_layout_direction(Window.LAYOUT_DIRECTION_LTR)
	expect_equal(root.get_layout_direction(), Window.LAYOUT_DIRECTION_LTR, "left-to-right")
	LanguageSettingsServer.reset_to_defaults()
	expect_equal(root.get_layout_direction(), Window.LAYOUT_DIRECTION_INHERITED, "reset_to_defaults() gives back the inherited direction")


func _check_auto_translate_mode() -> void:
	var root := get_tree().root
	LanguageSettingsServer.set_root_auto_translate_mode(Node.AUTO_TRANSLATE_MODE_DISABLED)
	expect_equal(root.auto_translate_mode, Node.AUTO_TRANSLATE_MODE_DISABLED, "the root window gets the auto-translate mode")
	expect_equal(LanguageSettingsServer.get_root_auto_translate_mode(), Node.AUTO_TRANSLATE_MODE_DISABLED, "get_root_auto_translate_mode()")
	LanguageSettingsServer.reset_to_defaults()
	expect_equal(root.auto_translate_mode, Node.AUTO_TRANSLATE_MODE_ALWAYS, "reset_to_defaults() gives back ALWAYS")


func _check_resource() -> void:
	var settings := LanguageSettings.new()
	expect_equal(settings.locale, "", "LanguageSettings: default locale")
	expect_equal(settings.pseudolocalization_enabled, false, "LanguageSettings: default pseudolocalization")
	expect_equal(settings.layout_direction, Window.LAYOUT_DIRECTION_INHERITED, "LanguageSettings: default layout direction")
	settings.locale = "fr"
	expect_equal(settings.locale, "fr", "LanguageSettings: locale is a plain property")


func _check_restart() -> void:
	if not OS.has_feature("pc"):
		print("  SKIP  restart check (needs a desktop platform to start a second process)")
		return

	# 1) The saved locale, layout direction and auto-translate mode are applied before the main scene.
	LanguageSettingsServer.set_locale("fr")
	LanguageSettingsServer.set_layout_direction(Window.LAYOUT_DIRECTION_RTL)
	LanguageSettingsServer.set_root_auto_translate_mode(Node.AUTO_TRANSLATE_MODE_DISABLED)
	expect_equal(LanguageSettingsServer.save(), OK, "save() writes the player file")
	expect(FileAccess.file_exists(LANGUAGE_FILE), "the player file is in user://settings/")
	expect_equal(_read_stored_locale(), "fr", "the file contains the locale")
	var probe := _run_probe([])
	expect(probe.get("started", false), "restart: the domain was started")
	expect_equal(probe.get("locale", ""), "fr", "restart: the saved locale is active")
	expect_equal(probe.get("hello", ""), "Bonjour", "restart: the first text is already French")
	expect_equal(int(probe.get("layout_direction", -1)), Window.LAYOUT_DIRECTION_RTL, "restart: the saved layout direction is on the root window")
	expect_equal(int(probe.get("auto_translate_mode", -1)), Node.AUTO_TRANSLATE_MODE_DISABLED, "restart: the saved auto-translate mode is on the root window")
	expect_equal(int(probe.get("ready_frame", -1)), 0, "restart: applied before the first frame")

	# 2) --language is a session override: active, but never saved.
	probe = _run_probe(["--language", "de"])
	expect_equal(probe.get("command_line_locale", ""), "de", "--language: the argument is read")
	expect_equal(probe.get("locale", ""), "de", "--language wins over the stored locale")
	expect_equal(probe.get("hello", ""), "Hallo", "--language: the first text is German")
	expect_equal(_read_stored_locale(), "fr", "--language is not saved: the file still has the stored locale")

	# 3) Stored pseudolocalization is applied; an unknown stored locale falls back without an error.
	LanguageSettingsServer.set_pseudolocalization_enabled(true)
	LanguageSettingsServer.set_locale("zz_ZZ")
	expect_equal(LanguageSettingsServer.save(), OK, "save() with an unknown locale")
	probe = _run_probe([])
	expect(probe.get("pseudolocalization", false), "restart: the saved pseudolocalization is on")
	expect_equal(probe.get("locale", ""), probe.get("project_locale", "?"), "restart: an unknown stored locale falls back to the project locale")


func _read_stored_locale() -> String:
	# The settings file is written in the plain INI mode of the text formats, not as a ConfigFile.
	if not FileAccess.file_exists(LANGUAGE_FILE):
		return "<no file>"
	for line in FileAccess.get_file_as_string(LANGUAGE_FILE).split("\n"):
		var stripped := line.strip_edges()
		if stripped.begins_with("locale="):
			return stripped.trim_prefix("locale=").trim_prefix("\"").trim_suffix("\"")
	return "<no locale key>"


func _run_probe(extra_args: Array) -> Dictionary:
	var output := []
	var args := PackedStringArray(["--headless"])
	args.append_array(PackedStringArray(extra_args))
	args.append_array(PackedStringArray(["--path", ProjectSettings.globalize_path("res://"), PROBE_SCENE]))
	var code := OS.execute(OS.get_executable_path(), args, output, true)
	expect_equal(code, 0, "restart: the probe process ran")
	for chunk in output:
		for line in str(chunk).split("\n"):
			if line.begins_with("CHECK17_PROBE "):
				var parsed = JSON.parse_string(line.trim_prefix("CHECK17_PROBE "))
				if parsed is Dictionary:
					return parsed
	expect(false, "restart: the probe printed its result")
	return {}


## Issue #173: the typed signal fires once per applied change, not for pending changes in manual mode.
func _check_typed_signals() -> void:
	if not Engine.has_singleton("LanguageSettingsServer"):
		return
	var s: SettingsServer = Engine.get_singleton("LanguageSettingsServer")
	expect(s.has_signal(&"pseudolocalization_enabled_changed"), "LanguageSettingsServer has the typed signal pseudolocalization_enabled_changed")
	var arg_info: Dictionary = {}
	for info: Dictionary in s.get_signal_list():
		if info.name == "pseudolocalization_enabled_changed":
			arg_info = info.args[0]
	expect_equal(arg_info.get("type"), TYPE_BOOL, "pseudolocalization_enabled_changed has a typed argument")
	var file_path := s.get_file_path()
	var had_file := FileAccess.file_exists(file_path)
	var previous_mode := s.apply_mode
	s.apply_mode = SettingsServer.APPLY_MODE_IMMEDIATE
	s.reset_value(&"pseudolocalization_enabled")
	var new_value: Variant = true if s.get_applied_value(&"pseudolocalization_enabled") != true else false
	var received: Array = []
	var on_changed := func(v: Variant) -> void: received.append(v)
	s.connect(&"pseudolocalization_enabled_changed", on_changed)

	s.apply_mode = SettingsServer.APPLY_MODE_MANUAL
	s.set_value(&"pseudolocalization_enabled", new_value)
	expect_equal(received.size(), 0, "pseudolocalization_enabled_changed does not fire for a pending change in manual mode")
	s.apply()
	expect_equal(received, [new_value], "pseudolocalization_enabled_changed fires once on apply() with the applied value")
	if not received.is_empty():
		expect_equal(typeof(received[0]), TYPE_BOOL, "pseudolocalization_enabled_changed passes a typed value")
	s.apply()
	expect_equal(received.size(), 1, "pseudolocalization_enabled_changed does not fire when nothing changed")
	s.reset_value(&"pseudolocalization_enabled")
	expect_equal(received.size(), 1, "pseudolocalization_enabled_changed waits for apply() after a reset in manual mode")
	s.apply()
	expect_equal(received.size(), 2, "pseudolocalization_enabled_changed fires again when the default is applied")

	s.disconnect(&"pseudolocalization_enabled_changed", on_changed)
	s.apply_mode = previous_mode
	await get_tree().process_frame
	if not had_file:
		DirAccess.remove_absolute(file_path)
