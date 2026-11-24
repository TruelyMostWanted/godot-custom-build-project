using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.Issue29FeatureTtsPersonaSettings;

/// <summary>
/// C# checks of 29-feature-tts-persona-settings. Check the same things as scripts/gd/check.gd.
/// The checks work without voices (headless, or with "audio/general/text_to_speech" off): personas are stored
/// and applied to TtsServer even when nothing can be spoken. The restart check starts the project a second time
/// as its own process (desktop only) and reads the personas TtsServer has before the main scene is ready.
/// </summary>
public partial class Issue29FeatureTtsPersonaSettingsCheck : FeatureCheckCs
{
    private const string Dir = "user://29-feature-tts-persona-settings";
    private const string ProbeScene = "res://29-feature-tts-persona-settings/scenes/restart_probe.tscn";
    private const string PersonasFile = "user://settings/tts_personas.ini";

    protected override string CheckName => "29-feature-tts-persona-settings";

    private static TtsPersona MakePersona(string name, string voice, string language, float rate)
    {
        return new TtsPersona { PersonaName = name, Voice = voice, Language = language, Rate = rate };
    }

    protected override async Task RunChecks()
    {
        DirAccess.MakeDirRecursiveAbsolute(Dir);
        Expect(Engine.HasSingleton("TTSPersonaSettingsServer"), "TTSPersonaSettingsServer is a registered singleton");
        Expect(Engine.GetSingleton("TTSPersonaSettingsServer") is ListSettingsServer, "TTSPersonaSettingsServer is a ListSettingsServer");
        Expect(ClassDB.ClassExists("TTSPersonaSettings"), "TTSPersonaSettings is a registered class");
        Expect(ClassDB.ClassExists("TTSPersonaListSettings"), "TTSPersonaListSettings is a registered class");
        Expect(SettingsServerManager.GetServer("tts_personas") == Engine.GetSingleton("TTSPersonaSettingsServer"), "the domain is registered as \"tts_personas\"");
        var server = TtsPersonaSettingsServer.Singleton;
        Expect(server.IsStarted(), "the domain was started before the main scene");
        Expect(server is TtsPersonaSettingsServerInstance, "TtsPersonaSettingsServer.Singleton is the instance type");
        ExpectEqual((int)server.GetApplyPhase(), 1, "applied in APPLY_PHASE_SERVERS");
        ExpectEqual(server.GetFilePath(), PersonasFile, "file path");
        TtsServer.ClearPersonas();
        server.ResetToDefaults();

        CheckEntry();
        CheckOverrides();
        CheckDefaultPersona();
        CheckCsv();
        CheckMissingVoices();
        CheckRestart();

        // Leave no player files behind for later runs.
        server.FilePath = "";
        server.ResetToDefaults();
        TtsServer.ClearPersonas();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        DirAccess.RemoveAbsolute(PersonasFile);
        foreach (string fileName in DirAccess.GetFilesAt(Dir))
        {
            DirAccess.RemoveAbsolute(Dir.PathJoin(fileName));
        }
        DirAccess.RemoveAbsolute(Dir);
    }

    private void CheckEntry()
    {
        var entry = new TtsPersonaSettings();
        ExpectEqual((string)entry.PersonaName, "", "default PersonaName is empty");
        ExpectEqual(entry.Volume, 50, "default volume is 50");
        Expect(!entry.Default, "default flag is false");
        entry.Volume = 300;
        ExpectEqual(entry.Volume, 100, "entry volume is clamped like TtsPersona");
        entry.CopyFromPersona(MakePersona("guard", "Voice", "de", 0.8f));
        TtsPersona persona = entry.ToPersona();
        ExpectEqual((string)persona.PersonaName, "guard", "ToPersona() keeps the name");
        Expect(Mathf.IsEqualApprox(persona.Rate, 0.8f), "ToPersona() keeps the rate");
        persona.Rate = 2.0f;
        Expect(Mathf.IsEqualApprox(entry.Rate, 0.8f), "ToPersona() returns a copy");
    }

