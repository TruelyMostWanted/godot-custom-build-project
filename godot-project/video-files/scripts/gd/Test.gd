extends FeatureTest
## Full API test of Video Files (runtime loaders of .ogv and .webm). Same tests as scripts/cs/Test.cs.

const DIR := "user://video-files-test"
const RES := "res://video-files/resources"
const LENGTH := 1.0
const LENGTH_TOLERANCE := 0.2
# [file in resources, extension, class, video size]. Both files are 1 s long, without audio.
const VIDEOS := [
	["clip.ogv", "ogv", "VideoStreamTheora", Vector2(64, 48)],
	["testsrc.webm", "webm", "VideoStreamWebm", Vector2(64, 64)],
]


func _run_tests() -> void:
	DirAccess.make_dir_recursive_absolute(DIR)
	for video: Array in VIDEOS:
		_copy_to_user(video[0], video[1])
		_test_load(video[1], video[2])
		_test_saver_refused(video[1])
		await _test_playback(video[1], video[3])
		await _test_threaded(video[1], video[2])
	_test_invalid_files()
	_test_res_files()


func _copy_to_user(file_name: String, extension: String) -> void:
	var bytes: PackedByteArray = FileAccess.get_file_as_bytes("%s/%s" % [RES, file_name])
	var file: FileAccess = FileAccess.open("%s/video.%s" % [DIR, extension], FileAccess.WRITE)
	file.store_buffer(bytes)
	file.close()


func _test_load(extension: String, expected_class: String) -> void:
	var path: String = "%s/video.%s" % [DIR, extension]
	expect(ResourceLoader.exists(path), "ResourceLoader.exists finds the .%s in user://" % extension)
	expect(ResourceLoader.exists(path, "VideoStream"), ".%s exists with the hint VideoStream" % extension)
	expect(ResourceLoader.exists(path, "Resource"), ".%s exists with the hint Resource" % extension)
	expect(not ResourceLoader.exists("%s/missing.%s" % [DIR, extension]), "a missing .%s does not exist" % extension)
	expect(extension in ResourceLoader.get_recognized_extensions_for_type(expected_class), "%s is a recognized extension" % extension)
	var stream: VideoStream = ResourceLoader.load(path, "", ResourceLoader.CACHE_MODE_IGNORE) as VideoStream
	expect_equal(stream.get_class(), expected_class, ".%s loads as %s" % [extension, expected_class])
	expect_equal(stream.get("file"), path, ".%s stream points to the loaded file" % extension)
	var as_resource: Resource = ResourceLoader.load(path, "Resource", ResourceLoader.CACHE_MODE_IGNORE)
	expect_equal(as_resource.get_class(), expected_class, ".%s loads with the hint Resource" % extension)
	var first: Resource = load(path)
	var second: Resource = load(path)
	expect(first == second, ".%s is cached" % extension)
	expect(ResourceLoader.has_cached(path), ".%s path is in the resource cache" % extension)


func _test_saver_refused(extension: String) -> void:
	var stream: VideoStream = load("%s/video.%s" % [DIR, extension]) as VideoStream
	expect(not (extension in ResourceSaver.get_recognized_extensions(stream)), "ResourceSaver offers no .%s" % extension)
	expect(ResourceSaver.save(stream, "%s/copy.%s" % [DIR, extension]) != OK, "ResourceSaver.save cannot write .%s" % extension)


func _test_playback(extension: String, video_size: Vector2) -> void:
	var player := VideoStreamPlayer.new()
	player.size = video_size
	add_child(player)
	player.stream = load("%s/video.%s" % [DIR, extension]) as VideoStream
	expect(absf(player.get_stream_length() - LENGTH) < LENGTH_TOLERANCE, ".%s length is about 1 s" % extension)
	player.play()
	for i in 5:
		await get_tree().process_frame
	expect(player.is_playing(), ".%s plays" % extension)
	var texture: Texture2D = player.get_video_texture()
	expect_equal(texture.get_size(), video_size, ".%s video size" % extension)
	player.queue_free()


func _test_threaded(extension: String, expected_class: String) -> void:
	var path: String = "%s/video.%s" % [DIR, extension]
	expect_equal(ResourceLoader.load_threaded_request(path, "VideoStream"), OK, "load_threaded_request accepts the .%s" % extension)
	while ResourceLoader.load_threaded_get_status(path) == ResourceLoader.THREAD_LOAD_IN_PROGRESS:
		await get_tree().process_frame
	expect_equal(ResourceLoader.load_threaded_get_status(path), ResourceLoader.THREAD_LOAD_LOADED, "the threaded load of the .%s finishes" % extension)
	var stream: Resource = ResourceLoader.load_threaded_get(path)
	expect_equal(stream.get_class(), expected_class, "load_threaded_get returns %s" % expected_class)


func _test_invalid_files() -> void:
	for extension: String in ["ogv", "webm"]:
		var path: String = "%s/broken.%s" % [DIR, extension]
		var file: FileAccess = FileAccess.open(path, FileAccess.WRITE)
		file.store_string("not a video")
		file.close()
		var broken: Resource = ResourceLoader.load(path, "", ResourceLoader.CACHE_MODE_IGNORE)
		expect(broken == null, "a .%s that is not a video does not load" % extension)


# Videos are never imported: files in res:// without .import and with import mode Keep File use the runtime loader.
func _test_res_files() -> void:
	expect(load(RES + "/clip.ogv") is VideoStreamTheora, "res:// .ogv without import loads")
	expect(load(RES + "/kept_clip.ogv") is VideoStreamTheora, ".ogv with import mode Keep File loads")
	expect(load(RES + "/testsrc.webm") is VideoStreamWebm, "res:// .webm loads")
