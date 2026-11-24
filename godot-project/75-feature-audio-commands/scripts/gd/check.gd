extends FeatureCheck
## GDScript checks of 75-feature-audio-commands. Check the same things as scripts/cs/Issue75FeatureAudioCommandsCheck.cs.
## The checks run headless with the dummy audio driver: they check state, not sound.
## Two expected errors are printed in check 4 (unknown channel, missing file).

const DATA_DIR := "user://75-feature-audio-commands-gd"
const PADS: Array[StringName] = [&"pad_1", &"pad_2", &"pad_3", &"pad_4", &"pad_5"]
const FOREST: Array[StringName] = [&"birds", &"wind", &"river"]

var _executed: Array[StringName] = []


func _get_check_name() -> String:
	return "75-feature-audio-commands"


## Saves a silent 16-bit mono WAV of the given length as .tres and returns its path.
func _save_wav(file_name: String, seconds: float) -> String:
	var wav := AudioStreamWAV.new()
	wav.format = AudioStreamWAV.FORMAT_16_BITS
	wav.mix_rate = 44100
	wav.stereo = false
	var data := PackedByteArray()
	data.resize(int(seconds * 44100.0) * 2)
	wav.data = data
	var path := DATA_DIR.path_join(file_name)
	ResourceSaver.save(wav, path)
	return path


func _make_channel(channel_name: StringName) -> AudioMixerChannel:
	var channel := AudioMixerChannel.new()
	channel.channel_name = channel_name
	return channel


func _make_command(command_name: StringName, type: AudioCommand.Command, channel: StringName, stream_path := "") -> AudioCommand:
	var command := AudioCommand.new()
	command.command_name = command_name
	command.command = type
	command.channel = channel
	command.stream_path = stream_path
	return command


func _on_command_executed(command_name: StringName, _command: AudioCommand) -> void:
	_executed.append(command_name)


func _same_command(a: AudioCommand, b: AudioCommand) -> bool:
	for property in ["command_name", "group", "channel", "command", "stream_path", "volume_db", "pitch_scale", "fade_time", "fade_curve", "queue_index", "position", "loop_mode", "clip_name"]:
		if a.get(property) != b.get(property):
			return false
	return true


