extends Node
## Minimal example of Audio Mixer: the snippets of the wiki guide, in order.
## Wiki: https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Audio-Mixer

const TONE := "res://audio-mixer/resources/tone.tres"


func _ready() -> void:
	# Set up the channels in the Inspector. The layout is applied here: the project setting would apply to every scene.
	AudioMixerServer.apply_layout(load("res://audio-mixer/resources/mixer_layout.tres"))
	AudioMixerServer.play(&"music", TONE)
	print("Music playing: ", AudioMixerServer.is_playing(&"music"))

	# Set up the channels in code.
	var music := AudioMixerChannel.new()
	music.channel_name = &"music"
	music.bus = &"Music"
	music.default_fade_time = 2.0
	music.queue_mode = AudioMixerChannel.QUEUE_MODE_LOOP_QUEUE
	var sfx := AudioMixerChannel.new()
	sfx.channel_name = &"sfx"
	sfx.bus = &"Sfx"
	sfx.max_polyphony = 8
	var layout := AudioMixerLayout.new()
	layout.add_channel(music)
	layout.add_channel(sfx)
	AudioMixerServer.apply_layout(layout)
	print(AudioMixerServer.get_channel_names()) # Prints ["music", "sfx"]

	# Show the title of the current track (connected first, so that the playlist below shows its title).
	AudioMixerServer.channel_started.connect(_on_channel_started)

	# Play a playlist.
	AudioMixerServer.enqueue(&"music", TONE)
	AudioMixerServer.enqueue(&"music", TONE)
	AudioMixerServer.enqueue(&"music", TONE)
	AudioMixerServer.play(&"music")
	AudioMixerServer.play_next(&"music", 0.5) # cross-fade in half a second
	print("Queue index: ", AudioMixerServer.get_queue_index(&"music"))

	# Cross-fade to the boss music.
	AudioMixerServer.cross_fade(&"music", TONE, 3.0)
	print("Music fading: ", AudioMixerServer.is_fading(&"music"))

	# Play a sound effect.
	AudioMixerServer.play_one_shot(&"sfx", TONE, -3.0, randf_range(0.9, 1.1))
	print("Sound effects: ", AudioMixerServer.get_voice_count(&"sfx"))

	# Keep the music in the pause menu.
	var voice := AudioMixerChannel.new()
	voice.channel_name = &"voice"
	voice.pause_with_tree = true # silent in the pause menu
	voice.stop_on_scene_change = true # doesn't carry over into the next level
	AudioMixerServer.add_channel(voice)
	print("Channels: ", AudioMixerServer.get_channel_names())

	# Lower the music while a voice speaks.
	music.duck_on_tts = true
	music.duck_volume_db = -15.0
	AudioMixerServer.add_channel(music)
	if TTSServer.is_available(): # no speech when headless
		TTSServer.speak("Welcome back!") # the music fades down while the text is spoken
	await play_dialog_line(TONE)

	AudioMixerServer.channel_started.disconnect(_on_channel_started)
	get_tree().quit()


func _on_channel_started(channel: StringName, _index: int, stream_path: String) -> void:
	if channel == &"music":
		$NowPlaying.text = stream_path.get_file().get_basename()
		print("Now playing: ", $NowPlaying.text)


func play_dialog_line(path: String) -> void:
	AudioMixerServer.duck(&"music", -10.0)
	print("Music ducked: ", AudioMixerServer.is_ducked(&"music"))
	AudioMixerServer.play(&"voice", path)
	await AudioMixerServer.channel_finished
	AudioMixerServer.unduck(&"music")
	print("Music ducked: ", AudioMixerServer.is_ducked(&"music"))
