using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.TextToSpeech;

/// <summary>
/// Full API test of Text to Speech (TtsServer, TtsPersona). Same tests as scripts/gd/Test.gd.
/// Most tests work without voices (headless, or with "audio/general/text_to_speech" off); the events are simulated
/// through DisplayServer like the OS callbacks. Real speech is only tested when TtsServer.IsAvailable() and the system has voices.
/// </summary>
public partial class Test : FeatureTest
{
    private const ulong SpeechTimeoutMsec = 15000;

    private readonly List<string> _events = [];

    protected override async Task RunTests()
    {
        TtsServer.ClearPersonas();
        TtsServer.UtteranceStarted += id => _events.Add($"started {id}");
        TtsServer.UtteranceEnded += id => _events.Add($"ended {id}");
        TtsServer.UtteranceCanceled += id => _events.Add($"canceled {id}");
        TtsServer.UtteranceBoundary += (id, charPos) => _events.Add($"boundary {id} {charPos}");
        TestPersonaValues();
        TestPersonaSaveLoad();
        TestPersonaList();
        TestPersonaOverrides();
        TestUnavailable();
        await TestSignals();
        await TestRealSpeech();
        TtsServer.ClearPersonas();
    }

    private void TestPersonaValues()
    {
        var persona = new TtsPersona();
        ExpectEqual(persona.PersonaName, (StringName)"", "PersonaName is empty by default");
        ExpectEqual(persona.Voice, "", "Voice is empty by default");
        ExpectEqual(persona.Language, "", "Language is empty by default");
        ExpectEqual(persona.Volume, 50, "Volume is 50 by default");
        ExpectEqual(persona.Pitch, 1.0f, "Pitch is 1 by default");
        ExpectEqual(persona.Rate, 1.0f, "Rate is 1 by default");
        Expect(!persona.Interrupt, "Interrupt is false by default");
        persona.Volume = 250;
        ExpectEqual(persona.Volume, 100, "Volume is clamped to 100");
        persona.Volume = -3;
        ExpectEqual(persona.Volume, 0, "Volume is clamped to 0");
        persona.Pitch = 9.0f;
        ExpectEqual(persona.Pitch, 2.0f, "Pitch is clamped to 2");
        persona.Rate = 0.0f;
        Expect(Mathf.IsEqualApprox(persona.Rate, 0.1f), "Rate is clamped to 0.1");
        persona.Rate = 99.0f;
        ExpectEqual(persona.Rate, 10.0f, "Rate is clamped to 10");
        var changed = new List<bool>();
        persona.Changed += () => changed.Add(true);
        persona.Language = "de";
        Expect(changed.SequenceEqual([true]), "setting a property emits Changed");
    }

    private void TestPersonaSaveLoad()
    {
        TtsPersona persona = Persona("narrator", "en_US");
        persona.Voice = "Some Voice";
        persona.Volume = 80;
        persona.Pitch = 1.25f;
        persona.Rate = 0.75f;
        persona.Interrupt = true;
        const string path = "user://text_to_speech_narrator.tres";
        ExpectEqual(ResourceSaver.Save(persona, path), Error.Ok, "a persona saves as .tres");
        var loaded = ResourceLoader.Load<TtsPersona>(path, "", ResourceLoader.CacheMode.Ignore);
        ExpectEqual(loaded.PersonaName, (StringName)"narrator", "PersonaName survives saving");
        ExpectEqual(loaded.Voice, "Some Voice", "Voice survives saving");
        ExpectEqual(loaded.Language, "en_US", "Language survives saving");
        ExpectEqual(loaded.Volume, 80, "Volume survives saving");
        ExpectEqual(loaded.Pitch, 1.25f, "Pitch survives saving");
        ExpectEqual(loaded.Rate, 0.75f, "Rate survives saving");
        Expect(loaded.Interrupt, "Interrupt survives saving");
    }

