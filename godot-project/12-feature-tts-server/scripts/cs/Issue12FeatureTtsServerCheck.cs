using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.Issue12FeatureTtsServer;

/// <summary>
/// C# checks of 12-feature-tts-server. Check the same things as scripts/gd/check.gd.
/// Most checks work without voices (headless, or with "audio/general/text_to_speech" off).
/// Real speech is only checked when TtsServer.IsAvailable() and the system has voices.
/// </summary>
public partial class Issue12FeatureTtsServerCheck : FeatureCheckCs
{
    private const string Dir = "user://12-feature-tts-server";
    private const ulong SpeechTimeoutMsec = 15000;

    private readonly List<string> _events = [];

    protected override string CheckName => "12-feature-tts-server";

    private void OnStarted(long id) => _events.Add($"started:{id}");
    private void OnEnded(long id) => _events.Add($"ended:{id}");
    private void OnCanceled(long id) => _events.Add($"canceled:{id}");
    private void OnBoundary(long id, long charPos) => _events.Add($"boundary:{id}:{charPos}");

    private static TtsPersona MakePersona(string name, string voice, string language)
    {
        return new TtsPersona { Name = name, Voice = voice, Language = language };
    }

    private static string BaseLanguage(string language)
        => language.Replace("-", "_").ToLowerInvariant().Split('_')[0];

    protected override async Task RunChecks()
    {
        Expect(Engine.HasSingleton("TtsServer"), "TtsServer is a registered singleton");
        Expect(ClassDB.ClassExists("TtsPersona"), "TtsPersona is a registered class");
        Expect(!ClassDB.CanInstantiate("TtsServer"), "TtsServer cannot be instantiated");
        TtsServer.ClearPersonas();

        CheckPersonaValues();
        CheckPersonaSaveLoad();
        CheckPersonaList();
        CheckAvailability();
        await CheckSignals();
        await CheckRealSpeech();

        TtsServer.ClearPersonas();
        DirAccess.RemoveAbsolute(Dir.PathJoin("narrator.tres"));
        DirAccess.RemoveAbsolute(Dir);
    }

    private void CheckPersonaValues()
    {
        var persona = new TtsPersona();
        ExpectEqual(persona.Name, "", "default name is empty");
        ExpectEqual(persona.Voice, "", "default voice is empty");
        ExpectEqual(persona.Language, "", "default language is empty");
        ExpectEqual(persona.Volume, 50, "default volume is 50");
        Expect(Mathf.IsEqualApprox(persona.Pitch, 1.0f), "default pitch is 1.0");
        Expect(Mathf.IsEqualApprox(persona.Rate, 1.0f), "default rate is 1.0");
        Expect(!persona.Interrupt, "default interrupt is false");

        persona.Volume = 250;
        ExpectEqual(persona.Volume, 100, "volume is clamped to 100");
        persona.Volume = -3;
        ExpectEqual(persona.Volume, 0, "volume is clamped to 0");
        persona.Pitch = 9.0f;
        Expect(Mathf.IsEqualApprox(persona.Pitch, 2.0f), "pitch is clamped to 2.0");
        persona.Rate = 0.0f;
        Expect(Mathf.IsEqualApprox(persona.Rate, 0.1f), "rate is clamped to 0.1");
        persona.Rate = 99.0f;
        Expect(Mathf.IsEqualApprox(persona.Rate, 10.0f), "rate is clamped to 10.0");

        bool changed = false;
        persona.Changed += () => changed = true;
        persona.Language = "de";
        Expect(changed, "setting a property emits changed");
    }

    private void CheckPersonaSaveLoad()
    {
        DirAccess.MakeDirRecursiveAbsolute(Dir);
        var persona = MakePersona("narrator", "Some Voice", "en_US");
        persona.Volume = 80;
        persona.Pitch = 1.25f;
        persona.Rate = 0.75f;
        persona.Interrupt = true;
        string path = Dir.PathJoin("narrator.tres");
        ExpectEqual(ResourceSaver.Save(persona, path), Error.Ok, "persona saves as .tres");

        var loaded = ResourceLoader.Load(path, "", ResourceLoader.CacheMode.Ignore) as TtsPersona;
        Expect(loaded != null, "persona loads again as TtsPersona");
        if (loaded == null)
        {
            return;
        }
        Expect(loaded != persona, "loaded persona is a new object");
        ExpectEqual(loaded.Name, "narrator", "loaded name");
        ExpectEqual(loaded.Voice, "Some Voice", "loaded voice");
        ExpectEqual(loaded.Language, "en_US", "loaded language");
        ExpectEqual(loaded.Volume, 80, "loaded volume");
        Expect(Mathf.IsEqualApprox(loaded.Pitch, 1.25f), "loaded pitch");
        Expect(Mathf.IsEqualApprox(loaded.Rate, 0.75f), "loaded rate");
        Expect(loaded.Interrupt, "loaded interrupt");
    }

