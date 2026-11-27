using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.AudioMixer;

/// <summary>
/// Full API test of Audio Mixer (AudioMixerServer, AudioMixerChannel, AudioMixerLayout) without the commands
/// (those are tested in audio-commands). Same tests as scripts/gd/Test.gd.
/// Runs headless with the dummy audio driver: it checks state, queue, volumes and fades, not sound.
/// Speech is simulated through DisplayServer (like the OS callbacks), so ducking needs no speech engine.
/// </summary>
public partial class Test : FeatureTest
{
    private const string DataDir = "user://audio-mixer";
    private const string NextLevel = "res://audio-mixer/scenes/next_level.tscn";
    private const float EqualPowerHalfWay = 0.71f; // sin(PI / 4); a linear fade is at 0.5

    private string _short = "";
    private string _long = "";
    private string _longB = "";
    private readonly List<long> _started = [];
    private readonly List<long> _finished = [];
    private int _queueFinished;
    private int _fadesFinished;
    private readonly List<string> _duckingEvents = [];

    protected override async Task RunTests()
    {
        DirAccess.MakeDirRecursiveAbsolute(DataDir);
        _short = SaveSilence("short.tres", 0.1);
        _long = SaveSilence("long.tres", 10.0);
        _longB = SaveSilence("long_b.tres", 10.0);
        Resource[] cached = [GD.Load(_long), GD.Load(_longB)]; // kept in the cache: a load during a fade would delay the frame
        AudioMixerServerInstance mixer = AudioMixerServer.Singleton;
        mixer.ChannelStarted += OnChannelStarted;
        mixer.ChannelFinished += OnChannelFinished;
        mixer.QueueFinished += OnQueueFinished;
        mixer.FadeFinished += OnFadeFinished;
        mixer.DuckingChanged += OnDuckingChanged;
        TestChannelDefaults();
        TestLayout();
        TestPlayback();
        TestQueue();
        await TestQueueModes();
        await TestFades();
        TestOneShots();
        await TestSwitchToClip();
        await TestPauseWithTree();
        await TestDucking();
        await TestSceneChange();
        mixer.ChannelStarted -= OnChannelStarted;
        mixer.ChannelFinished -= OnChannelFinished;
        mixer.QueueFinished -= OnQueueFinished;
        mixer.FadeFinished -= OnFadeFinished;
        mixer.DuckingChanged -= OnDuckingChanged;
        AudioMixerServer.StopAll();
        AudioMixerServer.ApplyLayout(new AudioMixerLayout());
        System.GC.KeepAlive(cached);
    }

    private void TestChannelDefaults()
    {
        var channel = new AudioMixerChannel();
        ExpectEqual(channel.ChannelName, (StringName)"", "ChannelName defaults to empty");
        ExpectEqual(channel.Bus, (StringName)"Master", "Bus defaults to Master");
        ExpectEqual(channel.VolumeDb, 0.0f, "VolumeDb defaults to 0");
        ExpectEqual(channel.PitchScale, 1.0f, "PitchScale defaults to 1");
        ExpectEqual(channel.DefaultFadeTime, 0.0f, "DefaultFadeTime defaults to 0");
        ExpectEqual(channel.QueueMode, AudioMixerChannel.QueueModeEnum.Stop, "QueueMode defaults to Stop");
        ExpectEqual(channel.LoopCurrentEntry, false, "LoopCurrentEntry defaults to false");
        ExpectEqual(channel.PreloadNext, false, "PreloadNext defaults to false");
        ExpectEqual(channel.MaxPolyphony, 1, "MaxPolyphony defaults to 1");
        ExpectEqual(channel.PauseWithTree, false, "PauseWithTree defaults to false");
        ExpectEqual(channel.StopOnSceneChange, false, "StopOnSceneChange defaults to false");
        ExpectEqual(channel.DuckOnTts, false, "DuckOnTts defaults to false");
        ExpectEqual(channel.DuckVolumeDb, -12.0f, "DuckVolumeDb defaults to -12");
        ExpectEqual(channel.DuckFadeTime, 0.25f, "DuckFadeTime defaults to 0.25");
        channel.DuckVolumeDb = 6.0f;
        ExpectEqual(channel.DuckVolumeDb, 0.0f, "DuckVolumeDb is clamped to 0 (ducking only lowers)");
    }

