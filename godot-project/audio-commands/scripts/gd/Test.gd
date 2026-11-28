extends FeatureTest
## Full API test of Audio Commands (AudioCommand, AudioCommands and the command part of AudioMixerServer).
## Same tests as scripts/cs/Test.cs. Runs headless with the dummy audio driver: it checks state, not sound.
## The error tests print the expected errors.

const DATA_DIR := "user://audio-commands"
const PROPERTIES: Array[String] = ["command_name", "group", "channel", "command", "stream_path", "volume_db", "pitch_scale", "fade_time", "fade_curve", "queue_index", "position", "loop_mode", "clip_name"]

var _stream_a: String
var _stream_b: String
var _executed: Array[StringName] = []


func _run_tests() -> void:
	DirAccess.make_dir_recursive_absolute(DATA_DIR)
	_stream_a = _save_silence("a.tres")
	_stream_b = _save_silence("b.tres")
	var layout := AudioMixerLayout.new()
	for channel_name: StringName in [&"music", &"pad_1", &"pad_2", &"birds", &"wind"]:
		var channel := AudioMixerChannel.new()
		channel.channel_name = channel_name
		layout.add_channel(channel)
	AudioMixerServer.apply_layout(layout)
	AudioMixerServer.command_executed.connect(_on_command_executed)
	_test_defaults()
	_test_command_list()
	_test_text_formats()
	_test_preload_streams()
	_test_execute()
	_test_command_types()
	_test_errors()
	_test_named_commands()
	_test_groups()
	_test_legacy_type()
	AudioMixerServer.command_executed.disconnect(_on_command_executed)
	AudioMixerServer.apply_layout(AudioMixerLayout.new())


func _test_defaults() -> void:
	var command := AudioCommand.new()
	expect_equal(command.command, AudioCommand.COMMAND_PLAY, "command defaults to COMMAND_PLAY")
	expect_equal(command.fade_time, -1.0, "fade_time defaults to -1 (the channel's fade time)")
	expect_equal(command.loop_mode, AudioCommand.LOOP_MODE_CHANNEL, "loop_mode defaults to LOOP_MODE_CHANNEL")
	expect_equal(command.queue_index, -1, "queue_index defaults to -1")
	expect_equal(command.pitch_scale, 1.0, "pitch_scale defaults to 1")


func _test_command_list() -> void:
	var list := AudioCommands.new()
	var horn: AudioCommand = _command(AudioCommand.COMMAND_TOGGLE, &"pad_1")
	horn.command_name = &"horn"
	list.add_command(horn)
	list.add_command(_command(AudioCommand.COMMAND_STOP, &"pad_2"))
	expect_equal(list.commands.size(), 2, "add_command appends to commands")
	expect(list.find_command(&"horn") == horn, "find_command finds a command by name")
	expect(list.find_command(&"none") == null, "find_command returns null for an unknown name")


func _test_text_formats() -> void:
	var full: AudioCommand = _command(AudioCommand.COMMAND_CROSS_FADE, &"music", _stream_a)
	full.command_name = &"battle"
	full.group = &"fight"
	full.volume_db = -6.5
	full.pitch_scale = 1.25
	full.fade_time = 2.5
	full.fade_curve = AudioMixerServer.FADE_CURVE_EQUAL_POWER
	full.queue_index = 3
	full.position = 1.5
	full.loop_mode = AudioCommand.LOOP_MODE_ON
	full.clip_name = &"Battle"
	var list := AudioCommands.new()
	list.add_command(full)
	list.add_command(_command(AudioCommand.COMMAND_STOP_ALL, &""))
	var csv_path: String = DATA_DIR.path_join("commands.csv")
	expect_equal(ResourceSaver.save(list, csv_path), OK, "ResourceSaver saves an AudioCommands as CSV")
	var csv_text: String = FileAccess.get_file_as_string(csv_path)
	expect_equal(csv_text.strip_edges().split("\n").size(), 3, "the CSV has a header and one row per command")
	expect(csv_text.contains("Cross Fade"), "the CSV writes the command by name")
	expect(csv_text.contains("Equal Power"), "the CSV writes the fade curve by name")
	var csv_list: AudioCommands = ResourceLoader.load(csv_path, "AudioCommands", ResourceLoader.CACHE_MODE_IGNORE)
	expect(_same_command(csv_list.commands[0], full), "every property survives the CSV round trip")
	var json: String = ResourceTextSerializer.serialize(list, ResourceTextSerializer.FORMAT_JSON)
	var json_list: AudioCommands = ResourceTextSerializer.deserialize(json, ResourceTextSerializer.FORMAT_JSON, "AudioCommands")
	expect(_same_command(json_list.commands[0], full), "every property survives the JSON round trip")
	var ini: String = ResourceTextSerializer.serialize(list, ResourceTextSerializer.FORMAT_INI)
	var ini_list: AudioCommands = ResourceTextSerializer.deserialize(ini, ResourceTextSerializer.FORMAT_INI, "AudioCommands")
	expect(_same_command(ini_list.commands[0], full), "every property survives the INI round trip")


