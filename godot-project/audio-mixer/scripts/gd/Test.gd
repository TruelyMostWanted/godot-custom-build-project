extends FeatureTest
## Full API test of Audio Mixer (AudioMixerServer, AudioMixerChannel, AudioMixerLayout) without the commands
## (those are tested in audio-commands). Same tests as scripts/cs/Test.cs.
## Runs headless with the dummy audio driver: it checks state, queue, volumes and fades, not sound.
## Speech is simulated through DisplayServer (like the OS callbacks), so ducking needs no speech engine.

const DATA_DIR := "user://audio-mixer"
const NEXT_LEVEL := "res://audio-mixer/scenes/next_level.tscn"
const EQUAL_POWER_HALF_WAY := 0.71 # sin(PI / 4); a linear fade is at 0.5

var _short: String
var _long: String
var _long_b: String
var _started: Array[int] = []
var _finished: Array[int] = []
var _queue_finished: int = 0
var _fades_finished: int = 0
var _ducking_events: Array[String] = []


func _run_tests() -> void:
	DirAccess.make_dir_recursive_absolute(DATA_DIR)
	_short = _save_silence("short.tres", 0.1)
	_long = _save_silence("long.tres", 10.0)
	_long_b = _save_silence("long_b.tres", 10.0)
	var _cached: Array[Resource] = [load(_long), load(_long_b)] # kept in the cache: a load during a fade would delay the frame
	AudioMixerServer.channel_started.connect(_on_channel_started)
	AudioMixerServer.channel_finished.connect(_on_channel_finished)
	AudioMixerServer.queue_finished.connect(_on_queue_finished)
	AudioMixerServer.fade_finished.connect(_on_fade_finished)
	AudioMixerServer.ducking_changed.connect(_on_ducking_changed)
	_test_channel_defaults()
	_test_layout()
	_test_playback()
	_test_queue()
	await _test_queue_modes()
	await _test_fades()
	_test_one_shots()
	await _test_switch_to_clip()
	await _test_pause_with_tree()
	await _test_ducking()
	await _test_scene_change()
	AudioMixerServer.channel_started.disconnect(_on_channel_started)
	AudioMixerServer.channel_finished.disconnect(_on_channel_finished)
	AudioMixerServer.queue_finished.disconnect(_on_queue_finished)
	AudioMixerServer.fade_finished.disconnect(_on_fade_finished)
	AudioMixerServer.ducking_changed.disconnect(_on_ducking_changed)
	AudioMixerServer.stop_all()
	AudioMixerServer.apply_layout(AudioMixerLayout.new())


func _test_channel_defaults() -> void:
	var channel := AudioMixerChannel.new()
	expect_equal(channel.channel_name, &"", "channel_name defaults to empty")
	expect_equal(channel.bus, &"Master", "bus defaults to Master")
	expect_equal(channel.volume_db, 0.0, "volume_db defaults to 0")
	expect_equal(channel.pitch_scale, 1.0, "pitch_scale defaults to 1")
	expect_equal(channel.default_fade_time, 0.0, "default_fade_time defaults to 0")
	expect_equal(channel.queue_mode, AudioMixerChannel.QUEUE_MODE_STOP, "queue_mode defaults to QUEUE_MODE_STOP")
	expect_equal(channel.loop_current_entry, false, "loop_current_entry defaults to false")
	expect_equal(channel.preload_next, false, "preload_next defaults to false")
	expect_equal(channel.max_polyphony, 1, "max_polyphony defaults to 1")
	expect_equal(channel.pause_with_tree, false, "pause_with_tree defaults to false")
	expect_equal(channel.stop_on_scene_change, false, "stop_on_scene_change defaults to false")
	expect_equal(channel.duck_on_tts, false, "duck_on_tts defaults to false")
	expect_equal(channel.duck_volume_db, -12.0, "duck_volume_db defaults to -12")
	expect_equal(channel.duck_fade_time, 0.25, "duck_fade_time defaults to 0.25")
	channel.duck_volume_db = 6.0
	expect_equal(channel.duck_volume_db, 0.0, "duck_volume_db is clamped to 0 (ducking only lowers)")