    private void CheckOverrides()
    {
        TtsPersona gameNarrator = MakePersona("narrator", "", "en", 1.0f);
        TtsServer.AddPersona(gameNarrator);

        TtsPersonaSettingsServer.AddPersona(MakePersona("helper", "Some Voice", "fr", 1.5f));
        Expect(TtsPersonaSettingsServer.HasPersona("helper"), "AddPersona() stores the player's persona");
        Expect(TtsServer.HasPersona("helper"), "the player's persona reaches TtsServer at once");
        Expect(TtsServer.HasPersonaOverride("helper"), "it is a persona override");
        Expect(Mathf.IsEqualApprox(TtsServer.GetPersona("helper").Rate, 1.5f), "TtsServer has the player's rate");

        TtsPersonaSettingsServer.SetPersonaValue("narrator", "rate", 2.0);
        Expect(TtsPersonaSettingsServer.HasPersona("narrator"), "changing a game persona creates the player's version");
        Expect(Mathf.IsEqualApprox(TtsServer.GetPersona("narrator").Rate, 2.0f), "TtsServer uses the player's narrator");
        ExpectEqual(TtsServer.GetPersona("narrator").Language, "en", "the other values come from the game's persona");
        Expect(TtsServer.GetBasePersona("narrator") == gameNarrator, "the game's persona stays the base persona");
        Expect(Mathf.IsEqualApprox(gameNarrator.Rate, 1.0f), "the game's persona is unchanged");

        TtsServer.AddPersona(MakePersona("narrator", "", "en", 1.0f));
        Expect(Mathf.IsEqualApprox(TtsServer.GetPersona("narrator").Rate, 2.0f), "the game adding its persona again does not hide the player's version");

        TtsPersonaSettingsServer.SetPersonaValue("narrator", "volume", 500);
        ExpectEqual(TtsPersonaSettingsServer.GetPersonaValue("narrator", "volume").AsInt32(), 100, "volume is clamped");

        TtsPersonaSettingsServer.RemovePersona("narrator");
        Expect(Mathf.IsEqualApprox(TtsServer.GetPersona("narrator").Rate, 1.0f), "removing the player's version brings the game's persona back");
        ExpectEqual(string.Join(",", TtsPersonaSettingsServer.GetPersonaNames()), "helper", "the player's persona names");
    }

    private void CheckDefaultPersona()
    {
        TtsServer.SetDefaultPersona("narrator");
        ExpectEqual((string)TtsServer.GetDefaultPersonaName(), "narrator", "the game's default persona");
        TtsPersonaSettingsServer.SetDefaultPersona("helper");
        ExpectEqual((string)TtsPersonaSettingsServer.GetDefaultPersona(), "helper", "the player's default persona is stored");
        ExpectEqual((string)TtsServer.GetDefaultPersonaName(), "helper", "the player's default persona wins over the game's");
        TtsServer.SetDefaultPersona("narrator");
        ExpectEqual((string)TtsServer.GetDefaultPersonaName(), "helper", "also when the game sets its default later");
        TtsPersonaSettingsServer.SetDefaultPersona("");
        ExpectEqual((string)TtsServer.GetDefaultPersonaName(), "narrator", "an empty name brings the game's default back");
        TtsPersonaSettingsServer.SetDefaultPersona("helper");

        // SpeakWithPersona() uses the player's persona; -1 without text to speech.
        if (!TtsServer.IsAvailable())
        {
            ExpectEqual(TtsServer.SpeakWithPersona("Hello", "helper"), -1L, "SpeakWithPersona() returns -1 without text to speech");
        }
        else
        {
            long id = TtsServer.SpeakWithPersona("Hello", "helper");
            Expect(id > 0, "SpeakWithPersona() returns an utterance id");
            TtsServer.Stop();
        }
    }