    private void CheckPersonaList()
    {
        TtsPersona builtin = TtsServer.GetDefaultPersona();
        Expect(builtin != null, "a built-in default persona exists");
        ExpectEqual(TtsServer.GetDefaultPersonaName(), "", "no default persona name at start");

        var narrator = MakePersona("narrator", "", "en");
        var guard = MakePersona("guard", "", "de");
        TtsServer.AddPersona(narrator);
        TtsServer.AddPersona(guard);
        Expect(TtsServer.HasPersona("narrator") && TtsServer.HasPersona("guard"), "has_persona knows both personas");
        Expect(!TtsServer.HasPersona("king"), "has_persona is false for an unknown name");
        Expect(TtsServer.GetPersona("narrator") == narrator, "get_persona returns the added persona");
        Expect(TtsServer.GetPersona("king") == null, "get_persona returns null for an unknown name");
        ExpectEqual(TtsServer.GetPersonas().Count, 2, "get_personas returns both personas");
        Expect(TtsServer.GetPersonaNames().SequenceEqual(["narrator", "guard"]), "get_persona_names keeps the order");

        // Errors are printed, nothing crashes.
        TtsServer.AddPersona(null);
        TtsServer.AddPersona(MakePersona("", "", ""));
        TtsServer.SetDefaultPersona("king");
        ExpectEqual(TtsServer.GetPersonas().Count, 2, "invalid personas are not added");
        ExpectEqual(TtsServer.GetDefaultPersonaName(), "", "an unknown default persona is rejected");

        TtsServer.SetDefaultPersona("guard");
        ExpectEqual(TtsServer.GetDefaultPersonaName(), "guard", "set_default_persona sets the name");
        Expect(TtsServer.GetDefaultPersona() == guard, "get_default_persona returns the persona");

        var guard2 = MakePersona("guard", "", "fr");
        TtsServer.AddPersona(guard2);
        ExpectEqual(TtsServer.GetPersonas().Count, 2, "a persona with the same name replaces the old one");
        Expect(TtsServer.GetDefaultPersona() == guard2, "the default persona follows the replacement");

        TtsServer.RemovePersona("guard");
        Expect(!TtsServer.HasPersona("guard"), "remove_persona removes the persona");
        ExpectEqual(TtsServer.GetDefaultPersonaName(), "", "removing the default persona resets the default");
        Expect(TtsServer.GetDefaultPersona() == builtin, "the built-in persona is the default again");
        TtsServer.RemovePersona("does_not_exist");

        TtsServer.ClearPersonas();
        ExpectEqual(TtsServer.GetPersonas().Count, 0, "clear_personas removes all personas");
    }

    private void CheckAvailability()
    {
        bool enabled = ProjectSettings.GetSetting("audio/general/text_to_speech", false).AsBool();
        if (enabled && DisplayServer.HasFeature(DisplayServer.Feature.TextToSpeech))
        {
            Expect(TtsServer.IsAvailable(), "is_available is true with the setting and a TTS display server");
            return;
        }
        Expect(!TtsServer.IsAvailable(), "is_available is false without the setting or TTS support");
        ExpectEqual(TtsServer.GetVoices().Count, 0, "get_voices is empty when unavailable");
        Expect(!TtsServer.IsSpeaking(), "is_speaking is false when unavailable");
        Expect(!TtsServer.IsPaused(), "is_paused is false when unavailable");
        TtsServer.Pause();
        TtsServer.Resume();
        TtsServer.Stop();
        // A clear error, no crash.
        ExpectEqual(TtsServer.Speak("Hello"), -1L, "speak returns -1 when unavailable");
        ExpectEqual(TtsServer.Speak("Hello", MakePersona("x", "Voice", "en")), -1L, "speak with a persona returns -1 when unavailable");
        ExpectEqual(TtsServer.GetPendingUtterances().Length, 0, "nothing is pending when unavailable");
        ExpectEqual(TtsServer.ResolveVoice(MakePersona("x", "Some Voice", "en")), "Some Voice", "without voices, resolve_voice passes the voice on");
    }

    private void ConnectSignals()
    {
        _events.Clear();
        TtsServer.UtteranceStarted += OnStarted;
        TtsServer.UtteranceEnded += OnEnded;
        TtsServer.UtteranceCanceled += OnCanceled;
        TtsServer.UtteranceBoundary += OnBoundary;
    }