    private void TestLayout()
    {
        var music = new AudioMixerChannel { ChannelName = "music", DefaultFadeTime = 0.3f };
        var ambience = new AudioMixerChannel { ChannelName = "ambience", PauseWithTree = true, StopOnSceneChange = true };
        var playlist = new AudioMixerChannel { ChannelName = "playlist" };
        var sfx = new AudioMixerChannel { ChannelName = "sfx", MaxPolyphony = 2 };
        var layout = new AudioMixerLayout();
        layout.AddChannel(music);
        layout.AddChannel(ambience);
        layout.AddChannel(playlist);
        layout.AddChannel(sfx);
        ExpectEqual(layout.Channels.Count, 4, "AddChannel appends to Channels");
        Expect(layout.FindChannel("sfx") == sfx, "FindChannel finds a channel by name");
        Expect(layout.FindChannel("none") == null, "FindChannel returns null for an unknown name");
        AudioMixerServer.ApplyLayout(layout);
        Expect(AudioMixerServer.GetChannelNames().SequenceEqual(["music", "ambience", "playlist", "sfx"]), "ApplyLayout adds the channels in order");
        Expect(AudioMixerServer.GetChannel("ambience") == ambience, "GetChannel returns the definition");
        AudioMixerServer.AddChannel(new AudioMixerChannel { ChannelName = "extra" });
        Expect(AudioMixerServer.HasChannel("extra"), "AddChannel adds a channel");
        var replacement = new AudioMixerChannel { ChannelName = "extra" };
        AudioMixerServer.AddChannel(replacement);
        Expect(AudioMixerServer.GetChannel("extra") == replacement, "AddChannel replaces a channel of the same name");
        AudioMixerServer.RemoveChannel("extra");
        Expect(!AudioMixerServer.HasChannel("extra"), "RemoveChannel removes the channel");
    }

    private void TestPlayback()
    {
        AudioMixerServer.Play("music", _long, 1.0f);
        Expect(AudioMixerServer.IsPlaying("music"), "Play starts the channel");
        Expect(AudioMixerServer.GetPlaybackPosition("music") >= 1.0f, "Play starts at fromPosition");
        Expect(AudioMixerServer.GetChannelPlayback("music") != null, "GetChannelPlayback returns the playback");
        AudioMixerServer.Seek("music", 5.0f);
        Expect(AudioMixerServer.GetPlaybackPosition("music") >= 5.0f, "Seek moves the position");
        AudioMixerServer.Pause("music");
        Expect(AudioMixerServer.IsPaused("music"), "Pause pauses the channel");
        AudioMixerServer.Resume("music");
        Expect(!AudioMixerServer.IsPaused("music"), "Resume resumes the channel");
        AudioMixerServer.SetChannelPitchScale("music", 1.5f);
        ExpectEqual(AudioMixerServer.GetChannelPitchScale("music"), 1.5f, "SetChannelPitchScale sets the pitch");
        AudioMixerServer.SetChannelPitchScale("music", 1.0f);
        AudioMixerServer.Stop("music");
        Expect(!AudioMixerServer.IsPlaying("music"), "Stop stops the channel");
    }

    private void TestQueue()
    {
        AudioMixerServer.Enqueue("music", _long);
        AudioMixerServer.Enqueue("music", _longB);
        AudioMixerServer.Enqueue("music", _short, 0);
        Expect(AudioMixerServer.GetQueue("music").SequenceEqual([_short, _long, _longB]), "Enqueue appends, or inserts at an index");
        AudioMixerServer.Dequeue("music", 0);
        Expect(AudioMixerServer.GetQueue("music").SequenceEqual([_long, _longB]), "Dequeue removes the entry at an index");
        AudioMixerServer.Play("music");
        ExpectEqual(AudioMixerServer.GetQueueIndex("music"), 0, "Play without a stream starts at the first entry");
        AudioMixerServer.PlayNext("music");
        ExpectEqual(AudioMixerServer.GetQueueIndex("music"), 1, "PlayNext moves to the next entry");
        ExpectEqual(AudioMixerServer.GetVoiceCount("music"), 2, "PlayNext cross-fades with DefaultFadeTime: two voices");
        AudioMixerServer.PlayPrevious("music");
        ExpectEqual(AudioMixerServer.GetQueueIndex("music"), 0, "PlayPrevious moves to the previous entry");
        AudioMixerServer.SetQueueIndex("music", 1, 0.0f);
        ExpectEqual(AudioMixerServer.GetQueueIndex("music"), 1, "SetQueueIndex moves to the entry");
        AudioMixerServer.ClearQueue("music");
        Expect(AudioMixerServer.GetQueue("music").Length == 0, "ClearQueue empties the queue");
        AudioMixerServer.Stop("music");
    }

