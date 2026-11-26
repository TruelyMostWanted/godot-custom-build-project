extends FeatureCheck
## GDScript checks of 343-feature-text-localization-server. Check the same things as scripts/cs/Issue343FeatureTextLocalizationServerCheck.cs.

const DATA_DIR := "user://check_343"

var _locale_changes: Array = []
var _catalogs_changed := 0


func _get_check_name() -> String:
	return "343-feature-text-localization-server"


func _on_locale_changed(channel: StringName, locale: String, previous_locale: String) -> void:
	_locale_changes.append([channel, locale, previous_locale])


func _on_catalogs_changed() -> void:
	_catalogs_changed += 1


func _game_catalog() -> TextLocalizationCatalog:
	var catalog := TextLocalizationCatalog.new()
	catalog.set_text(&"HELLO", "en", "Hello")
	catalog.set_text(&"HELLO", "de", "Hallo")
	catalog.set_text(&"HELLO", "de", "Hallo (Menü)", &"menu")
	catalog.set_text(&"BYE", "en", "Bye")
	catalog.set_text(&"GREET", "de", "Hallo {name}")
	catalog.set_plural_rule("de", "nplurals=2; plural=(n != 1);")
	catalog.set_plural_texts(&"APPLE", "de", PackedStringArray(["{n} Apfel", "{n} Äpfel"]))
	catalog.set_text(&"HELLO", "fr", "Bonjour")
	return catalog


func _mod_catalog() -> TextLocalizationCatalog:
	var catalog := TextLocalizationCatalog.new()
	catalog.set_text(&"HELLO", "de", "Servus")
	catalog.set_text(&"MOD_ONLY", "de", "Nur im Mod")
	return catalog


