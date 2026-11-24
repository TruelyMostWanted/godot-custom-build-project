using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.Issue76FeatureTtsDucking;

/// <summary>C# checks of 76-feature-tts-ducking. Check the same things as scripts/gd/check.gd.</summary>
/// <remarks>
/// The utterance events are simulated through DisplayServer (like the OS callbacks), so no speech engine is needed.
/// Real speech is only checked when TtsServer.IsAvailable() and the system has voices.
/// </remarks>
public partial class Issue76FeatureTtsDuckingCheck : FeatureCheckCs
{
    private readonly List<string> _duckingEvents = [];

    protected override string CheckName => "76-feature-tts-ducking";

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

    private async Task Wait(double seconds)
        => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);

    private async Task NextFrames(int count)
    {
        for (int i = 0; i < count; i++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }

    /// <summary>Simulates the operating system: the event goes through DisplayServer and TtsServer (deferred to the main thread).</summary>
    private static void Post(DisplayServer.TtsUtteranceEvent utteranceEvent, long id)
    {
        GodotObject displayServer = Engine.GetSingleton("DisplayServer");
        displayServer.Call("_tts_post_utterance_event", (long)utteranceEvent, id, 0);
    }

    private static bool Near(float value, float expected) => Math.Abs(value - expected) < 0.01f;

    private void OnDuckingChanged(StringName channel, bool ducked)
        => _duckingEvents.Add($"{channel}:{(ducked ? "true" : "false")}");

    protected override async Task RunChecks()
    {
        // 1. Settings on AudioMixerChannel.
        var defaults = new AudioMixerChannel();
        Expect(!defaults.DuckOnTts, "duck_on_tts is false by default");
        Expect(Near(defaults.DuckVolumeDb, -12.0f), "duck_volume_db is -12 dB by default");
        Expect(Near(defaults.DuckFadeTime, 0.25f), "duck_fade_time is 0.25 s by default");
        defaults.DuckVolumeDb = 6.0f;
        Expect(Near(defaults.DuckVolumeDb, 0.0f), "duck_volume_db is clamped to 0 dB (ducking only lowers)");

        var music = new AudioMixerChannel
        {
            ChannelName = "music",
            DuckOnTts = true,
            DuckVolumeDb = -12.0f,
            DuckFadeTime = 0.2f,
        };
        var sfx = new AudioMixerChannel { ChannelName = "sfx" };
        var layout = new AudioMixerLayout();
        layout.AddChannel(music);
        layout.AddChannel(sfx);
        AudioMixerServer.ApplyLayout(layout);
        AudioMixerServer.SetChannelVolumeDb("music", -3.0f);
        AudioMixerServer.Play("music", MakeWav(30.0));
        AudioMixerServer.Play("sfx", MakeWav(30.0));
        AudioMixerServer.DuckingChanged += OnDuckingChanged;
        Expect(AudioMixerServer.IsDuckingEnabled(), "ducking is enabled by default");
        Expect(AudioMixerServer.GetActiveUtteranceCount() == 0, "no active utterances at the start");

        // 2. Music drops by duck_volume_db while speech runs.
        Post(DisplayServer.TtsUtteranceEvent.Started, 7611);
        await NextFrames(2);
        Expect(AudioMixerServer.GetActiveUtteranceCount() == 1, "utterance_started counts one utterance");
        Expect(AudioMixerServer.IsDucked("music"), "music (duck_on_tts) is ducked while speaking");
        Expect(!AudioMixerServer.IsDucked("sfx"), "sfx (duck_on_tts = false) is not ducked");
        await Wait(0.4);
        float duckDb = AudioMixerServer.GetDuckVolumeDb("music");
        Expect(Near(duckDb, -12.0f), $"music fades down by 12 dB (got {duckDb})");
        Expect(Near(AudioMixerServer.GetChannelVolumeDb("music"), -3.0f), "the channel volume itself stays -3 dB");
        Expect(_duckingEvents.SequenceEqual(new[] { "music:true" }), $"ducking_changed(music, true) is emitted once (got {string.Join(", ", _duckingEvents)})");

        // 3. Overlapping utterances: ducked until the last one ends; Stop() restores.
        Post(DisplayServer.TtsUtteranceEvent.Started, 7612);
        Post(DisplayServer.TtsUtteranceEvent.Ended, 7611);
        await NextFrames(2);
        Expect(AudioMixerServer.IsDucked("music"), "music stays ducked while a second utterance runs");
        TtsServer.Stop();
        Post(DisplayServer.TtsUtteranceEvent.Canceled, 7612); // The platform cancels the utterance on stop().
        await NextFrames(2);
        Expect(!AudioMixerServer.IsDucked("music"), "TTSServer.stop() during speech ends the ducking");
        await Wait(0.4);
        Expect(Near(AudioMixerServer.GetDuckVolumeDb("music"), 0.0f), "music returns to its volume after speech");
        Expect(AudioMixerServer.IsPlaying("music"), "music keeps playing");
        Expect(_duckingEvents.SequenceEqual(new[] { "music:true", "music:false" }), $"ducking_changed(music, false) is emitted at the end (got {string.Join(", ", _duckingEvents)})");

        // 4. A FadeOut() during ducking ends at silence and does not jump back up.
        Post(DisplayServer.TtsUtteranceEvent.Started, 7613);
        await Wait(0.3);
        AudioMixerServer.FadeOut("music", 0.3f);
        Post(DisplayServer.TtsUtteranceEvent.Ended, 7613);
        await Wait(0.6);
        Expect(AudioMixerServer.GetVoiceCount("music") == 0, "the faded-out voice is stopped");
        Expect(!AudioMixerServer.IsPlaying("music"), "music stays silent after the speech ended");

        // 5. Global switch and manual ducking.
        AudioMixerServer.Play("music", MakeWav(30.0));
        AudioMixerServer.SetDuckingEnabled(false);
        Post(DisplayServer.TtsUtteranceEvent.Started, 7614);
        await NextFrames(2);
        Expect(!AudioMixerServer.IsDucked("music"), "no ducking while SetDuckingEnabled(false)");
        AudioMixerServer.SetDuckingEnabled(true);
        Expect(AudioMixerServer.IsDucked("music"), "enabling during speech ducks at once");
        Post(DisplayServer.TtsUtteranceEvent.Ended, 7614);
        await NextFrames(2);
        Expect(!AudioMixerServer.IsDucked("music"), "restored after the utterance ended");
        AudioMixerServer.Duck("sfx", -6.0f, 0.0f);
        Expect(AudioMixerServer.IsDucked("sfx"), "Duck() ducks any channel");
        Expect(Near(AudioMixerServer.GetDuckVolumeDb("sfx"), -6.0f), "Duck() with fade 0 applies at once");
        AudioMixerServer.Unduck("sfx", 0.0f);
        Expect(!AudioMixerServer.IsDucked("sfx"), "Unduck() restores the channel");

        // 6. Real speech, if available.
        if (TtsServer.IsAvailable() && TtsServer.GetVoices().Count > 0)
        {
            long id = TtsServer.Speak("Ducking check.");
            for (int i = 0; i < 60 && !AudioMixerServer.IsDucked("music"); i++) // At most 3 seconds until the speech starts.
            {
                await Wait(0.05);
            }
            Expect(AudioMixerServer.IsDucked("music"), $"real speech (utterance {id}) ducks music");
            for (int i = 0; i < 200 && AudioMixerServer.IsDucked("music"); i++) // At most 10 seconds until it ends.
            {
                await Wait(0.05);
            }
            Expect(!AudioMixerServer.IsDucked("music"), "music is restored after real speech");
        }
        else
        {
            GD.Print("  (skipped: text to speech is not available, real speech not checked)");
        }

        AudioMixerServer.DuckingChanged -= OnDuckingChanged;
        AudioMixerServer.SetDuckingEnabled(true);
        AudioMixerServer.StopAll();
        AudioMixerServer.ApplyLayout(new AudioMixerLayout());
    }
}
