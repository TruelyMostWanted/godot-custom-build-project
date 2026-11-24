using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.Issue75FeatureAudioCommands;

/// <summary>C# checks of 75-feature-audio-commands. Check the same things as scripts/gd/check.gd.</summary>
/// <remarks>
/// The checks run headless with the dummy audio driver: they check state, not sound.
/// Two expected errors are printed in check 4 (unknown channel, missing file).
/// </remarks>
public partial class Issue75FeatureAudioCommandsCheck : FeatureCheckCs
{
    private const string DataDir = "user://75-feature-audio-commands-cs";
    private static readonly StringName[] Pads = ["pad_1", "pad_2", "pad_3", "pad_4", "pad_5"];
    private static readonly StringName[] Forest = ["birds", "wind", "river"];

    private readonly List<StringName> _executed = [];

    protected override string CheckName => "75-feature-audio-commands";

    /// <summary>Saves a silent 16-bit mono WAV of the given length as .tres and returns its path.</summary>
    private static string SaveWav(string fileName, double seconds)
    {
        var wav = new AudioStreamWav
        {
            Format = AudioStreamWav.FormatEnum.Format16Bits,
            MixRate = 44100,
            Stereo = false,
            Data = new byte[(int)(seconds * 44100.0) * 2],
        };
        string path = DataDir.PathJoin(fileName);
        ResourceSaver.Save(wav, path);
        return path;
    }

    private static AudioMixerChannel MakeChannel(StringName channelName)
        => new() { ChannelName = channelName };

    private static AudioCommand MakeCommand(StringName commandName, AudioCommand.CommandEnum type, StringName channel, string streamPath = "")
        => new() { CommandName = commandName, Command = type, Channel = channel, StreamPath = streamPath };

    private static bool SameCommand(AudioCommand a, AudioCommand b)
        => a.CommandName == b.CommandName && a.Group == b.Group && a.Channel == b.Channel && a.Command == b.Command
            && a.StreamPath == b.StreamPath && Mathf.IsEqualApprox(a.VolumeDb, b.VolumeDb) && Mathf.IsEqualApprox(a.PitchScale, b.PitchScale)
            && Mathf.IsEqualApprox(a.FadeTime, b.FadeTime) && a.FadeCurve == b.FadeCurve && a.QueueIndex == b.QueueIndex
            && Mathf.IsEqualApprox(a.Position, b.Position) && a.LoopMode == b.LoopMode && a.ClipName == b.ClipName;

    private void OnCommandExecuted(StringName commandName, AudioCommand command)
        => _executed.Add(commandName);

