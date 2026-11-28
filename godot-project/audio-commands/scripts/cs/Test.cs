using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.AudioCommandsGuide;

/// <summary>
/// Full API test of Audio Commands (AudioCommand, AudioCommands and the command part of AudioMixerServer).
/// Same tests as scripts/gd/Test.gd. Runs headless with the dummy audio driver: it checks state, not sound.
/// The error tests print the expected errors.
/// </summary>
public partial class Test : FeatureTest
{
    private const string DataDir = "user://audio-commands";
    private static readonly string[] Properties = ["command_name", "group", "channel", "command", "stream_path", "volume_db", "pitch_scale", "fade_time", "fade_curve", "queue_index", "position", "loop_mode", "clip_name"];

    private string _streamA = "";
    private string _streamB = "";
    private readonly List<StringName> _executed = [];

    protected override Task RunTests()
    {
        DirAccess.MakeDirRecursiveAbsolute(DataDir);
        _streamA = SaveSilence("a.tres");
        _streamB = SaveSilence("b.tres");
        var layout = new AudioMixerLayout();
        foreach (StringName channelName in new StringName[] { "music", "pad_1", "pad_2", "birds", "wind" })
        {
            layout.AddChannel(new AudioMixerChannel { ChannelName = channelName });
        }
        AudioMixerServer.ApplyLayout(layout);
        AudioMixerServer.Singleton.CommandExecuted += OnCommandExecuted;
        TestDefaults();
        TestCommandList();
        TestTextFormats();
        TestPreloadStreams();
        TestExecute();
        TestCommandTypes();
        TestErrors();
        TestNamedCommands();
        TestGroups();
        TestLegacyType();
        AudioMixerServer.Singleton.CommandExecuted -= OnCommandExecuted;
        AudioMixerServer.ApplyLayout(new AudioMixerLayout());
        return Task.CompletedTask;
    }

    private void TestDefaults()
    {
        var command = new AudioCommand();
        ExpectEqual(command.Command, AudioCommand.CommandEnum.Play, "Command defaults to Play");
        ExpectEqual(command.FadeTime, -1.0f, "FadeTime defaults to -1 (the channel's fade time)");
        ExpectEqual(command.LoopMode, AudioCommand.LoopModeEnum.Channel, "LoopMode defaults to Channel");
        ExpectEqual(command.QueueIndex, -1, "QueueIndex defaults to -1");
        ExpectEqual(command.PitchScale, 1.0f, "PitchScale defaults to 1");
    }

    private void TestCommandList()
    {
        var list = new AudioCommands();
        AudioCommand horn = Command(AudioCommand.CommandEnum.Toggle, "pad_1");
        horn.CommandName = "horn";
        list.AddCommand(horn);
        list.AddCommand(Command(AudioCommand.CommandEnum.Stop, "pad_2"));
        ExpectEqual(list.Commands.Count, 2, "AddCommand appends to Commands");
        Expect(list.FindCommand("horn") == horn, "FindCommand finds a command by name");
        Expect(list.FindCommand("none") == null, "FindCommand returns null for an unknown name");
    }

    private void TestTextFormats()
    {
        AudioCommand full = Command(AudioCommand.CommandEnum.CrossFade, "music", _streamA);
        full.CommandName = "battle";
        full.Group = "fight";
        full.VolumeDb = -6.5f;
        full.PitchScale = 1.25f;
        full.FadeTime = 2.5f;
        full.FadeCurve = AudioMixerServer.FadeCurve.EqualPower;
        full.QueueIndex = 3;
        full.Position = 1.5f;
        full.LoopMode = AudioCommand.LoopModeEnum.On;
        full.ClipName = "Battle";
        var list = new AudioCommands();
        list.AddCommand(full);
        list.AddCommand(Command(AudioCommand.CommandEnum.StopAll, ""));
        string csvPath = DataDir.PathJoin("commands.csv");
        ExpectEqual(ResourceSaver.Save(list, csvPath), Error.Ok, "ResourceSaver saves an AudioCommands as CSV");
        string csvText = FileAccess.GetFileAsString(csvPath);
        ExpectEqual(csvText.StripEdges().Split("\n").Length, 3, "the CSV has a header and one row per command");
        Expect(csvText.Contains("Cross Fade"), "the CSV writes the command by name");
        Expect(csvText.Contains("Equal Power"), "the CSV writes the fade curve by name");
        var csvList = ResourceLoader.Load<AudioCommands>(csvPath, "AudioCommands", ResourceLoader.CacheMode.Ignore);
        Expect(SameCommand(csvList.Commands[0], full), "every property survives the CSV round trip");
        string json = ResourceTextSerializer.Serialize(list, ResourceTextSerializer.Format.Json);
        var jsonList = (AudioCommands)ResourceTextSerializer.Deserialize(json, ResourceTextSerializer.Format.Json, "AudioCommands");
        Expect(SameCommand(jsonList.Commands[0], full), "every property survives the JSON round trip");
        string ini = ResourceTextSerializer.Serialize(list, ResourceTextSerializer.Format.Ini);
        var iniList = (AudioCommands)ResourceTextSerializer.Deserialize(ini, ResourceTextSerializer.Format.Ini, "AudioCommands");
        Expect(SameCommand(iniList.Commands[0], full), "every property survives the INI round trip");
    }