func _test_layout() -> void:
	var music := AudioMixerChannel.new()
	music.channel_name = &"music"
	music.default_fade_time = 0.3
	var ambience := AudioMixerChannel.new()
	ambience.channel_name = &"ambience"
	ambience.pause_with_tree = true
	ambience.stop_on_scene_change = true
	var playlist := AudioMixerChannel.new()
	playlist.channel_name = &"playlist"
	var sfx := AudioMixerChannel.new()
	sfx.channel_name = &"sfx"
	sfx.max_polyphony = 2
	var layout := AudioMixerLayout.new()
	layout.add_channel(music)
	layout.add_channel(ambience)
	layout.add_channel(playlist)
	layout.add_channel(sfx)
	expect_equal(layout.channels.size(), 4, "add_channel appends to channels")
	expect(layout.find_channel(&"sfx") == sfx, "find_channel finds a channel by name")
	expect(layout.find_channel(&"none") == null, "find_channel returns null for an unknown name")
	AudioMixerServer.apply_layout(layout)
	expect_equal(AudioMixerServer.get_channel_names(), PackedStringArray(["music", "ambience", "playlist", "sfx"]), "apply_layout adds the channels in order")
	expect(AudioMixerServer.get_channel(&"ambience") == ambience, "get_channel returns the definition")
	var extra := AudioMixerChannel.new()
	extra.channel_name = &"extra"
	AudioMixerServer.add_channel(extra)
	expect(AudioMixerServer.has_channel(&"extra"), "add_channel adds a channel")
	var replacement := AudioMixerChannel.new()
	replacement.channel_name = &"extra"
	AudioMixerServer.add_channel(replacement)
	expect(AudioMixerServer.get_channel(&"extra") == replacement, "add_channel replaces a channel of the same name")
	AudioMixerServer.remove_channel(&"extra")
	expect(not AudioMixerServer.has_channel(&"extra"), "remove_channel removes the channel")


func _test_playback() -> void:
	AudioMixerServer.play(&"music", _long, 1.0)
	expect(AudioMixerServer.is_playing(&"music"), "play starts the channel")
	expect(AudioMixerServer.get_playback_position(&"music") >= 1.0, "play starts at from_position")
	expect(AudioMixerServer.get_channel_playback(&"music") != null, "get_channel_playback returns the playback")
	AudioMixerServer.seek(&"music", 5.0)
	expect(AudioMixerServer.get_playback_position(&"music") >= 5.0, "seek moves the position")
	AudioMixerServer.pause(&"music")
	expect(AudioMixerServer.is_paused(&"music"), "pause pauses the channel")
	AudioMixerServer.resume(&"music")
	expect(not AudioMixerServer.is_paused(&"music"), "resume resumes the channel")
	AudioMixerServer.set_channel_pitch_scale(&"music", 1.5)
	expect_equal(AudioMixerServer.get_channel_pitch_scale(&"music"), 1.5, "set_channel_pitch_scale sets the pitch")
	AudioMixerServer.set_channel_pitch_scale(&"music", 1.0)
	AudioMixerServer.stop(&"music")
	expect(not AudioMixerServer.is_playing(&"music"), "stop stops the channel")


func _test_queue() -> void:
	AudioMixerServer.enqueue(&"music", _long)
	AudioMixerServer.enqueue(&"music", _long_b)
	AudioMixerServer.enqueue(&"music", _short, 0)
	expect_equal(AudioMixerServer.get_queue(&"music"), PackedStringArray([_short, _long, _long_b]), "enqueue appends, or inserts at an index")
	AudioMixerServer.dequeue(&"music", 0)
	expect_equal(AudioMixerServer.get_queue(&"music"), PackedStringArray([_long, _long_b]), "dequeue removes the entry at an index")
	AudioMixerServer.play(&"music")
	expect_equal(AudioMixerServer.get_queue_index(&"music"), 0, "play without a stream starts at the first entry")
	AudioMixerServer.play_next(&"music")
	expect_equal(AudioMixerServer.get_queue_index(&"music"), 1, "play_next moves to the next entry")
	expect_equal(AudioMixerServer.get_voice_count(&"music"), 2, "play_next cross-fades with default_fade_time: two voices")
	AudioMixerServer.play_previous(&"music")
	expect_equal(AudioMixerServer.get_queue_index(&"music"), 0, "play_previous moves to the previous entry")
	AudioMixerServer.set_queue_index(&"music", 1, 0.0)
	expect_equal(AudioMixerServer.get_queue_index(&"music"), 1, "set_queue_index moves to the entry")
	AudioMixerServer.clear_queue(&"music")
	expect(AudioMixerServer.get_queue(&"music").is_empty(), "clear_queue empties the queue")
	AudioMixerServer.stop(&"music")


