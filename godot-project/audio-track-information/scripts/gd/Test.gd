extends FeatureTest
## Full API test of Audio Track Information: the tag properties, format getters and load_info() of AudioStream.
## Same tests as scripts/cs/Test.cs.
## resources/test/sample.wav has RIFF INFO tags and an id3 chunk, resources/test/sample.mp3 an ID3v2.3 tag (UTF-16, Latin-1,
## an 8x8 PNG cover), 40 silent frames and an ID3v1 tag. Both are kept as files (not imported).

const WAV_PATH := "res://audio-track-information/resources/test/sample.wav"
const MP3_PATH := "res://audio-track-information/resources/test/sample.mp3"
const IMPORTED_PATH := "res://audio-track-information/resources/night_drive.wav"
const WAV_SAMPLE_BYTES := 1600
const WAV_BITRATE := 128000 # 8000 Hz * 16 bit
const MP3_LENGTH := 40.0 * 1152.0 / 44100.0 # 40 frames of 1152 samples


func _run_tests() -> void:
	_test_wav_tags()
	_test_mp3_tags()
	_test_load_from_file()
	_test_imported_tags()
	_test_defaults_and_manual_values()
	_test_wrapper_streams()
	_test_read_file_tags_setting()


func _test_wav_tags() -> void:
	var stream: AudioStream = AudioStream.load_info(WAV_PATH)
	expect(stream is AudioStreamWAV, "load_info of a WAV returns an AudioStreamWAV")
	expect_equal(stream.title, "Wave Title", "WAV: title from INAM")
	expect_equal(stream.artist, "Wave Artist", "WAV: artist from IART")
	expect_equal(stream.album, "Älbum ✓", "WAV: album from the UTF-8 id3 chunk")
	expect_equal(stream.composer, "Composer", "WAV: composer from the id3 chunk")
	expect_equal(stream.track_number, 7, "WAV: track_number from ITRK")
	expect_equal(stream.year, 1999, "WAV: year from ICRD")
	expect_equal(stream.genre, "Jazz", "WAV: genre from IGNR")
	expect_equal(stream.get_sample_rate(), 8000, "WAV: get_sample_rate")
	expect_equal(stream.get_channel_count(), 1, "WAV: get_channel_count")
	expect_equal(stream.get_codec(), "PCM", "WAV: get_codec")
	expect_equal(stream.get_bitrate(), WAV_BITRATE, "WAV: get_bitrate")
	expect(absf(stream.get_length() - 0.1) < 0.01, "WAV: get_length is 0.1 s")
	expect(stream.cover == null, "WAV: no cover")
	expect(stream.cover_data.is_empty(), "WAV: no cover_data")


func _test_mp3_tags() -> void:
	var stream: AudioStream = AudioStream.load_info(MP3_PATH)
	expect(stream is AudioStreamMP3, "load_info of an MP3 returns an AudioStreamMP3")
	expect_equal(stream.title, "Nine Thou (Superstars Remix)", "MP3: title from UTF-16 with BOM, ID3v2 wins over ID3v1")
	expect_equal(stream.artist, "Café", "MP3: artist from Latin-1")
	expect_equal(stream.track_number, 3, "MP3: track_number from TRCK n/total")
	expect_equal(stream.track_count, 12, "MP3: track_count from TRCK n/total")
	expect_equal(stream.disc_number, 1, "MP3: disc_number from TPOS n/total")
	expect_equal(stream.disc_count, 2, "MP3: disc_count from TPOS n/total")
	expect_equal(stream.year, 2001, "MP3: year from TYER")
	expect_equal(stream.genre, "Rock", "MP3: genre from the ID3 code (17)")
	expect_equal(stream.comment, "Hello", "MP3: comment from COMM")
	expect_equal(stream.get_sample_rate(), 44100, "MP3: get_sample_rate")
	expect_equal(stream.get_channel_count(), 2, "MP3: get_channel_count")
	expect_equal(stream.get_codec(), "MP3", "MP3: get_codec")
	expect(absf(stream.get_bitrate() - 128000) < 12800, "MP3: get_bitrate is about 128 kbit/s")
	expect(absf(stream.get_length() - MP3_LENGTH) < 0.1, "MP3: get_length is about 1.04 s")
	expect(not stream.cover_data.is_empty(), "MP3: cover_data holds the APIC bytes")
	var cover: Texture2D = stream.cover
	expect_equal(cover.get_size(), Vector2(8, 8), "MP3: cover is the decoded 8x8 picture")
	expect(stream.get_cover() == cover, "MP3: the cover is decoded once and cached")


