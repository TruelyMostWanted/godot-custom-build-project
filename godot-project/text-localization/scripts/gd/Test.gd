extends FeatureTest
## Full API test of Text Localization (TextLocalizationServer and its base LocalizationServer): every method and signal.
## Same tests as scripts/cs/Test.cs.

const DATA_DIR := "user://text_localization_test"
const PLURAL_RULE_DE := "nplurals=2; plural=(n != 1);"

var _game: TextLocalizationCatalog
var _mod: TextLocalizationCatalog
var _locale_changes: Array = []
var _catalogs_changed: int = 0
var _missing_keys: Array = []
var _loaded_locales: Array[String] = []


func _run_tests() -> void:
	TextLocalizationServer.clear_catalogs()
	TranslationServer.set_locale("en")
	_game = _game_catalog()
	_mod = _mod_catalog()
	_test_singleton()
	_test_locale_chain()
	_test_catalogs()
	_test_locale_source()
	_test_layers()
	_test_key_missing_signal()
	_test_adapter()
	_test_key_ids()
	_test_plurals_and_args()
	_test_reverse_lookup()
	_test_available_locales()
	await _test_catalog_change()
	_test_manifests()
	_test_catalog_only_locale()
	TextLocalizationServer.clear_catalogs()
	TranslationServer.set_locale("en")


func _test_singleton() -> void:
	expect(Engine.has_singleton("TextLocalizationServer"), "TextLocalizationServer is a registered singleton")
	expect(ClassDB.class_exists("LocalizationServer"), "LocalizationServer exists")
	expect(not ClassDB.can_instantiate("LocalizationServer"), "LocalizationServer is abstract")


func _test_locale_chain() -> void:
	var fallback: String = TranslationServer.get_fallback_locale()
	var chain: PackedStringArray = TextLocalizationServer.get_locale_chain("de_DE")
	expect_equal(chain, PackedStringArray(["de_DE", "de", fallback]), "get_locale_chain: de_DE, de, then the fallback")


func _test_catalogs() -> void:
	TextLocalizationServer.catalogs_changed.connect(_on_catalogs_changed)
	TextLocalizationServer.add_catalog(_game)
	TextLocalizationServer.add_catalog(_mod, 100)
	expect_equal(_catalogs_changed, 2, "catalogs_changed is emitted per add_catalog")
	expect(TextLocalizationServer.has_catalog(_mod), "has_catalog finds an added catalog")
	expect_equal(TextLocalizationServer.get_catalogs().size(), 2, "get_catalogs lists both catalogs")
	expect_equal(TextLocalizationServer.get_key_count(), 6, "get_key_count counts the keys of all catalogs")
	TextLocalizationServer.remove_catalog(_mod)
	expect(not TextLocalizationServer.has_catalog(_mod), "remove_catalog removes a catalog")
	expect_equal(_catalogs_changed, 3, "catalogs_changed is emitted by remove_catalog")
	TextLocalizationServer.catalogs_changed.disconnect(_on_catalogs_changed)
	TextLocalizationServer.add_catalog(_mod, 100)


func _test_locale_source() -> void:
	TranslationServer.locale_changed.connect(_on_locale_changed)
	TranslationServer.set_locale("de_DE")
	TranslationServer.locale_changed.disconnect(_on_locale_changed)
	expect_equal(_locale_changes, [[&"", "de_DE", "en"]], "TranslationServer.locale_changed(channel, locale, previous_locale)")
	expect_equal(TextLocalizationServer.translate(&"HELLO"), "Servus", "TranslationServer.set_locale reaches the server")


func _test_layers() -> void:
	expect_equal(TextLocalizationServer.translate(&"HELLO"), "Servus", "the mod (priority 100) wins over the game")
	expect_equal(TextLocalizationServer.translate(&"HELLO", &"menu"), "Hallo (Menü)", "translate with a context")
	expect_equal(TextLocalizationServer.translate(&"BYE"), "Bye", "a key without German text falls back to English")
	expect_equal(TextLocalizationServer.translate(&"UNKNOWN"), "UNKNOWN", "a missing key returns the key")
	expect_equal(TextLocalizationServer.translate_for_locale("fr", &"HELLO"), "Bonjour", "translate_for_locale reads another locale")
	expect(TextLocalizationServer.has_translation(&"MOD_ONLY"), "has_translation finds a key of the mod")
	expect(TextLocalizationServer.has_translation(&"HELLO", &"menu"), "has_translation finds a key with context")
	expect(not TextLocalizationServer.has_translation(&"UNKNOWN"), "has_translation: unknown key")