func _test_queue_modes() -> void:
	for i: int in 3:
		AudioMixerServer.enqueue(&"playlist", _short)
	AudioMixerServer.play(&"playlist")
	await _wait_for_queue_finished()
	expect_equal(_started, [0, 1, 2], "QUEUE_MODE_STOP: the entries start in order")
	expect_equal(_finished, [0, 1, 2], "channel_finished is emitted for each entry")
	expect_equal(_queue_finished, 1, "QUEUE_MODE_STOP: queue_finished is emitted after the last entry")
	expect(not AudioMixerServer.is_playing(&"playlist"), "QUEUE_MODE_STOP: the channel stops after the last entry")
	var playlist: AudioMixerChannel = AudioMixerServer.get_channel(&"playlist")
	playlist.queue_mode = AudioMixerChannel.QUEUE_MODE_LOOP_QUEUE
	AudioMixerServer.add_channel(playlist)
	_started.clear()
	AudioMixerServer.set_queue_index(&"playlist", 0, 0.0)
	AudioMixerServer.play(&"playlist")
	await _wait_for_started(4)
	expect_equal(_started.slice(0, 4), [0, 1, 2, 0], "QUEUE_MODE_LOOP_QUEUE: the queue starts over")
	AudioMixerServer.stop(&"playlist")
	AudioMixerServer.dequeue(&"playlist")
	playlist.queue_mode = AudioMixerChannel.QUEUE_MODE_SHUFFLE
	AudioMixerServer.add_channel(playlist)
	_started.clear()
	AudioMixerServer.set_queue_index(&"playlist", 0, 0.0)
	AudioMixerServer.play(&"playlist")
	await _wait_for_started(3)
	expect_equal(_started.slice(0, 3), [0, 1, 0], "QUEUE_MODE_SHUFFLE: another entry follows")
	AudioMixerServer.stop(&"playlist")
	playlist.loop_current_entry = true
	AudioMixerServer.add_channel(playlist)
	_started.clear()
	AudioMixerServer.set_queue_index(&"playlist", 0, 0.0)
	AudioMixerServer.play(&"playlist")
	await _wait_for_started(3)
	expect_equal(_started.slice(0, 3), [0, 0, 0], "loop_current_entry repeats the entry instead of moving on")
	AudioMixerServer.stop(&"playlist")
	AudioMixerServer.clear_queue(&"playlist")


func _test_fades() -> void:
	AudioMixerServer.play(&"music", _long)
	_fades_finished = 0
	AudioMixerServer.cross_fade(&"music", _long_b, 0.4)
	expect_equal(AudioMixerServer.get_voice_count(&"music"), 2, "cross_fade keeps the old voice while fading")
	await _wait(0.2)
	var gain: float = AudioMixerServer.get_fade_gain(&"music")
	expect(absf(gain - EQUAL_POWER_HALF_WAY) < 0.15, "cross_fade uses equal power: about 0.71 half way (got %f)" % gain)
	await _wait(0.4)
	expect_equal(AudioMixerServer.get_voice_count(&"music"), 1, "cross_fade stops the old voice at the end")
	expect_equal(_fades_finished, 1, "fade_finished is emitted after the cross-fade")
	AudioMixerServer.fade_out(&"music", 0.3)
	expect(not AudioMixerServer.is_playing(&"music"), "fade_out ends the current voice")
	await _wait(0.5)
	expect_equal(AudioMixerServer.get_voice_count(&"music"), 0, "fade_out stops the voice at the end")
	AudioMixerServer.enqueue(&"music", _long)
	AudioMixerServer.fade_in(&"music", 0.3, AudioMixerServer.FADE_CURVE_LINEAR)
	expect(AudioMixerServer.is_fading(&"music"), "fade_in fades the channel in")
	expect(AudioMixerServer.get_fade_gain(&"music") < 0.5, "fade_in starts quiet")
	AudioMixerServer.stop(&"music")
	AudioMixerServer.clear_queue(&"music")
	AudioMixerServer.set_channel_volume_db(&"sfx", -12.0, 0.3)
	expect(AudioMixerServer.is_fading(&"sfx"), "set_channel_volume_db with a fade time fades")
	await _wait(0.5)
	expect_equal(AudioMixerServer.get_channel_volume_db(&"sfx"), -12.0, "set_channel_volume_db reaches the volume")


