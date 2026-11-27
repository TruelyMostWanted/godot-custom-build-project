extends FeatureTest
## Full API test of Audio Files (runtime loaders and the WAV saver). Same tests as scripts/cs/Test.cs.

const DIR := "user://audio-files-test"
const RES := "res://audio-files/resources"
const MIX_RATE := 22050
const FRAMES := 64
const LENGTH_TOLERANCE := 0.1
# Compressed formats: [extension, class, length in seconds]. kept_tone.<extension> is a raw file (import mode Keep File).
const FORMATS := [
	["ogg", "AudioStreamOggVorbis", 0.5],
	["mp3", "AudioStreamMP3", 0.5],
	["opus", "AudioStreamOpus", 1.0],
]


func _run_tests() -> void:
	DirAccess.make_dir_recursive_absolute(DIR)
	_test_wav_load()
	_test_wav_save()
	_test_wav_qoa_refused()
	_test_compressed_formats()
	_test_invalid_file()
	_test_importer_precedence()
	await _test_threaded()


# A stereo stream of FRAMES frames: 2 bytes per frame and channel for 16 bits, 1 for 8 bits.
func _make_wav(format: AudioStreamWAV.Format) -> AudioStreamWAV:
	var stream := AudioStreamWAV.new()
	stream.format = format
	stream.stereo = true
	stream.mix_rate = MIX_RATE
	var data := PackedByteArray()
	var bytes_per_sample: int = 2 if format == AudioStreamWAV.FORMAT_16_BITS else 1
	data.resize(FRAMES * 2 * bytes_per_sample)
	for i in data.size():
		data[i] = (i * 7) % 256
	stream.data = data
	return stream


func _copy_kept(extension: String, target: String) -> void:
	var bytes: PackedByteArray = FileAccess.get_file_as_bytes("%s/kept_tone.%s" % [RES, extension])
	var file: FileAccess = FileAccess.open(target, FileAccess.WRITE)
	file.store_buffer(bytes)
	file.close()


func _test_wav_load() -> void:
	var path: String = DIR + "/source.wav"
	var original: AudioStreamWAV = _make_wav(AudioStreamWAV.FORMAT_16_BITS)
	expect_equal(original.save_to_wav(path), OK, "save_to_wav writes a .wav to user://")
	expect(ResourceLoader.exists(path), "ResourceLoader.exists finds a .wav in user://")
	expect(ResourceLoader.exists(path, "AudioStream"), "ResourceLoader.exists finds it with the hint AudioStream")
	expect("wav" in ResourceLoader.get_recognized_extensions_for_type("AudioStreamWAV"), "wav is a recognized extension")
	var loaded: AudioStreamWAV = ResourceLoader.load(path, "", ResourceLoader.CACHE_MODE_IGNORE) as AudioStreamWAV
	expect(loaded != null, "ResourceLoader.load returns an AudioStreamWAV from user://")
	expect_equal(loaded.format, AudioStreamWAV.FORMAT_16_BITS, "format is 16 bits")
	expect(loaded.stereo, "stream is stereo")
	expect_equal(loaded.mix_rate, MIX_RATE, "mix_rate is kept")
	expect(loaded.data == original.data, "PCM data is unchanged")
	expect_equal(loaded.get_length(), float(FRAMES) / MIX_RATE, "length is frames / mix_rate")
	expect(loaded.instantiate_playback() != null, "the stream can be played")
	var first: Resource = load(path)
	var second: Resource = load(path)
	expect(first == second, "load caches the stream")
	expect_equal(first.resource_path, path, "resource_path is the loaded path")


func _test_wav_save() -> void:
	for format: AudioStreamWAV.Format in [AudioStreamWAV.FORMAT_8_BITS, AudioStreamWAV.FORMAT_16_BITS]:
		var original: AudioStreamWAV = _make_wav(format)
		var path: String = "%s/saved_%d.wav" % [DIR, format]
		expect("wav" in ResourceSaver.get_recognized_extensions(original), "ResourceSaver offers .wav for format %d" % format)
		expect_equal(ResourceSaver.save(original, path), OK, "ResourceSaver.save writes format %d" % format)
		var loaded: AudioStreamWAV = ResourceLoader.load(path, "AudioStreamWAV", ResourceLoader.CACHE_MODE_IGNORE) as AudioStreamWAV
		expect_equal(loaded.format, format, "format %d survives save and load" % format)
		expect(loaded.data == original.data, "data of format %d survives save and load" % format)


