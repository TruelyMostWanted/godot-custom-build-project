extends FeatureCheck
## GDScript checks of 196-feature-webm-video. Check the same things as scripts/cs/Issue196FeatureWebmVideoCheck.cs.

const VIDEO_PATH := "res://196-feature-webm-video/resources/testsrc.webm"
const ALPHA_PATH := "res://196-feature-webm-video/resources/testsrc_alpha.webm"


func _get_check_name() -> String:
	return "196-feature-webm-video"


func _run_checks() -> void:
	expect(ClassDB.class_exists("VideoStreamWebm"), "VideoStreamWebm is registered")

	# Resource loading.
	var stream = load(VIDEO_PATH)
	expect(stream is VideoStreamWebm, "the .webm loads as VideoStreamWebm")
	if not (stream is VideoStreamWebm):
		return
	expect(stream.file == VIDEO_PATH, "file is the loaded path")
	var alpha = load(ALPHA_PATH)
	expect(alpha is VideoStreamWebm, "the alpha .webm loads as VideoStreamWebm")

	# Runtime: a new stream with the file set.
	var runtime := VideoStreamWebm.new()
	runtime.file = VIDEO_PATH
	expect(runtime.file == VIDEO_PATH, "the file property can be set at runtime")

	# Playback.
	var player := VideoStreamPlayer.new()
	player.size = Vector2(64, 64)
	player.stream = stream
	add_child(player)
	player.play()
	expect(player.is_playing(), "VideoStreamPlayer is playing")
	expect(absf(player.get_stream_length() - 1.0) < 0.2, "stream length is about 1 s (got %s)" % player.get_stream_length())
	await get_tree().create_timer(0.4).timeout
	expect(player.stream_position > 0.0, "stream position advances (%s)" % player.stream_position)
	var texture := player.get_video_texture()
	expect(texture != null, "a video texture exists")
	if texture != null:
		expect(texture.get_width() == 64 and texture.get_height() == 64, "the video texture is 64x64")

	# Seek.
	player.stream_position = 0.8
	await get_tree().create_timer(0.1).timeout
	expect(player.stream_position >= 0.5, "seek moves the position forward (%s)" % player.stream_position)
	player.stop()
	player.queue_free()

	# Alpha file plays too.
	var alpha_player := VideoStreamPlayer.new()
	alpha_player.stream = alpha
	add_child(alpha_player)
	alpha_player.play()
	expect(absf(alpha_player.get_stream_length() - 1.0) < 0.2, "alpha stream length is about 1 s (got %s)" % alpha_player.get_stream_length())
	await get_tree().create_timer(0.3).timeout
	expect(alpha_player.get_video_texture() != null, "the alpha video has a texture")
	alpha_player.stop()
	alpha_player.queue_free()