func _test_key_missing_signal() -> void:
	TextLocalizationServer.key_missing.connect(_on_key_missing)
	TextLocalizationServer.translate(&"NOT_THERE")
	TextLocalizationServer.translate(&"NOT_THERE")
	TextLocalizationServer.key_missing.disconnect(_on_key_missing)
	expect_equal(_missing_keys, [[&"NOT_THERE", "de_DE"]], "key_missing is emitted once per key and locale")


func _test_adapter() -> void:
	expect_equal(tr(&"HELLO"), "Servus", "tr() reads the catalogs through the adapter")
	expect_equal(tr(&"HELLO", &"menu"), "Hallo (Menü)", "tr() with a context")
	expect_equal(atr(&"HELLO"), "Servus", "atr() reads the catalogs through the adapter")
	expect_equal(tr_n(&"APPLE", &"APPLES", 5), "{n} Äpfel", "tr_n() picks the plural form")
	TranslationServer.pseudolocalization_enabled = true
	var pseudo: String = tr(&"HELLO")
	TranslationServer.pseudolocalization_enabled = false
	expect(pseudo.begins_with("["), "pseudolocalization still applies to tr(): " + pseudo)


func _test_key_ids() -> void:
	var hello_id: int = TextLocalizationServer.get_key_id(&"HELLO")
	expect(hello_id >= 0, "get_key_id returns an id")
	expect_equal(TextLocalizationServer.get_key_id(&"UNKNOWN"), -1, "get_key_id of an unknown key is -1")
	expect(TextLocalizationServer.get_key_id(&"HELLO", &"menu") != hello_id, "get_key_id: a context is its own id")
	expect_equal(TextLocalizationServer.translate_id(hello_id), "Servus", "translate_id")
	TextLocalizationServer.remove_catalog(_mod)
	expect_equal(TextLocalizationServer.get_key_id(&"HELLO"), hello_id, "key ids stay valid after remove_catalog")
	expect_equal(TextLocalizationServer.translate_id(hello_id), "Hallo", "translate_id after remove_catalog")
	TextLocalizationServer.add_catalog(_mod, 100)


func _test_plurals_and_args() -> void:
	expect_equal(TextLocalizationServer.translate(&"APPLE", &"", 1, { "n": 1 }), "1 Apfel", "plural form for 1")
	expect_equal(TextLocalizationServer.translate(&"APPLE", &"", 3, { "n": 3 }), "3 Äpfel", "plural form for 3 with args")
	expect_equal(TextLocalizationServer.translate(&"APPLE", &"", 3), "{n} Äpfel", "without args the placeholders stay")
	expect_equal(TextLocalizationServer.translate(&"GREET", &"", -1, { "name": "Ann" }), "Hallo Ann", "placeholders from args")
	var apple_id: int = TextLocalizationServer.get_key_id(&"APPLE")
	expect_equal(TextLocalizationServer.translate_id(apple_id, 2, { "n": 2 }), "2 Äpfel", "translate_id with count and args")
	var de_text: String = TextLocalizationServer.translate_for_locale("de", &"APPLE", &"", 1, { "n": 1 })
	expect_equal(de_text, "1 Apfel", "translate_for_locale with count and args")
	expect_equal(TextInsertionServer.insert("{k:t}!", { "k": "HELLO" }), "Servus!", "the t modifier of TextInsertionServer translates")


func _test_reverse_lookup() -> void:
	expect_equal(TextLocalizationServer.get_translation_keys("Bye"), PackedStringArray(["BYE"]), "get_translation_keys finds the key of a text")
	expect(TextLocalizationServer.get_missing_keys("de").has("BYE"), "get_missing_keys lists a key without German text")
	expect(not TextLocalizationServer.get_missing_keys("de").has("HELLO"), "get_missing_keys skips translated keys")
	expect(TextLocalizationServer.get_missing_keys().has("BYE"), "get_missing_keys without locale uses the current locale")


func _test_available_locales() -> void:
	var locales: PackedStringArray = TextLocalizationServer.get_available_locales()
	expect(locales.has("fr"), "get_available_locales lists French")
	expect(locales.has("de"), "get_available_locales lists German")
	var interface_locales: PackedStringArray = TextLocalizationServer.get_available_locales_for(&"interface")
	expect(interface_locales.has("fr"), "get_available_locales_for(interface) lists the text catalog locales")


