extends Control
## Minimal example of Audio Player Fades And Playlists: the snippets of the wiki guide, in order.
## Wiki: https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Audio-Player-Fades-And-Playlists

const RES := "res://audio-player-fades-and-playlists/resources/"

var menu_calm: AudioStream = load(RES + "title.tres")
var menu_action: AudioStream = load(RES + "forest.tres")
var _calm := true

@onready var _music: AudioStreamPlayer = $Music
var _playlist: AudioStreamPlaylist


func _ready() -> void:
	# Assign it to a player (music.tres is the playlist built in the Inspector).
	$Music.stream = load(RES + "music.tres")
	$Music.fade_in(2.0)
	print("Playing: ", $Music.playing)

	# Fades.
	var next_track: AudioStream = load(RES + "river.tres")
	$Music.fade_in(2.0) # Starts playing from silence if stopped.
	$Music.fade_out(1.0, AudioMixerServer.FADE_CURVE_EASE_OUT)
	$Music.crossfade_to(next_track, 3.0) # Equal power by default.
	print("Fading: ", $Music.is_fading(), ", fade gain: ", $Music.get_fade_gain())

	# Track info and signals.
	print("Track index: ", $Music.get_current_track_index())

	# Playlist list and time helpers.
	var playlist := AudioStreamPlaylist.new()
	playlist.add_stream(load(RES + "forest.tres"))
	playlist.add_stream(load(RES + "river.tres"))
	playlist.insert_stream(0, load(RES + "title.tres"))
	playlist.move_stream(2, 1)
	print(playlist.find_stream(load(RES + "river.tres")))
	playlist.remove_stream(0)

	playlist = load(RES + "music.tres")
	for i: int in playlist.streams.size():
		print("%d %s  length %.1f s  starts at %.1f s" % [i, playlist.get_stream_name(i), playlist.get_stream_length(i), playlist.get_stream_start_time(i)])
	print("track at 3.5 s: ", playlist.get_stream_index_at(3.5))

	$Music.stream = playlist
	$Music.play()
	$Music.seek(playlist.get_stream_start_time(2)) # Jump to the third track.
	await get_tree().process_frame
	print("Track index after seek: ", $Music.get_current_track_index())

	# Build a music menu with a crossfade (the button press is simulated).
	$Music.stream = menu_calm
	$Music.fade_in(1.5)
	$SwitchButton.pressed.connect(_on_switch_pressed)
	$SwitchButton.pressed.emit()
	print("Menu music: ", $Music.stream.resource_path.get_file())

	# Show the playing track and a progress bar.
	_music.stream = playlist
	_playlist = _music.stream as AudioStreamPlaylist
	_music.position_signal_interval = 0.1
	_music.track_changed.connect(_on_track_changed)
	_music.position_changed.connect(_on_position_changed)
	_music.play()
	await get_tree().create_timer(0.3).timeout
	print("Now playing: ", $NowPlaying.text, ", progress: ", $Progress.value, " of ", $Progress.max_value)

	get_tree().quit()


func _on_switch_pressed() -> void:
	_calm = not _calm
	$Music.crossfade_to(menu_calm if _calm else menu_action, 2.0)


func _on_track_changed(index: int, _stream: AudioStream) -> void:
	$NowPlaying.text = _playlist.get_stream_name(index)
	$Progress.max_value = _playlist.get_stream_length(index)


func _on_position_changed(position: float) -> void:
	$Progress.value = position

