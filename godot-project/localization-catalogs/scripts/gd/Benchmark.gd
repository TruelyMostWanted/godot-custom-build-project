extends Node
## Benchmark of the localization data and servers: a catalog against Godot's Translation, lookups through
## TextLocalizationServer and tr(), localized paths. Prints measurements only; not run by run-checks.ps1.
## Same measurements as scripts/cs/Benchmark.cs. Bigger runs: raise KEY_COUNT (the wiki uses 20 000).
## Run: godot --headless --path godot-project res://localization-catalogs/scenes/benchmark_gd.tscn

const LOCALES: Array[String] = ["en", "de", "fr", "es", "it", "pt", "ru", "ja", "zh", "ko", "pl", "nl"]
const KEY_COUNT := 2000
const CALLS := 20000
const LOOKUP_INDEX := 1000
const PATH := "user://localization_catalogs_benchmark.lcat"

var _keys: Array[StringName] = []


func _ready() -> void:
	print("cpu: %s, debug build: %s" % [OS.get_processor_name(), OS.is_debug_build()])
	print("data: %d keys x %d locales" % [KEY_COUNT, LOCALES.size()])
	for i: int in KEY_COUNT:
		_keys.append(StringName("UI_KEY_%05d" % i))
	_bench_catalog_file()
	_bench_memory()
	_bench_lookup()
	_bench_localized_paths()
	DirAccess.remove_absolute(PATH)
	get_tree().quit()


func _text(locale: String, i: int) -> String:
	return "%s text %d: the quick brown fox jumps over the lazy dog" % [locale, i]


func _ms_since(start: int) -> float:
	return (Time.get_ticks_usec() - start) / 1000.0


func _mb(bytes: float) -> float:
	return bytes / 1048576.0


func _static_memory() -> float:
	return Performance.get_monitor(Performance.MEMORY_STATIC)


func _bench_catalog_file() -> void:
	var start: int = Time.get_ticks_usec()
	var catalog := TextLocalizationCatalog.new()
	for locale: String in LOCALES:
		catalog.add_locale(locale)
	for i: int in KEY_COUNT:
		for locale: String in LOCALES:
			catalog.set_text(_keys[i], locale, _text(locale, i))
	print("generate catalog: %.1f ms" % _ms_since(start))
	start = Time.get_ticks_usec()
	catalog.save_to_file(PATH)
	print("save .lcat: %.1f ms" % _ms_since(start))
	var file := FileAccess.open(PATH, FileAccess.READ)
	var size: int = file.get_length()
	print(".lcat file: %.2f MB, %.1f bytes per message" % [_mb(size), float(size) / (KEY_COUNT * LOCALES.size())])


## Catalog (.lcat, all locales and only en + de) against one Godot Translation per locale.
func _bench_memory() -> void:
	var messages: int = KEY_COUNT * LOCALES.size()
	var memory: float = _static_memory()
	var start: int = Time.get_ticks_usec()
	var catalog: TextLocalizationCatalog = ResourceLoader.load(PATH, "", ResourceLoader.CACHE_MODE_IGNORE)
	for locale: String in LOCALES:
		catalog.load_locale(locale)
	var used: float = _static_memory() - memory
	print("catalog, load all locales: %.1f ms, %.2f MB, %.1f bytes per message" % [_ms_since(start), _mb(used), used / messages])
	catalog = null

	memory = _static_memory()
	start = Time.get_ticks_usec()
	var two: TextLocalizationCatalog = ResourceLoader.load(PATH, "", ResourceLoader.CACHE_MODE_IGNORE)
	two.load_locale("en")
	two.load_locale("de")
	used = _static_memory() - memory
	print("catalog, load en + de: %.1f ms, %.2f MB" % [_ms_since(start), _mb(used)])
	two = null

	memory = _static_memory()
	start = Time.get_ticks_usec()
	var translations: Array[Translation] = []
	for locale: String in LOCALES:
		var translation := Translation.new()
		translation.locale = locale
		for i: int in KEY_COUNT:
			translation.add_message(_keys[i], _text(locale, i))
		translations.append(translation)
	used = _static_memory() - memory
	print("Godot Translation, build all locales: %.1f ms, %.2f MB, %.1f bytes per message" % [_ms_since(start), _mb(used), used / messages])


## ns per call, including the GDScript call.
func _bench_lookup() -> void:
	var catalog: TextLocalizationCatalog = ResourceLoader.load(PATH, "", ResourceLoader.CACHE_MODE_IGNORE)
	var translation := Translation.new()
	translation.locale = "de"
	for i: int in KEY_COUNT:
		translation.add_message(_keys[i], _text("de", i))
	TextLocalizationServer.add_catalog(catalog)
	var previous_locale: String = TranslationServer.get_locale()
	TranslationServer.set_locale("de")
	var key: StringName = _keys[LOOKUP_INDEX]
	var id: int = TextLocalizationServer.get_key_id(key)

	var start: int = Time.get_ticks_usec()
	for i: int in CALLS:
		catalog.get_key_count()
	print("call overhead (get_key_count): %.0f ns" % (_ms_since(start) * 1000000.0 / CALLS))
	start = Time.get_ticks_usec()
	for i: int in CALLS:
		catalog.get_text(key, "de")
	print("TextLocalizationCatalog.get_text: %.0f ns" % (_ms_since(start) * 1000000.0 / CALLS))
	start = Time.get_ticks_usec()
	for i: int in CALLS:
		translation.get_message(key)
	print("Translation.get_message: %.0f ns" % (_ms_since(start) * 1000000.0 / CALLS))
	start = Time.get_ticks_usec()
	for i: int in CALLS:
		TextLocalizationServer.translate(key)
	print("TextLocalizationServer.translate: %.0f ns" % (_ms_since(start) * 1000000.0 / CALLS))
	start = Time.get_ticks_usec()
	for i: int in CALLS:
		TextLocalizationServer.translate_id(id)
	print("TextLocalizationServer.translate_id: %.0f ns" % (_ms_since(start) * 1000000.0 / CALLS))
	start = Time.get_ticks_usec()
	for i: int in CALLS:
		tr(key)
	print("tr() through the adapter: %.0f ns" % (_ms_since(start) * 1000000.0 / CALLS))

	TextLocalizationServer.clear_catalogs()
	TranslationServer.set_locale(previous_locale)


func _bench_localized_paths() -> void:
	var catalog := ResourceLocalizationCatalog.new()
	for i: int in KEY_COUNT:
		catalog.set_localized_path("res://voice/line_%d.ogg" % i, "de", "res://voice/de/line_%d.ogg" % i)
	ResourceLocalizationServer.add_catalog(catalog)
	var path: String = "res://voice/line_%d.ogg" % LOOKUP_INDEX
	var start: int = Time.get_ticks_usec()
	for i: int in CALLS:
		ResourceLocalizationServer.get_localized_path(path, "de")
	print("ResourceLocalizationServer.get_localized_path: %.0f ns" % (_ms_since(start) * 1000000.0 / CALLS))
	ResourceLocalizationServer.clear_catalogs()
