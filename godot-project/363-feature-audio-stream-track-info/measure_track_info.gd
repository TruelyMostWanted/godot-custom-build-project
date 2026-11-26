extends SceneTree
## Measurement (not part of the pass/fail checks) for #363: cost of reading track information.
## Generates 100 MP3 files (ID3v2.3 tag with a 500x500 noise JPEG cover, 40 frames) into user://363_measure/ and measures
## load_from_file with audio/info/read_file_tags on and off, AudioStream.load_info and the first get_cover() decode.
## Prints a Markdown table.
##
## Run (headless, from the repository folder of the test project):
##   godot --headless --path godot-project -s res://363-feature-audio-stream-track-info/measure_track_info.gd

const Gen := preload("res://363-feature-audio-stream-track-info/scripts/gd/check.gd")
const DIR := "user://363_measure"
const FILES := 100


func _initialize() -> void:
	DirAccess.make_dir_recursive_absolute(DIR)
	var image := Image.create(500, 500, false, Image.FORMAT_RGB8)
	var rng := RandomNumberGenerator.new()
	rng.seed = 363
	for y in 500:
		for x in 500:
			image.set_pixel(x, y, Color(rng.randf(), rng.randf(), rng.randf()))
	var jpg := image.save_jpg_to_buffer(0.9)
	var bytes := Gen.make_mp3_bytes(jpg, "image/jpeg")
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
