extends FeatureCheck
## GDScript checks of 74-feature-audio-mixer-server. Check the same things as scripts/cs/Issue74FeatureAudioMixerServerCheck.cs.
## The checks run headless with the dummy audio driver: they check state, queue, volumes and fades, not sound.

const SCENE_B := "res://74-feature-audio-mixer-server/packed-scenes/scene_b.tscn"

var _started: Array[int] = []
var _queue_finished := 0
var _fades_finished := 0


func _get_check_name() -> String:
	return "74-feature-audio-mixer-server"


## A silent 16-bit mono WAV of the given length.
func _make_wav(seconds: float) -> AudioStreamWAV:
	var wav := AudioStreamWAV.new()
	wav.format = AudioStreamWAV.FORMAT_16_BITS
	wav.mix_rate = 44100
	wav.stereo = false
	var data := PackedByteArray()
	data.resize(int(seconds * 44100.0) * 2)
	wav.data = data
	return wav


func _make_channel(channel_name: StringName) -> AudioMixerChannel:
	var channel := AudioMixerChannel.new()
	channel.channel_name = channel_name
	return channel


func _wait(seconds: float) -> void:
	await get_tree().create_timer(seconds).timeout


func _on_channel_started(channel: StringName, index: int, _stream_path: String) -> void:
	if channel == &"playlist":
		_started.append(index)


func _on_queue_finished(channel: StringName) -> void:
	if channel == &"playlist":
		_queue_finished += 1


func _on_fade_finished(channel: StringName) -> void:
	if channel == &"music":
		_fades_finished += 1


