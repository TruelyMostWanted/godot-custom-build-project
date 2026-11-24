extends FeatureCheck
## GDScript checks of 105-feature-theora-loader. Check the same things as scripts/cs/Issue105FeatureTheoraLoaderCheck.cs.

const DIR := "user://105-feature-theora-loader"
const RES := "res://105-feature-theora-loader/resources"


func _get_check_name() -> String:
	return "105-feature-theora-loader"


func _run_checks() -> void:
	DirAccess.make_dir_recursive_absolute(DIR)
	var path := DIR.path_join("clip.ogv")
	# clip.ogv: 1 s of a 64x48 test pattern at 10 FPS, Ogg Theora without audio. The editor does not import .ogv files.
	var bytes := FileAccess.get_file_as_bytes(RES.path_join("clip.ogv"))
	var file := FileAccess.open(path, FileAccess.WRITE)
	file.store_buffer(bytes)
	file.close()

	expect(ResourceLoader.exists(path), "ResourceLoader.exists() finds an .ogv in user://")
	expect(ResourceLoader.exists(path, "VideoStream"), "exists() with type hint VideoStream")
	expect("ogv" in ResourceLoader.get_recognized_extensions_for_type("VideoStreamTheora"), "ogv is a recognized extension")
	expect("ogv" in ResourceLoader.get_recognized_extensions_for_type("Resource"), "ogv is recognized for the type Resource")

	var stream := ResourceLoader.load(path, "", ResourceLoader.CACHE_MODE_IGNORE) as VideoStreamTheora
	if expect(stream != null, "ResourceLoader.load() returns a VideoStreamTheora from user://"):
		expect_equal(stream.file, path, "the stream points to the loaded file")
		expect(ResourceLoader.load(path, "Resource", ResourceLoader.CACHE_MODE_IGNORE) is VideoStreamTheora, "load() with type hint Resource")

		# No Theora encoder at runtime.
		expect(not ("ogv" in ResourceSaver.get_recognized_extensions(stream)), "ResourceSaver offers no .ogv")
		expect(ResourceSaver.save(stream, DIR.path_join("copy.ogv")) != OK, "ResourceSaver.save() cannot write .ogv")

		# Plays in a VideoStreamPlayer.
		var player := VideoStreamPlayer.new()
		add_child(player)
		player.stream = stream
		expect(absf(player.get_stream_length() - 1.0) < 0.15, "length is about 1 s (got %f)" % player.get_stream_length())
		player.play()
		for i in 5:
			await get_tree().process_frame
		expect(player.is_playing(), "the stream plays")
		var texture := player.get_video_texture()
		if expect(texture != null, "the player has a video texture"):
			expect_equal(texture.get_size(), Vector2(64, 48), "video size is 64x48")
		player.queue_free()

	var first := load(path)
	expect(first != null and first == load(path), "load() caches the stream")

	# Not an Ogg file.
	var broken := DIR.path_join("broken.ogv")
	var broken_file := FileAccess.open(broken, FileAccess.WRITE)
	broken_file.store_string("not a video")
	broken_file.close()
	expect(ResourceLoader.load(broken, "", ResourceLoader.CACHE_MODE_IGNORE) == null, "a file that is not Ogg does not load")

	# res:// without import and with import mode Keep File: both come from the runtime loader.
	expect(load(RES.path_join("clip.ogv")) is VideoStreamTheora, "res:// .ogv without import loads")
	expect(load(RES.path_join("kept_clip.ogv")) is VideoStreamTheora, ".ogv with import mode Keep File loads")

	# Threaded loading.
	var threaded := DIR.path_join("threaded.ogv")
	DirAccess.copy_absolute(path, threaded)
	expect_equal(ResourceLoader.load_threaded_request(threaded, "VideoStreamTheora"), OK, "load_threaded_request() accepts a user:// .ogv")
	var status := ResourceLoader.load_threaded_get_status(threaded)
	var frames := 0
	while status == ResourceLoader.THREAD_LOAD_IN_PROGRESS and frames < 600:
		await get_tree().process_frame
		frames += 1
		status = ResourceLoader.load_threaded_get_status(threaded)
	expect_equal(status, ResourceLoader.THREAD_LOAD_LOADED, "threaded load finishes")
	expect(ResourceLoader.load_threaded_get(threaded) is VideoStreamTheora, "load_threaded_get() returns the VideoStreamTheora")
