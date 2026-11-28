extends Node
## Minimal example of Audio Commands: the snippets of the wiki guide, in order.
## Wiki: https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Audio-Commands

const RES := "res://audio-commands/resources/"


func _ready() -> void:
	# Setup: the mixer channels the snippets use. The CSV files are loaded from user://: a CSV kept in res:// prints an error (engine bug).
	AudioMixerServer.apply_layout(load(RES + "mixer_layout.tres"))
	for file_name: String in ["soundboard.csv", "forest.csv", "mod.csv"]:
		DirAccess.copy_absolute(RES + file_name, "user://" + file_name)

	# Run a command.
	var birds := AudioCommand.new()
	birds.channel = &"ambience"
	birds.command = AudioCommand.COMMAND_FADE_IN
	birds.stream_path = RES + "tone.tres"
	birds.fade_time = 3.0
	birds.loop_mode = AudioCommand.LOOP_MODE_ON
	var err: Error = AudioMixerServer.execute(birds)
	if err != OK:
		print("Audio command failed: ", error_string(err))
	print("Ambience playing: ", AudioMixerServer.is_playing(&"ambience"))

	# Build a soundboard from a CSV file.
	var board: AudioCommands = ResourceLoader.load("user://soundboard.csv", "AudioCommands")
	AudioMixerServer.register_commands(board)
	for command: AudioCommand in board.commands:
		var button := Button.new()
		button.text = command.command_name
		button.pressed.connect(AudioMixerServer.execute_named.bind(command.command_name))
		$Pads.add_child(button)
	print("Pads: ", $Pads.get_child_count())

	# Start the ambience of a level.
	var forest: AudioCommands = ResourceLoader.load("user://forest.csv", "AudioCommands")
	AudioMixerServer.register_commands(forest)
	AudioMixerServer.execute_group(&"forest")
	print("Music playing: ", AudioMixerServer.is_playing(&"music"))
	# When the level ends:
	AudioMixerServer.unregister_commands(forest)

	# Let a mod replace a sound.
	var base_game: AudioCommands = board
	var mod: AudioCommands = ResourceLoader.load("user://mod.csv", "AudioCommands")
	AudioMixerServer.register_commands(base_game) # "horn" plays tone.tres
	AudioMixerServer.register_commands(mod) # "horn" plays mod_horn.tres
	AudioMixerServer.execute_named(&"horn") # the mod's horn
	print("Pad 1 playing: ", AudioMixerServer.is_playing(&"pad_1"))

	# Avoid a hitch on the first press.
	board.preload_streams()
	print(board.get_preloaded_stream_count(), " streams in memory")
	# When the soundboard closes:
	board.clear_preloaded_streams()

	# Register commands at startup. The project setting Default Commands is left out: it would apply to every scene here.
	AudioMixerServer.command_executed.connect(func(command_name: StringName, command: AudioCommand) -> void:
		print("Ran ", command_name, " on ", command.channel))
	AudioMixerServer.execute_named(&"horn")

	get_tree().quit()
