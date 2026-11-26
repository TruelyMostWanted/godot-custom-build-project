extends FeatureCheck
## GDScript checks of 344-feature-resource-localization-server. Check the same things as scripts/cs/Issue344FeatureResourceLocalizationServerCheck.cs.

const DATA_DIR := "user://check_344"
const REMAPS_SETTING := "internationalization/locale/translation_remaps"


func _get_check_name() -> String:
	return "344-feature-resource-localization-server"


# A voice line (a Resource named after its locale) and a logo (ImageTexture, 8 px wide in English, 16 px in German).
func _write_files() -> void:
	DirAccess.make_dir_recursive_absolute(DATA_DIR + "/mods/polish/voice")
	for locale in ["en", "de", "pl"]:
		var voice := Resource.new()
		voice.resource_name = locale
		var folder := DATA_DIR + "/mods/polish/voice" if locale == "pl" else DATA_DIR
		ResourceSaver.save(voice, folder + ("/voice.tres" if locale == "en" else "/voice_%s.tres" % locale))
	for size in [8, 16]:
		var logo := ImageTexture.create_from_image(Image.create(size, 4, false, Image.FORMAT_RGBA8))
		ResourceSaver.save(logo, DATA_DIR + ("/logo.res" if size == 8 else "/logo_de.res"))


func _run_checks() -> void:
	var server := ResourceLocalizationServer
	expect(Engine.has_singleton("ResourceLocalizationServer"), "ResourceLocalizationServer is a registered singleton")
	expect(ClassDB.is_parent_class("ResourceLocalizationServer", "LocalizationServer"), "inherits LocalizationServer")
	expect(ProjectSettings.has_setting("internationalization/localization/catalog_manifests"), "setting catalog_manifests")
	var previous_locale := TranslationServer.get_locale()
	server.clear_catalogs()
	TranslationServer.set_locale("en")
	_write_files()

	var voice_path := DATA_DIR + "/voice.tres"
	var logo_path := DATA_DIR + "/logo.res"
	var game := ResourceLocalizationCatalog.new()
	game.set_localized_path(voice_path, "de", DATA_DIR + "/voice_de.tres")
	game.set_localized_path(logo_path, "de", DATA_DIR + "/logo_de.res")
	game.set_localized_path(logo_path, "fr", DATA_DIR + "/logo_fr.res") # Missing file: falls back with a warning.
	server.add_catalog(game)

	# Paths.
	expect(server.has_localization(voice_path) and not server.has_localization(DATA_DIR + "/other.tres"), "has_localization")
	expect_equal(server.get_localized_path(voice_path), voice_path, "no English entry: the original path")
	expect_equal(server.get_localized_path(voice_path, "de_DE"), DATA_DIR + "/voice_de.tres", "de_DE takes de")
	expect_equal(server.get_localized_path(DATA_DIR + "/other.tres", "de"), DATA_DIR + "/other.tres", "unknown path unchanged")
	expect_equal(server.get_localized_paths(logo_path), {"de": DATA_DIR + "/logo_de.res", "fr": DATA_DIR + "/logo_fr.res"}, "get_localized_paths")
	expect_equal(server.get_missing_locales(voice_path), PackedStringArray(["fr"]), "get_missing_locales")

	# load() follows the locale; cached resources are updated in place in the background.
	var voice: Resource = load(voice_path)
	var logo: Texture2D = load(logo_path)
	expect_equal(voice.resource_name, "en", "load() in English")
	expect_equal(logo.get_width(), 8, "English logo")
	expect_equal(voice.resource_path, voice_path, "cached under the original path")
	TranslationServer.set_locale("de")
	var paths: PackedStringArray = await server.resources_relocalized
	expect(paths.has(voice_path) and paths.has(logo_path), "resources_relocalized(paths): " + str(paths))
	expect(not server.is_relocalizing(), "is_relocalizing() false after the signal")
	expect_equal(voice.resource_name, "de", "voice updated in place")
	expect_equal(logo.get_width(), 16, "logo updated in place")
	expect(load(voice_path) == voice, "load() returns the same instance")

	# A missing localized file: the original is loaded.
	TranslationServer.set_locale("fr")
	await server.resources_relocalized
	expect_equal(logo.get_width(), 8, "missing fr file falls back to the original")

	# A mod adds Polish voice files through a catalog manifest with a wildcard folder (priority 100).
	var mod := ResourceLocalizationCatalog.new()
	mod.set_localized_path(voice_path, "pl", DATA_DIR + "/mods/polish/voice/voice_pl.tres")
	expect_equal(mod.save_to_file(DATA_DIR + "/mods/polish/voice/voice.lcat"), OK, "mod catalog saved as .lcat")
	var manifest := FilesManifest.new()
	manifest.type_hint = &"ResourceLocalizationCatalog"
	manifest.folder_paths = PackedStringArray([DATA_DIR + "/mods/*/voice"])
	expect_equal(server.add_catalog_manifest(manifest, 100), 1, "add_catalog_manifest() adds the mod")
	TranslationServer.set_locale("pl")
	await server.resources_relocalized
	expect_equal(voice.resource_name, "pl", "Polish voice from the mod")
	expect(server.get_available_locales().has("pl"), "pl available")

	# Old translation_remaps are imported as a layer.
	server.clear_catalogs()
	var previous_remaps: Variant = ProjectSettings.get_setting(REMAPS_SETTING, PackedStringArray())
	ProjectSettings.set_setting(REMAPS_SETTING, {voice_path: PackedStringArray([DATA_DIR + "/voice_de.tres:de"])})
	expect_equal(server.load_project_catalogs(), 1, "load_project_catalogs() imports translation_remaps")
	expect_equal(server.get_localized_path(voice_path, "de"), DATA_DIR + "/voice_de.tres", "remap entry resolved")
	ProjectSettings.set_setting(REMAPS_SETTING, previous_remaps)

	server.clear_catalogs()
	await get_tree().process_frame
	TranslationServer.set_locale(previous_locale)
