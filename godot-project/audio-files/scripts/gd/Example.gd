extends Node
## Minimal example of Audio Files: the snippets of the wiki guide, in order.
## Wiki: https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Audio-Files


func _ready() -> void:
	# Setup: an Ogg Vorbis file in user://music and an AudioStreamPlayer named Music (the guide assumes them).
	DirAccess.make_dir_recursive_absolute("user://music")
	DirAccess.make_dir_recursive_absolute("user://recordings")
	DirAccess.copy_absolute("res://audio-files/resources/kept_tone.ogg", "user://music/theme.ogg")
	var player := AudioStreamPlayer.new()
	player.name = "Music"
	add_child(player)
	var recorded_pcm := PackedByteArray()
	recorded_pcm.resize(8820)

	# Play music from the player's folder
	var music := load("user://music/theme.ogg") as AudioStreamOggVorbis
	music.loop = true
	$Music.stream = music
	$Music.play()
	print("Music length: ", music.get_length())

	# Save a recording as WAV
	var stream := AudioStreamWAV.new()
	stream.format = AudioStreamWAV.FORMAT_16_BITS
	stream.mix_rate = 44100
	stream.data = recorded_pcm
	var error: Error = ResourceSaver.save(stream, "user://recordings/take1.wav")
	print("Saved WAV: ", error == OK)
	$Music.stop()
	get_tree().quit()
