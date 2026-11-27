extends SceneTree
## Measurement (not part of the pass/fail checks) for #363: cost of reading track information.
## Generates 100 MP3 files (ID3v2.3 tag with a 500x500 noise JPEG cover, 40 frames) into user://363_measure/ and measures
## load_from_file with audio/info/read_file_tags on and off, AudioStream.load_info and the first get_cover() decode.
## Prints a Markdown table.
##
## Run (headless, from the repository folder of the test project):
##   godot --headless --path godot-project -s res://audio-track-information/scripts/gd/measure_track_info.gd

const DIR := "user://363_measure"
const FILES := 100


static func _be32(v: int) -> PackedByteArray:
	return PackedByteArray([(v >> 24) & 0xFF, (v >> 16) & 0xFF, (v >> 8) & 0xFF, v & 0xFF])


static func _syncsafe(v: int) -> PackedByteArray:
	return PackedByteArray([(v >> 21) & 0x7F, (v >> 14) & 0x7F, (v >> 7) & 0x7F, v & 0x7F])


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


## A zero padded fixed size field of an ID3v1 tag.
static func _field(text: String, size: int) -> PackedByteArray:
	var out := text.to_ascii_buffer()
	out.resize(size)
	return out


## An MP3 with an ID3v2.3 tag, 40 frames and an ID3v1 tag. Without arguments the cover is an 8x8 red PNG.
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


func _initialize() -> void:
	DirAccess.make_dir_recursive_absolute(DIR)
	var image := Image.create(500, 500, false, Image.FORMAT_RGB8)
	var rng := RandomNumberGenerator.new()
	rng.seed = 363
	for y in 500:
		for x in 500:
			image.set_pixel(x, y, Color(rng.randf(), rng.randf(), rng.randf()))
	var jpg := image.save_jpg_to_buffer(0.9)
	var bytes := make_mp3_bytes(jpg, "image/jpeg")
	var paths: Array[String] = []
	for i in FILES:
		var path := "%s/track_%03d.mp3" % [DIR, i]
		var file := FileAccess.open(path, FileAccess.WRITE)
		file.store_buffer(bytes)
		file.close()
		paths.append(path)

	ProjectSettings.set_setting("audio/info/read_file_tags", true)
	var t0 := Time.get_ticks_usec()
	var failed_on := 0
	var kept: Array[AudioStream] = []
	for path in paths:
		var s := AudioStreamMP3.load_from_file(path)
		if s == null:
			failed_on += 1
		else:
			kept.append(s)
	var load_on := (Time.get_ticks_usec() - t0) / 1000.0

	ProjectSettings.set_setting("audio/info/read_file_tags", false)
	t0 = Time.get_ticks_usec()
	var failed_off := 0
	for path in paths:
		if AudioStreamMP3.load_from_file(path) == null:
			failed_off += 1
	var load_off := (Time.get_ticks_usec() - t0) / 1000.0
	ProjectSettings.set_setting("audio/info/read_file_tags", true)

	t0 = Time.get_ticks_usec()
	var infos: Array[AudioStream] = []
	for path in paths:
		var info := AudioStream.load_info(path)
		if info != null:
			infos.append(info)
	var info_ms := (Time.get_ticks_usec() - t0) / 1000.0

	t0 = Time.get_ticks_usec()
	var covers := 0
	for info in infos:
		if info.get_cover() != null:
			covers += 1
	var cover_ms := (Time.get_ticks_usec() - t0) / 1000.0

	print("")
	print("Measurements (%d MP3 files, %d bytes each, cover JPEG %d bytes)" % [FILES, bytes.size(), jpg.size()])
	print("")
	print("| Measurement | Total (ms) | Per file (ms) | Notes |")
	print("|---|---|---|---|")
	print("| AudioStreamMP3.load_from_file, read_file_tags on | %.2f | %.3f | %d failed |" % [load_on, load_on / FILES, failed_on])
	print("| AudioStreamMP3.load_from_file, read_file_tags off | %.2f | %.3f | %d failed |" % [load_off, load_off / FILES, failed_off])
	print("| AudioStream.load_info | %.2f | %.3f | %d streams |" % [info_ms, info_ms / maxi(infos.size(), 1), infos.size()])
	print("| first get_cover() decode (500x500 JPEG) | %.2f | %.3f | %d covers |" % [cover_ms, cover_ms / maxi(covers, 1), covers])
	print("")
	print("sizeof: see engine unit test")

	for path in paths:
		DirAccess.remove_absolute(path)
	DirAccess.remove_absolute(DIR)
	quit(0)
