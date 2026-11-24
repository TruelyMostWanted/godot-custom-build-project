extends FeatureCheck
## GDScript checks of 103-feature-ogg-vorbis-loader. Check the same things as scripts/cs/Issue103FeatureOggVorbisLoaderCheck.cs.

const DIR := "user://103-feature-ogg-vorbis-loader"
const RES := "res://103-feature-ogg-vorbis-loader/resources"


func _get_check_name() -> String:
	return "103-feature-ogg-vorbis-loader"


func _run_checks() -> void:
	DirAccess.make_dir_recursive_absolute(DIR)
	var path := DIR.path_join("tone.ogg")
	# kept_tone.ogg uses import mode "Keep File": its bytes are a raw Ogg Vorbis file (0.5 s, 440 Hz, 22050 Hz mono).
	var bytes := FileAccess.get_file_as_bytes(RES.path_join("kept_tone.ogg"))
	var file := FileAccess.open(path, FileAccess.WRITE)
	file.store_buffer(bytes)
	file.close()

	expect(ResourceLoader.exists(path), "ResourceLoader.exists() finds an .ogg in user://")
	expect(ResourceLoader.exists(path, "AudioStream"), "exists() with type hint AudioStream")
	expect("ogg" in ResourceLoader.get_recognized_extensions_for_type("AudioStreamOggVorbis"), "ogg is a recognized extension")

	var stream := ResourceLoader.load(path, "", ResourceLoader.CACHE_MODE_IGNORE) as AudioStreamOggVorbis
	if expect(stream != null, "ResourceLoader.load() returns an AudioStreamOggVorbis from user://"):
		expect(absf(stream.get_length() - 0.5) < 0.05, "length is about 0.5 s (got %f)" % stream.get_length())
		expect_equal(stream.loop, false, "default import options: no loop")
		expect(stream.instantiate_playback() != null, "the stream can be played")

	var first := load(path)
	expect(first != null and first == load(path), "load() caches the stream")

	# No Ogg Vorbis encoder in the engine.
	expect(not ("ogg" in ResourceSaver.get_recognized_extensions(stream)), "ResourceSaver offers no .ogg")
	expect(ResourceSaver.save(stream, DIR.path_join("copy.ogg")) != OK, "ResourceSaver.save() cannot write .ogg")

	# Importer precedence: imported_tone.ogg.import sets loop=true, the runtime loader never loops.
	var imported := load(RES.path_join("imported_tone.ogg")) as AudioStreamOggVorbis
	if expect(imported != null, "imported res:// Ogg Vorbis loads"):
		expect_equal(imported.loop, true, "imported res:// file comes from the importer (loop=true)")
	var kept := load(RES.path_join("kept_tone.ogg")) as AudioStreamOggVorbis
	if expect(kept != null, "Ogg Vorbis with import mode Keep File loads"):
		expect_equal(kept.loop, false, "Keep File comes from the runtime loader (loop=false)")

	# Threaded loading.
	var threaded := DIR.path_join("threaded.ogg")
	DirAccess.copy_absolute(path, threaded)
	expect_equal(ResourceLoader.load_threaded_request(threaded, "AudioStreamOggVorbis"), OK, "load_threaded_request() accepts a user:// Ogg Vorbis file")
	var status := ResourceLoader.load_threaded_get_status(threaded)
	var frames := 0
	while status == ResourceLoader.THREAD_LOAD_IN_PROGRESS and frames < 600:
		await get_tree().process_frame
		frames += 1
		status = ResourceLoader.load_threaded_get_status(threaded)
	expect_equal(status, ResourceLoader.THREAD_LOAD_LOADED, "threaded load finishes")
	expect(ResourceLoader.load_threaded_get(threaded) is AudioStreamOggVorbis, "load_threaded_get() returns the AudioStreamOggVorbis")