    private async Task TestQueueModes()
    {
        for (int i = 0; i < 3; i++)
        {
            AudioMixerServer.Enqueue("playlist", _short);
        }
        AudioMixerServer.Play("playlist");
        await WaitForQueueFinished();
        Expect(_started.SequenceEqual([0L, 1L, 2L]), "QueueMode Stop: the entries start in order");
        Expect(_finished.SequenceEqual([0L, 1L, 2L]), "ChannelFinished is emitted for each entry");
        ExpectEqual(_queueFinished, 1, "QueueMode Stop: QueueFinished is emitted after the last entry");
        Expect(!AudioMixerServer.IsPlaying("playlist"), "QueueMode Stop: the channel stops after the last entry");
        AudioMixerChannel playlist = AudioMixerServer.GetChannel("playlist");
        playlist.QueueMode = AudioMixerChannel.QueueModeEnum.LoopQueue;
        AudioMixerServer.AddChannel(playlist);
        _started.Clear();
        AudioMixerServer.SetQueueIndex("playlist", 0, 0.0f);
        AudioMixerServer.Play("playlist");
        await WaitForStarted(4);
        Expect(_started.Take(4).SequenceEqual([0L, 1L, 2L, 0L]), "QueueMode LoopQueue: the queue starts over");
        AudioMixerServer.Stop("playlist");
        AudioMixerServer.Dequeue("playlist");
        playlist.QueueMode = AudioMixerChannel.QueueModeEnum.Shuffle;
        AudioMixerServer.AddChannel(playlist);
        _started.Clear();
        AudioMixerServer.SetQueueIndex("playlist", 0, 0.0f);
        AudioMixerServer.Play("playlist");
        await WaitForStarted(3);
        Expect(_started.Take(3).SequenceEqual([0L, 1L, 0L]), "QueueMode Shuffle: another entry follows");
        AudioMixerServer.Stop("playlist");
        playlist.LoopCurrentEntry = true;
        AudioMixerServer.AddChannel(playlist);
        _started.Clear();
        AudioMixerServer.SetQueueIndex("playlist", 0, 0.0f);
        AudioMixerServer.Play("playlist");
        await WaitForStarted(3);
        Expect(_started.Take(3).SequenceEqual([0L, 0L, 0L]), "LoopCurrentEntry repeats the entry instead of moving on");
        AudioMixerServer.Stop("playlist");
        AudioMixerServer.ClearQueue("playlist");
    }

    private async Task TestFades()
    {
        AudioMixerServer.Play("music", _long);
        _fadesFinished = 0;
        AudioMixerServer.CrossFade("music", _longB, 0.4f);
        ExpectEqual(AudioMixerServer.GetVoiceCount("music"), 2, "CrossFade keeps the old voice while fading");
        await Wait(0.2);
        float gain = AudioMixerServer.GetFadeGain("music");
        Expect(Mathf.Abs(gain - EqualPowerHalfWay) < 0.15f, $"CrossFade uses equal power: about 0.71 half way (got {gain})");
        await Wait(0.4);
        ExpectEqual(AudioMixerServer.GetVoiceCount("music"), 1, "CrossFade stops the old voice at the end");
        ExpectEqual(_fadesFinished, 1, "FadeFinished is emitted after the cross-fade");
        AudioMixerServer.FadeOut("music", 0.3f);
        Expect(!AudioMixerServer.IsPlaying("music"), "FadeOut ends the current voice");
        await Wait(0.5);
        ExpectEqual(AudioMixerServer.GetVoiceCount("music"), 0, "FadeOut stops the voice at the end");
        AudioMixerServer.Enqueue("music", _long);
        AudioMixerServer.FadeIn("music", 0.3f, AudioMixerServer.FadeCurve.Linear);
        Expect(AudioMixerServer.IsFading("music"), "FadeIn fades the channel in");
        Expect(AudioMixerServer.GetFadeGain("music") < 0.5f, "FadeIn starts quiet");
        AudioMixerServer.Stop("music");
        AudioMixerServer.ClearQueue("music");
        AudioMixerServer.SetChannelVolumeDb("sfx", -12.0f, 0.3f);
        Expect(AudioMixerServer.IsFading("sfx"), "SetChannelVolumeDb with a fade time fades");
        await Wait(0.5);
        ExpectEqual(AudioMixerServer.GetChannelVolumeDb("sfx"), -12.0f, "SetChannelVolumeDb reaches the volume");
    }

