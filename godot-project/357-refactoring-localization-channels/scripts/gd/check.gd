extends FeatureCheck
## GDScript checks of 357-refactoring-localization-channels. Check the same things as scripts/cs/Issue357RefactoringLocalizationChannelsCheck.cs.

const DATA_DIR := "user://check_357"
const VOICE := "res://voice/line_1.ogg"
const LOGO := "res://ui/logo.png"

var _events: Array = []


## Counts NOTIFICATION_TRANSLATION_CHANGED.
class Counter extends Node:
	var count := 0

	func _notification(what: int) -> void:
		if what == NOTIFICATION_TRANSLATION_CHANGED:
			count += 1


func _get_check_name() -> String:
	return "357-refactoring-localization-channels"


func _on_locale_changed(channel: StringName, locale: String, previous_locale: String) -> void:
	_events.append([channel, locale, previous_locale])


func _ui_catalog() -> TextLocalizationCatalog:
	var catalog := TextLocalizationCatalog.new()
	catalog.set_text(&"HELLO", "en", "Hello")
	catalog.set_text(&"HELLO", "de", "Hallo")
	catalog.set_text(&"HELLO", "ja", "konnichiwa")
	return catalog


func _subtitle_catalog() -> TextLocalizationCatalog:
	var catalog := TextLocalizationCatalog.new()
	catalog.channel = &"subtitles"
	catalog.set_text(&"LINE", "en", "Where are we?")
	catalog.set_text(&"LINE", "de", "Wo sind wir?")
	catalog.set_text(&"LINE", "ja", "doko desu ka")
	return catalog


func _reset_channels() -> void:
	for channel in TranslationServer.get_locale_channels():
		TranslationServer.clear_locale_for(channel)