func _run_checks() -> void:
	var server := TextLocalizationServer
	expect(Engine.has_singleton("TextLocalizationServer"), "TextLocalizationServer is a registered singleton")
	expect(ClassDB.class_exists("LocalizationServer") and not ClassDB.can_instantiate("LocalizationServer"), "LocalizationServer is abstract")
	var previous_locale := TranslationServer.get_locale()
	server.clear_catalogs()
	TranslationServer.set_locale("en")
	TranslationServer.locale_changed.connect(_on_locale_changed)
	server.catalogs_changed.connect(_on_catalogs_changed)

	# Locale (TranslationServer) and chain (the closest other locale comes before the fallback).
	var fallback := TranslationServer.get_fallback_locale()
	expect_equal(server.get_locale_chain("de_DE"), PackedStringArray(["de_DE", "de", fallback]), "chain de_DE -> de -> fallback")

	# Game and mod layers: the mod (priority 100) overrides the game.
	var game := _game_catalog()
	var mod := _mod_catalog()
	server.add_catalog(game)
	server.add_catalog(mod, 100)
	expect_equal(_catalogs_changed, 2, "catalogs_changed per add_catalog")
	TranslationServer.set_locale("de_DE")
	expect_equal(TranslationServer.get_locale(), "de_DE", "TranslationServer is the locale source")
	expect_equal(_locale_changes.back(), [&"", "de_DE", "en"], "TranslationServer.locale_changed(channel, locale, previous_locale)")
	expect_equal(server.translate(&"HELLO"), "Servus", "mod wins over game")
	expect_equal(server.translate(&"HELLO", &"menu"), "Hallo (Menü)", "context")
	expect_equal(server.translate(&"BYE"), "Bye", "fallback locale")
	expect_equal(server.translate(&"UNKNOWN"), "UNKNOWN", "missing key returns the key")
	expect_equal(server.translate_for_locale("fr", &"HELLO"), "Bonjour", "translate_for_locale")
	expect(server.has_translation(&"MOD_ONLY") and not server.has_translation(&"UNKNOWN"), "has_translation")

	# tr(), atr(), plurals and pseudolocalization through the adapter.
	expect_equal(tr(&"HELLO"), "Servus", "tr() through the adapter")
	expect_equal(tr(&"HELLO", &"menu"), "Hallo (Menü)", "tr() with context")
	expect_equal(atr(&"HELLO"), "Servus", "atr() through the adapter")
	expect_equal(tr_n(&"APPLE", &"APPLES", 5), "{n} Äpfel", "tr_n() through the adapter")
	TranslationServer.pseudolocalization_enabled = true
	var pseudo := tr(&"HELLO")
	TranslationServer.pseudolocalization_enabled = false
	expect(pseudo != "Servus" and pseudo.begins_with("["), "pseudolocalization still applies: " + pseudo)

	# A direct TranslationServer.set_locale() reaches the server too.
	TranslationServer.set_locale("en")
	expect_equal(server.translate(&"HELLO"), "Hello", "TranslationServer.set_locale is the locale source")
	TranslationServer.set_locale("de")

	# Key ids, plurals and arguments.
	var hello_id := server.get_key_id(&"HELLO")
	expect(hello_id >= 0 and server.get_key_id(&"UNKNOWN") == -1, "get_key_id")
	expect_equal(server.translate_id(hello_id), "Servus", "translate_id")
	server.remove_catalog(mod)
	expect_equal(server.get_key_id(&"HELLO"), hello_id, "key ids stay valid after remove_catalog")
	expect_equal(server.translate_id(hello_id), "Hallo", "translate_id after remove_catalog")
	server.add_catalog(mod, 100)
	expect_equal(server.translate(&"APPLE", &"", 1, { "n": 1 }), "1 Apfel", "plural form 1")
	expect_equal(server.translate(&"APPLE", &"", 3, { "n": 3 }), "3 Äpfel", "plural form 3 with args")
	expect_equal(server.translate(&"GREET", &"", -1, { "name": "Ann" }), "Hallo Ann", "placeholders from args")
	expect_equal(TextInsertionServer.insert("{k:t}!", { "k": "HELLO" }), "Servus!", "t modifier uses TextLocalizationServer")

	# Keys of a text and missing keys.
	expect_equal(server.get_translation_keys("Bye"), PackedStringArray(["BYE"]), "get_translation_keys")
	expect(server.get_missing_keys("de").has("BYE"), "get_missing_keys")
	expect(server.get_available_locales().has("fr"), "get_available_locales")

	# Catalog changes are applied on the next idle frame.
	game.set_text(&"BYE", "de", "Tschüss")
	await get_tree().process_frame
	expect_equal(server.translate(&"BYE"), "Tschüss", "catalog change picked up")

	# Catalog manifests with folder wildcards and on-demand locales.
	server.clear_catalogs()
	DirAccess.make_dir_recursive_absolute(DATA_DIR + "/mods/knights/translations")
	game.save_to_file(DATA_DIR + "/game.lcat")
	_mod_catalog().save_to_file(DATA_DIR + "/mods/knights/translations/texts.lcat")
	var base_manifest := FilesManifest.new()
	base_manifest.file_paths = PackedStringArray([DATA_DIR + "/game.lcat"])
	var mod_manifest := FilesManifest.new()
	mod_manifest.type_hint = &"TextLocalizationCatalog"
	mod_manifest.folder_paths = PackedStringArray([DATA_DIR + "/mods/*/translations"])
	expect_equal(server.add_catalog_manifest(base_manifest), 1, "add_catalog_manifest loads the game")
	expect_equal(server.add_catalog_manifest(mod_manifest, 100), 1, "add_catalog_manifest loads the mod (wildcard folder)")
	var loaded: LocalizationCatalog = server.get_catalogs()[0]
	expect_equal(server.translate(&"HELLO"), "Servus", "mod from user://mods/*/translations")
	expect(loaded.is_locale_loaded("de") and not loaded.is_locale_loaded("fr"), "only the chain locales are read")

	# Language settings list and accept a locale that only a catalog has (#352).
	server.clear_catalogs()
	var italian := TextLocalizationCatalog.new()
	italian.set_text(&"HELLO", "it", "Ciao")
	server.add_catalog(italian)
	var settings_locale := LanguageSettingsServer.get_locale()
	expect(not TranslationServer.get_loaded_locales().has("it"), "no Italian translation in TranslationServer")
	expect(LanguageSettingsServer.get_available_locales().has("it"), "LanguageSettingsServer lists the catalog locale")
	LanguageSettingsServer.set_locale("it")
	expect_equal(TranslationServer.get_locale(), "it", "LanguageSettingsServer.set_locale switches to a catalog-only locale")
	expect_equal(tr(&"HELLO"), "Ciao", "tr() after the switch")
	LanguageSettingsServer.set_locale(settings_locale)

	TranslationServer.locale_changed.disconnect(_on_locale_changed)
	server.catalogs_changed.disconnect(_on_catalogs_changed)
	server.clear_catalogs()
	TranslationServer.set_locale(previous_locale)
