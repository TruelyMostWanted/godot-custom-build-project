extends FeatureTest
## Full API test of WebM Video (VideoStreamWebm: VP9, VP8, alpha, seek). Same tests as scripts/cs/Test.cs.

const RES := "res://webm-video/resources"
const LENGTH := 1.0
const LENGTH_TOLERANCE := 0.2
const VIDEO_SIZE := Vector2(64, 64)
const SEEK_POSITION := 0.8
const MIN_POSITION_AFTER_SEEK := 0.5
# VP9, VP9 with alpha, VP8 with alpha, VP8 with Vorbis audio. All are 1 s long and 64x64.
const FILES := ["testsrc.webm", "testsrc_alpha.webm", "vp8_alpha.webm", "vp8_vorbis.webm"]


func _run_tests() -> void:
	expect(ClassDB.class_exists("VideoStreamWebm"), "VideoStreamWebm is registered")
	_test_file_property()
	for file_name: String in FILES:
		await _test_video(file_name)


func _test_file_property() -> void:
	var stream := VideoStreamWebm.new()
	stream.file = RES + "/testsrc.webm"
	expect_equal(stream.file, RES + "/testsrc.webm", "the file property can be set at runtime")


func _test_video(file_name: String) -> void:
	var path: String = "%s/%s" % [RES, file_name]
	var stream: VideoStreamWebm = load(path) as VideoStreamWebm
	expect(stream != null, "%s loads as VideoStreamWebm" % file_name)
	expect_equal(stream.file, path, "%s: file is the loaded path" % file_name)
	var player := VideoStreamPlayer.new()
	player.size = VIDEO_SIZE
	player.stream = stream
	add_child(player)
	player.play()
	expect(player.is_playing(), "%s: VideoStreamPlayer is playing" % file_name)
	expect(absf(player.get_stream_length() - LENGTH) < LENGTH_TOLERANCE, "%s: stream length is about 1 s" % file_name)
	await get_tree().create_timer(0.4).timeout
	expect(player.stream_position > 0.0, "%s: stream position advances" % file_name)
	var texture: Texture2D = player.get_video_texture()
	expect_equal(texture.get_size(), VIDEO_SIZE, "%s: the video texture is 64x64" % file_name)
	player.stream_position = SEEK_POSITION
	await get_tree().create_timer(0.1).timeout
	expect(player.stream_position >= MIN_POSITION_AFTER_SEEK, "%s: seek moves the position forward" % file_name)
	player.queue_free()