    protected override Task RunChecks()
    {
        DirAccess.MakeDirRecursiveAbsolute(DataDir);
        Expect(ClassDB.ClassExists("AudioCommand") && ClassDB.ClassExists("AudioCommandList"), "AudioCommand and AudioCommandList are registered");

        var layout = new AudioMixerLayout();
        foreach (StringName channelName in Pads.Concat(Forest).Concat(new StringName[] { "music", "traffic" }))
        {
            layout.AddChannel(MakeChannel(channelName));
        }
        AudioMixerServer.ApplyLayout(layout);
        AudioMixerServer.Singleton.CommandExecuted += OnCommandExecuted;
        string longWav = SaveWav("long.tres", 30.0);

        // 1. A command list saved as CSV loads back without loss; enums appear as names.
        var list = new AudioCommandList();
        var toggle = MakeCommand("horn", AudioCommand.CommandEnum.Toggle, "pad_1", longWav);
        toggle.Group = "board";
        list.AddCommand(toggle);
        var fade = MakeCommand("forest_in", AudioCommand.CommandEnum.FadeIn, "birds", longWav);
        fade.Group = "forest";
        fade.FadeTime = 2.5f;
        fade.FadeCurve = AudioMixerServer.FadeCurve.EqualPower;
        fade.LoopMode = AudioCommand.LoopModeEnum.On;
        list.AddCommand(fade);
        var jump = MakeCommand("track_3", AudioCommand.CommandEnum.SetQueueIndex, "music");
        jump.QueueIndex = 3;
        list.AddCommand(jump);
        var clip = MakeCommand("battle", AudioCommand.CommandEnum.SwitchToClip, "music");
        clip.ClipName = "Battle";
        clip.VolumeDb = -6.5f;
        list.AddCommand(clip);
        string csvPath = DataDir.PathJoin("commands.csv");
        Expect(ResourceSaver.Save(list, csvPath) == Error.Ok, "ResourceSaver saves an AudioCommandList as CSV");
        string csvText = FileAccess.GetFileAsString(csvPath);
        ExpectEqual(csvText.StripEdges().Split("\n").Length, 5, "the CSV has a header and one row per command");
        Expect(csvText.Contains("Toggle") && csvText.Contains("Fade In") && csvText.Contains("Equal Power"), "enums are written as names");
        var loaded = ResourceLoader.Load<AudioCommandList>(csvPath, "AudioCommandList", ResourceLoader.CacheMode.Ignore);
        Expect(loaded != null && loaded.Commands.Count == 4, "the CSV loads back with the type hint");
        if (loaded != null && loaded.Commands.Count == 4)
        {
            Expect(Enumerable.Range(0, 4).All(i => SameCommand(loaded.Commands[i], list.Commands[i])), "every property of every command survives the CSV round trip");
            Expect(loaded.Commands[1].StreamPath == longWav, "paths stay paths");
        }
        foreach (ResourceTextSerializer.Format format in new[] { ResourceTextSerializer.Format.Json, ResourceTextSerializer.Format.Ini })
        {
            string text = ResourceTextSerializer.Serialize(list, format);
            var back = ResourceTextSerializer.Deserialize(text, format, "AudioCommandList") as AudioCommandList;
            Expect(back != null && back.Commands.Count == 4 && SameCommand(back.Commands[1], fade), $"{format} round trip keeps the commands");
        }

        // 2. A soundboard of five TOGGLE commands works with ExecuteNamed().
        var board = new AudioCommandList();
        for (int i = 0; i < Pads.Length; i++)
        {
            board.AddCommand(MakeCommand(Pads[i], AudioCommand.CommandEnum.Toggle, Pads[i], SaveWav($"pad_{i + 1}.tres", 30.0)));
        }
        Expect(board.PreloadStreams() == Error.Ok && board.GetPreloadedStreamCount() == 5, "PreloadStreams() loads the five pad streams");
        Expect(AudioMixerServer.RegisterCommands(board) == Error.Ok, "RegisterCommands() registers the soundboard");
        ExpectEqual(string.Join(",", AudioMixerServer.GetCommandNames()), "pad_1,pad_2,pad_3,pad_4,pad_5", "the five pads are registered by name");
        var button = new Button();
        AddChild(button);
        button.Pressed += () => AudioMixerServer.ExecuteNamed("pad_3");
        button.EmitSignal(BaseButton.SignalName.Pressed);
        Expect(AudioMixerServer.IsPlaying("pad_3"), "a button press toggles pad 3 on");
        Expect(!AudioMixerServer.IsPlaying("pad_2"), "other pads stay silent");
        Expect(_executed.Contains("pad_3"), "CommandExecuted reports the pad");
        button.EmitSignal(BaseButton.SignalName.Pressed);
        Expect(!AudioMixerServer.IsPlaying("pad_3"), "the second press toggles pad 3 off");
        foreach (StringName pad in Pads)
        {
            AudioMixerServer.ExecuteNamed(pad);
        }
        Expect(Pads.All(pad => AudioMixerServer.IsPlaying(pad)), "all five pads play after one press each");
        button.QueueFree();

        // 3. ExecuteGroup("forest") starts all ambience commands of that group.
        var ambience = new AudioCommandList();
        foreach (StringName channelName in Forest)
        {
            var command = MakeCommand(new StringName(), AudioCommand.CommandEnum.Play, channelName, longWav);
            command.Group = "forest";
            command.LoopMode = AudioCommand.LoopModeEnum.On;
            ambience.AddCommand(command);
        }
        var city = MakeCommand("city", AudioCommand.CommandEnum.Play, "traffic", longWav);
        city.Group = "city";
        ambience.AddCommand(city);
        AudioMixerServer.RegisterCommands(ambience);
        Expect(AudioMixerServer.ExecuteGroup("forest") == Error.Ok, "ExecuteGroup(\"forest\") returns Ok");
        Expect(Forest.All(channelName => AudioMixerServer.IsPlaying(channelName)), "all forest channels play");
        Expect(!AudioMixerServer.IsPlaying("traffic"), "the city command did not run");
        Expect(AudioMixerServer.GetChannel("birds").LoopCurrentEntry, "LoopMode On loops the channel entry");

        // 4. An invalid command returns an error without stopping other channels.
        var ghost = MakeCommand("ghost", AudioCommand.CommandEnum.Play, "ghost", longWav);
        Expect(AudioMixerServer.Execute(ghost) == Error.DoesNotExist, "an unknown channel returns DoesNotExist");
        var missing = MakeCommand("missing", AudioCommand.CommandEnum.Play, "traffic", DataDir.PathJoin("missing.tres"));
        Expect(AudioMixerServer.Execute(missing) == Error.FileNotFound, "a missing file returns FileNotFound");
        Expect(AudioMixerServer.IsPlaying("birds") && AudioMixerServer.IsPlaying("pad_1"), "other channels keep playing");
        Expect(!AudioMixerServer.IsPlaying("traffic"), "the failed command started nothing");

        // 5. Collisions and unregistering.
        var mod = new AudioCommandList();
        mod.AddCommand(MakeCommand("pad_1", AudioCommand.CommandEnum.Stop, "pad_1"));
        AudioMixerServer.RegisterCommands(mod);
        Expect(AudioMixerServer.ExecuteNamed("pad_1") == Error.Ok && !AudioMixerServer.IsPlaying("pad_1"), "the last registered pad_1 (Stop) wins");
        AudioMixerServer.UnregisterCommands(mod);
        AudioMixerServer.UnregisterCommands(board);
        AudioMixerServer.UnregisterCommands(ambience);
        Expect(AudioMixerServer.GetCommandNames().Length == 0, "UnregisterCommands() removes the commands");

        // 6. StopAll through ExecuteList().
        var stopList = new AudioCommandList();
        var stopAll = MakeCommand(new StringName(), AudioCommand.CommandEnum.StopAll, new StringName());
        stopAll.FadeTime = 0.0f;
        stopList.AddCommand(stopAll);
        Expect(AudioMixerServer.ExecuteList(stopList) == Error.Ok, "ExecuteList() runs StopAll");
        Expect(!AudioMixerServer.IsPlaying("birds") && !AudioMixerServer.IsPlaying("pad_2"), "StopAll stops every channel");

        AudioMixerServer.Singleton.CommandExecuted -= OnCommandExecuted;
        board.ClearPreloadedStreams();
        AudioMixerServer.ApplyLayout(new AudioMixerLayout());
        return Task.CompletedTask;
    }
}
