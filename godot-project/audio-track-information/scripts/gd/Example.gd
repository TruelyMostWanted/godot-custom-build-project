extends Control
## Minimal example of Audio Track Information: the snippets of the wiki guide, in order.
## Wiki: https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Audio-Track-Information

const RES := "res://audio-track-information/resources/"

@export var fallback_cover: Texture2D
@onready var list: ItemList = $ItemList
@onready var player: AudioStreamPlayer = $AudioStreamPlayer
@onready var cover_rect: TextureRect = $Cover
@onready var label: Label = $Label
var tracks: Array[Dictionary] = [] # { "path": String, "info": AudioStream }


func _ready() -> void:
	# Read the tags from code.
	var s: AudioStream = load(RES + "night_drive.wav")
	print("%s - %s (%s, %d)" % [s.artist, s.title, s.album, s.year])
	print("%s, %d Hz, %d ch, %d kbit/s, %.1f s" % [s.get_codec(), s.get_sample_rate(), s.get_channel_count(), s.get_bitrate() / 1000, s.get_length()])

	# Music library list.
	scan(RES)
	print("Library: ", list.get_item_text(0))
	_on_item_list_item_activated(0)

	# Now playing with cover.
	show_track(RES + "night_drive.wav")
	print("Now playing: ", label.text.replace("\n", " / "))

	# Format getters.
	var info: AudioStream = AudioStream.load_info(RES + "night_drive.wav")
	print("%s, %d Hz, %d ch, %d kbit/s" % [info.get_codec(), info.get_sample_rate(), info.get_channel_count(), info.get_bitrate() / 1000])

	# Runtime and import.
	print(info.title, " by ", info.artist)

	# Playlists and wrapper streams.
	var album := AudioStreamPlaylist.new()
	album.title = "Night Drive"
	album.artist = "Example Band"
	album.year = 2026
	album.cover = preload(RES + "night_drive.png")
	print("Album: ", album.title, ", cover ", album.cover.get_size())

	# Localized titles.
	label.text = tr(s.title) # or TextLocalizationServer.translate(s.title)
	print("Label: ", label.text)

	get_tree().quit()


func scan(dir: String) -> void:
	for file: String in DirAccess.get_files_at(dir):
		if not file.get_extension().to_lower() in ["mp3", "ogg", "opus", "wav"]:
			continue
		var path: String = dir.path_join(file)
		var info: AudioStream = AudioStream.load_info(path)
		if info != null:
			tracks.append({"path": path, "info": info})
	tracks.sort_custom(func(a: Dictionary, b: Dictionary) -> bool:
		var x: AudioStream = a["info"]
		var y: AudioStream = b["info"]
		if x.album != y.album:
			return x.album < y.album
		if x.disc_number != y.disc_number:
			return x.disc_number < y.disc_number
		return x.track_number < y.track_number)
	list.clear()
	for t: Dictionary in tracks:
		var s: AudioStream = t["info"]
		var length: int = int(s.get_length())
		list.add_item("%s - %s (%d:%02d)" % [s.artist, s.title, length / 60, length % 60])


func _on_item_list_item_activated(index: int) -> void:
	player.stream = load(tracks[index]["path"]) # the real stream, with audio
	player.play()


func show_track(path: String) -> void:
	var stream: AudioStream = load(path)
	player.stream = stream
	player.play()
	cover_rect.texture = stream.cover if stream.cover != null else fallback_cover
	label.text = "%s\n%s - %s" % [stream.title, stream.artist, stream.album]