    private void TestPersonaList()
    {
        TtsPersona builtin = TtsServer.GetDefaultPersona();
        Expect(builtin != null, "a built-in default persona exists");
        ExpectEqual(TtsServer.GetDefaultPersonaName(), (StringName)"", "there is no default persona name at the start");
        TtsPersona narrator = Persona("narrator", "en");
        TtsPersona guard = Persona("guard", "de");
        TtsServer.AddPersona(narrator);
        TtsServer.AddPersona(guard);
        Expect(TtsServer.HasPersona("guard"), "HasPersona finds an added persona");
        Expect(!TtsServer.HasPersona("king"), "HasPersona is false for an unknown name");
        Expect(TtsServer.GetPersona("narrator") == narrator, "GetPersona returns the added persona");
        Expect(TtsServer.GetPersona("king") == null, "GetPersona returns null for an unknown name");
        Expect(TtsServer.GetPersonas().SequenceEqual([narrator, guard]), "GetPersonas returns the personas in order");
        Expect(TtsServer.GetPersonaNames().SequenceEqual(["narrator", "guard"]), "GetPersonaNames keeps the order");
        TtsServer.AddPersona(null);
        TtsServer.AddPersona(Persona("", ""));
        ExpectEqual(TtsServer.GetPersonas().Count, 2, "a null persona or one without a name is not added");
        TtsServer.SetDefaultPersona("king");
        ExpectEqual(TtsServer.GetDefaultPersonaName(), (StringName)"", "an unknown default persona is rejected");
        TtsServer.SetDefaultPersona("guard");
        ExpectEqual(TtsServer.GetDefaultPersonaName(), (StringName)"guard", "SetDefaultPersona sets the name");
        Expect(TtsServer.GetDefaultPersona() == guard, "GetDefaultPersona returns that persona");
        TtsPersona guard2 = Persona("guard", "fr");
        TtsServer.AddPersona(guard2);
        ExpectEqual(TtsServer.GetPersonas().Count, 2, "a persona with the same name replaces the old one");
        Expect(TtsServer.GetDefaultPersona() == guard2, "the default persona follows the replacement");
        TtsServer.RemovePersona("guard");
        Expect(!TtsServer.HasPersona("guard"), "RemovePersona removes the persona");
        Expect(TtsServer.GetDefaultPersona() == builtin, "removing the default persona brings back the built-in one");
        TtsServer.ClearPersonas();
        Expect(TtsServer.GetPersonas().Count == 0, "ClearPersonas removes all personas");
    }

    private void TestPersonaOverrides()
    {
        TtsPersona narrator = Persona("narrator", "en");
        TtsServer.AddPersona(narrator);
        TtsPersona faster = Persona("narrator", "en");
        faster.Rate = 1.5f;
        TtsServer.SetPersonaOverride(faster);
        Expect(TtsServer.HasPersonaOverride("narrator"), "HasPersonaOverride finds the override");
        Expect(TtsServer.GetPersonaOverride("narrator") == faster, "GetPersonaOverride returns the override");
        Expect(TtsServer.GetPersona("narrator") == faster, "GetPersona returns the override first");
        Expect(TtsServer.GetBasePersona("narrator") == narrator, "GetBasePersona returns the persona of the game");
        TtsPersona own = Persona("own", "de");
        TtsServer.SetPersonaOverride(own);
        Expect(TtsServer.GetPersonaOverrides().SequenceEqual([faster, own]), "GetPersonaOverrides returns the overrides in order");
        TtsServer.SetDefaultPersonaOverride("own");
        ExpectEqual(TtsServer.GetDefaultPersonaOverride(), (StringName)"own", "GetDefaultPersonaOverride returns the player's default");
        Expect(TtsServer.GetDefaultPersona() == own, "the player's default persona comes first");
        TtsServer.RemovePersonaOverride("narrator");
        Expect(TtsServer.GetPersona("narrator") == narrator, "RemovePersonaOverride brings back the persona of the game");
        TtsServer.ClearPersonaOverrides();
        Expect(TtsServer.GetPersonaOverrides().Count == 0, "ClearPersonaOverrides removes all overrides");
        ExpectEqual(TtsServer.GetDefaultPersonaOverride(), (StringName)"", "ClearPersonaOverrides removes the player's default");
        TtsServer.ClearPersonas();
    }

    private void TestUnavailable()
    {
        if (TtsServer.IsAvailable())
        {
            Expect(DisplayServer.HasFeature(DisplayServer.Feature.TextToSpeech), "IsAvailable needs a display server with speech");
            return;
        }
        Expect(TtsServer.GetVoices().Count == 0, "GetVoices is empty when unavailable");
        Expect(!TtsServer.IsSpeaking(), "IsSpeaking is false when unavailable");
        TtsServer.Pause();
        Expect(!TtsServer.IsPaused(), "IsPaused stays false when unavailable");
        TtsServer.Resume();
        TtsServer.Stop();
        ExpectEqual(TtsServer.Speak("Hello"), -1L, "Speak returns -1 when unavailable");
        ExpectEqual(TtsServer.SpeakWithPersona("Hello", "unknown"), -1L, "SpeakWithPersona returns -1 when unavailable");
        Expect(TtsServer.GetPendingUtterances().Length == 0, "nothing is pending when unavailable");
        TtsPersona persona = Persona("x", "en");
        persona.Voice = "Some Voice";
        ExpectEqual(TtsServer.ResolveVoiceId(persona), "Some Voice", "without voices, ResolveVoiceId passes the voice on");
    }