    private void TestOneShots()
    {
        for (int i = 0; i < 3; i++)
        {
            AudioMixerServer.PlayOneShot("sfx", _long, -3.0f, 1.1f);
        }
        ExpectEqual(AudioMixerServer.GetVoiceCount("sfx"), 2, "PlayOneShot is limited by MaxPolyphony");
        AudioMixerServer.Play("music", _long);
        AudioMixerServer.StopAll();
        ExpectEqual(AudioMixerServer.GetVoiceCount("sfx"), 0, "StopAll stops the one-shots");
        Expect(!AudioMixerServer.IsPlaying("music"), "StopAll stops every channel");
    }

    private async Task TestSwitchToClip()
    {
        var interactive = new AudioStreamInteractive { ClipCount = 2 };
        interactive.SetClipName(0, "Calm");
        interactive.SetClipStream(0, GD.Load<AudioStream>(_long));
        interactive.SetClipName(1, "Battle");
        interactive.SetClipStream(1, GD.Load<AudioStream>(_longB));
        AudioMixerServer.Play("music", interactive);
        AudioMixerServer.SwitchToClip("music", "Battle");
        await Wait(0.3);
        var playback = (AudioStreamPlaybackInteractive)AudioMixerServer.GetChannelPlayback("music");
        ExpectEqual(playback.GetCurrentClipIndex(), 1, "SwitchToClip switches the clip of an AudioStreamInteractive");
        AudioMixerServer.Stop("music");
    }

    private async Task TestPauseWithTree()
    {
        AudioMixerServer.Play("music", _long);
        AudioMixerServer.Play("ambience", _long);
        GetTree().Paused = true;
        await Frames(2);
        Expect(!AudioMixerServer.IsPaused("music"), "a channel keeps playing while the tree is paused");
        Expect(AudioMixerServer.IsPaused("ambience"), "PauseWithTree pauses the channel with the tree");
        GetTree().Paused = false;
        await Frames(2);
        Expect(!AudioMixerServer.IsPaused("ambience"), "PauseWithTree resumes the channel with the tree");
        AudioMixerServer.StopAll();
    }

    private async Task TestDucking()
    {
        AudioMixerChannel music = AudioMixerServer.GetChannel("music");
        music.DuckOnTts = true;
        music.DuckFadeTime = 0.2f;
        AudioMixerServer.AddChannel(music);
        AudioMixerServer.SetChannelVolumeDb("music", -3.0f);
        AudioMixerServer.Play("music", _long);
        AudioMixerServer.Play("sfx", _long);
        Expect(AudioMixerServer.IsDuckingEnabled(), "ducking is enabled by default");
        ExpectEqual(AudioMixerServer.GetActiveUtteranceCount(), 0, "no active utterances at the start");
        Post(DisplayServer.TtsUtteranceEvent.Started, 1);
        await Frames(2);
        ExpectEqual(AudioMixerServer.GetActiveUtteranceCount(), 1, "utterance_started counts one utterance");
        Expect(AudioMixerServer.IsDucked("music"), "DuckOnTts ducks the channel while speaking");
        Expect(!AudioMixerServer.IsDucked("sfx"), "a channel without DuckOnTts is not ducked");
        await Wait(0.4);
        ExpectEqual(AudioMixerServer.GetDuckVolumeDb("music"), -12.0f, "the channel fades down by DuckVolumeDb");
        ExpectEqual(AudioMixerServer.GetChannelVolumeDb("music"), -3.0f, "ducking leaves the channel volume alone");
        Expect(_duckingEvents.SequenceEqual(["music:true"]), "DuckingChanged(music, true) is emitted once");
        Post(DisplayServer.TtsUtteranceEvent.Started, 2);
        Post(DisplayServer.TtsUtteranceEvent.Ended, 1);
        await Frames(2);
        Expect(AudioMixerServer.IsDucked("music"), "the channel stays ducked while a second utterance runs");
        Post(DisplayServer.TtsUtteranceEvent.Canceled, 2);
        await Wait(0.4);
        Expect(!AudioMixerServer.IsDucked("music"), "a canceled last utterance ends the ducking");
        ExpectEqual(AudioMixerServer.GetDuckVolumeDb("music"), 0.0f, "the channel returns to its volume");
        Expect(_duckingEvents.SequenceEqual(["music:true", "music:false"]), "DuckingChanged(music, false) is emitted at the end");
        Post(DisplayServer.TtsUtteranceEvent.Started, 3);
        await Wait(0.3);
        AudioMixerServer.FadeOut("music", 0.3f);
        Post(DisplayServer.TtsUtteranceEvent.Ended, 3);
        await Wait(0.6);
        ExpectEqual(AudioMixerServer.GetVoiceCount("music"), 0, "a FadeOut during ducking stays silent after the speech");
        AudioMixerServer.Play("music", _long);
        AudioMixerServer.SetDuckingEnabled(false);
        Post(DisplayServer.TtsUtteranceEvent.Started, 4);
        await Frames(2);
        Expect(!AudioMixerServer.IsDucked("music"), "SetDuckingEnabled(false) turns ducking off");
        AudioMixerServer.SetDuckingEnabled(true);
        Expect(AudioMixerServer.IsDucked("music"), "SetDuckingEnabled(true) during speech ducks at once");
        Post(DisplayServer.TtsUtteranceEvent.Ended, 4);
        await Frames(2);
        AudioMixerServer.Duck("sfx", -6.0f, 0.0f);
        Expect(AudioMixerServer.IsDucked("sfx"), "Duck ducks any channel");
        ExpectEqual(AudioMixerServer.GetDuckVolumeDb("sfx"), -6.0f, "Duck with fade time 0 applies at once");
        AudioMixerServer.Unduck("sfx", 0.0f);
        Expect(!AudioMixerServer.IsDucked("sfx"), "Unduck restores the channel");
        AudioMixerServer.StopAll();
    }

