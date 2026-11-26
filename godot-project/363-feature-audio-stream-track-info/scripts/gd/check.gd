extends FeatureCheck
## GDScript checks of 363-feature-audio-stream-track-info. Check the same things as scripts/cs/Issue363FeatureAudioStreamTrackInfoCheck.cs.
## The sample files are generated into user:// by the check itself.

const DIR := "user://363-feature-audio-stream-track-info"
const WAV_PATH := DIR + "/sample.wav"
const MP3_PATH := DIR + "/sample.mp3"


func _get_check_name() -> String:
	return "363-feature-audio-stream-track-info"


# --- byte helpers -------------------------------------------------------------

static func _le16(v: int) -> PackedByteArray:
	return PackedByteArray([v & 0xFF, (v >> 8) & 0xFF])


static func _le32(v: int) -> PackedByteArray:
	return PackedByteArray([v & 0xFF, (v >> 8) & 0xFF, (v >> 16) & 0xFF, (v >> 24) & 0xFF])


static func _be32(v: int) -> PackedByteArray:
	return PackedByteArray([(v >> 24) & 0xFF, (v >> 16) & 0xFF, (v >> 8) & 0xFF, v & 0xFF])


static func _syncsafe(v: int) -> PackedByteArray:
	return PackedByteArray([(v >> 21) & 0x7F, (v >> 14) & 0x7F, (v >> 7) & 0x7F, v & 0x7F])


## A RIFF chunk: id, little endian size, body, padding to an even size.
static func _riff_chunk(id: String, body: PackedByteArray) -> PackedByteArray:
	var out := id.to_ascii_buffer()
	out.append_array(_le32(body.size()))
	out.append_array(body)
	if body.size() % 2 == 1:
		out.append(0)
	return out


## An INFO sub-chunk with a zero terminated string.
static func _info(id: String, text: String) -> PackedByteArray:
	var body := text.to_ascii_buffer()
	body.append(0)
	return _riff_chunk(id, body)


## An ID3v2 text frame body: encoding byte + text bytes.
static func _text_payload(encoding: int, text: PackedByteArray) -> PackedByteArray:
	var out := PackedByteArray([encoding])
	out.append_array(text)
	return out


## An ID3v2 frame. version 4: syncsafe size, version 3: big endian size.
static func _frame(version: int, id: String, payload: PackedByteArray) -> PackedByteArray:
	var out := id.to_ascii_buffer()
	out.append_array(_syncsafe(payload.size()) if version == 4 else _be32(payload.size()))
	out.append_array(PackedByteArray([0, 0]))
	out.append_array(payload)
	return out


static func _id3_tag(version: int, frames: PackedByteArray) -> PackedByteArray:
	var out := "ID3".to_ascii_buffer()
	out.append_array(PackedByteArray([version, 0, 0]))
	out.append_array(_syncsafe(frames.size()))
	out.append_array(frames)
	return out


static func _write(path: String, bytes: PackedByteArray) -> void:
	var file := FileAccess.open(path, FileAccess.WRITE)
	file.store_buffer(bytes)
	file.close()


## A zero padded fixed size field of an ID3v1 tag.
static func _field(text: String, size: int) -> PackedByteArray:
	var out := text.to_ascii_buffer()
	out.resize(size)
	return out


# --- sample files -------------------------------------------------------------

static func make_wav_bytes() -> PackedByteArray:
	var fmt := PackedByteArray()
	fmt.append_array(_le16(1))  # PCM
	fmt.append_array(_le16(1))  # mono
	fmt.append_array(_le32(8000))
	fmt.append_array(_le32(16000))
	fmt.append_array(_le16(2))
	fmt.append_array(_le16(16))
	var data := PackedByteArray()
	data.resize(1600)
	var info := "INFO".to_ascii_buffer()
	info.append_array(_info("INAM", "Wave Title"))
	info.append_array(_info("IART", "Wave Artist"))
	info.append_array(_info("ITRK", "7"))
	info.append_array(_info("ICRD", "1999"))
	info.append_array(_info("IGNR", "Jazz"))
	var frames := PackedByteArray()
	frames.append_array(_frame(4, "TALB", _text_payload(3, "Älbum ✓".to_utf8_buffer())))
	frames.append_array(_frame(4, "TCOM", _text_payload(3, "Composer".to_utf8_buffer())))
	var body := "WAVE".to_ascii_buffer()
	body.append_array(_riff_chunk("fmt ", fmt))
	body.append_array(_riff_chunk("data", data))
	body.append_array(_riff_chunk("LIST", info))
	body.append_array(_riff_chunk("id3 ", _id3_tag(4, frames)))
	var out := "RIFF".to_ascii_buffer()
	out.append_array(_le32(body.size()))
	out.append_array(body)
	return out


