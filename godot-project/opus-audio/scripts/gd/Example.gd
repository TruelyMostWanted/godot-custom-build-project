extends Node
## Minimal example of Opus Audio: the snippets of the wiki guide, in order.
## Wiki: https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Opus-Audio


func _ready() -> void:
	# Setup: a copy of the sample in user://downloads, its bytes, and AudioStreamPlayers Music and Player (the guide assumes them).
	DirAccess.make_dir_recursive_absolute("user://downloads")
	DirAccess.copy_absolute("res://opus-audio/resources/sine_440.opus", "user://downloads/voice.opus")
	var downloaded_bytes: PackedByteArray = FileAccess.get_file_as_bytes("user://downloads/voice.opus")
	for player_name in ["Music", "Player"]:
		var audio_player := AudioStreamPlayer.new()
		audio_player.name = player_name
		add_child(audio_player)

	# Import an Opus file (the sample is imported as res://opus-audio/resources/sine_440.opus)
	var music := load("res://opus-audio/resources/sine_440.opus") as AudioStreamOpus
	music.loop = true
	$Music.stream = music
	$Music.play()
	print("Music length: ", music.get_length())

	# Load at runtime
	var voice := load("user://downloads/voice.opus") as AudioStreamOpus
	voice.loop = true
	$Player.stream = voice
	$Player.play()
	var from_buffer := AudioStreamOpus.load_from_buffer(downloaded_bytes)
	print("From buffer: ", from_buffer.get_length())

	# Play a stream
	var stream: AudioStream = from_buffer
	var playback := stream.instantiate_playback()
	playback.start(0.0)
	print("Playback: ", playback.get_class())
	$Music.stop()
	$Player.stop()
	get_tree().quit()