func _test_preload_streams() -> void:
	var list := AudioCommands.new()
	list.add_command(_command(AudioCommand.COMMAND_PLAY, &"pad_1", _stream_a))
	list.add_command(_command(AudioCommand.COMMAND_PLAY, &"pad_2", _stream_b))
	expect_equal(list.preload_streams(), OK, "preload_streams returns OK")
	expect_equal(list.get_preloaded_stream_count(), 2, "preload_streams keeps both streams")
	list.clear_preloaded_streams()
	expect_equal(list.get_preloaded_stream_count(), 0, "clear_preloaded_streams releases the streams")


func _test_execute() -> void:
	var play: AudioCommand = _command(AudioCommand.COMMAND_PLAY, &"music", _stream_a)
	play.command_name = &"theme"
	play.loop_mode = AudioCommand.LOOP_MODE_ON
	expect_equal(AudioMixerServer.execute(play), OK, "execute returns OK")
	expect(AudioMixerServer.is_playing(&"music"), "PLAY starts the channel")
	var channel: AudioMixerChannel = AudioMixerServer.get_channel(&"music")
	expect(channel.loop_current_entry, "LOOP_MODE_ON sets loop_current_entry of the channel")
	expect_equal(_executed, [&"theme"], "command_executed reports the command name")


func _test_command_types() -> void:
	AudioMixerServer.execute(_command(AudioCommand.COMMAND_PAUSE, &"music"))
	expect(AudioMixerServer.is_paused(&"music"), "PAUSE pauses the channel")
	AudioMixerServer.execute(_command(AudioCommand.COMMAND_RESUME, &"music"))
	expect(not AudioMixerServer.is_paused(&"music"), "RESUME resumes the channel")
	var volume: AudioCommand = _command(AudioCommand.COMMAND_SET_VOLUME, &"music")
	volume.volume_db = -6.0
	AudioMixerServer.execute(volume)
	expect_equal(AudioMixerServer.get_channel_volume_db(&"music"), -6.0, "SET_VOLUME sets the channel volume")
	var pitch: AudioCommand = _command(AudioCommand.COMMAND_SET_PITCH, &"music")
	pitch.pitch_scale = 1.5
	AudioMixerServer.execute(pitch)
	expect_equal(AudioMixerServer.get_channel_pitch_scale(&"music"), 1.5, "SET_PITCH sets the channel pitch")
	var seek: AudioCommand = _command(AudioCommand.COMMAND_SEEK, &"music")
	seek.position = 2.0
	expect_equal(AudioMixerServer.execute(seek), OK, "SEEK on a playing channel returns OK")
	var fade_out: AudioCommand = _command(AudioCommand.COMMAND_FADE_OUT, &"music")
	fade_out.fade_time = 1.0
	AudioMixerServer.execute(fade_out)
	expect(AudioMixerServer.is_fading(&"music"), "FADE_OUT starts a fade")
	AudioMixerServer.execute(_command(AudioCommand.COMMAND_STOP, &"music"))
	expect(not AudioMixerServer.is_playing(&"music"), "STOP stops the channel")
	var fade_in: AudioCommand = _command(AudioCommand.COMMAND_FADE_IN, &"music", _stream_a)
	fade_in.fade_time = 1.0
	AudioMixerServer.execute(fade_in)
	expect(AudioMixerServer.is_fading(&"music"), "FADE_IN with a stream fades the stream in")
	var cross_fade: AudioCommand = _command(AudioCommand.COMMAND_CROSS_FADE, &"music", _stream_b)
	cross_fade.fade_time = 1.0
	AudioMixerServer.execute(cross_fade)
	expect(AudioMixerServer.is_fading(&"music"), "CROSS_FADE fades to the new stream")
	AudioMixerServer.execute(_command(AudioCommand.COMMAND_STOP, &"music"))
	AudioMixerServer.execute(_command(AudioCommand.COMMAND_PLAY_ONE_SHOT, &"pad_1", _stream_a))
	expect_equal(AudioMixerServer.get_voice_count(&"pad_1"), 1, "PLAY_ONE_SHOT starts a voice")
	AudioMixerServer.execute(_command(AudioCommand.COMMAND_ENQUEUE, &"wind", _stream_a))
	AudioMixerServer.execute(_command(AudioCommand.COMMAND_ENQUEUE, &"wind", _stream_b))
	expect_equal(AudioMixerServer.get_queue(&"wind"), PackedStringArray([_stream_a, _stream_b]), "ENQUEUE appends to the queue")
	var jump: AudioCommand = _command(AudioCommand.COMMAND_SET_QUEUE_INDEX, &"wind")
	jump.queue_index = 1
	AudioMixerServer.execute(jump)
	expect_equal(AudioMixerServer.get_queue_index(&"wind"), 1, "SET_QUEUE_INDEX jumps to the entry")
	AudioMixerServer.execute(_command(AudioCommand.COMMAND_PLAY_PREVIOUS, &"wind"))
	expect_equal(AudioMixerServer.get_queue_index(&"wind"), 0, "PLAY_PREVIOUS goes one entry back")
	AudioMixerServer.execute(_command(AudioCommand.COMMAND_PLAY_NEXT, &"wind"))
	expect_equal(AudioMixerServer.get_queue_index(&"wind"), 1, "PLAY_NEXT goes one entry on")
	AudioMixerServer.execute(_command(AudioCommand.COMMAND_DEQUEUE, &"wind"))
	expect_equal(AudioMixerServer.get_queue(&"wind").size(), 1, "DEQUEUE removes the last entry")
	AudioMixerServer.execute(_command(AudioCommand.COMMAND_CLEAR, &"wind"))
	expect(AudioMixerServer.get_queue(&"wind").is_empty(), "CLEAR empties the queue")


