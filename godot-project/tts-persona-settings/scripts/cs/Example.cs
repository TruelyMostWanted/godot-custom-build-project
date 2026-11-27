using Godot;

namespace TruelyInteractive.TtsPersonaSettings;

/// <summary>Minimal example of TTS Persona Settings: the snippets of the wiki guide, in order. Same as scripts/gd/Example.gd.</summary>
public partial class Example : Node
{
    public override void _Ready()
    {
        // The personas of the game: add them to TtsServer as usual.
        var narrator = new TtsPersona();
        narrator.PersonaName = "narrator";
        TtsServer.AddPersona(narrator);

        // Using the API: set the value of a game persona, and the default persona of the player.
        TtsPersonaSettingsServer.SetPersonaValue("narrator", "rate", 1.5f);
        TtsPersonaSettingsServer.SetDefaultPersona("narrator");

        // Using the API: get the value the player hears, and the persona TtsServer uses.
        GD.Print(TtsPersonaSettingsServer.GetPersonaValue("narrator", "rate"));
        GD.Print(TtsServer.GetPersona("narrator").Rate);
        GD.Print(TtsServer.GetBasePersona("narrator").Rate);
        GD.Print(TtsPersonaSettingsServer.GetDefaultPersona());

        // Using the API: apply, save and load (methods of the base class go through Singleton).
        TtsPersonaSettingsServer.Singleton.Apply();
        TtsPersonaSettingsServer.Singleton.Save();
        TtsPersonaSettingsServer.Singleton.Load();
        TtsPersonaSettingsServer.Singleton.Apply();

        // A persona button: needs a player who picks a persona, so it is not run here.

        // Leave no player file behind.
        TtsPersonaSettingsServer.Singleton.ResetToDefaults();
        TtsPersonaSettingsServer.Singleton.Save();
        TtsServer.ClearPersonas();
        GetTree().Quit();
    }
}