func _test_one_shots() -> void:
	for i: int in 3:
		AudioMixerServer.play_one_shot(&"sfx", _long, -3.0, 1.1)
	expect_equal(AudioMixerServer.get_voice_count(&"sfx"), 2, "play_one_shot is limited by max_polyphony")
	AudioMixerServer.play(&"music", _long)
	AudioMixerServer.stop_all()
	expect_equal(AudioMixerServer.get_voice_count(&"sfx"), 0, "stop_all stops the one-shots")
	expect(not AudioMixerServer.is_playing(&"music"), "stop_all stops every channel")


func _test_switch_to_clip() -> void:
	var interactive := AudioStreamInteractive.new()
	interactive.clip_count = 2
	interactive.set_clip_name(0, &"Calm")
	interactive.set_clip_stream(0, load(_long))
	interactive.set_clip_name(1, &"Battle")
	interactive.set_clip_stream(1, load(_long_b))
	AudioMixerServer.play(&"music", interactive)
	AudioMixerServer.switch_to_clip(&"music", &"Battle")
	await _wait(0.3)
	var playback: AudioStreamPlaybackInteractive = AudioMixerServer.get_channel_playback(&"music")
	expect_equal(playback.get_current_clip_index(), 1, "switch_to_clip switches the clip of an AudioStreamInteractive")
	AudioMixerServer.stop(&"music")


func _test_pause_with_tree() -> void:
	AudioMixerServer.play(&"music", _long)
	AudioMixerServer.play(&"ambience", _long)
	get_tree().paused = true
	await _frames(2)
	expect(not AudioMixerServer.is_paused(&"music"), "a channel keeps playing while the tree is paused")
	expect(AudioMixerServer.is_paused(&"ambience"), "pause_with_tree pauses the channel with the tree")
	get_tree().paused = false
	await _frames(2)
	expect(not AudioMixerServer.is_paused(&"ambience"), "pause_with_tree resumes the channel with the tree")
	AudioMixerServer.stop_all()


func _test_ducking() -> void:
	var music: AudioMixerChannel = AudioMixerServer.get_channel(&"music")
	music.duck_on_tts = true
	music.duck_fade_time = 0.2
	AudioMixerServer.add_channel(music)
	AudioMixerServer.set_channel_volume_db(&"music", -3.0)
	AudioMixerServer.play(&"music", _long)
	AudioMixerServer.play(&"sfx", _long)
	expect(AudioMixerServer.is_ducking_enabled(), "ducking is enabled by default")
	expect_equal(AudioMixerServer.get_active_utterance_count(), 0, "no active utterances at the start")
	_post(DisplayServer.TTS_UTTERANCE_STARTED, 1)
	await _frames(2)
	expect_equal(AudioMixerServer.get_active_utterance_count(), 1, "utterance_started counts one utterance")
	expect(AudioMixerServer.is_ducked(&"music"), "duck_on_tts ducks the channel while speaking")
	expect(not AudioMixerServer.is_ducked(&"sfx"), "a channel without duck_on_tts is not ducked")
	await _wait(0.4)
	expect_equal(AudioMixerServer.get_duck_volume_db(&"music"), -12.0, "the channel fades down by duck_volume_db")
	expect_equal(AudioMixerServer.get_channel_volume_db(&"music"), -3.0, "ducking leaves the channel volume alone")
	expect_equal(_ducking_events, ["music:true"], "ducking_changed(music, true) is emitted once")
	_post(DisplayServer.TTS_UTTERANCE_STARTED, 2)
	_post(DisplayServer.TTS_UTTERANCE_ENDED, 1)
	await _frames(2)
	expect(AudioMixerServer.is_ducked(&"music"), "the channel stays ducked while a second utterance runs")
	_post(DisplayServer.TTS_UTTERANCE_CANCELED, 2)
	await _wait(0.4)
	expect(not AudioMixerServer.is_ducked(&"music"), "a canceled last utterance ends the ducking")
	expect_equal(AudioMixerServer.get_duck_volume_db(&"music"), 0.0, "the channel returns to its volume")
	expect_equal(_ducking_events, ["music:true", "music:false"], "ducking_changed(music, false) is emitted at the end")
	_post(DisplayServer.TTS_UTTERANCE_STARTED, 3)
	await _wait(0.3)
	AudioMixerServer.fade_out(&"music", 0.3)
	_post(DisplayServer.TTS_UTTERANCE_ENDED, 3)
	await _wait(0.6)
	expect_equal(AudioMixerServer.get_voice_count(&"music"), 0, "a fade_out during ducking stays silent after the speech")
	AudioMixerServer.play(&"music", _long)
	AudioMixerServer.set_ducking_enabled(false)
	_post(DisplayServer.TTS_UTTERANCE_STARTED, 4)
	await _frames(2)
	expect(not AudioMixerServer.is_ducked(&"music"), "set_ducking_enabled(false) turns ducking off")
	AudioMixerServer.set_ducking_enabled(true)
	expect(AudioMixerServer.is_ducked(&"music"), "set_ducking_enabled(true) during speech ducks at once")
	_post(DisplayServer.TTS_UTTERANCE_ENDED, 4)
	await _frames(2)
	AudioMixerServer.duck(&"sfx", -6.0, 0.0)
	expect(AudioMixerServer.is_ducked(&"sfx"), "duck ducks any channel")
	expect_equal(AudioMixerServer.get_duck_volume_db(&"sfx"), -6.0, "duck with fade time 0 applies at once")
	AudioMixerServer.unduck(&"sfx", 0.0)
	expect(not AudioMixerServer.is_ducked(&"sfx"), "unduck restores the channel")
	AudioMixerServer.stop_all()


