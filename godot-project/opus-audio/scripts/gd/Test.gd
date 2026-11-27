extends FeatureTest
## Full API test of Opus Audio (AudioStreamOpus, AudioStreamPlaybackOpus). Same tests as scripts/cs/Test.cs.

const OPUS := "res://opus-audio/resources/sine_440.opus"
const DIR := "user://opus-audio-test"
const LENGTH := 1.0
const LENGTH_TOLERANCE := 0.1
const DEFAULT_BAR_BEATS := 4
const GARBAGE := [1, 2, 3, 4]


func _run_tests() -> void:
	DirAccess.make_dir_recursive_absolute(DIR)
	expect(ClassDB.class_exists("AudioStreamOpus"), "AudioStreamOpus is registered")
	expect(ClassDB.class_exists("AudioStreamPlaybackOpus"), "AudioStreamPlaybackOpus is registered")
	_test_imported()
	_test_properties()
	_test_load_from_file()
	_test_load_from_buffer()
	_test_save_as_tres()
	await _test_playback()


func _test_imported() -> void:
	var stream: AudioStreamOpus = load(OPUS) as AudioStreamOpus
	expect(stream != null, "the imported .opus loads as AudioStreamOpus")
	expect(absf(stream.get_length() - LENGTH) < LENGTH_TOLERANCE, "length is about 1 s")
	expect(stream.data.size() > 0, "the stream keeps the compressed data")
	expect(stream.tags is Dictionary, "tags is a Dictionary")


func _test_properties() -> void:
	var stream := AudioStreamOpus.new()
	expect(not stream.loop, "loop is off by default")
	expect_equal(stream.loop_offset, 0.0, "loop_offset defaults to 0")
	expect_equal(stream.bpm, 0.0, "bpm defaults to 0")
	expect_equal(stream.beat_count, 0, "beat_count defaults to 0")
	expect_equal(stream.bar_beats, DEFAULT_BAR_BEATS, "bar_beats defaults to 4")
	stream.loop = true
	stream.loop_offset = 0.25
	stream.bpm = 120.0
	stream.beat_count = 2
	stream.bar_beats = 3
	expect(stream.loop, "loop is settable")
	expect_equal(stream.loop_offset, 0.25, "loop_offset is settable")
	expect_equal(stream.bpm, 120.0, "bpm is settable")
	expect_equal(stream.beat_count, 2, "beat_count is settable")
	expect_equal(stream.bar_beats, 3, "bar_beats is settable")


func _test_load_from_file() -> void:
	var path: String = DIR + "/voice.opus"
	DirAccess.copy_absolute(OPUS, path)
	var stream: AudioStreamOpus = AudioStreamOpus.load_from_file(path)
	expect(absf(stream.get_length() - LENGTH) < LENGTH_TOLERANCE, "load_from_file returns a stream of about 1 s")
	expect(AudioStreamOpus.load_from_file(DIR + "/missing.opus") == null, "load_from_file of a missing file gives null")


func _test_load_from_buffer() -> void:
	var bytes: PackedByteArray = FileAccess.get_file_as_bytes(OPUS)
	var stream: AudioStreamOpus = AudioStreamOpus.load_from_buffer(bytes)
	expect(absf(stream.get_length() - LENGTH) < LENGTH_TOLERANCE, "load_from_buffer returns a stream of about 1 s")
	expect(stream.data == bytes, "load_from_buffer keeps the data")
	var garbage: AudioStreamOpus = AudioStreamOpus.load_from_buffer(PackedByteArray(GARBAGE))
	expect(garbage.data.is_empty(), "load_from_buffer of invalid bytes gives a stream with empty data")


func _test_save_as_tres() -> void:
	var stream: AudioStreamOpus = load(OPUS) as AudioStreamOpus
	expect_equal(ResourceSaver.save(stream, DIR + "/copy.tres"), OK, "the stream saves as .tres")
	var copy: AudioStreamOpus = ResourceLoader.load(DIR + "/copy.tres", "", ResourceLoader.CACHE_MODE_IGNORE) as AudioStreamOpus
	expect(copy.data == stream.data, "the saved .tres loads again with the same data")


func _test_playback() -> void:
	var stream: AudioStreamOpus = load(OPUS) as AudioStreamOpus
	var playback: AudioStreamPlayback = stream.instantiate_playback()
	expect(playback is AudioStreamPlaybackOpus, "instantiate_playback returns an AudioStreamPlaybackOpus")
	playback.start(0.0)
	expect(playback.is_playing(), "the playback is playing after start")
	var player := AudioStreamPlayer.new()
	player.stream = stream
	add_child(player)
	player.play()
	await get_tree().create_timer(0.4).timeout
	expect(player.playing, "AudioStreamPlayer is playing")
	expect(player.get_playback_position() > 0.0, "the playback position advances")
	player.queue_free()
