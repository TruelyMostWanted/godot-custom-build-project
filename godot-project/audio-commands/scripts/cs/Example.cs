using Godot;

namespace TruelyInteractive.AudioCommandsGuide;

/// <summary>Minimal example of Audio Commands: the snippets of the wiki guide, in order. Same as scripts/gd/Example.gd.</summary>
public partial class Example : Node
{
    private const string Res = "res://audio-commands/resources/";

    public override void _Ready()
    {
        // Setup: the mixer channels the snippets use. The CSV files are loaded from user://: a CSV kept in res:// prints an error (engine bug).
        AudioMixerServer.ApplyLayout(GD.Load<AudioMixerLayout>(Res + "mixer_layout.tres"));
        foreach (string fileName in new[] { "soundboard.csv", "forest.csv", "mod.csv" })
        {
            DirAccess.CopyAbsolute(Res + fileName, "user://" + fileName);
        }

        // Run a command.
        var birds = new AudioCommand
        {
            Channel = "ambience",
            Command = AudioCommand.CommandEnum.FadeIn,
            StreamPath = Res + "tone.tres",
            FadeTime = 3.0f,
            LoopMode = AudioCommand.LoopModeEnum.On,
        };
        Error err = AudioMixerServer.Execute(birds);
        if (err != Error.Ok)
        {
            GD.Print("Audio command failed: ", err);
        }
        GD.Print("Ambience playing: ", AudioMixerServer.IsPlaying("ambience"));

        // Build a soundboard from a CSV file.
        var board = ResourceLoader.Load<AudioCommands>("user://soundboard.csv", "AudioCommands");
        AudioMixerServer.RegisterCommands(board);
        foreach (AudioCommand command in board.Commands)
        {
            StringName padName = command.CommandName;
            var button = new Button { Text = padName };
            button.Pressed += () => AudioMixerServer.ExecuteNamed(padName);
            GetNode("Pads").AddChild(button);
        }
        GD.Print("Pads: ", GetNode("Pads").GetChildCount());

        // Start the ambience of a level.
        var forest = ResourceLoader.Load<AudioCommands>("user://forest.csv", "AudioCommands");
        AudioMixerServer.RegisterCommands(forest);
        AudioMixerServer.ExecuteGroup("forest");
        GD.Print("Music playing: ", AudioMixerServer.IsPlaying("music"));
        // When the level ends:
        AudioMixerServer.UnregisterCommands(forest);

        // Let a mod replace a sound.
        AudioCommands baseGame = board;
        var mod = ResourceLoader.Load<AudioCommands>("user://mod.csv", "AudioCommands");
        AudioMixerServer.RegisterCommands(baseGame);
        AudioMixerServer.RegisterCommands(mod);
        AudioMixerServer.ExecuteNamed("horn");
        GD.Print("Pad 1 playing: ", AudioMixerServer.IsPlaying("pad_1"));

        // Avoid a hitch on the first press.
        board.PreloadStreams();
        GD.Print(board.GetPreloadedStreamCount(), " streams in memory");
        board.ClearPreloadedStreams();

        // Register commands at startup. The project setting Default Commands is left out: it would apply to every scene here.
        AudioMixerServer.Singleton.CommandExecuted += (commandName, command) => GD.Print($"Ran {commandName} on {command.Channel}");
        AudioMixerServer.ExecuteNamed("horn");

        GetTree().Quit();
    }
}