    private async Task TestSceneChange()
    {
        var level = new Node();
        GetTree().Root.AddChild(level);
        GetTree().CurrentScene = level; // this test node is no longer the current scene, so it survives the change
        AudioMixerServer.Play("music", _long);
        AudioMixerServer.Play("ambience", _long);
        GetTree().ChangeSceneToFile(NextLevel);
        await Frames(5);
        ExpectEqual(GetTree().CurrentScene.Name, (StringName)"NextLevel", "the scene changed");
        Expect(AudioMixerServer.IsPlaying("music"), "a channel keeps playing after a scene change");
        Expect(!AudioMixerServer.IsPlaying("ambience"), "StopOnSceneChange stops the channel");
    }

    /// <summary>Saves silence of the given length as AudioStreamWav and returns the path.</summary>
    private static string SaveSilence(string fileName, double seconds)
    {
        var wav = new AudioStreamWav
        {
            Format = AudioStreamWav.FormatEnum.Format16Bits,
            MixRate = 44100,
            Data = new byte[(int)(seconds * 44100.0) * 2],
        };
        string path = DataDir.PathJoin(fileName);
        ResourceSaver.Save(wav, path);
        return path;
    }

    /// <summary>Simulates the operating system: the event goes through DisplayServer to TtsServer and the mixer.</summary>
    private static void Post(DisplayServer.TtsUtteranceEvent utteranceEvent, long id)
    {
        GodotObject displayServer = Engine.GetSingleton("DisplayServer");
        displayServer.Call("_tts_post_utterance_event", (long)utteranceEvent, id, 0);
    }

    private async Task Wait(double seconds)
    {
        await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
    }

    private async Task Frames(int count)
    {
        for (int i = 0; i < count; i++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }

    private async Task WaitForQueueFinished()
    {
        for (int i = 0; i < 100 && _queueFinished == 0; i++) // at most 5 seconds
        {
            await Wait(0.05);
        }
    }

    private async Task WaitForStarted(int count)
    {
        for (int i = 0; i < 100 && _started.Count < count; i++) // at most 5 seconds
        {
            await Wait(0.05);
        }
    }

    private void OnChannelStarted(StringName channel, long index, string streamPath)
    {
        if (channel == "playlist")
        {
            _started.Add(index);
        }
    }

    private void OnChannelFinished(StringName channel, long index)
    {
        if (channel == "playlist")
        {
            _finished.Add(index);
        }
    }

    private void OnQueueFinished(StringName channel)
    {
        if (channel == "playlist")
        {
            _queueFinished++;
        }
    }

    private void OnFadeFinished(StringName channel)
    {
        if (channel == "music")
        {
            _fadesFinished++;
        }
    }

    private void OnDuckingChanged(StringName channel, bool ducked)
    {
        _duckingEvents.Add($"{channel}:{(ducked ? "true" : "false")}");
    }
}