func _test_load_from_file() -> void:
	var wav: AudioStreamWAV = AudioStreamWAV.load_from_file(WAV_PATH)
	expect_equal(wav.title, "Wave Title", "AudioStreamWAV.load_from_file reads the tags")
	expect_equal(wav.data.size(), WAV_SAMPLE_BYTES, "AudioStreamWAV.load_from_file keeps the audio data")
	var mp3: AudioStreamMP3 = AudioStreamMP3.load_from_file(MP3_PATH)
	expect_equal(mp3.title, "Nine Thou (Superstars Remix)", "AudioStreamMP3.load_from_file reads the tags")


func _test_imported_tags() -> void:
	var stream: AudioStream = load(IMPORTED_PATH)
	expect_equal(stream.title, "Night Drive", "the importer stores the tags in the imported resource")
	expect_equal(stream.get_codec(), "QOA", "an imported WAV reports the codec of the imported data")


func _test_defaults_and_manual_values() -> void:
	var stream := AudioStreamWAV.new()
	for property: String in ["title", "artist", "album", "album_artist", "genre", "composer", "copyright", "comment"]:
		expect_equal(stream.get(property), "", "%s is empty by default" % property)
	for property: String in ["track_number", "track_count", "disc_number", "disc_count", "year"]:
		expect_equal(stream.get(property), 0, "%s is 0 by default" % property)
	expect(stream.cover == null, "cover is null by default")
	expect(stream.cover_data.is_empty(), "cover_data is empty by default")
	stream.title = "By Hand"
	stream.track_number = 2
	expect_equal(stream.title, "By Hand", "title can be set by hand")
	expect_equal(stream.track_number, 2, "track_number can be set by hand")
	var mp3: AudioStream = AudioStream.load_info(MP3_PATH)
	var image: Image = Image.create(4, 4, false, Image.FORMAT_RGB8)
	mp3.cover = ImageTexture.create_from_image(image)
	expect_equal(mp3.cover.get_size(), Vector2(4, 4), "cover can be set by hand")
	expect(mp3.cover_data.is_empty(), "setting a cover texture clears cover_data")


func _test_wrapper_streams() -> void:
	var playlist := AudioStreamPlaylist.new()
	playlist.title = "Album"
	playlist.artist = "Someone"
	playlist.year = 2020
	expect_equal(playlist.title, "Album", "a playlist has its own title")
	expect_equal(playlist.year, 2020, "a playlist has its own year")
	expect_equal(playlist.get_codec(), "", "a playlist has no codec")
	expect_equal(playlist.get_sample_rate(), 0, "a playlist has no sample rate")


func _test_read_file_tags_setting() -> void:
	expect_equal(ProjectSettings.get_setting("audio/info/read_file_tags"), true, "audio/info/read_file_tags defaults to true")
	ProjectSettings.set_setting("audio/info/read_file_tags", false)
	var off: AudioStreamWAV = AudioStreamWAV.load_from_file(WAV_PATH)
	expect_equal(off.title, "", "read_file_tags off: no tags are read")
	ProjectSettings.set_setting("audio/info/read_file_tags", true)
	var on: AudioStreamWAV = AudioStreamWAV.load_from_file(WAV_PATH)
	expect_equal(on.title, "Wave Title", "read_file_tags on: the tags are read again")
