extends Node
## Minimal example of Video Files: the snippets of the wiki guide, in order.
## Wiki: https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Video-Files


func _ready() -> void:
	# Setup: an .ogv file in user://videos and a VideoStreamPlayer (the guide assumes them).
	DirAccess.make_dir_recursive_absolute("user://videos")
	DirAccess.copy_absolute("res://video-files/resources/clip.ogv", "user://videos/intro.ogv")
	var video_player := VideoStreamPlayer.new()
	video_player.name = "VideoStreamPlayer"
	add_child(video_player)

	# Play a video from any folder
	$VideoStreamPlayer.stream = load("user://videos/intro.ogv")
	$VideoStreamPlayer.play()
	await get_tree().process_frame
	print("Playing: ", $VideoStreamPlayer.is_playing())
	get_tree().quit()