    private async Task TestSignals()
    {
        _events.Clear();
        GodotObject displayServer = Engine.GetSingleton("DisplayServer");
        displayServer.Call("_tts_post_utterance_event", (long)DisplayServer.TtsUtteranceEvent.Started, 9001, 0);
        displayServer.Call("_tts_post_utterance_event", (long)DisplayServer.TtsUtteranceEvent.Boundary, 9001, 4);
        displayServer.Call("_tts_post_utterance_event", (long)DisplayServer.TtsUtteranceEvent.Ended, 9001, 0);
        displayServer.Call("_tts_post_utterance_event", (long)DisplayServer.TtsUtteranceEvent.Canceled, 9002, 0);
        Expect(_events.Count == 0, "the utterance signals are deferred to the main thread");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Expect(_events.SequenceEqual(["started 9001", "boundary 9001 4", "ended 9001", "canceled 9002"]), "start, boundary, end and cancel arrive in order");
    }

    private async Task TestRealSpeech()
    {
        if (!TtsServer.IsAvailable() || TtsServer.GetVoices().Count == 0)
        {
            GD.Print("  SKIP  real speech: text to speech is not available or the system has no voices");
            return;
        }
        Godot.Collections.Dictionary first = TtsServer.GetVoices()[0];
        string firstId = (string)first["id"];
        string language = (string)first["language"];
        Expect(TtsServer.GetVoices(language).Count > 0, "GetVoices filters by language");
        TtsPersona byId = Persona("x", "");
        byId.Voice = firstId;
        ExpectEqual(TtsServer.ResolveVoiceId(byId), firstId, "ResolveVoiceId keeps an existing voice ID");
        TtsPersona byName = Persona("x", "");
        byName.Voice = (string)first["name"];
        ExpectEqual(TtsServer.ResolveVoiceId(byName), firstId, "ResolveVoiceId finds a voice by name");
        TtsPersona missing = Persona("missing", language);
        missing.Voice = "voice-that-does-not-exist";
        missing.Volume = 10;
        Expect(TtsServer.ResolveVoiceId(missing) != "", "a missing voice falls back to a voice of the language");
        AudioMixerServer.AddChannel(new AudioMixerChannel { ChannelName = "music", DuckOnTts = true });
        _events.Clear();
        long id = TtsServer.Speak("Hello", missing);
        Expect(TtsServer.GetPendingUtterances().Contains(id), "Speak returns a pending utterance ID");
        await WaitForEvent(["started"], id);
        Expect(AudioMixerServer.IsDucked("music"), "real speech ducks a mixer channel with DuckOnTts");
        await WaitForEvent(["ended", "canceled"], id);
        Expect(_events.Contains($"ended {id}"), "UtteranceEnded arrives for real speech");
        await ToSignal(GetTree().CreateTimer(0.1), SceneTreeTimer.SignalName.Timeout);
        Expect(!AudioMixerServer.IsDucked("music"), "the channel is restored after real speech");
        long longId = TtsServer.Speak("This is a long sentence that is stopped before it ends.", missing);
        await ToSignal(GetTree().CreateTimer(0.3), SceneTreeTimer.SignalName.Timeout);
        TtsServer.Stop();
        await WaitForEvent(["canceled", "ended"], longId);
        Expect(_events.Contains($"canceled {longId}"), "Stop cancels the current utterance");
        AudioMixerServer.RemoveChannel("music");
    }

    private async Task WaitForEvent(string[] eventNames, long id)
    {
        ulong start = Time.GetTicksMsec();
        while (Time.GetTicksMsec() - start < SpeechTimeoutMsec)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (eventNames.Any(eventName => _events.Contains($"{eventName} {id}")))
            {
                return;
            }
        }
    }

    private static TtsPersona Persona(string personaName, string language)
    {
        return new TtsPersona { PersonaName = personaName, Language = language };
    }
}
