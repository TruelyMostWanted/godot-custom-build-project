extends FeatureCheck
## GDScript checks of 195-feature-opus-audio. Check the same things as scripts/cs/Issue195FeatureOpusAudioCheck.cs.

const OPUS_PATH := "res://195-feature-opus-audio/resources/sine_440.opus"
const COPY_PATH := "user://195_sine_440.opus"


func _get_check_name() -> String:
	return "195-feature-opus-audio"


func _run_checks() -> void:
	expect(ClassDB.class_exists("AudioStreamOpus"), "AudioStreamOpus is registered")
	expect(ClassDB.class_exists("AudioStreamPlaybackOpus"), "AudioStreamPlaybackOpus is registered")

	# Imported resource.
	var imported = load(OPUS_PATH)
	expect(imported is AudioStreamOpus, "the imported .opus loads as AudioStreamOpus")
	if not (imported is AudioStreamOpus):
		return
	expect(absf(imported.get_length() - 1.0) < 0.1, "imported length is about 1 s (got %s)" % imported.get_length())
	expect(imported.data.size() > 0, "imported stream has data")
	expect(imported.loop == false, "loop is off by default")
	imported.loop = true
	imported.loop_offset = 0.25
	imported.bpm = 120.0
	imported.beat_count = 2
	imported.bar_beats = 4
	expect(imported.has_loop() and is_equal_approx(imported.loop_offset, 0.25), "loop and loop_offset are settable")
	expect(is_equal_approx(imported.bpm, 120.0) and imported.beat_count == 2 and imported.bar_beats == 4, "bpm, beat_count, bar_beats are settable")
	expect(imported.tags is Dictionary, "tags is a Dictionary")

	# Runtime load from file.
	var bytes := FileAccess.get_file_as_bytes(OPUS_PATH)
	expect(bytes.size() > 0, "the source file can be read")
	var out := FileAccess.open(COPY_PATH, FileAccess.WRITE)
	out.store_buffer(bytes)
	out.close()
	var from_file := AudioStreamOpus.load_from_file(COPY_PATH)
	expect(from_file != null, "load_from_file returns a stream")
	if from_file != null:
		expect(absf(from_file.get_length() - 1.0) < 0.1, "load_from_file length is about 1 s")
	expect(AudioStreamOpus.load_from_file("user://does_not_exist.opus") == null, "load_from_file of a missing file gives null")

	# Runtime load from buffer.
	var from_buffer := AudioStreamOpus.load_from_buffer(bytes)
	expect(from_buffer != null, "load_from_buffer returns a stream")
	if from_buffer != null:
		expect(absf(from_buffer.get_length() - 1.0) < 0.1, "load_from_buffer length is about 1 s")
		expect(from_buffer.data == bytes, "load_from_buffer keeps the data")
	expect(AudioStreamOpus.load_from_buffer(PackedByteArray([1, 2, 3, 4])) == null, "load_from_buffer of garbage gives null")

	# Playback.
	var playback: AudioStreamPlayback = (from_buffer if from_buffer != null else imported).instantiate_playback()
	expect(playback != null, "instantiate_playback returns a playback")
	expect(playback is AudioStreamPlaybackOpus, "the playback is an AudioStreamPlaybackOpus")
	playback.start(0.0)
	expect(playback.is_playing(), "the playback is playing after start()")

	var player := AudioStreamPlayer.new()
	player.stream = from_buffer if from_buffer != null else imported
	add_child(player)
	player.play()
	await get_tree().create_timer(0.4).timeout
	expect(player.playing, "AudioStreamPlayer is playing")
	expect(player.get_playback_position() > 0.0, "the playback position advances (%s)" % player.get_playback_position())
	player.stop()
	player.queue_free()
	DirAccess.remove_absolute(COPY_PATH)