func _run_checks() -> void:
	var text_server := TextLocalizationServer
	var resource_server := ResourceLocalizationServer
	var previous_locale := TranslationServer.get_locale()
	text_server.clear_catalogs()
	resource_server.clear_catalogs()
	_reset_channels()
	TranslationServer.set_locale("en")
	TranslationServer.locale_changed.connect(_on_locale_changed)

	# Removed API.
	expect(not ClassDB.class_exists("LocalizationCatalogList"), "LocalizationCatalogList is removed")
	expect(not ClassDB.class_has_method("LocalizationServer", "set_locale", true), "LocalizationServer.set_locale is removed")
	expect(not ClassDB.class_has_method("TextLocalizationServer", "get_translation_key", true), "get_translation_key is removed")
	expect(not ClassDB.class_has_method("LocalizationServer", "add_catalog_list", true), "add_catalog_list is removed")
	expect(not ProjectSettings.has_setting("internationalization/localization/external_catalogs"), "external_catalogs setting is removed")
	expect(ProjectSettings.has_setting("internationalization/localization/catalog_manifests"), "setting catalog_manifests")

	# Channels: UI German, audio Japanese, subtitles English.
	var channels := TranslationServer.get_locale_channels()
	expect(channels.has("interface") and channels.has("audio") and channels.has("subtitles"), "default channels: " + str(channels))
	TranslationServer.set_locale_for(&"interface", "de")
	expect_equal(_events.back(), [&"interface", "de", "en"], "locale_changed(channel, locale, previous_locale)")
	TranslationServer.set_locale_for(&"audio", "ja")
	TranslationServer.set_locale_for(&"subtitles", "en")
	expect_equal(TranslationServer.get_locale_for(&"interface"), "de", "get_locale_for interface")
	expect_equal(TranslationServer.get_locale_for(&"audio"), "ja", "get_locale_for audio")
	expect_equal(TranslationServer.get_locale_for(&"subtitles"), "en", "get_locale_for subtitles")
	expect_equal(TranslationServer.get_locale(), "en", "the general locale is untouched")
	TranslationServer.set_locale("fr")
	expect_equal(_events.back(), [&"", "fr", "en"], "the general locale emits the channel &\"\"")
	expect_equal(TranslationServer.get_locale_for(&"interface"), "de", "an override survives a general change")
	TranslationServer.set_locale("en")
	TranslationServer.set_locale_for(&"custom", "pl")
	expect(TranslationServer.get_locale_channels().has("custom"), "a custom channel is listed once set")
	TranslationServer.clear_locale_for(&"custom")
	expect_equal(TranslationServer.get_locale_for(&"custom"), "en", "empty override -> general locale")

	# Text catalogs: tr() follows interface, the domain "subtitles" follows subtitles.
	text_server.add_catalog(_ui_catalog())
	text_server.add_catalog(_subtitle_catalog())
	expect_equal(tr(&"HELLO"), "Hallo", "tr() follows the interface channel")
	expect_equal(text_server.translate(&"HELLO"), "Hallo", "translate() uses the channel locale of the key")
	var subtitles_domain := TranslationServer.get_or_add_domain(&"subtitles")
	expect_equal(String(subtitles_domain.translate(&"LINE")), "Where are we?", "domain subtitles follows the subtitles channel")
	expect_equal(tr(&"LINE"), "LINE", "a subtitle key is not in the main domain")
	var node := Node.new()
	node.set_translation_domain(&"subtitles")
	expect_equal(node.tr(&"LINE"), "Where are we?", "Node translation domain picks the channel")
	expect_equal(text_server.translate_for_locale("de", &"LINE"), "Wo sind wir?", "translate_for_locale")
	TranslationServer.set_locale_for(&"subtitles", "de")
	expect_equal(String(subtitles_domain.translate(&"LINE")), "Wo sind wir?", "subtitles channel change")
	expect_equal(tr(&"HELLO"), "Hallo", "interface texts stay")
	TranslationServer.clear_locale_for(&"subtitles")
	expect_equal(text_server.translate(&"LINE"), "Where are we?", "cleared override follows the general locale")
	expect(text_server.get_missing_keys("fr").has("HELLO"), "get_missing_keys(locale)")
	node.free()

	# NOTIFICATION_TRANSLATION_CHANGED: a channel with a domain notifies, an audio-only change does not.
	var counter := Counter.new()
	add_child(counter)
	await get_tree().process_frame
	var before := counter.count
	TranslationServer.set_locale_for(&"audio", "de")
	await get_tree().process_frame
	expect_equal(counter.count, before, "an audio-only change sends no notification")
	TranslationServer.set_locale_for(&"subtitles", "fr")
	await get_tree().process_frame
	expect(counter.count > before, "a subtitles change sends NOTIFICATION_TRANSLATION_CHANGED")
	TranslationServer.clear_locale_for(&"subtitles")
	TranslationServer.set_locale_for(&"audio", "ja")
	counter.queue_free()

	# Resource catalog: an AudioStream key follows the audio channel only.
	var resources := ResourceLocalizationCatalog.new()
	expect_equal(resources.channel, &"", "ResourceLocalizationCatalog default channel is by key type")
	resources.set_expected_type(VOICE, "AudioStream")
	resources.set_localized_path(VOICE, "de", "res://voice/line_1_de.ogg")
	resources.set_localized_path(VOICE, "ja", "res://voice/line_1_ja.ogg")
	resources.set_localized_path(LOGO, "de", "res://ui/logo_de.png")
	resources.set_localized_path(LOGO, "ja", "res://ui/logo_ja.png")
	resource_server.add_catalog(resources)
	expect_equal(resource_server.get_localized_path(VOICE), "res://voice/line_1_ja.ogg", "the voice file follows audio (ja)")
	expect_equal(resource_server.get_localized_path(LOGO), "res://ui/logo_de.png", "other resources follow interface (de)")
	TranslationServer.set_locale_for(&"audio", "de")
	expect_equal(resource_server.get_localized_path(VOICE), "res://voice/line_1_de.ogg", "audio channel change")
	TranslationServer.clear_locale_for(&"audio")
	expect_equal(resource_server.get_localized_path(VOICE), VOICE, "audio follows the general locale (en, no entry)")
	TranslationServer.set_locale_for(&"audio", "ja")

	# The channel property round trips through the file formats.
	DirAccess.make_dir_recursive_absolute(DATA_DIR + "/formats")
	for extension in ["json", "yaml", "lcat", "tres"]:
		var path: String = DATA_DIR + "/formats/subs." + extension
		var saved: Error = ResourceSaver.save(_subtitle_catalog(), path) if extension == "tres" else _subtitle_catalog().save_to_file(path)
		expect_equal(saved, OK, "save " + extension)
		var loaded := ResourceLoader.load(path, "TextLocalizationCatalog", ResourceLoader.CACHE_MODE_IGNORE) as TextLocalizationCatalog
		expect(loaded != null and loaded.channel == &"subtitles", "channel round trip: " + extension)
	var extensions := LocalizationCatalog.get_file_extensions()
	expect(extensions.has("lcat") and extensions.has("json"), "get_file_extensions")

	# Mods: one manifest with a wildcard folder, the list order is the priority.
	text_server.clear_catalogs()
	DirAccess.make_dir_recursive_absolute(DATA_DIR + "/mods/a/translations")
	DirAccess.make_dir_recursive_absolute(DATA_DIR + "/mods/b/translations")
	DirAccess.make_dir_recursive_absolute(DATA_DIR + "/mods/c/translations")
	_ui_catalog().save_to_file(DATA_DIR + "/game.lcat")
	var mod_json := TextLocalizationCatalog.new()
	mod_json.set_text(&"MOD_JSON", "de", "aus json")
	mod_json.save_to_file(DATA_DIR + "/mods/a/translations/mod.json")
	var mod_yaml := TextLocalizationCatalog.new()
	mod_yaml.channel = &"subtitles"
	mod_yaml.set_text(&"MOD_YAML", "en", "aus yaml")
	mod_yaml.save_to_file(DATA_DIR + "/mods/b/translations/mod.yaml")
	var mod_lcat := TextLocalizationCatalog.new()
	mod_lcat.set_text(&"HELLO", "de", "Servus")
	mod_lcat.save_to_file(DATA_DIR + "/mods/c/translations/mod.lcat")
	var base_manifest := FilesManifest.new()
	base_manifest.file_paths = PackedStringArray([DATA_DIR + "/game.lcat"])
	var mod_manifest := FilesManifest.new()
	mod_manifest.type_hint = &"TextLocalizationCatalog"
	mod_manifest.folder_paths = PackedStringArray([DATA_DIR + "/mods/*/translations"])
	expect_equal(text_server.add_catalog_manifest(base_manifest), 1, "add_catalog_manifest loads the game")
	expect_equal(text_server.add_catalog_manifest(mod_manifest, 10), 3, "add_catalog_manifest loads json, yaml and lcat of every mod")
	expect_equal(text_server.translate(&"HELLO"), "Servus", "the mod (higher priority) wins")
	expect_equal(text_server.translate(&"MOD_JSON"), "aus json", "json mod (interface, de)")
	expect_equal(String(subtitles_domain.translate(&"MOD_YAML")), "aus yaml", "yaml mod follows its channel (subtitles, en)")

	# The locale chain: the closest other locale comes before the fallback.
	text_server.clear_catalogs()
	var austrian := TextLocalizationCatalog.new()
	austrian.set_text(&"HELLO", "de_AT", "Servus")
	austrian.set_text(&"HELLO", "en", "Hello")
	text_server.add_catalog(austrian)
	var chain := text_server.get_locale_chain("de_DE")
	expect(chain.size() >= 3 and chain[0] == "de_DE" and chain[1] == "de" and chain[2] == "de_AT", "chain de_DE -> de -> de_AT: " + str(chain))
	TranslationServer.set_locale_for(&"interface", "de_DE")
	expect_equal(text_server.translate(&"HELLO"), "Servus", "de_DE falls back to de_AT before en")

	TranslationServer.locale_changed.disconnect(_on_locale_changed)
	text_server.clear_catalogs()
	resource_server.clear_catalogs()
	_reset_channels()
	TranslationServer.set_locale(previous_locale)