func _test_wav_qoa_refused() -> void:
	var stream: AudioStreamWAV = _make_wav(AudioStreamWAV.FORMAT_16_BITS)
	stream.format = AudioStreamWAV.FORMAT_QOA
	var path: String = DIR + "/qoa.wav"
	expect(not ("wav" in ResourceSaver.get_recognized_extensions(stream)), "no .wav is offered for QOA data")
	expect_equal(ResourceSaver.save(stream, path), ERR_UNAVAILABLE, "ResourceSaver.save refuses QOA data")
	expect(not FileAccess.file_exists(path), "nothing is written for QOA data")


func _test_compressed_formats() -> void:
	for entry: Array in FORMATS:
		var extension: String = entry[0]
		var path: String = "%s/tone.%s" % [DIR, extension]
		_copy_kept(extension, path)
		expect(ResourceLoader.exists(path), "ResourceLoader.exists finds the .%s" % extension)
		expect(ResourceLoader.exists(path, "AudioStream"), "ResourceLoader.exists finds the .%s with the hint AudioStream" % extension)
		expect(extension in ResourceLoader.get_recognized_extensions_for_type(entry[1]), "%s is a recognized extension" % extension)
		var stream: AudioStream = ResourceLoader.load(path, "", ResourceLoader.CACHE_MODE_IGNORE) as AudioStream
		expect_equal(stream.get_class(), entry[1], ".%s loads as %s" % [extension, entry[1]])
		expect(absf(stream.get_length() - entry[2]) < LENGTH_TOLERANCE, ".%s length is about %s s" % [extension, entry[2]])
		expect(not stream.get("loop"), ".%s uses the importer default: no loop" % extension)
		expect(stream.instantiate_playback() != null, ".%s can be played" % extension)
		var first: Resource = load(path)
		var second: Resource = load(path)
		expect(first == second, ".%s is cached" % extension)
		expect(not (extension in ResourceSaver.get_recognized_extensions(stream)), "ResourceSaver offers no .%s" % extension)
		expect(ResourceSaver.save(stream, DIR + "/copy." + extension) != OK, "ResourceSaver.save cannot write .%s" % extension)


func _test_invalid_file() -> void:
	var path: String = DIR + "/broken.opus"
	var file: FileAccess = FileAccess.open(path, FileAccess.WRITE)
	file.store_string("This is not an Ogg Opus file.")
	file.close()
	var broken: Resource = ResourceLoader.load(path, "", ResourceLoader.CACHE_MODE_IGNORE)
	expect(broken == null, "an invalid .opus file does not load")


# The .import files of imported_tone.* set loop=true (WAV: compress/mode=2, QOA); Keep File files go to the runtime loader.
func _test_importer_precedence() -> void:
	var wav: AudioStreamWAV = load(RES + "/imported_tone.wav") as AudioStreamWAV
	expect_equal(wav.format, AudioStreamWAV.FORMAT_QOA, "imported WAV comes from the importer (QOA)")
	var kept_wav: AudioStreamWAV = load(RES + "/kept_tone.wav") as AudioStreamWAV
	expect_equal(kept_wav.format, AudioStreamWAV.FORMAT_16_BITS, "Keep File WAV comes from the runtime loader (16 bits)")
	expect_equal(kept_wav.mix_rate, 8000, "Keep File WAV keeps its mix rate")
	for extension: String in ["ogg", "mp3"]:
		var imported: AudioStream = load("%s/imported_tone.%s" % [RES, extension]) as AudioStream
		expect(imported.get("loop"), "imported .%s comes from the importer (loop is on)" % extension)
		var kept: AudioStream = load("%s/kept_tone.%s" % [RES, extension]) as AudioStream
		expect(not kept.get("loop"), "Keep File .%s comes from the runtime loader (loop is off)" % extension)


func _test_threaded() -> void:
	var wav_path: String = DIR + "/threaded.wav"
	ResourceSaver.save(_make_wav(AudioStreamWAV.FORMAT_16_BITS), wav_path)
	await _load_threaded(wav_path, "AudioStreamWAV")
	for entry: Array in FORMATS:
		var path: String = "%s/threaded.%s" % [DIR, entry[0]]
		_copy_kept(entry[0], path)
		await _load_threaded(path, entry[1])


func _load_threaded(path: String, expected_class: String) -> void:
	expect_equal(ResourceLoader.load_threaded_request(path, expected_class), OK, "load_threaded_request accepts %s" % path.get_file())
	while ResourceLoader.load_threaded_get_status(path) == ResourceLoader.THREAD_LOAD_IN_PROGRESS:
		await get_tree().process_frame
	expect_equal(ResourceLoader.load_threaded_get_status(path), ResourceLoader.THREAD_LOAD_LOADED, "the threaded load of %s finishes" % path.get_file())
	var stream: Resource = ResourceLoader.load_threaded_get(path)
	expect_equal(stream.get_class(), expected_class, "load_threaded_get returns %s" % expected_class)
