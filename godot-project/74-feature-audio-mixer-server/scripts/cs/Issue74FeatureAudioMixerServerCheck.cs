using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.Issue74FeatureAudioMixerServer;

/// <summary>C# checks of 74-feature-audio-mixer-server. Check the same things as scripts/gd/check.gd.</summary>
/// <remarks>The checks run headless with the dummy audio driver: they check state, queue, volumes and fades, not sound.</remarks>
public partial class Issue74FeatureAudioMixerServerCheck : FeatureCheckCs
{
    private const string SceneB = "res://74-feature-audio-mixer-server/packed-scenes/scene_b.tscn";

    private readonly List<long> _started = [];
    private int _queueFinished;
    private int _fadesFinished;

    protected override string CheckName => "74-feature-audio-mixer-server";

    /// <summary>A silent 16-bit mono WAV of the given length.</summary>
    private static AudioStreamWav MakeWav(double seconds)
    {
        var data = new byte[(int)(seconds * 44100.0) * 2];
        return new AudioStreamWav
        {
            Format = AudioStreamWav.FormatEnum.Format16Bits,
            MixRate = 44100,
            Stereo = false,
            Data = data,
        };
    }

    private static AudioMixerChannel MakeChannel(StringName channelName)
        => new() { ChannelName = channelName };

