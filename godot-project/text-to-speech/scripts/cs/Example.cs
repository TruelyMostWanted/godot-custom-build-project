using Godot;

namespace TruelyInteractive.TextToSpeech;

/// <summary>
/// Minimal example of Text to Speech: the snippets of the wiki guide, in order. Same as scripts/gd/Example.gd.
/// Headless runs have no speech: Speak() would print an error, so every Speak() is behind IsAvailable().
/// </summary>
public partial class Example : Node
{
    private const string Res = "res://text-to-speech/resources/";

    public override async void _Ready()
    {
        // Give characters their own voice.
        TtsServer.AddPersona(GD.Load<TtsPersona>(Res + "narrator.tres"));
        TtsServer.AddPersona(GD.Load<TtsPersona>(Res + "guard.tres"));
        TtsServer.SetDefaultPersona("narrator");
        if (TtsServer.IsAvailable())
        {
            TtsServer.Speak("Welcome!");
            TtsServer.Speak("Halt!", TtsServer.GetPersona("guard"));
        }
        GD.Print(string.Join(", ", TtsServer.GetPersonaNames()));

        // Speak a line.
        if (!TtsServer.IsAvailable())
        {
            GD.PushWarning("Text to speech is off or not supported here.");
        }
        var narrator = new TtsPersona
        {
            PersonaName = "narrator",
            Language = "en",
            Volume = 70,
            Rate = 0.9f,
        };
        ResourceSaver.Save(narrator, "user://narrator.tres");
        long id = -1;
        if (TtsServer.IsAvailable())
        {
            id = TtsServer.Speak("Once upon a time...", narrator);
        }
        if (id == -1)
        {
            GD.Print("Not spoken.");
        }

        // Choose a voice.
        foreach (Godot.Collections.Dictionary voice in TtsServer.GetVoices("de"))
        {
            GD.Print(voice["name"], " (", voice["language"], ")");
        }
        var guard = new TtsPersona { Voice = "Microsoft Hedda Desktop", Language = "de" };
        GD.Print(TtsServer.ResolveVoiceId(guard));

        // Highlight the spoken word.
        TtsServer.UtteranceBoundary += (utteranceId, charPos) => GetNode<Label>("Subtitle").VisibleCharacters = (int)charPos;
        TtsServer.UtteranceEnded += utteranceId => GD.Print("Done: ", utteranceId);

        // Lower the music while speaking (the mixer channel "music" is created first).
        AudioMixerServer.AddChannel(new AudioMixerChannel { ChannelName = "music" });
        var music = AudioMixerServer.GetChannel("music");
        music.DuckOnTts = true;
        music.DuckVolumeDb = -12.0f;
        if (TtsServer.IsAvailable())
        {
            TtsServer.Speak("The bridge is out. Take the north road.");
        }
        GD.Print("Music ducks on speech: ", AudioMixerServer.GetChannel("music").DuckOnTts);

        // Let the player change the voices.
        TtsServer.AddPersona(narrator);
        TtsPersonaSettingsServer.SetPersonaValue("narrator", "rate", 1.5);
        if (TtsServer.IsAvailable())
        {
            TtsServer.SpeakWithPersona("Welcome!", "narrator");
            TtsServer.SpeakWithPersona("Halt!", "unknown");
        }
        GD.Print("Player's rate: ", TtsServer.GetPersona("narrator").Rate, ", game's rate: ", TtsServer.GetBasePersona("narrator").Rate);

        // Clean up: the player's change is saved with the settings.
        TtsPersonaSettingsServer.RemovePersona("narrator");
        TtsServer.ClearPersonas();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        GetTree().Quit();
    }
}
