extends FeatureCheck
## GDScript checks of 199-feature-opus-runtime-loader. Check the same things as scripts/cs/Issue199FeatureOpusRuntimeLoaderCheck.cs.

const OPUS_PATH := "res://199-feature-opus-runtime-loader/resources/sine_440.opus"
const USER_DIR := "user://199-feature-opus-runtime-loader"
const USER_PATH := "user://199-feature-opus-runtime-loader/sine_440.opus"
const THREADED_PATH := "user://199-feature-opus-runtime-loader/threaded.opus"
const BROKEN_PATH := "user://199-feature-opus-runtime-loader/broken.opus"


func _get_check_name() -> String:
	return "199-feature-opus-runtime-loader"


func _write(path: String, bytes: PackedByteArray) -> void:
	var file := FileAccess.open(path, FileAccess.WRITE)
	file.store_buffer(bytes)
	file.close()


func _run_checks() -> void:
	# Imported res:// file: still loaded through the importer.
	var imported = load(OPUS_PATH)
	expect(imported is AudioStreamOpus, "the imported res:// .opus loads as AudioStreamOpus")

	# Copy the file into user:// at runtime (no .import file there).
	var bytes := FileAccess.get_file_as_bytes(OPUS_PATH)
	expect(bytes.size() > 0, "the source file can be read")
	DirAccess.make_dir_recursive_absolute(USER_DIR)
	_write(USER_PATH, bytes)
	_write(THREADED_PATH, bytes)

	expect(ResourceLoader.exists(USER_PATH), "ResourceLoader.exists() finds the user:// .opus")
	expect(ResourceLoader.exists(USER_PATH, "AudioStream"), "ResourceLoader.exists() with type hint AudioStream")
	expect(not ResourceLoader.exists(USER_DIR + "/missing.opus"), "ResourceLoader.exists() is false for a missing file")

	var stream = ResourceLoader.load(USER_PATH, "", ResourceLoader.CACHE_MODE_IGNORE)
	expect(stream is AudioStreamOpus, "ResourceLoader.load() of user:// returns an AudioStreamOpus")
	if not (stream is AudioStreamOpus):
		return
	expect(absf(stream.get_length() - 1.0) < 0.1, "the length is about 1 s (got %s)" % stream.get_length())
	expect(stream.data == bytes, "the stream keeps the compressed data")
	expect(stream.loop == false and stream.bpm == 0.0 and stream.bar_beats == 4, "the importer defaults are used")

	# Resource cache.
	var first = load(USER_PATH)
	var second = load(USER_PATH)
	expect(first != null and first == second, "a normal load is cached")
	expect(ResourceLoader.has_cached(USER_PATH), "the path is in the resource cache")

	# Threaded loading.
	var err := ResourceLoader.load_threaded_request(THREADED_PATH, "AudioStream")
	expect(err == OK, "load_threaded_request() returns OK")
	var threaded = ResourceLoader.load_threaded_get(THREADED_PATH)
	expect(threaded is AudioStreamOpus, "load_threaded_get() returns an AudioStreamOpus")

	# Invalid file.
	_write(BROKEN_PATH, "This is not an Ogg Opus file.".to_utf8_buffer())
	var broken = ResourceLoader.load(BROKEN_PATH, "", ResourceLoader.CACHE_MODE_IGNORE)
	expect(broken == null, "an invalid .opus file does not load")

	# No saver: the engine has no Opus encoder.
	var save_err := ResourceSaver.save(stream, USER_DIR + "/copy.opus")
	expect(save_err != OK, "ResourceSaver.save() to .opus fails (no encoder)")
	expect(ResourceSaver.save(stream, USER_DIR + "/copy.tres") == OK, "ResourceSaver.save() to .tres works")
	var copy = ResourceLoader.load(USER_DIR + "/copy.tres", "", ResourceLoader.CACHE_MODE_IGNORE)
	expect(copy is AudioStreamOpus and copy.data == bytes, "the saved .tres loads again with the same data")

	# Playback.
	var player := AudioStreamPlayer.new()
	player.stream = stream
	add_child(player)
	player.play()
	await get_tree().create_timer(0.4).timeout
	expect(player.playing, "AudioStreamPlayer plays the runtime-loaded stream")
	expect(player.get_playback_position() > 0.0, "the playback position advances (%s)" % player.get_playback_position())
	player.stop()
	player.queue_free()

	for file_name in ["sine_440.opus", "threaded.opus", "broken.opus", "copy.tres"]:
		DirAccess.remove_absolute(USER_DIR + "/" + file_name)
