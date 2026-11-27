extends Node
## Minimal example of Subtitle Player: the snippets of the wiki guide, in order.
## Wiki: https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Subtitle-Player
## The scene has Voice (an AudioStreamPlayer with voice.tres), SubtitlePlayer (night_drive.srt, own clock) and SubtitleLabel.

const RES := "res://subtitle-player/resources/"

var playback := SubtitlePlayback.new()
var time: float = 0.0


func _ready() -> void:
	set_process(false) # _process belongs to "Using SubtitlePlayback without a node" below

	# Give a stream a subtitle file.
	$Voice.stream.subtitle_path = RES + "night_drive.srt" # on the stream
	$Voice.subtitle_path = RES + "night_drive_alt.srt" # override on the player

	# Add a SubtitlePlayer.
	var player := SubtitlePlayer.new()
	$Voice.add_child(player)
	player.sync_node = ".."

	# Show the text and play.
	player.segment_changed.connect(_on_voice_segment_changed)
	$Voice.playing_changed.connect(func(playing: bool) -> void: print("playing_changed ", playing))
	$Voice.play()
	await $Voice.finished

	# Play, pause, stop.
	$Voice.playing_changed.connect(func(playing: bool) -> void: $SubtitleLabel.visible = playing)
	$Voice.play()
	$Voice.stop()
	print("Label visible after stop: ", $SubtitleLabel.visible)

	# A playlist with a subtitle per track.
	var a: AudioStream = load(RES + "line_a.tres")
	a.subtitle_path = RES + "line_a.srt"
	var b: AudioStream = load(RES + "line_b.tres")
	b.subtitle_path = RES + "line_b.srt"
	$Voice.subtitle_path = "" # the override of the player would win over the streams
	$Voice.stream = a
	$Voice.play()
	await get_tree().create_timer(0.2).timeout
	$Voice.crossfade_to(b, 0.1) # b.subtitle_path is shown afterwards
	await get_tree().create_timer(0.3).timeout
	print("After the crossfade: ", player.get_playback().get_subtitle().resource_path)
	$Voice.stop()

	# Explicit subtitle wins.
	print($SubtitlePlayer.subtitle.resource_path) # null unless set explicitly
	print($SubtitlePlayer.get_playback().get_subtitle().resource_path) # the subtitle in use

	# Setting up by hand (the SubtitlePlayer of the scene, without a sync node).
	$SubtitlePlayer.segment_changed.connect(_on_segment_changed)

	# Sync sources (own clock).
	$SubtitlePlayer.play(0.5) # Start at 0.5 seconds; the segments active then are emitted.
	$SubtitlePlayer.seek(1.5)
	print($SubtitlePlayer.get_playback_position(), " ", $SubtitlePlayer.is_playing())
	$SubtitlePlayer.stop() # Position 0.0, all active segments end.

	# Using SubtitlePlayback without a node.
	playback.subtitle = load(RES + "night_drive.srt")
	playback.segment_changed.connect(func(segment: SubtitleSegment) -> void: $SubtitleLabel.text = segment.text if segment else "")
	set_process(true)
	await get_tree().create_timer(0.5).timeout
	print("Playback without a node: ", $SubtitleLabel.text)
	set_process(false)

	# Overlapping segments.
	var player_playback: SubtitlePlayback = $SubtitlePlayer.get_playback()
	player_playback.segment_started.connect(func(_s: SubtitleSegment) -> void: _show(player_playback))
	player_playback.segment_ended.connect(func(_s: SubtitleSegment) -> void: _show(player_playback))
	$SubtitlePlayer.play(0.5)
	print("Overlapping: ", $SubtitleLabel.text)
	$SubtitlePlayer.stop()

	get_tree().quit()


func _process(delta: float) -> void:
	time += delta
	playback.update(time)


func _on_voice_segment_changed(segment: SubtitleSegment) -> void:
	$SubtitleLabel.text = segment.text if segment else ""
	print(segment.text if segment else "(none)")


func _on_segment_changed(segment: SubtitleSegment) -> void:
	$SubtitleLabel.text = SubtitleServer.get_display_text($SubtitlePlayer.subtitle, segment) if segment else ""
	print("SubtitlePlayer: ", $SubtitleLabel.text)


func _show(player_playback: SubtitlePlayback) -> void:
	var lines := PackedStringArray()
	for segment: SubtitleSegment in player_playback.get_active_segments():
		lines.append(SubtitleServer.get_display_text($SubtitlePlayer.subtitle, segment))
	$SubtitleLabel.text = "\n".join(lines)