func _test_errors() -> void:
	AudioMixerServer.execute(_command(AudioCommand.COMMAND_PLAY, &"birds", _stream_a))
	var ghost: AudioCommand = _command(AudioCommand.COMMAND_PLAY, &"ghost", _stream_a)
	expect_equal(AudioMixerServer.execute(ghost), ERR_DOES_NOT_EXIST, "an unknown channel returns ERR_DOES_NOT_EXIST")
	var missing: AudioCommand = _command(AudioCommand.COMMAND_PLAY, &"music", DATA_DIR.path_join("missing.tres"))
	expect_equal(AudioMixerServer.execute(missing), ERR_FILE_NOT_FOUND, "a missing file returns ERR_FILE_NOT_FOUND")
	expect(not AudioMixerServer.is_playing(&"music"), "a failed command starts nothing")
	expect(AudioMixerServer.is_playing(&"birds"), "a failed command keeps other channels playing")
	var seek: AudioCommand = _command(AudioCommand.COMMAND_SEEK, &"music")
	expect_equal(AudioMixerServer.execute(seek), ERR_UNAVAILABLE, "SEEK on a silent channel returns ERR_UNAVAILABLE")
	var dequeue: AudioCommand = _command(AudioCommand.COMMAND_DEQUEUE, &"music")
	expect_equal(AudioMixerServer.execute(dequeue), ERR_PARAMETER_RANGE_ERROR, "DEQUEUE on an empty queue returns ERR_PARAMETER_RANGE_ERROR")
	var pitch: AudioCommand = _command(AudioCommand.COMMAND_SET_PITCH, &"music")
	pitch.pitch_scale = 0.0
	expect_equal(AudioMixerServer.execute(pitch), ERR_INVALID_PARAMETER, "SET_PITCH of 0 returns ERR_INVALID_PARAMETER")
	var clip: AudioCommand = _command(AudioCommand.COMMAND_SWITCH_TO_CLIP, &"music")
	expect_equal(AudioMixerServer.execute(clip), ERR_INVALID_PARAMETER, "SWITCH_TO_CLIP without clip_name returns ERR_INVALID_PARAMETER")


