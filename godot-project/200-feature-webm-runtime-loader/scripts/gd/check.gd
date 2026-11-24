extends FeatureCheck
## GDScript checks of 200-feature-webm-runtime-loader. Check the same things as scripts/cs/Issue200FeatureWebmRuntimeLoaderCheck.cs.

const VIDEO_PATH := "res://200-feature-webm-runtime-loader/resources/testsrc.webm"
const USER_DIR := "user://200-feature-webm-runtime-loader"
const USER_PATH := "user://200-feature-webm-runtime-loader/testsrc.webm"
const THREADED_PATH := "user://200-feature-webm-runtime-loader/threaded.webm"
const BROKEN_PATH := "user://200-feature-webm-runtime-loader/broken.webm"


func _get_check_name() -> String:
	return "200-feature-webm-runtime-loader"


func _write(path: String, bytes: PackedByteArray) -> void:
	var file := FileAccess.open(path, FileAccess.WRITE)
	file.store_buffer(bytes)
	file.close()


func _run_checks() -> void:
	# res:// file (WebM has no importer): loaded by the same loader.
	var in_project = load(VIDEO_PATH)
	expect(in_project is VideoStreamWebm, "the res:// .webm loads as VideoStreamWebm")

	# Copy the file into user:// at runtime.
	var bytes := FileAccess.get_file_as_bytes(VIDEO_PATH)
	expect(bytes.size() > 0, "the source file can be read")
	DirAccess.make_dir_recursive_absolute(USER_DIR)
	_write(USER_PATH, bytes)
	_write(THREADED_PATH, bytes)

	expect(ResourceLoader.exists(USER_PATH), "ResourceLoader.exists() finds the user:// .webm")
	expect(ResourceLoader.exists(USER_PATH, "VideoStream"), "ResourceLoader.exists() with type hint VideoStream")
	expect(ResourceLoader.exists(USER_PATH, "Resource"), "ResourceLoader.exists() with type hint Resource")
	expect(not ResourceLoader.exists(USER_DIR + "/missing.webm"), "ResourceLoader.exists() is false for a missing file")

	var stream = ResourceLoader.load(USER_PATH, "", ResourceLoader.CACHE_MODE_IGNORE)
	expect(stream is VideoStreamWebm, "ResourceLoader.load() of user:// returns a VideoStreamWebm")
	if not (stream is VideoStreamWebm):
		return
	expect(stream.file == USER_PATH, "file is the user:// path")
	var as_resource = ResourceLoader.load(USER_PATH, "Resource", ResourceLoader.CACHE_MODE_IGNORE)
	expect(as_resource is VideoStreamWebm, "the type hint Resource works")

	# Resource cache.
	var first = load(USER_PATH)
	var second = load(USER_PATH)
	expect(first != null and first == second, "a normal load is cached")
	expect(ResourceLoader.has_cached(USER_PATH), "the path is in the resource cache")

	# Threaded loading.
	var err := ResourceLoader.load_threaded_request(THREADED_PATH, "VideoStream")
	expect(err == OK, "load_threaded_request() returns OK")
	var threaded = ResourceLoader.load_threaded_get(THREADED_PATH)
	expect(threaded is VideoStreamWebm, "load_threaded_get() returns a VideoStreamWebm")

	# Invalid file.
	_write(BROKEN_PATH, "This is not a WebM file.".to_utf8_buffer())
	var broken = ResourceLoader.load(BROKEN_PATH, "", ResourceLoader.CACHE_MODE_IGNORE)
	expect(broken == null, "an invalid .webm file does not load")

	# No saver: the engine has no WebM encoder.
	expect(ResourceSaver.save(stream, USER_DIR + "/copy.webm") != OK, "ResourceSaver.save() to .webm fails (no encoder)")

	# Playback from user://.
	var player := VideoStreamPlayer.new()
	player.size = Vector2(64, 64)
	player.stream = stream
	add_child(player)
	player.play()
	expect(player.is_playing(), "VideoStreamPlayer plays the user:// stream")
	expect(absf(player.get_stream_length() - 1.0) < 0.2, "stream length is about 1 s (got %s)" % player.get_stream_length())
	await get_tree().create_timer(0.4).timeout
	expect(player.stream_position > 0.0, "stream position advances (%s)" % player.stream_position)
	expect(player.get_video_texture() != null, "a video texture exists")
	player.stop()
	player.queue_free()

	for file_name in ["testsrc.webm", "threaded.webm", "broken.webm"]:
		DirAccess.remove_absolute(USER_DIR + "/" + file_name)
