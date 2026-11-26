extends FeatureCheck
## Benchmark of ResourceLocalizationServer (#344).
## Run: godot --headless --path godot-project res://344-feature-resource-localization-server/scenes/bench_gd.tscn
## 1. get_localized_path() in ns per call (incl. the GDScript call) for N keys, active locale and explicit locale.
##    Godot's _path_remap() is not exposed to scripts: the engine unit test "[ResourceLocalizationServer] Remap cost"
##    prints both sides in C++.
## 2. Relocalization of 100 textures + 100 audio streams after a locale change.
##    BEFORE = what translation_remaps does: every cached remapped resource is loaded again on the main thread during
##    set_locale() (emulated with ResourceLoader.load(CACHE_MODE_REPLACE), the same load + copy_from()).
##    AFTER = ResourceLocalizationServer: set_locale() returns, the loads run on worker threads.
## Results: one log line per value "344 bench <metric> <value> <unit>".

const OUT_DIR := "user://bench_344"
const COUNT := 100
const TEXTURE_SIZE := 256
const CALLS := 100000


func _get_check_name() -> String:
	return "344-resource-localization-bench"


func _report(metric: String, value: float, unit: String) -> void:
	print("344 bench %s %.1f %s" % [metric, value, unit])


func _bench_lookup() -> void:
	var server := ResourceLocalizationServer
	for keys in [100, 1000, 10000]:
		server.clear_catalogs()
		var catalog := ResourceLocalizationCatalog.new()
		for index in keys:
			catalog.set_localized_path("res://voice/line_%d.ogg" % index, "de", "res://voice/de/line_%d.ogg" % index)
		server.add_catalog(catalog)
		LocalizationServer.set_locale("de")
		var path := "res://voice/line_%d.ogg" % (keys >> 1)
		var start := Time.get_ticks_usec()
		for _i in CALLS:
			server.get_localized_path(path)
		_report("get_localized_path_active_%d_keys" % keys, (Time.get_ticks_usec() - start) * 1000.0 / CALLS, "ns")
		start = Time.get_ticks_usec()
		for _i in CALLS / 10:
			server.get_localized_path(path, "de_AT")
		_report("get_localized_path_explicit_%d_keys" % keys, (Time.get_ticks_usec() - start) * 10000.0 / CALLS, "ns")
		start = Time.get_ticks_usec()
		LocalizationServer.set_locale("en")
		_report("set_locale_resolve_%d_keys" % keys, (Time.get_ticks_usec() - start) / 1000.0, "ms")
	server.clear_catalogs()


func _write_files() -> Array[String]:
	var originals: Array[String] = []
	for locale in ["en", "de"]:
		DirAccess.make_dir_recursive_absolute(OUT_DIR + "/" + locale)
		for index in COUNT:
			# Size per locale: the headless dummy RenderingServer keeps no pixels, but the size is kept.
			var size := TEXTURE_SIZE if locale == "en" else TEXTURE_SIZE * 2
			var image := Image.create(size, size, false, Image.FORMAT_RGBA8)
			image.fill(Color.RED if locale == "en" else Color.BLUE)
			ResourceSaver.save(ImageTexture.create_from_image(image), OUT_DIR + "/%s/texture_%d.res" % [locale, index])
			var audio := AudioStreamWAV.new()
			audio.format = AudioStreamWAV.FORMAT_16_BITS
			audio.stereo = true
			var data := PackedByteArray()
			data.resize(44100 * 4)
			data.fill(1 if locale == "en" else 2)
			audio.data = data
			ResourceSaver.save(audio, OUT_DIR + "/%s/audio_%d.res" % [locale, index])
	for index in COUNT:
		originals.append(OUT_DIR + "/en/texture_%d.res" % index)
		originals.append(OUT_DIR + "/en/audio_%d.res" % index)
	return originals


func _bench_relocalization() -> void:
	var server := ResourceLocalizationServer
	var originals := _write_files()
	var catalog := ResourceLocalizationCatalog.new()
	for path in originals:
		catalog.set_localized_path(path, "de", path.replace("/en/", "/de/"))
	server.clear_catalogs()
	LocalizationServer.set_locale("en")
	server.add_catalog(catalog)
	var cached: Array[Resource] = []
	for path in originals:
		cached.append(load(path))

	# BEFORE: synchronous reload on the main thread, as translation_remaps does in set_locale().
	LocalizationServer.set_locale("de")
	var start := Time.get_ticks_usec()
	for path in originals:
		ResourceLoader.load(path, "", ResourceLoader.CACHE_MODE_REPLACE)
	var sync_ms := (Time.get_ticks_usec() - start) / 1000.0
	if server.is_relocalizing():
		await server.resources_relocalized
	_report("relocalize_sync_main_thread_block", sync_ms, "ms")

	# AFTER: set_locale() returns at once, the resources follow in the background.
	for run in 3:
		var locale := "en" if run % 2 == 0 else "de"
		start = Time.get_ticks_usec()
		LocalizationServer.set_locale(locale)
		var block_ms := (Time.get_ticks_usec() - start) / 1000.0
		var max_frame_ms := 0.0
		var frames := 0
		while server.is_relocalizing():
			var frame_start := Time.get_ticks_usec()
			await get_tree().process_frame
			max_frame_ms = maxf(max_frame_ms, (Time.get_ticks_usec() - frame_start) / 1000.0)
			frames += 1
		var total_ms := (Time.get_ticks_usec() - start) / 1000.0
		_report("relocalize_async_set_locale_block_run%d" % run, block_ms, "ms")
		_report("relocalize_async_total_run%d" % run, total_ms, "ms")
		_report("relocalize_async_max_frame_run%d" % run, max_frame_ms, "ms")
		_report("relocalize_async_frames_run%d" % run, frames, "frames")
		var texture: ImageTexture = cached[0]
		expect_equal(texture.get_width(), TEXTURE_SIZE if locale == "en" else TEXTURE_SIZE * 2, "texture follows %s" % locale)
	_report("processor_count", OS.get_processor_count(), "cores")
	server.clear_catalogs()


func _run_checks() -> void:
	var previous_locale := LocalizationServer.get_locale()
	_bench_lookup()
	await _bench_relocalization()
	LocalizationServer.set_locale(previous_locale)