## An MP3 with an ID3v2.3 tag, 40 frames and an ID3v1 tag. Without arguments the cover is an 8x8 red PNG;
## the measurement script passes bigger JPEG bytes.
static func make_mp3_bytes(cover_bytes: PackedByteArray = PackedByteArray(), mime: String = "image/png") -> PackedByteArray:
	if cover_bytes.is_empty():
		var image := Image.create(8, 8, false, Image.FORMAT_RGB8)
		image.fill(Color.RED)
		cover_bytes = image.save_png_to_buffer()
	var title := PackedByteArray([0xFF, 0xFE])
	title.append_array("Nine Thou (Superstars Remix)".to_utf16_buffer())
	var comm := _text_payload(0, "eng".to_ascii_buffer())
	comm.append(0)
	comm.append_array("Hello".to_ascii_buffer())
	var apic := _text_payload(0, mime.to_ascii_buffer())
	apic.append(0)
	apic.append(3)
	apic.append(0)
	apic.append_array(cover_bytes)
	var frames := PackedByteArray()
	frames.append_array(_frame(3, "TIT2", _text_payload(1, title)))
	frames.append_array(_frame(3, "TPE1", _text_payload(0, PackedByteArray([0x43, 0x61, 0x66, 0xE9]))))
	frames.append_array(_frame(3, "TRCK", _text_payload(0, "3/12".to_ascii_buffer())))
	frames.append_array(_frame(3, "TPOS", _text_payload(0, "1/2".to_ascii_buffer())))
	frames.append_array(_frame(3, "TYER", _text_payload(0, "2001".to_ascii_buffer())))
	frames.append_array(_frame(3, "TCON", _text_payload(0, "(17)".to_ascii_buffer())))
	frames.append_array(_frame(3, "COMM", comm))
	frames.append_array(_frame(3, "APIC", apic))
	var out := _id3_tag(3, frames)
	for i in 40:
		out.append_array(PackedByteArray([0xFF, 0xFB, 0x90, 0x64]))
		var zeros := PackedByteArray()
		zeros.resize(413)
		out.append_array(zeros)
	var v1 := "TAG".to_ascii_buffer()
	v1.append_array(_field("Old", 30))
	v1.append_array(_field("", 30))
	v1.append_array(_field("", 30))
	v1.append_array(_field("", 4))
	v1.append_array(_field("", 30))
	v1.append(17)
	out.append_array(v1)
	return out


# --- checks -------------------------------------------------------------------

func _run_checks() -> void:
	DirAccess.make_dir_recursive_absolute(DIR)
	_write(WAV_PATH, make_wav_bytes())
	_write(MP3_PATH, make_mp3_bytes())
	_check_wav(AudioStream.load_info(WAV_PATH), "WAV load_info")
	_check_mp3(AudioStream.load_info(MP3_PATH), "MP3 load_info")
	var wav := AudioStreamWAV.load_from_file(WAV_PATH)
	if expect(wav != null, "AudioStreamWAV.load_from_file returns a stream"):
		_check_wav(wav, "WAV load_from_file")
		expect(wav.data.size() == 1600, "WAV load_from_file has the 1600 sample bytes")
	var mp3 := AudioStreamMP3.load_from_file(MP3_PATH)
	if expect(mp3 != null, "AudioStreamMP3.load_from_file returns a stream (decoder accepts the zero frames)"):
		_check_mp3(mp3, "MP3 load_from_file")
	_check_fresh_and_manual()
	_check_setting()


func _near(actual: float, expected: float, tolerance_fraction: float) -> bool:
	return absf(actual - expected) <= expected * tolerance_fraction


func _check_wav(stream: AudioStream, label: String) -> void:
	if not expect(stream != null, "%s: returns a stream" % label):
		return
	expect(stream is AudioStreamWAV, "%s: class is AudioStreamWAV" % label)
	expect_equal(stream.title, "Wave Title", "%s: title" % label)
	expect_equal(stream.artist, "Wave Artist", "%s: artist" % label)
	expect_equal(stream.album, "Älbum ✓", "%s: album (UTF-8 from id3 chunk)" % label)
	expect_equal(stream.composer, "Composer", "%s: composer" % label)
	expect_equal(stream.track_number, 7, "%s: track_number" % label)
	expect_equal(stream.year, 1999, "%s: year" % label)
	expect_equal(stream.genre, "Jazz", "%s: genre" % label)
	expect_equal(stream.get_sample_rate(), 8000, "%s: sample rate" % label)
	expect_equal(stream.get_channel_count(), 1, "%s: channel count" % label)
	expect_equal(stream.get_codec(), "PCM", "%s: codec" % label)
	expect_equal(stream.get_bitrate(), 128000, "%s: bitrate (8000 * 16)" % label)
	expect(_near(stream.get_length(), 0.1, 0.1), "%s: length about 0.1 s (got %s)" % [label, stream.get_length()])
	expect(stream.cover == null, "%s: no cover" % label)
	expect(stream.cover_data.is_empty(), "%s: no cover data" % label)