func _run_checks() -> void:
	DirAccess.make_dir_recursive_absolute(DATA_DIR)
	expect(ClassDB.class_exists("AudioCommand") and ClassDB.class_exists("AudioCommandList"), "AudioCommand and AudioCommandList are registered")

	var layout := AudioMixerLayout.new()
	var channel_names: Array[StringName] = [&"music", &"traffic"]
	channel_names.append_array(PADS)
	channel_names.append_array(FOREST)
	for channel_name in channel_names:
		layout.add_channel(_make_channel(channel_name))
	AudioMixerServer.apply_layout(layout)
	AudioMixerServer.command_executed.connect(_on_command_executed)
	var long_wav := _save_wav("long.tres", 30.0)

	# 1. A command list saved as CSV loads back without loss; enums appear as names.
	var list := AudioCommandList.new()
	var toggle := _make_command(&"horn", AudioCommand.COMMAND_TOGGLE, &"pad_1", long_wav)
	toggle.group = &"board"
	list.add_command(toggle)
	var fade := _make_command(&"forest_in", AudioCommand.COMMAND_FADE_IN, &"birds", long_wav)
	fade.group = &"forest"
	fade.fade_time = 2.5
	fade.fade_curve = AudioMixerServer.FADE_CURVE_EQUAL_POWER
	fade.loop_mode = AudioCommand.LOOP_MODE_ON
	list.add_command(fade)
	var jump := _make_command(&"track_3", AudioCommand.COMMAND_SET_QUEUE_INDEX, &"music")
	jump.queue_index = 3
	list.add_command(jump)
	var clip := _make_command(&"battle", AudioCommand.COMMAND_SWITCH_TO_CLIP, &"music")
	clip.clip_name = &"Battle"
	clip.volume_db = -6.5
	list.add_command(clip)
	var csv_path := DATA_DIR.path_join("commands.csv")
	expect(ResourceSaver.save(list, csv_path) == OK, "ResourceSaver saves an AudioCommandList as CSV")
	var csv_text := FileAccess.get_file_as_string(csv_path)
	expect_equal(csv_text.strip_edges().split("\n").size(), 5, "the CSV has a header and one row per command")
	expect(csv_text.contains("Toggle") and csv_text.contains("Fade In") and csv_text.contains("Equal Power"), "enums are written as names")
	var loaded := ResourceLoader.load(csv_path, "AudioCommandList", ResourceLoader.CACHE_MODE_IGNORE) as AudioCommandList
	expect(loaded != null and loaded.commands.size() == 4, "the CSV loads back with the type hint")
	if loaded != null and loaded.commands.size() == 4:
		var same := true
		for i in 4:
			same = same and _same_command(loaded.commands[i], list.commands[i])
		expect(same, "every property of every command survives the CSV round trip")
		expect(loaded.commands[1].stream_path == long_wav and loaded.commands[0].channel is StringName, "paths stay paths, names stay StringNames")
	for format: ResourceTextSerializer.Format in [ResourceTextSerializer.FORMAT_JSON, ResourceTextSerializer.FORMAT_INI]:
		var text := ResourceTextSerializer.serialize(list, format)
		var back := ResourceTextSerializer.deserialize(text, format, "AudioCommandList") as AudioCommandList
		expect(back != null and back.commands.size() == 4 and _same_command(back.commands[1], fade), "%s round trip keeps the commands" % format)

	# 2. A soundboard of five TOGGLE commands works with execute_named().
	var board := AudioCommandList.new()
	for i in PADS.size():
		board.add_command(_make_command(PADS[i], AudioCommand.COMMAND_TOGGLE, PADS[i], _save_wav("pad_%d.tres" % (i + 1), 30.0)))
	expect(board.preload_streams() == OK and board.get_preloaded_stream_count() == 5, "preload_streams() loads the five pad streams")
	expect(AudioMixerServer.register_commands(board) == OK, "register_commands() registers the soundboard")
	expect_equal(AudioMixerServer.get_command_names(), PackedStringArray(["pad_1", "pad_2", "pad_3", "pad_4", "pad_5"]), "the five pads are registered by name")
	var button := Button.new()
	add_child(button)
	button.pressed.connect(AudioMixerServer.execute_named.bind(&"pad_3"))
	button.pressed.emit()
	expect(AudioMixerServer.is_playing(&"pad_3"), "a button press toggles pad 3 on")
	expect(not AudioMixerServer.is_playing(&"pad_2"), "other pads stay silent")
	expect(_executed.has(&"pad_3"), "command_executed reports the pad")
	button.pressed.emit()
	expect(not AudioMixerServer.is_playing(&"pad_3"), "the second press toggles pad 3 off")
	for pad in PADS:
		AudioMixerServer.execute_named(pad)
	var all_on := true
	for pad in PADS:
		all_on = all_on and AudioMixerServer.is_playing(pad)
	expect(all_on, "all five pads play after one press each")
	button.queue_free()

	# 3. execute_group(&"forest") starts all ambience commands of that group.
	var ambience := AudioCommandList.new()
	for channel_name in FOREST:
		var command := _make_command(StringName(), AudioCommand.COMMAND_PLAY, channel_name, long_wav)
		command.group = &"forest"
		command.loop_mode = AudioCommand.LOOP_MODE_ON
		ambience.add_command(command)
	var city := _make_command(&"city", AudioCommand.COMMAND_PLAY, &"traffic", long_wav)
	city.group = &"city"
	ambience.add_command(city)
	AudioMixerServer.register_commands(ambience)
	expect(AudioMixerServer.execute_group(&"forest") == OK, "execute_group(&\"forest\") returns OK")
	var forest_on := true
	for channel_name in FOREST:
		forest_on = forest_on and AudioMixerServer.is_playing(channel_name)
	expect(forest_on, "all forest channels play")
	expect(not AudioMixerServer.is_playing(&"traffic"), "the city command did not run")
	expect(AudioMixerServer.get_channel(&"birds").loop_current_entry, "loop_mode ON loops the channel entry")

	# 4. An invalid command returns an error without stopping other channels.
	var ghost := _make_command(&"ghost", AudioCommand.COMMAND_PLAY, &"ghost", long_wav)
	expect(AudioMixerServer.execute(ghost) == ERR_DOES_NOT_EXIST, "an unknown channel returns ERR_DOES_NOT_EXIST")
	var missing := _make_command(&"missing", AudioCommand.COMMAND_PLAY, &"traffic", DATA_DIR.path_join("missing.tres"))
	expect(AudioMixerServer.execute(missing) == ERR_FILE_NOT_FOUND, "a missing file returns ERR_FILE_NOT_FOUND")
	expect(AudioMixerServer.is_playing(&"birds") and AudioMixerServer.is_playing(&"pad_1"), "other channels keep playing")
	expect(not AudioMixerServer.is_playing(&"traffic"), "the failed command started nothing")

	# 5. Collisions and unregistering.
	var mod := AudioCommandList.new()
	mod.add_command(_make_command(&"pad_1", AudioCommand.COMMAND_STOP, &"pad_1"))
	AudioMixerServer.register_commands(mod)
	expect(AudioMixerServer.execute_named(&"pad_1") == OK and not AudioMixerServer.is_playing(&"pad_1"), "the last registered pad_1 (STOP) wins")
	AudioMixerServer.unregister_commands(mod)
	AudioMixerServer.unregister_commands(board)
	AudioMixerServer.unregister_commands(ambience)
	expect(AudioMixerServer.get_command_names().is_empty(), "unregister_commands() removes the commands")

	# 6. STOP_ALL through execute_list().
	var stop_list := AudioCommandList.new()
	var stop_all := _make_command(StringName(), AudioCommand.COMMAND_STOP_ALL, StringName())
	stop_all.fade_time = 0.0
	stop_list.add_command(stop_all)
	expect(AudioMixerServer.execute_list(stop_list) == OK, "execute_list() runs STOP_ALL")
	expect(not AudioMixerServer.is_playing(&"birds") and not AudioMixerServer.is_playing(&"pad_2"), "STOP_ALL stops every channel")

	AudioMixerServer.command_executed.disconnect(_on_command_executed)
	board.clear_preloaded_streams()
	AudioMixerServer.apply_layout(AudioMixerLayout.new())
