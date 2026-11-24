extends FeatureCheck
## GDScript checks of 201-feature-webm-vp8. Check the same things as scripts/cs/Issue201FeatureWebmVp8Check.cs.

const ALPHA_PATH := "res://201-feature-webm-vp8/resources/vp8_alpha.webm"
const VORBIS_PATH := "res://201-feature-webm-vp8/resources/vp8_vorbis.webm"


func _get_check_name() -> String:
	return "201-feature-webm-vp8"


func _run_checks() -> void:
	expect(ClassDB.class_exists("VideoStreamWebm"), "VideoStreamWebm is registered")
	for path in [ALPHA_PATH, VORBIS_PATH]:
		await _check_file(path)


func _check_file(path: String) -> void:
	var name := path.get_file()
	var stream = load(path)
	expect(stream is VideoStreamWebm, "%s loads as VideoStreamWebm" % name)
	if not (stream is VideoStreamWebm):
		return

	var player := VideoStreamPlayer.new()
	player.size = Vector2(64, 64)
	player.stream = stream
	add_child(player)
	player.play()
	expect(player.is_playing(), "%s: VideoStreamPlayer is playing" % name)
	expect(absf(player.get_stream_length() - 1.0) < 0.2, "%s: stream length is about 1 s (got %s)" % [name, player.get_stream_length()])
	await get_tree().create_timer(0.4).timeout
	expect(player.stream_position > 0.0, "%s: stream position advances (%s)" % [name, player.stream_position])
	var texture := player.get_video_texture()
	expect(texture != null, "%s: a video texture exists" % name)
	if texture != null:
		expect(texture.get_width() == 64 and texture.get_height() == 64, "%s: the video texture is 64x64" % name)
		# The headless dummy renderer keeps no pixels: check alpha only with a real renderer.
		if path == ALPHA_PATH and DisplayServer.get_name() != "headless":
			var image := texture.get_image()
			if image != null and not image.is_empty():
				expect(image.get_pixel(8, 32).a < 0.5, "alpha: pixel in the transparent half has alpha < 0.5 (%s)" % image.get_pixel(8, 32).a)
				expect(image.get_pixel(56, 32).a > 0.5, "alpha: pixel in the opaque half has alpha > 0.5 (%s)" % image.get_pixel(56, 32).a)
			else:
				expect(false, "alpha: the video texture image is readable")

	player.stream_position = 0.8
	await get_tree().create_timer(0.1).timeout
	expect(player.stream_position >= 0.5, "%s: seek moves the position forward (%s)" % [name, player.stream_position])
	player.stop()
	player.queue_free()