func _check_mp3(stream: AudioStream, label: String) -> void:
	if not expect(stream != null, "%s: returns a stream" % label):
		return
	expect(stream is AudioStreamMP3, "%s: class is AudioStreamMP3" % label)
	expect_equal(stream.title, "Nine Thou (Superstars Remix)", "%s: title (UTF-16 with BOM, ID3v2 wins over ID3v1)" % label)
	expect_equal(stream.artist, "Café", "%s: artist (Latin-1)" % label)
	expect_equal(stream.track_number, 3, "%s: track_number" % label)
	expect_equal(stream.track_count, 12, "%s: track_count" % label)
	expect_equal(stream.disc_number, 1, "%s: disc_number" % label)
	expect_equal(stream.disc_count, 2, "%s: disc_count" % label)
	expect_equal(stream.year, 2001, "%s: year" % label)
	expect_equal(stream.genre, "Rock", "%s: genre from (17)" % label)
	expect_equal(stream.comment, "Hello", "%s: comment" % label)
	expect_equal(stream.get_sample_rate(), 44100, "%s: sample rate" % label)
	expect_equal(stream.get_channel_count(), 2, "%s: channel count" % label)
	expect_equal(stream.get_codec(), "MP3", "%s: codec" % label)
	expect(_near(stream.get_bitrate(), 128000.0, 0.1), "%s: bitrate about 128000 (got %s)" % [label, stream.get_bitrate()])
	expect(_near(stream.get_length(), 40.0 * 1152.0 / 44100.0, 0.1), "%s: length about 1.04 s (got %s)" % [label, stream.get_length()])
	expect(not stream.cover_data.is_empty(), "%s: cover_data not empty" % label)
	var cover: Texture2D = stream.cover
	if expect(cover != null, "%s: cover is a Texture2D" % label):
		expect(cover.get_width() == 8 and cover.get_height() == 8, "%s: cover is 8x8" % label)
		expect(stream.get_cover() == cover, "%s: get_cover() returns the cached cover" % label)


func _check_fresh_and_manual() -> void:
	var fresh := AudioStreamWAV.new()
	expect(fresh.title == "" and fresh.artist == "" and fresh.album == "" and fresh.album_artist == "", "fresh stream: text fields empty")
	expect(fresh.composer == "" and fresh.copyright == "" and fresh.genre == "" and fresh.comment == "", "fresh stream: other text fields empty")
	expect(fresh.track_number == 0 and fresh.track_count == 0 and fresh.disc_number == 0 and fresh.disc_count == 0 and fresh.year == 0, "fresh stream: numbers 0")
	expect(fresh.cover == null, "fresh stream: cover null")
	expect(fresh.cover_data.is_empty(), "fresh stream: cover_data empty")
	fresh.title = "By Hand"
	fresh.track_number = 2
	var image := Image.create(4, 4, false, Image.FORMAT_RGB8)
	fresh.cover = ImageTexture.create_from_image(image)
	expect_equal(fresh.title, "By Hand", "title set by hand")
	expect_equal(fresh.track_number, 2, "track_number set by hand")
	expect(fresh.cover != null and fresh.cover.get_width() == 4, "cover set by hand")
	expect(fresh.cover_data.is_empty(), "setting a cover texture clears cover_data")
	var playlist := AudioStreamPlaylist.new()
	expect(playlist.title == "" and playlist.cover == null and playlist.track_number == 0, "playlist has the fields, empty by default")
	playlist.title = "Album"
	playlist.artist = "Someone"
	playlist.year = 2020
	expect(playlist.title == "Album" and playlist.artist == "Someone" and playlist.year == 2020, "playlist fields set by hand")
	expect_equal(playlist.get_codec(), "", "playlist codec is empty")
	expect_equal(playlist.get_sample_rate(), 0, "playlist sample rate is 0")


func _check_setting() -> void:
	expect(ProjectSettings.get_setting("audio/info/read_file_tags", true) == true, "audio/info/read_file_tags defaults to true")
	ProjectSettings.set_setting("audio/info/read_file_tags", false)
	var off := AudioStreamWAV.load_from_file(WAV_PATH)
	if expect(off != null, "load_from_file works with read_file_tags off"):
		expect_equal(off.title, "", "title is empty with read_file_tags off")
	ProjectSettings.set_setting("audio/info/read_file_tags", true)
	var on := AudioStreamWAV.load_from_file(WAV_PATH)
	if expect(on != null, "load_from_file works with read_file_tags on again"):
		expect_equal(on.title, "Wave Title", "title is read again with read_file_tags on")