    private void TestPreloadStreams()
    {
        var list = new AudioCommands();
        list.AddCommand(Command(AudioCommand.CommandEnum.Play, "pad_1", _streamA));
        list.AddCommand(Command(AudioCommand.CommandEnum.Play, "pad_2", _streamB));
        ExpectEqual(list.PreloadStreams(), Error.Ok, "PreloadStreams returns Ok");
        ExpectEqual(list.GetPreloadedStreamCount(), 2, "PreloadStreams keeps both streams");
        list.ClearPreloadedStreams();
        ExpectEqual(list.GetPreloadedStreamCount(), 0, "ClearPreloadedStreams releases the streams");
    }

    private void TestExecute()
    {
        AudioCommand play = Command(AudioCommand.CommandEnum.Play, "music", _streamA);
        play.CommandName = "theme";
        play.LoopMode = AudioCommand.LoopModeEnum.On;
        ExpectEqual(AudioMixerServer.Execute(play), Error.Ok, "Execute returns Ok");
        Expect(AudioMixerServer.IsPlaying("music"), "Play starts the channel");
        AudioMixerChannel channel = AudioMixerServer.GetChannel("music");
        Expect(channel.LoopCurrentEntry, "LoopMode On sets LoopCurrentEntry of the channel");
        Expect(_executed.SequenceEqual([(StringName)"theme"]), "CommandExecuted reports the command name");
    }

    private void TestCommandTypes()
    {
        AudioMixerServer.Execute(Command(AudioCommand.CommandEnum.Pause, "music"));
        Expect(AudioMixerServer.IsPaused("music"), "Pause pauses the channel");
        AudioMixerServer.Execute(Command(AudioCommand.CommandEnum.Resume, "music"));
        Expect(!AudioMixerServer.IsPaused("music"), "Resume resumes the channel");
        AudioCommand volume = Command(AudioCommand.CommandEnum.SetVolume, "music");
        volume.VolumeDb = -6.0f;
        AudioMixerServer.Execute(volume);
        ExpectEqual(AudioMixerServer.GetChannelVolumeDb("music"), -6.0f, "SetVolume sets the channel volume");
        AudioCommand pitch = Command(AudioCommand.CommandEnum.SetPitch, "music");
        pitch.PitchScale = 1.5f;
        AudioMixerServer.Execute(pitch);
        ExpectEqual(AudioMixerServer.GetChannelPitchScale("music"), 1.5f, "SetPitch sets the channel pitch");
        AudioCommand seek = Command(AudioCommand.CommandEnum.Seek, "music");
        seek.Position = 2.0f;
        ExpectEqual(AudioMixerServer.Execute(seek), Error.Ok, "Seek on a playing channel returns Ok");
        AudioCommand fadeOut = Command(AudioCommand.CommandEnum.FadeOut, "music");
        fadeOut.FadeTime = 1.0f;
        AudioMixerServer.Execute(fadeOut);
        Expect(AudioMixerServer.IsFading("music"), "FadeOut starts a fade");
        AudioMixerServer.Execute(Command(AudioCommand.CommandEnum.Stop, "music"));
        Expect(!AudioMixerServer.IsPlaying("music"), "Stop stops the channel");
        AudioCommand fadeIn = Command(AudioCommand.CommandEnum.FadeIn, "music", _streamA);
        fadeIn.FadeTime = 1.0f;
        AudioMixerServer.Execute(fadeIn);
        Expect(AudioMixerServer.IsFading("music"), "FadeIn with a stream fades the stream in");
        AudioCommand crossFade = Command(AudioCommand.CommandEnum.CrossFade, "music", _streamB);
        crossFade.FadeTime = 1.0f;
        AudioMixerServer.Execute(crossFade);
        Expect(AudioMixerServer.IsFading("music"), "CrossFade fades to the new stream");
        AudioMixerServer.Execute(Command(AudioCommand.CommandEnum.Stop, "music"));
        AudioMixerServer.Execute(Command(AudioCommand.CommandEnum.PlayOneShot, "pad_1", _streamA));
        ExpectEqual(AudioMixerServer.GetVoiceCount("pad_1"), 1, "PlayOneShot starts a voice");
        AudioMixerServer.Execute(Command(AudioCommand.CommandEnum.Enqueue, "wind", _streamA));
        AudioMixerServer.Execute(Command(AudioCommand.CommandEnum.Enqueue, "wind", _streamB));
        Expect(AudioMixerServer.GetQueue("wind").SequenceEqual([_streamA, _streamB]), "Enqueue appends to the queue");
        AudioCommand jump = Command(AudioCommand.CommandEnum.SetQueueIndex, "wind");
        jump.QueueIndex = 1;
        AudioMixerServer.Execute(jump);
        ExpectEqual(AudioMixerServer.GetQueueIndex("wind"), 1, "SetQueueIndex jumps to the entry");
        AudioMixerServer.Execute(Command(AudioCommand.CommandEnum.PlayPrevious, "wind"));
        ExpectEqual(AudioMixerServer.GetQueueIndex("wind"), 0, "PlayPrevious goes one entry back");
        AudioMixerServer.Execute(Command(AudioCommand.CommandEnum.PlayNext, "wind"));
        ExpectEqual(AudioMixerServer.GetQueueIndex("wind"), 1, "PlayNext goes one entry on");
        AudioMixerServer.Execute(Command(AudioCommand.CommandEnum.Dequeue, "wind"));
        ExpectEqual(AudioMixerServer.GetQueue("wind").Length, 1, "Dequeue removes the last entry");
        AudioMixerServer.Execute(Command(AudioCommand.CommandEnum.Clear, "wind"));
        Expect(AudioMixerServer.GetQueue("wind").Length == 0, "Clear empties the queue");
    }