func _test_catalog_change() -> void:
	_game.set_text(&"BYE", "de", "Tschüss")
	await get_tree().process_frame
	expect_equal(TextLocalizationServer.translate(&"BYE"), "Tschüss", "a catalog change is picked up on the next frame")


func _test_manifests() -> void:
	TextLocalizationServer.clear_catalogs()
	expect(TextLocalizationServer.get_catalogs().is_empty(), "clear_catalogs removes all catalogs")
	DirAccess.make_dir_recursive_absolute(DATA_DIR + "/mods/knights/translations")
	_game.save_to_file(DATA_DIR + "/game.lcat")
	_mod.save_to_file(DATA_DIR + "/mods/knights/translations/texts.lcat")
	var game_manifest := FilesManifest.new()
	game_manifest.file_paths = PackedStringArray([DATA_DIR + "/game.lcat"])
	var mod_manifest := FilesManifest.new()
	mod_manifest.type_hint = &"TextLocalizationCatalog"
	mod_manifest.folder_paths = PackedStringArray([DATA_DIR + "/mods/*/translations"])
	TextLocalizationServer.locale_loaded.connect(_on_locale_loaded)
	expect_equal(TextLocalizationServer.add_catalog_manifest(game_manifest), 1, "add_catalog_manifest loads the game catalog")
	expect_equal(TextLocalizationServer.add_catalog_manifest(mod_manifest, 100), 1, "add_catalog_manifest loads the mod from a wildcard folder")
	TextLocalizationServer.locale_loaded.disconnect(_on_locale_loaded)
	expect(_loaded_locales.has("de"), "locale_loaded is emitted for a chain locale of an .lcat")
	expect_equal(TextLocalizationServer.translate(&"HELLO"), "Servus", "the mod from user://mods/*/translations wins")
	var loaded: LocalizationCatalog = TextLocalizationServer.get_catalogs()[0]
	expect(loaded.is_locale_loaded("de"), "the chain locale de is read")
	expect(not loaded.is_locale_loaded("fr"), "the locale fr outside the chain is not read")
	TextLocalizationServer.clear_catalogs()
	expect_equal(TextLocalizationServer.load_project_catalogs(), 0, "load_project_catalogs: this project lists no catalog manifests")


func _test_catalog_only_locale() -> void:
	var italian := TextLocalizationCatalog.new()
	italian.set_text(&"HELLO", "it", "Ciao")
	TextLocalizationServer.add_catalog(italian)
	var settings_locale: String = LanguageSettingsServer.get_locale()
	expect(not TranslationServer.get_loaded_locales().has("it"), "no Italian translation in TranslationServer")
	expect(LanguageSettingsServer.get_available_locales().has("it"), "LanguageSettingsServer lists a catalog-only locale")
	LanguageSettingsServer.set_locale("it")
	expect_equal(TranslationServer.get_locale(), "it", "LanguageSettingsServer.set_locale switches to a catalog-only locale")
	expect_equal(tr(&"HELLO"), "Ciao", "tr() after the switch")
	LanguageSettingsServer.set_locale(settings_locale)


func _game_catalog() -> TextLocalizationCatalog:
	var catalog := TextLocalizationCatalog.new()
	catalog.set_text(&"HELLO", "en", "Hello")
	catalog.set_text(&"HELLO", "de", "Hallo")
	catalog.set_text(&"HELLO", "de", "Hallo (Menü)", &"menu")
	catalog.set_text(&"HELLO", "fr", "Bonjour")
	catalog.set_text(&"BYE", "en", "Bye")
	catalog.set_text(&"GREET", "de", "Hallo {name}")
	catalog.set_plural_rule("de", PLURAL_RULE_DE)
	catalog.set_plural_texts(&"APPLE", "de", PackedStringArray(["{n} Apfel", "{n} Äpfel"]))
	return catalog


func _mod_catalog() -> TextLocalizationCatalog:
	var catalog := TextLocalizationCatalog.new()
	catalog.set_text(&"HELLO", "de", "Servus")
	catalog.set_text(&"MOD_ONLY", "de", "Nur im Mod")
	return catalog


func _on_catalogs_changed() -> void:
	_catalogs_changed += 1


func _on_locale_changed(channel: StringName, locale: String, previous_locale: String) -> void:
	_locale_changes.append([channel, locale, previous_locale])


func _on_key_missing(key: StringName, locale: String) -> void:
	_missing_keys.append([key, locale])


func _on_locale_loaded(locale: String) -> void:
	_loaded_locales.append(locale)
