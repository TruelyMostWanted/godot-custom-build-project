extends Node
## Minimal example of WebM Video: the snippets of the wiki guide, in order.
## Wiki: https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/WebM-Video


func _ready() -> void:
	# Setup: a .webm file in user://videos and a VideoStreamPlayer (the guide assumes them).
	DirAccess.make_dir_recursive_absolute("user://videos")
	DirAccess.copy_absolute("res://webm-video/resources/testsrc.webm", "user://videos/intro.webm")
	var video_player := VideoStreamPlayer.new()
	video_player.name = "VideoStreamPlayer"
	add_child(video_player)

	# Play a video
	$VideoStreamPlayer.stream = load("user://videos/intro.webm")
	$VideoStreamPlayer.play()
	var s := VideoStreamWebm.new()
	s.file = "user://videos/intro.webm"
	print("Playing: ", $VideoStreamPlayer.is_playing(), ", own stream: ", s.file)

	# Load at runtime
	if ResourceLoader.exists("user://videos/intro.webm", "VideoStream"):
		ResourceLoader.load_threaded_request("user://videos/intro.webm", "VideoStream")
		# ... later:
		$VideoStreamPlayer.stream = ResourceLoader.load_threaded_get("user://videos/intro.webm")
		$VideoStreamPlayer.play()
	print("Threaded stream: ", $VideoStreamPlayer.stream.get_class())

	# Seek (the sample video is 1 s long)
	print($VideoStreamPlayer.get_stream_length())
	$VideoStreamPlayer.stream_position = 0.5
	get_tree().quit()