func _test_scene_change() -> void:
	var level := Node.new()
	get_tree().root.add_child(level)
	get_tree().current_scene = level # this test node is no longer the current scene, so it survives the change
	AudioMixerServer.play(&"music", _long)
	AudioMixerServer.play(&"ambience", _long)
	get_tree().change_scene_to_file(NEXT_LEVEL)
	await _frames(5)
	expect_equal(get_tree().current_scene.name, &"NextLevel", "the scene changed")
	expect(AudioMixerServer.is_playing(&"music"), "a channel keeps playing after a scene change")
	expect(not AudioMixerServer.is_playing(&"ambience"), "stop_on_scene_change stops the channel")


## Saves silence of the given length as AudioStreamWAV and returns the path.
func _save_silence(file_name: String, seconds: float) -> String:
	var wav := AudioStreamWAV.new()
	wav.format = AudioStreamWAV.FORMAT_16_BITS
	wav.mix_rate = 44100
	var data := PackedByteArray()
	data.resize(int(seconds * 44100.0) * 2)
	wav.data = data
	var path: String = DATA_DIR.path_join(file_name)
	ResourceSaver.save(wav, path)
	return path


## Simulates the operating system: the event goes through DisplayServer to TTSServer and the mixer.
func _post(event: DisplayServer.TTSUtteranceEvent, id: int) -> void:
	DisplayServer.call("_tts_post_utterance_event", event, id, 0)


func _wait(seconds: float) -> void:
	await get_tree().create_timer(seconds).timeout


func _frames(count: int) -> void:
	for i: int in count:
		await get_tree().process_frame


func _wait_for_queue_finished() -> void:
	for i: int in 100: # at most 5 seconds
		if _queue_finished > 0:
			return
		await _wait(0.05)


func _wait_for_started(count: int) -> void:
	for i: int in 100: # at most 5 seconds
		if _started.size() >= count:
			return
		await _wait(0.05)


func _on_channel_started(channel: StringName, index: int, _stream_path: String) -> void:
	if channel == &"playlist":
		_started.append(index)


func _on_channel_finished(channel: StringName, index: int) -> void:
	if channel == &"playlist":
		_finished.append(index)


func _on_queue_finished(channel: StringName) -> void:
	if channel == &"playlist":
		_queue_finished += 1


func _on_fade_finished(channel: StringName) -> void:
	if channel == &"music":
		_fades_finished += 1


func _on_ducking_changed(channel: StringName, ducked: bool) -> void:
	_ducking_events.append("%s:%s" % [channel, ducked])
