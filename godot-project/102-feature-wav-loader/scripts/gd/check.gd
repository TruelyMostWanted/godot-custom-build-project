extends FeatureCheck
## GDScript checks of 102-feature-wav-loader. Check the same things as scripts/cs/Issue102FeatureWavLoaderCheck.cs.

const DIR := "user://102-feature-wav-loader"
const RES := "res://102-feature-wav-loader/resources"


func _get_check_name() -> String:
	return "102-feature-wav-loader"


func _run_checks() -> void:
	DirAccess.make_dir_recursive_absolute(DIR)
	_check_load_from_user()
	_check_save_and_reload()
	_check_compressed_cannot_be_saved()
	_check_importer_precedence()
	await _check_threaded_load()


## A stereo test stream, 64 frames (8-bit or 16-bit PCM).
func _make_stream(format: AudioStreamWAV.Format) -> AudioStreamWAV:
	var stream := AudioStreamWAV.new()
	stream.format = format
	stream.stereo = true
	stream.mix_rate = 22050
	var data := PackedByteArray()
	if format == AudioStreamWAV.FORMAT_8_BITS:
		data.resize(128)
		for i in data.size():
			data[i] = (i * 2) % 256
	else:
		data.resize(256)
		for i in 128:
			data.encode_s16(i * 2, (i * 997) % 65536 - 32768)
	stream.data = data
	return stream


func _check_load_from_user() -> void:
	var path := DIR.path_join("source.wav")
	var original := _make_stream(AudioStreamWAV.FORMAT_16_BITS)
	expect_equal(original.save_to_wav(path), OK, "save_to_wav writes a test file to user://")

	expect(ResourceLoader.exists(path), "ResourceLoader.exists() finds a .wav in user://")
	expect(ResourceLoader.exists(path, "AudioStream"), "exists() with type hint AudioStream")
	expect("wav" in ResourceLoader.get_recognized_extensions_for_type("AudioStreamWAV"), "wav is a recognized extension")

	var loaded := ResourceLoader.load(path, "", ResourceLoader.CACHE_MODE_IGNORE) as AudioStreamWAV
	if not expect(loaded != null, "ResourceLoader.load() returns an AudioStreamWAV from user://"):
		return
	expect_equal(loaded.format, AudioStreamWAV.FORMAT_16_BITS, "format")
	expect_equal(loaded.stereo, true, "stereo")
	expect_equal(loaded.mix_rate, 22050, "mix rate")
	expect(loaded.data == original.data, "PCM data is unchanged")
	expect(is_equal_approx(loaded.get_length(), 64.0 / 22050.0), "length")
	expect(loaded.instantiate_playback() != null, "the stream can be played")

	var first := load(path)
	var second := load(path)
	expect(first != null and first == second, "load() caches the stream")
	expect_equal(first.resource_path if first else "", path, "resource_path of the cached stream")


func _check_save_and_reload() -> void:
	for format in [AudioStreamWAV.FORMAT_8_BITS, AudioStreamWAV.FORMAT_16_BITS]:
		var original := _make_stream(format)
		var path := DIR.path_join("saved_%d.wav" % format)
		expect("wav" in ResourceSaver.get_recognized_extensions(original), "ResourceSaver offers .wav for format %d" % format)
		expect_equal(ResourceSaver.save(original, path), OK, "ResourceSaver.save() writes format %d" % format)
		var loaded := ResourceLoader.load(path, "AudioStreamWAV", ResourceLoader.CACHE_MODE_IGNORE) as AudioStreamWAV
		if expect(loaded != null, "saved file of format %d loads again" % format):
			expect_equal(loaded.format, format, "format after save and load")
			expect(loaded.data == original.data, "data after save and load (format %d)" % format)


func _check_compressed_cannot_be_saved() -> void:
	var stream := _make_stream(AudioStreamWAV.FORMAT_16_BITS)
	stream.format = AudioStreamWAV.FORMAT_QOA
	var path := DIR.path_join("qoa.wav")
	if FileAccess.file_exists(path):
		DirAccess.remove_absolute(path)
	expect(not ("wav" in ResourceSaver.get_recognized_extensions(stream)), "no .wav offered for QOA data")
	expect_equal(ResourceSaver.save(stream, path), ERR_UNAVAILABLE, "ResourceSaver.save() refuses QOA data")
	expect(not FileAccess.file_exists(path), "nothing is written for QOA data")


func _check_importer_precedence() -> void:
	# imported_tone.wav.import sets compress/mode=2 (QOA): only the importer produces QOA data.
	var imported := load(RES.path_join("imported_tone.wav")) as AudioStreamWAV
	if expect(imported != null, "imported res:// WAV loads"):
		expect_equal(imported.format, AudioStreamWAV.FORMAT_QOA, "imported res:// WAV comes from the importer (QOA)")
	# kept_tone.wav uses import mode "Keep File": the runtime loader reads the raw file.
	var kept := load(RES.path_join("kept_tone.wav")) as AudioStreamWAV
	if expect(kept != null, "WAV with import mode Keep File loads"):
		expect_equal(kept.format, AudioStreamWAV.FORMAT_16_BITS, "Keep File WAV comes from the runtime loader (16-bit)")
		expect_equal(kept.mix_rate, 8000, "Keep File WAV mix rate")


func _check_threaded_load() -> void:
	var path := DIR.path_join("threaded.wav")
	expect_equal(ResourceSaver.save(_make_stream(AudioStreamWAV.FORMAT_16_BITS), path), OK, "write a file for threaded loading")
	expect_equal(ResourceLoader.load_threaded_request(path, "AudioStreamWAV"), OK, "load_threaded_request() accepts a user:// WAV")
	var status := ResourceLoader.load_threaded_get_status(path)
	var frames := 0
	while status == ResourceLoader.THREAD_LOAD_IN_PROGRESS and frames < 600:
		await get_tree().process_frame
		frames += 1
		status = ResourceLoader.load_threaded_get_status(path)
	expect_equal(status, ResourceLoader.THREAD_LOAD_LOADED, "threaded load finishes")
	var stream := ResourceLoader.load_threaded_get(path) as AudioStreamWAV
	expect(stream != null and stream.data.size() == 256, "load_threaded_get() returns the AudioStreamWAV")