func _test_named_commands() -> void:
	var board := AudioCommands.new()
	for pad: StringName in [&"pad_1", &"pad_2"]:
		var toggle: AudioCommand = _command(AudioCommand.COMMAND_TOGGLE, pad, _stream_a)
		toggle.command_name = pad
		board.add_command(toggle)
	expect_equal(AudioMixerServer.register_commands(board), OK, "register_commands returns OK")
	expect_equal(AudioMixerServer.get_command_names(), PackedStringArray(["pad_1", "pad_2"]), "get_command_names lists the registered names")
	expect(AudioMixerServer.has_command(&"pad_2"), "has_command finds a registered name")
	AudioMixerServer.execute(_command(AudioCommand.COMMAND_STOP, &"pad_1"))
	AudioMixerServer.execute_named(&"pad_1")
	expect(AudioMixerServer.is_playing(&"pad_1"), "the first TOGGLE starts the channel")
	expect(not AudioMixerServer.is_playing(&"pad_2"), "execute_named runs only the named command")
	AudioMixerServer.execute_named(&"pad_1")
	expect(not AudioMixerServer.is_playing(&"pad_1"), "the second TOGGLE stops the channel")
	expect_equal(AudioMixerServer.execute_named(&"none"), ERR_DOES_NOT_EXIST, "execute_named of an unknown name returns ERR_DOES_NOT_EXIST")
	var mod := AudioCommands.new()
	var stop: AudioCommand = _command(AudioCommand.COMMAND_STOP, &"pad_2")
	stop.command_name = &"pad_2"
	mod.add_command(stop)
	AudioMixerServer.register_commands(mod)
	AudioMixerServer.execute(_command(AudioCommand.COMMAND_PLAY, &"pad_2", _stream_a))
	AudioMixerServer.execute_named(&"pad_2")
	expect(not AudioMixerServer.is_playing(&"pad_2"), "the last registered command of a name wins")
	AudioMixerServer.unregister_commands(mod)
	expect(not AudioMixerServer.has_command(&"pad_2"), "unregister_commands removes the overriding name")
	AudioMixerServer.unregister_commands(board)
	expect(AudioMixerServer.get_command_names().is_empty(), "unregister_commands removes all names of the list")


func _test_groups() -> void:
	AudioMixerServer.stop_all()
	var ambience := AudioCommands.new()
	for channel_name: StringName in [&"birds", &"wind"]:
		var forest: AudioCommand = _command(AudioCommand.COMMAND_PLAY, channel_name, _stream_a)
		forest.group = &"forest"
		ambience.add_command(forest)
	var city: AudioCommand = _command(AudioCommand.COMMAND_PLAY, &"music", _stream_a)
	city.group = &"city"
	ambience.add_command(city)
	AudioMixerServer.register_commands(ambience)
	expect_equal(AudioMixerServer.execute_group(&"forest"), OK, "execute_group returns OK")
	expect(AudioMixerServer.is_playing(&"birds"), "execute_group runs the first command of the group")
	expect(AudioMixerServer.is_playing(&"wind"), "execute_group runs the second command of the group")
	expect(not AudioMixerServer.is_playing(&"music"), "execute_group skips other groups")
	AudioMixerServer.unregister_commands(ambience)
	var stop_list := AudioCommands.new()
	stop_list.add_command(_command(AudioCommand.COMMAND_STOP_ALL, &""))
	expect_equal(AudioMixerServer.execute_commands(stop_list), OK, "execute_commands runs STOP_ALL")
	expect(not AudioMixerServer.is_playing(&"birds"), "STOP_ALL stops every channel")


func _command(type: AudioCommand.Command, channel: StringName, stream_path: String = "") -> AudioCommand:
	var command := AudioCommand.new()
	command.command = type
	command.channel = channel
	command.stream_path = stream_path
	command.fade_time = 0.0
	return command


## Saves 30 seconds of silence as AudioStreamWAV and returns the path.
func _save_silence(file_name: String) -> String:
	var wav := AudioStreamWAV.new()
	wav.format = AudioStreamWAV.FORMAT_16_BITS
	wav.mix_rate = 44100
	var data := PackedByteArray()
	data.resize(30 * 44100 * 2)
	wav.data = data
	var path: String = DATA_DIR.path_join(file_name)
	ResourceSaver.save(wav, path)
	return path


func _same_command(a: AudioCommand, b: AudioCommand) -> bool:
	for property: String in PROPERTIES:
		if a.get(property) != b.get(property):
			return false
	return true


func _on_command_executed(command_name: StringName, _command_ref: AudioCommand) -> void:
	_executed.append(command_name)


## The old name AudioCommandList is deprecated: a .tres with that type loads as AudioCommands.
func _test_legacy_type() -> void:
	var legacy: AudioCommands = load("res://audio-commands/resources/legacy_audio_command_list.tres")
	expect(legacy != null and legacy.get_class() == "AudioCommands", "a .tres with the old type AudioCommandList loads as AudioCommands")
	expect_equal(legacy.commands.size(), 4, "the legacy .tres keeps its commands")