    private void CheckCsv()
    {
        var server = TtsPersonaSettingsServer.Singleton;
        ProjectSettings.SetSetting("settings/general/default_format", (int)ResourceTextSerializer.Format.Csv);
        ExpectEqual(server.GetFilePath(), "user://settings/tts_personas.csv", "the personas can be stored as CSV");
        ProjectSettings.SetSetting("settings/general/default_format", (int)ResourceTextSerializer.Format.Ini);
        // #184: only this domain as CSV, through its own group settings/tts_personas/.
        ProjectSettings.SetSetting("settings/tts_personas/override_format", true);
        ProjectSettings.SetSetting("settings/tts_personas/format", (int)ResourceTextSerializer.Format.Csv);
        ExpectEqual(server.GetFilePath(), "user://settings/tts_personas.csv", "#184: override_format stores only this domain as CSV");
        ProjectSettings.SetSetting("settings/tts_personas/override_format", false);
        ProjectSettings.SetSetting("settings/tts_personas/format", (int)ResourceTextSerializer.Format.Ini);
        ExpectEqual(server.GetFilePath(), "user://settings/tts_personas.ini", "#184: without override_format the default format is used");

        string path = Dir.PathJoin("personas_cs.csv");
        TtsPersonaSettingsServer.AddPersona(MakePersona("guard", "", "de", 0.8f));
        server.Apply();
        server.FilePath = path;
        ExpectEqual(server.Save(), Error.Ok, "Save() as CSV");
        string[] lines = FileAccess.GetFileAsString(path).StripEdges().Split("\n");
        ExpectEqual(lines.Length, 3, "CSV: a header and one row per persona");
        int helperRows = 0;
        int guardRows = 0;
        foreach (string line in lines)
        {
            helperRows += line.Contains("helper") ? 1 : 0;
            guardRows += line.Contains("guard") ? 1 : 0;
        }
        Expect(helperRows == 1 && guardRows == 1, "CSV: one row per persona");

        TtsPersonaSettingsServer.RemovePersona("guard");
        TtsPersonaSettingsServer.SetPersonaValue("helper", "rate", 3.0);
        ExpectEqual(server.Load(), Error.Ok, "Load() reads the CSV");
        server.Apply();
        Expect(TtsServer.HasPersona("guard"), "CSV: the persona is back in TtsServer");
        Expect(Mathf.IsEqualApprox(TtsServer.GetPersona("helper").Rate, 1.5f), "CSV: the stored rate is applied");
        ExpectEqual((string)TtsServer.GetDefaultPersonaName(), "helper", "CSV: the default persona is a column");
        server.FilePath = "";
    }

    private void CheckMissingVoices()
    {
        TtsPersonaSettingsServer.SetPersonaValue("guard", "voice", "A Voice Not On This Device");
        string[] missing = TtsPersonaSettingsServer.GetMissingVoicePersonas();
        if (TtsServer.GetVoices().Count == 0)
        {
            ExpectEqual(missing.Length, 0, "without a voice list no voice is reported missing");
        }
        else
        {
            Expect(System.Array.IndexOf(missing, "guard") >= 0, "a voice missing on this device is reported");
            Expect(!string.IsNullOrEmpty(TtsServer.ResolveVoiceId(TtsServer.GetPersona("guard"))), "TtsServer falls back to another voice");
        }
        Expect(TtsServer.HasPersona("guard"), "a persona with a missing voice is kept");
    }

    private void CheckRestart()
    {
        if (!OS.HasFeature("pc"))
        {
            GD.Print("  SKIP  restart check (needs a desktop platform to start a second process)");
            return;
        }
        var server = TtsPersonaSettingsServer.Singleton;
        server.Apply();
        ExpectEqual(server.Save(), Error.Ok, "Save() writes the player file");
        Expect(FileAccess.FileExists(PersonasFile), "the player file is in user://settings/");

        Godot.Collections.Dictionary probe = RunProbe();
        Expect(probe.ContainsKey("started") && probe["started"].AsBool(), "restart: the domain was started");
        var names = new System.Collections.Generic.List<string>();
        if (probe.ContainsKey("persona_names"))
        {
            foreach (Variant personaName in probe["persona_names"].AsGodotArray())
            {
                names.Add(personaName.AsString());
            }
        }
        Expect(names.Contains("helper") && names.Contains("guard"), "restart: the saved personas are in TtsServer.GetPersonas()");
        Expect(probe.ContainsKey("helper_rate") && Mathf.IsEqualApprox(probe["helper_rate"].AsSingle(), 1.5f), "restart: the saved rate is applied");
        ExpectEqual(probe.ContainsKey("default_persona") ? probe["default_persona"].AsString() : "", "helper", "restart: the saved default persona is applied");
    }

    private Godot.Collections.Dictionary RunProbe()
    {
        var output = new Godot.Collections.Array();
        string[] args = ["--headless", "--path", ProjectSettings.GlobalizePath("res://"), ProbeScene];
        int code = OS.Execute(OS.GetExecutablePath(), args, output, true);
        ExpectEqual(code, 0, "restart: the probe process ran");
        foreach (Variant chunk in output)
        {
            foreach (string line in chunk.AsString().Split('\n'))
            {
                if (line.StartsWith("CHECK29_PROBE "))
                {
                    Variant parsed = Json.ParseString(line["CHECK29_PROBE ".Length..]);
                    if (parsed.VariantType == Variant.Type.Dictionary)
                    {
                        return parsed.AsGodotDictionary();
                    }
                }
            }
        }
        Expect(false, "restart: the probe printed its result");
        return new Godot.Collections.Dictionary();
    }
}