    private void DisconnectSignals()
    {
        TtsServer.UtteranceStarted -= OnStarted;
        TtsServer.UtteranceEnded -= OnEnded;
        TtsServer.UtteranceCanceled -= OnCanceled;
        TtsServer.UtteranceBoundary -= OnBoundary;
    }

    private async Task NextFrame()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private async Task CheckSignals()
    {
        // Simulate the platform: the events go through DisplayServer, like the OS callbacks.
        ConnectSignals();
        GodotObject displayServer = Engine.GetSingleton("DisplayServer");
        displayServer.Call("_tts_post_utterance_event", (long)DisplayServer.TtsUtteranceEvent.Started, 9001, 0);
        displayServer.Call("_tts_post_utterance_event", (long)DisplayServer.TtsUtteranceEvent.Boundary, 9001, 4);
        displayServer.Call("_tts_post_utterance_event", (long)DisplayServer.TtsUtteranceEvent.Ended, 9001, 0);
        displayServer.Call("_tts_post_utterance_event", (long)DisplayServer.TtsUtteranceEvent.Canceled, 9002, 0);
        Expect(_events.Count == 0, "signals are deferred, not emitted right away");
        await NextFrame();
        await NextFrame();
        string[] expected = ["started:9001", "boundary:9001:4", "ended:9001", "canceled:9002"];
        Expect(_events.SequenceEqual(expected), $"start, boundary, end and cancel arrive in order (got {string.Join(", ", _events)})");
        DisconnectSignals();
    }

    private async Task WaitForEvent(string[] eventNames, long id)
    {
        ulong start = Time.GetTicksMsec();
        while (Time.GetTicksMsec() - start < SpeechTimeoutMsec)
        {
            await NextFrame();
            if (eventNames.Any(name => _events.Contains($"{name}:{id}")))
            {
                return;
            }
        }
    }

    private async Task CheckRealSpeech()
    {
        if (!TtsServer.IsAvailable())
        {
            GD.Print("  SKIP  real speech: text to speech is not available (enable audio/general/text_to_speech)");
            return;
        }
        Godot.Collections.Array<Godot.Collections.Dictionary> voices = TtsServer.GetVoices();
        if (voices.Count == 0)
        {
            GD.Print("  SKIP  real speech: the system has no voices");
            return;
        }

        Godot.Collections.Dictionary first = voices[0];
        string firstId = first["id"].AsString();
        string firstName = first["name"].AsString();
        string language = first["language"].AsString();
        Expect(TtsServer.GetVoices(language).Count > 0, "get_voices filters by language");
        ExpectEqual(TtsServer.ResolveVoice(MakePersona("x", firstId, "")), firstId, "resolve_voice keeps an existing voice ID");
        ExpectEqual(TtsServer.ResolveVoice(MakePersona("x", firstName, "")), firstId, "resolve_voice finds a voice by name");

        // A missing voice falls back to a voice of the same language.
        var missing = MakePersona("missing", "voice-that-does-not-exist", language);
        string resolved = TtsServer.ResolveVoice(missing);
        string resolvedLanguage = "";
        foreach (Godot.Collections.Dictionary voice in voices)
        {
            if (voice["id"].AsString() == resolved)
            {
                resolvedLanguage = voice["language"].AsString();
            }
        }
        ExpectEqual(BaseLanguage(resolvedLanguage), BaseLanguage(language), "a missing voice falls back to the same language");

        ConnectSignals();
        missing.Volume = 10;
        long id = TtsServer.Speak("Hello", missing);
        Expect(id > 0, "speak returns a positive utterance ID");
        Expect(TtsServer.GetPendingUtterances().Contains(id), "the utterance is pending");
        await WaitForEvent(["ended", "canceled"], id);
        Expect(_events.Contains($"started:{id}"), "utterance_started arrives for real speech");
        Expect(_events.Contains($"ended:{id}"), "utterance_ended arrives for real speech");
        Expect(!TtsServer.GetPendingUtterances().Contains(id), "the utterance is no longer pending");

        // Stop cancels the current utterance.
        long longId = TtsServer.Speak("This is a long sentence that is stopped before it ends.", missing);
        await ToSignal(GetTree().CreateTimer(0.3), SceneTreeTimer.SignalName.Timeout);
        TtsServer.Stop();
        await WaitForEvent(["canceled", "ended"], longId);
        Expect(_events.Contains($"canceled:{longId}"), "utterance_canceled arrives after stop");
        DisconnectSignals();
    }
}