    private void TestErrors()
    {
        AudioMixerServer.Execute(Command(AudioCommand.CommandEnum.Play, "birds", _streamA));
        AudioCommand ghost = Command(AudioCommand.CommandEnum.Play, "ghost", _streamA);
        ExpectEqual(AudioMixerServer.Execute(ghost), Error.DoesNotExist, "an unknown channel returns DoesNotExist");
        AudioCommand missing = Command(AudioCommand.CommandEnum.Play, "music", DataDir.PathJoin("missing.tres"));
        ExpectEqual(AudioMixerServer.Execute(missing), Error.FileNotFound, "a missing file returns FileNotFound");
        Expect(!AudioMixerServer.IsPlaying("music"), "a failed command starts nothing");
        Expect(AudioMixerServer.IsPlaying("birds"), "a failed command keeps other channels playing");
        AudioCommand seek = Command(AudioCommand.CommandEnum.Seek, "music");
        ExpectEqual(AudioMixerServer.Execute(seek), Error.Unavailable, "Seek on a silent channel returns Unavailable");
        AudioCommand dequeue = Command(AudioCommand.CommandEnum.Dequeue, "music");
        ExpectEqual(AudioMixerServer.Execute(dequeue), Error.ParameterRangeError, "Dequeue on an empty queue returns ParameterRangeError");
        AudioCommand pitch = Command(AudioCommand.CommandEnum.SetPitch, "music");
        pitch.PitchScale = 0.0f;
        ExpectEqual(AudioMixerServer.Execute(pitch), Error.InvalidParameter, "SetPitch of 0 returns InvalidParameter");
        AudioCommand clip = Command(AudioCommand.CommandEnum.SwitchToClip, "music");
        ExpectEqual(AudioMixerServer.Execute(clip), Error.InvalidParameter, "SwitchToClip without ClipName returns InvalidParameter");
    }