    private async Task Wait(double seconds)
        => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);

    private async Task NextFrames(int count)
    {
        for (int i = 0; i < count; i++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }

    private void OnChannelStarted(StringName channel, long index, string streamPath)
    {
        if (channel == "playlist")
        {
            _started.Add(index);
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

    protected override async Task RunChecks()
    {
        Expect(Engine.HasSingleton("AudioMixerServer"), "AudioMixerServer is a registered singleton");

        // Layout with four channels.
        var layout = new AudioMixerLayout();
        var music = MakeChannel("music");
        music.DefaultFadeTime = 0.3f;
        layout.AddChannel(music);
        var ambience = MakeChannel("ambience");
        ambience.PauseWithTree = true;
        ambience.StopOnSceneChange = true;
        layout.AddChannel(ambience);
        layout.AddChannel(MakeChannel("playlist"));
        var sfx = MakeChannel("sfx");
        sfx.Polyphony = 2;
        layout.AddChannel(sfx);
        AudioMixerServer.LoadLayout(layout);
        Expect(AudioMixerServer.GetChannelNames().SequenceEqual(["music", "ambience", "playlist", "sfx"]), "load_layout adds the channels in order");
        Expect(AudioMixerServer.GetChannel("ambience") == ambience, "get_channel returns the definition");

        AudioMixerServer.ChannelStarted += OnChannelStarted;
        AudioMixerServer.QueueFinished += OnQueueFinished;
        AudioMixerServer.FadeFinished += OnFadeFinished;

        // 1. A queue of three entries plays in order, then finishes.
        for (int i = 0; i < 3; i++)
        {
            AudioMixerServer.Enqueue("playlist", MakeWav(0.1));
        }
        Expect(AudioMixerServer.GetQueue("playlist").Length == 3, "the queue has three entries");
        AudioMixerServer.Play("playlist");
        for (int i = 0; i < 100 && _queueFinished == 0; i++) // At most 5 seconds.
        {
            await Wait(0.05);
        }
        Expect(_started.SequenceEqual([0L, 1L, 2L]), $"the queue entries start in order (got {string.Join(",", _started)})");
        ExpectEqual(_queueFinished, 1, "queue_finished is emitted after the last entry");
        Expect(!AudioMixerServer.IsPlaying("playlist"), "the channel stops after the last entry");

        // 2. next(), previous() and change_index() jump with a cross-fade.
        for (int i = 0; i < 3; i++)
        {
            AudioMixerServer.Enqueue("music", MakeWav(30.0));
        }
        AudioMixerServer.Play("music");
        ExpectEqual(AudioMixerServer.GetIndex("music"), 0, "play() starts at the first entry");
        AudioMixerServer.Next("music");
        ExpectEqual(AudioMixerServer.GetIndex("music"), 1, "next() moves to entry 1");
        ExpectEqual(AudioMixerServer.GetVoiceCount("music"), 2, "next() cross-fades: two voices");
        Expect(AudioMixerServer.IsFading("music"), "next() fades");
        await Wait(0.6);
        ExpectEqual(AudioMixerServer.GetVoiceCount("music"), 1, "the old voice is stopped after the cross-fade");
        Expect(Mathf.IsEqualApprox(AudioMixerServer.GetFadeGain("music"), 1.0f), "the new voice is at full volume");
        AudioMixerServer.Previous("music");
        ExpectEqual(AudioMixerServer.GetIndex("music"), 0, "previous() moves to entry 0");
        AudioMixerServer.ChangeIndex("music", 2, 0.2f);
        ExpectEqual(AudioMixerServer.GetIndex("music"), 2, "change_index() moves to entry 2");
        await Wait(0.5);
        ExpectEqual(AudioMixerServer.GetVoiceCount("music"), 1, "one voice after change_index()");

        // 3. Equal-power cross-fade and fade-out.
        _fadesFinished = 0;
        AudioMixerServer.CrossFade("music", MakeWav(30.0), 0.4f);
        ExpectEqual(AudioMixerServer.GetVoiceCount("music"), 2, "cross_fade() keeps the old voice while fading");
        await Wait(0.2);
        // Half way, an equal-power fade is at sin(PI / 4) = 0.71 (a linear one would be at 0.5).
        float gain = AudioMixerServer.GetFadeGain("music");
        Expect(gain > 0.6f && gain < 0.9f, $"equal power: the incoming voice is at about 0.71 half way (got {gain})");
        await Wait(0.4);
        ExpectEqual(AudioMixerServer.GetVoiceCount("music"), 1, "one voice after the cross-fade");
        Expect(_fadesFinished >= 1, "fade_finished is emitted after the cross-fade");
        _fadesFinished = 0;
        AudioMixerServer.FadeOut("music", 0.3f);
        Expect(!AudioMixerServer.IsPlaying("music"), "fade_out() ends the current voice");
        await Wait(0.6);
        ExpectEqual(AudioMixerServer.GetVoiceCount("music"), 0, "the voice is stopped after fade_out()");
        ExpectEqual(_fadesFinished, 1, "fade_finished is emitted after fade_out()");

        // 4. Channel volume fade and one-shots.
        AudioMixerServer.SetChannelVolumeDb("sfx", -12.0f, 0.3f);
        Expect(AudioMixerServer.IsFading("sfx"), "the channel volume fades");
        await Wait(0.5);
        Expect(Mathf.IsEqualApprox(AudioMixerServer.GetChannelVolumeDb("sfx"), -12.0f), "the channel volume reaches -12 dB");
        for (int i = 0; i < 3; i++)
        {
            AudioMixerServer.PlayOneShot("sfx", MakeWav(5.0));
        }
        ExpectEqual(AudioMixerServer.GetVoiceCount("sfx"), 2, "one-shots are limited by polyphony");
        AudioMixerServer.Stop("sfx");
        ExpectEqual(AudioMixerServer.GetVoiceCount("sfx"), 0, "stop() stops the one-shots");

        // 5. pause_with_tree.
        AudioMixerServer.Play("music", MakeWav(30.0));
        AudioMixerServer.Play("ambience", MakeWav(30.0));
        GetTree().Paused = true;
        await NextFrames(2);
        Expect(!AudioMixerServer.IsPaused("music"), "music (pause_with_tree = false) keeps playing while the tree is paused");
        Expect(AudioMixerServer.IsPaused("ambience"), "ambience (pause_with_tree = true) pauses with the tree");
        GetTree().Paused = false;
        await NextFrames(2);
        Expect(!AudioMixerServer.IsPaused("ambience"), "ambience resumes with the tree");
        Expect(AudioMixerServer.IsPlaying("ambience"), "ambience still has its voice");

        // 6. Music keeps playing across change_scene_to_file().
        var sceneA = new Node { Name = "SceneA" };
        GetTree().Root.AddChild(sceneA);
        GetTree().CurrentScene = sceneA; // This check node is no longer the current scene, so it survives.
        AudioMixerServer.Play("music", MakeWav(30.0));
        AudioMixerServer.Play("ambience", MakeWav(30.0));
        ExpectEqual(GetTree().ChangeSceneToFile(SceneB), Error.Ok, "change_scene_to_file() to scene B");
        await NextFrames(5);
        Expect(GetTree().CurrentScene?.Name == "SceneB", "scene B is the current scene");
        Expect(AudioMixerServer.IsPlaying("music"), "music keeps playing after the scene change");
        Expect(!AudioMixerServer.IsPlaying("ambience"), "ambience (stop_on_scene_change) stops after the scene change");

        AudioMixerServer.ChannelStarted -= OnChannelStarted;
        AudioMixerServer.QueueFinished -= OnQueueFinished;
        AudioMixerServer.FadeFinished -= OnFadeFinished;
        AudioMixerServer.StopAll();
        AudioMixerServer.LoadLayout(new AudioMixerLayout());
    }
}
