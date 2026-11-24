extends FeatureCheck
## GDScript checks of 104-feature-mp3-loader. Check the same things as scripts/cs/Issue104FeatureMp3LoaderCheck.cs.

const DIR := "user://104-feature-mp3-loader"
const RES := "res://104-feature-mp3-loader/resources"


func _get_check_name() -> String:
	return "104-feature-mp3-loader"


func _run_checks() -> void:
	DirAccess.make_dir_recursive_absolute(DIR)
	var path := DIR.path_join("tone.mp3")
	# kept_tone.mp3 uses import mode "Keep File": its bytes are a raw MP3 file (0.5 s, 440 Hz, 22050 Hz mono).
	var bytes := FileAccess.get_file_as_bytes(RES.path_join("kept_tone.mp3"))
	var file := FileAccess.open(path, FileAccess.WRITE)
	file.store_buffer(bytes)
	file.close()

	expect(ResourceLoader.exists(path), "ResourceLoader.exists() finds an .mp3 in user://")
	expect(ResourceLoader.exists(path, "AudioStream"), "exists() with type hint AudioStream")
	expect("mp3" in ResourceLoader.get_recognized_extensions_for_type("AudioStreamMP3"), "mp3 is a recognized extension")

	var stream := ResourceLoader.load(path, "", ResourceLoader.CACHE_MODE_IGNORE) as AudioStreamMP3
	if expect(stream != null, "ResourceLoader.load() returns an AudioStreamMP3 from user://"):
		# The decoded length can include the encoder delay and padding.
		expect(absf(stream.get_length() - 0.5) < 0.1, "length is about 0.5 s (got %f)" % stream.get_length())
		expect_equal(stream.loop, false, "default import options: no loop")
		expect(stream.instantiate_playback() != null, "the stream can be played")

	var first := load(path)
	expect(first != null and first == load(path), "load() caches the stream")

	# No MP3 encoder in the engine.
	expect(not ("mp3" in ResourceSaver.get_recognized_extensions(stream)), "ResourceSaver offers no .mp3")
	expect(ResourceSaver.save(stream, DIR.path_join("copy.mp3")) != OK, "ResourceSaver.save() cannot write .mp3")

	# Importer precedence: imported_tone.mp3.import sets loop=true, the runtime loader never loops.
	var imported := load(RES.path_join("imported_tone.mp3")) as AudioStreamMP3
	if expect(imported != null, "imported res:// MP3 loads"):
		expect_equal(imported.loop, true, "imported res:// file comes from the importer (loop=true)")
	var kept := load(RES.path_join("kept_tone.mp3")) as AudioStreamMP3
	if expect(kept != null, "MP3 with import mode Keep File loads"):
		expect_equal(kept.loop, false, "Keep File comes from the runtime loader (loop=false)")

	# Threaded loading.
	var threaded := DIR.path_join("threaded.mp3")
	DirAccess.copy_absolute(path, threaded)
	expect_equal(ResourceLoader.load_threaded_request(threaded, "AudioStreamMP3"), OK, "load_threaded_request() accepts a user:// MP3 file")
	var status := ResourceLoader.load_threaded_get_status(threaded)
	var frames := 0
	while status == ResourceLoader.THREAD_LOAD_IN_PROGRESS and frames < 600:
		await get_tree().process_frame
		frames += 1
		status = ResourceLoader.load_threaded_get_status(threaded)
	expect_equal(status, ResourceLoader.THREAD_LOAD_LOADED, "threaded load finishes")
	expect(ResourceLoader.load_threaded_get(threaded) is AudioStreamMP3, "load_threaded_get() returns the AudioStreamMP3")