func _run_checks() -> void:
	expect(Engine.has_singleton("AudioMixerServer"), "AudioMixerServer is a registered singleton")

	# Layout with four channels.
	var layout := AudioMixerLayout.new()
	var music := _make_channel(&"music")
	music.default_fade_time = 0.3
	layout.add_channel(music)
	var ambience := _make_channel(&"ambience")
	ambience.pause_with_tree = true
	ambience.stop_on_scene_change = true
	layout.add_channel(ambience)
	layout.add_channel(_make_channel(&"playlist"))
	var sfx := _make_channel(&"sfx")
	sfx.polyphony = 2
	layout.add_channel(sfx)
	AudioMixerServer.load_layout(layout)
	expect_equal(AudioMixerServer.get_channel_names(), PackedStringArray(["music", "ambience", "playlist", "sfx"]), "load_layout adds the channels in order")
	expect(AudioMixerServer.get_channel(&"ambience") == ambience, "get_channel returns the definition")

	AudioMixerServer.channel_started.connect(_on_channel_started)
	AudioMixerServer.queue_finished.connect(_on_queue_finished)
	AudioMixerServer.fade_finished.connect(_on_fade_finished)

	# 1. A queue of three entries plays in order, then finishes.
	for i in 3:
		AudioMixerServer.enqueue(&"playlist", _make_wav(0.1))
	expect(AudioMixerServer.get_queue(&"playlist").size() == 3, "the queue has three entries")
	AudioMixerServer.play(&"playlist")
	for i in 100: # At most 5 seconds.
		if _queue_finished > 0:
			break
		await _wait(0.05)
	expect_equal(_started, [0, 1, 2], "the queue entries start in order")
	expect(_queue_finished == 1, "queue_finished is emitted after the last entry")
	expect(not AudioMixerServer.is_playing(&"playlist"), "the channel stops after the last entry")

	# 2. next(), previous() and change_index() jump with a cross-fade.
	for i in 3:
		AudioMixerServer.enqueue(&"music", _make_wav(30.0))
	AudioMixerServer.play(&"music")
	expect(AudioMixerServer.get_index(&"music") == 0, "play() starts at the first entry")
	AudioMixerServer.next(&"music")
	expect(AudioMixerServer.get_index(&"music") == 1, "next() moves to entry 1")
	expect(AudioMixerServer.get_voice_count(&"music") == 2, "next() cross-fades: two voices")
	expect(AudioMixerServer.is_fading(&"music"), "next() fades")
	await _wait(0.6)
	expect(AudioMixerServer.get_voice_count(&"music") == 1, "the old voice is stopped after the cross-fade")
	expect(is_equal_approx(AudioMixerServer.get_fade_gain(&"music"), 1.0), "the new voice is at full volume")
	AudioMixerServer.previous(&"music")
	expect(AudioMixerServer.get_index(&"music") == 0, "previous() moves to entry 0")
	AudioMixerServer.change_index(&"music", 2, 0.2)
	expect(AudioMixerServer.get_index(&"music") == 2, "change_index() moves to entry 2")
	await _wait(0.5)
	expect(AudioMixerServer.get_voice_count(&"music") == 1, "one voice after change_index()")

	# 3. Equal-power cross-fade and fade-out.
	_fades_finished = 0
	AudioMixerServer.cross_fade(&"music", _make_wav(30.0), 0.4)
	expect(AudioMixerServer.get_voice_count(&"music") == 2, "cross_fade() keeps the old voice while fading")
	await _wait(0.2)
	# Half way, an equal-power fade is at sin(PI / 4) = 0.71 (a linear one would be at 0.5).
	var gain := AudioMixerServer.get_fade_gain(&"music")
	expect(gain > 0.6 and gain < 0.9, "equal power: the incoming voice is at about 0.71 half way (got %f)" % gain)
	await _wait(0.4)
	expect(AudioMixerServer.get_voice_count(&"music") == 1, "one voice after the cross-fade")
	expect(_fades_finished >= 1, "fade_finished is emitted after the cross-fade")
	_fades_finished = 0
	AudioMixerServer.fade_out(&"music", 0.3)
	expect(not AudioMixerServer.is_playing(&"music"), "fade_out() ends the current voice")
	await _wait(0.6)
	expect(AudioMixerServer.get_voice_count(&"music") == 0, "the voice is stopped after fade_out()")
	expect(_fades_finished == 1, "fade_finished is emitted after fade_out()")

	# 4. Channel volume fade and one-shots.
	AudioMixerServer.set_channel_volume_db(&"sfx", -12.0, 0.3)
	expect(AudioMixerServer.is_fading(&"sfx"), "the channel volume fades")
	await _wait(0.5)
	expect(is_equal_approx(AudioMixerServer.get_channel_volume_db(&"sfx"), -12.0), "the channel volume reaches -12 dB")
	for i in 3:
		AudioMixerServer.play_one_shot(&"sfx", _make_wav(5.0))
	expect(AudioMixerServer.get_voice_count(&"sfx") == 2, "one-shots are limited by polyphony")
	AudioMixerServer.stop(&"sfx")
	expect(AudioMixerServer.get_voice_count(&"sfx") == 0, "stop() stops the one-shots")

	# 5. pause_with_tree.
	AudioMixerServer.play(&"music", _make_wav(30.0))
	AudioMixerServer.play(&"ambience", _make_wav(30.0))
	get_tree().paused = true
	await get_tree().process_frame
	await get_tree().process_frame
	expect(not AudioMixerServer.is_paused(&"music"), "music (pause_with_tree = false) keeps playing while the tree is paused")
	expect(AudioMixerServer.is_paused(&"ambience"), "ambience (pause_with_tree = true) pauses with the tree")
	get_tree().paused = false
	await get_tree().process_frame
	await get_tree().process_frame
	expect(not AudioMixerServer.is_paused(&"ambience"), "ambience resumes with the tree")
	expect(AudioMixerServer.is_playing(&"ambience"), "ambience still has its voice")

	# 6. Music keeps playing across change_scene_to_file().
	var scene_a := Node.new()
	scene_a.name = "SceneA"
	get_tree().root.add_child(scene_a)
	get_tree().current_scene = scene_a # This check node is no longer the current scene, so it survives.
	AudioMixerServer.play(&"music", _make_wav(30.0))
	AudioMixerServer.play(&"ambience", _make_wav(30.0))
	expect(get_tree().change_scene_to_file(SCENE_B) == OK, "change_scene_to_file() to scene B")
	for i in 5:
		await get_tree().process_frame
	expect(get_tree().current_scene != null and get_tree().current_scene.name == "SceneB", "scene B is the current scene")
	expect(AudioMixerServer.is_playing(&"music"), "music keeps playing after the scene change")
	expect(not AudioMixerServer.is_playing(&"ambience"), "ambience (stop_on_scene_change) stops after the scene change")

	AudioMixerServer.channel_started.disconnect(_on_channel_started)
	AudioMixerServer.queue_finished.disconnect(_on_queue_finished)
	AudioMixerServer.fade_finished.disconnect(_on_fade_finished)
	AudioMixerServer.stop_all()
	AudioMixerServer.load_layout(AudioMixerLayout.new())