    private void TestNamedCommands()
    {
        var board = new AudioCommands();
        foreach (StringName pad in new StringName[] { "pad_1", "pad_2" })
        {
            AudioCommand toggle = Command(AudioCommand.CommandEnum.Toggle, pad, _streamA);
            toggle.CommandName = pad;
            board.AddCommand(toggle);
        }
        ExpectEqual(AudioMixerServer.RegisterCommands(board), Error.Ok, "RegisterCommands returns Ok");
        Expect(AudioMixerServer.GetCommandNames().SequenceEqual(["pad_1", "pad_2"]), "GetCommandNames lists the registered names");
        Expect(AudioMixerServer.HasCommand("pad_2"), "HasCommand finds a registered name");
        AudioMixerServer.Execute(Command(AudioCommand.CommandEnum.Stop, "pad_1"));
        AudioMixerServer.ExecuteNamed("pad_1");
        Expect(AudioMixerServer.IsPlaying("pad_1"), "the first Toggle starts the channel");
        Expect(!AudioMixerServer.IsPlaying("pad_2"), "ExecuteNamed runs only the named command");
        AudioMixerServer.ExecuteNamed("pad_1");
        Expect(!AudioMixerServer.IsPlaying("pad_1"), "the second Toggle stops the channel");
        ExpectEqual(AudioMixerServer.ExecuteNamed("none"), Error.DoesNotExist, "ExecuteNamed of an unknown name returns DoesNotExist");
        var mod = new AudioCommands();
        AudioCommand stop = Command(AudioCommand.CommandEnum.Stop, "pad_2");
        stop.CommandName = "pad_2";
        mod.AddCommand(stop);
        AudioMixerServer.RegisterCommands(mod);
        AudioMixerServer.Execute(Command(AudioCommand.CommandEnum.Play, "pad_2", _streamA));
        AudioMixerServer.ExecuteNamed("pad_2");
        Expect(!AudioMixerServer.IsPlaying("pad_2"), "the last registered command of a name wins");
        AudioMixerServer.UnregisterCommands(mod);
        Expect(!AudioMixerServer.HasCommand("pad_2"), "UnregisterCommands removes the overriding name");
        AudioMixerServer.UnregisterCommands(board);
        Expect(AudioMixerServer.GetCommandNames().Length == 0, "UnregisterCommands removes all names of the list");
    }

    private void TestGroups()
    {
        AudioMixerServer.StopAll();
        var ambience = new AudioCommands();
        foreach (StringName channelName in new StringName[] { "birds", "wind" })
        {
            AudioCommand forest = Command(AudioCommand.CommandEnum.Play, channelName, _streamA);
            forest.Group = "forest";
            ambience.AddCommand(forest);
        }
        AudioCommand city = Command(AudioCommand.CommandEnum.Play, "music", _streamA);
        city.Group = "city";
        ambience.AddCommand(city);
        AudioMixerServer.RegisterCommands(ambience);
        ExpectEqual(AudioMixerServer.ExecuteGroup("forest"), Error.Ok, "ExecuteGroup returns Ok");
        Expect(AudioMixerServer.IsPlaying("birds"), "ExecuteGroup runs the first command of the group");
        Expect(AudioMixerServer.IsPlaying("wind"), "ExecuteGroup runs the second command of the group");
        Expect(!AudioMixerServer.IsPlaying("music"), "ExecuteGroup skips other groups");
        AudioMixerServer.UnregisterCommands(ambience);
        var stopList = new AudioCommands();
        stopList.AddCommand(Command(AudioCommand.CommandEnum.StopAll, ""));
        ExpectEqual(AudioMixerServer.ExecuteCommands(stopList), Error.Ok, "ExecuteCommands runs StopAll");
        Expect(!AudioMixerServer.IsPlaying("birds"), "StopAll stops every channel");
    }

    private static AudioCommand Command(AudioCommand.CommandEnum type, StringName channel, string streamPath = "")
    {
        return new AudioCommand { Command = type, Channel = channel, StreamPath = streamPath, FadeTime = 0.0f };
    }

    /// <summary>Saves 30 seconds of silence as AudioStreamWav and returns the path.</summary>
    private static string SaveSilence(string fileName)
    {
        var wav = new AudioStreamWav
        {
            Format = AudioStreamWav.FormatEnum.Format16Bits,
            MixRate = 44100,
            Data = new byte[30 * 44100 * 2],
        };
        string path = DataDir.PathJoin(fileName);
        ResourceSaver.Save(wav, path);
        return path;
    }

    private static bool SameCommand(AudioCommand a, AudioCommand b)
    {
        return Properties.All(property => Equals(a.Get(property).Obj, b.Get(property).Obj));
    }

    private void OnCommandExecuted(StringName commandName, AudioCommand command)
    {
        _executed.Add(commandName);
    }

    /// <summary>The old name AudioCommandList is deprecated: a .tres with that type loads as AudioCommands.</summary>
    private void TestLegacyType()
    {
        var legacy = GD.Load<AudioCommands>("res://audio-commands/resources/legacy_audio_command_list.tres");
        Expect(legacy != null && legacy.GetClass() == "AudioCommands", "a .tres with the old type AudioCommandList loads as AudioCommands");
        ExpectEqual(legacy.Commands.Count, 4, "the legacy .tres keeps its commands");
    }
}
