using System;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.TtsPersonaSettings;

using TtsPersonaSettings = Godot.TtsPersonaSettings;

/// <summary>
/// Full API test of TTS Persona Settings (TtsPersonaSettingsServer, TtsPersonaSettings, TtsPersonaListSettings). Same tests as scripts/gd/Test.gd.
/// The tests work without voices (headless, or with text to speech off): personas are stored and applied to TtsServer even when
/// nothing can be spoken. The restart test starts the project a second time as its own process (desktop only).
/// </summary>
public partial class Test : FeatureTest
{
    private const string TestDir = "user://tts-persona-settings";
    private const string ProbeScene = "res://tts-persona-settings/scenes/restart_probe.tscn";
    private const string PersonasFile = "user://settings/tts_personas.ini";
    private const string PersonasCsv = "user://settings/tts_personas.csv";
    private const string MissingVoice = "A Voice Not On This Device";

    protected override Task RunTests()
    {
        DirAccess.MakeDirRecursiveAbsolute(TestDir);
        TtsServer.ClearPersonas();
        TtsPersonaSettingsServer.Singleton.ResetToDefaults();
        TestEntry();
        TestOverrides();
        TestDefaultPersona();
        TestCsv();
        TestMissingVoices();
        TestListResource();
        TestRestart();
        TtsPersonaSettingsServer.Singleton.FilePath = "";
        TtsPersonaSettingsServer.Singleton.ResetToDefaults();
        TtsServer.ClearPersonas();
        DirAccess.RemoveAbsolute(PersonasFile);
        foreach (string fileName in DirAccess.GetFilesAt(TestDir))
        {
            DirAccess.RemoveAbsolute($"{TestDir}/{fileName}");
        }
        DirAccess.RemoveAbsolute(TestDir);
        return Task.CompletedTask;
    }

    private static TtsPersona MakePersona(string personaName, string voice, string language, float rate)
    {
        var persona = new TtsPersona();
        persona.PersonaName = personaName;
        persona.Voice = voice;
        persona.Language = language;
        persona.Rate = rate;
        return persona;
    }

    private void TestEntry()
    {
        var entry = new TtsPersonaSettings();
        ExpectEqual(entry.PersonaName, new StringName(""), "TtsPersonaSettings: the PersonaName is empty by default");
        ExpectEqual(entry.Volume, 50, "TtsPersonaSettings: the default Volume is 50");
        ExpectEqual(entry.Rate, 1.0f, "TtsPersonaSettings: the default Rate is 1");
        ExpectEqual(entry.Pitch, 1.0f, "TtsPersonaSettings: the default Pitch is 1");
        Expect(!entry.Default, "TtsPersonaSettings: Default is false");
        Expect(!entry.Interrupt, "TtsPersonaSettings: Interrupt is false");
        entry.Volume = 300;
        ExpectEqual(entry.Volume, 100, "TtsPersonaSettings: the Volume is clamped like TtsPersona");
        entry.CopyFromPersona(MakePersona("guard", "Voice", "de", 0.8f));
        TtsPersona persona = entry.ToPersona();
        ExpectEqual(persona.PersonaName, new StringName("guard"), "ToPersona keeps the name");
        ExpectEqual(persona.Voice, "Voice", "ToPersona keeps the voice");
        ExpectEqual(persona.Language, "de", "ToPersona keeps the language");
        Expect(Mathf.IsEqualApprox(persona.Rate, 0.8f), "ToPersona keeps the rate");
        persona.Rate = 2.0f;
        Expect(Mathf.IsEqualApprox(entry.Rate, 0.8f), "ToPersona returns a copy");
    }

    private void TestOverrides()
    {
        TtsPersona gameNarrator = MakePersona("narrator", "", "en", 1.0f);
        TtsServer.AddPersona(gameNarrator);
        TtsPersonaSettingsServer.AddPersona(MakePersona("helper", "Some Voice", "fr", 1.5f));
        Expect(TtsPersonaSettingsServer.HasPersona("helper"), "AddPersona stores the persona of the player");
        Expect(TtsServer.HasPersona("helper"), "the persona of the player reaches TtsServer at once");
        Expect(TtsServer.HasPersonaOverride("helper"), "it is a persona override");
        Expect(Mathf.IsEqualApprox(TtsServer.GetPersona("helper").Rate, 1.5f), "TtsServer has the rate of the player");
        ExpectEqual(TtsPersonaSettingsServer.GetPersona("helper").Language, "fr", "GetPersona returns the persona of the player");
        TtsPersonaSettingsServer.SetPersonaValue("narrator", "rate", 2.0f);
        Expect(TtsPersonaSettingsServer.HasPersona("narrator"), "changing a game persona creates the version of the player");
        Expect(Mathf.IsEqualApprox(TtsServer.GetPersona("narrator").Rate, 2.0f), "TtsServer uses the narrator of the player");
        ExpectEqual(TtsServer.GetPersona("narrator").Language, "en", "the other values come from the persona of the game");
        Expect(TtsServer.GetBasePersona("narrator") == gameNarrator, "the persona of the game stays the base persona");
        Expect(Mathf.IsEqualApprox(gameNarrator.Rate, 1.0f), "the persona of the game is unchanged");
        Expect(Mathf.IsEqualApprox((float)TtsPersonaSettingsServer.GetPersonaValue("narrator", "rate"), 2.0f), "GetPersonaValue returns the value the player hears");
        TtsServer.AddPersona(MakePersona("narrator", "", "en", 1.0f));
        Expect(Mathf.IsEqualApprox(TtsServer.GetPersona("narrator").Rate, 2.0f), "the game adding its persona again does not hide the version of the player");
        TtsPersonaSettingsServer.SetPersonaValue("narrator", "volume", 500);
        ExpectEqual((int)TtsPersonaSettingsServer.GetPersonaValue("narrator", "volume"), 100, "the volume is clamped");
        TtsPersonaSettingsServer.RemovePersona("narrator");
        Expect(Mathf.IsEqualApprox(TtsServer.GetPersona("narrator").Rate, 1.0f), "removing the version of the player brings the persona of the game back");
        string[] names = TtsPersonaSettingsServer.GetPersonaNames();
        Expect(names.AsSpan().SequenceEqual(["helper"]), "GetPersonaNames lists the personas of the player");
    }

    private void TestDefaultPersona()
    {
        TtsServer.SetDefaultPersona("narrator");
        ExpectEqual(TtsServer.GetDefaultPersonaName(), new StringName("narrator"), "the default persona of the game");
        TtsPersonaSettingsServer.SetDefaultPersona("helper");
        ExpectEqual(TtsPersonaSettingsServer.GetDefaultPersona(), new StringName("helper"), "the default persona of the player is stored");
        ExpectEqual(TtsServer.GetDefaultPersonaName(), new StringName("helper"), "the default persona of the player wins over the one of the game");
        TtsServer.SetDefaultPersona("narrator");
        ExpectEqual(TtsServer.GetDefaultPersonaName(), new StringName("helper"), "also when the game sets its default later");
        TtsPersonaSettingsServer.SetDefaultPersona("");
        ExpectEqual(TtsServer.GetDefaultPersonaName(), new StringName("narrator"), "an empty name brings the default of the game back");
        TtsPersonaSettingsServer.SetDefaultPersona("helper");
    }

    private void TestCsv()
    {
        TtsPersonaSettingsServerInstance server = TtsPersonaSettingsServer.Singleton;
        ProjectSettings.SetSetting("settings/general/default_format", (int)ResourceTextSerializer.Format.Csv);
        ExpectEqual(server.GetFilePath(), PersonasCsv, "the personas can be stored as CSV");
        ProjectSettings.SetSetting("settings/general/default_format", (int)ResourceTextSerializer.Format.Ini);
        ProjectSettings.SetSetting("settings/tts_personas/override_format", true);
        ProjectSettings.SetSetting("settings/tts_personas/format", (int)ResourceTextSerializer.Format.Csv);
        ExpectEqual(server.GetFilePath(), PersonasCsv, "override_format stores only this domain as CSV");
        ProjectSettings.SetSetting("settings/tts_personas/override_format", false);
        ProjectSettings.SetSetting("settings/tts_personas/format", (int)ResourceTextSerializer.Format.Ini);
        ExpectEqual(server.GetFilePath(), PersonasFile, "without override_format the default format is used");
        string path = $"{TestDir}/personas.csv";
        TtsPersonaSettingsServer.AddPersona(MakePersona("guard", "", "de", 0.8f));
        server.Apply();
        server.FilePath = path;
        ExpectEqual(server.Save(), Error.Ok, "Save writes the CSV");
        string[] lines = FileAccess.GetFileAsString(path).StripEdges().Split('\n');
        ExpectEqual(lines.Length, 3, "CSV: a header and one row per persona");
        TtsPersonaSettingsServer.RemovePersona("guard");
        TtsPersonaSettingsServer.SetPersonaValue("helper", "rate", 3.0f);
        ExpectEqual(server.Load(), Error.Ok, "Load reads the CSV");
        server.Apply();
        Expect(TtsServer.HasPersona("guard"), "CSV: the persona is back in TtsServer");
        Expect(Mathf.IsEqualApprox(TtsServer.GetPersona("helper").Rate, 1.5f), "CSV: the stored rate is applied");
        ExpectEqual(TtsServer.GetDefaultPersonaName(), new StringName("helper"), "CSV: the default persona is a column");
        server.FilePath = "";
    }

    private void TestMissingVoices()
    {
        TtsPersonaSettingsServer.SetPersonaValue("guard", "voice", MissingVoice);
        string[] missing = TtsPersonaSettingsServer.GetMissingVoicePersonas();
        if (TtsServer.GetVoices().Count == 0)
        {
            Expect(missing.Length == 0, "without a voice list no voice is reported missing");
        }
        else
        {
            Expect(System.Array.IndexOf(missing, "guard") >= 0, "a voice missing on this device is reported");
        }
        Expect(TtsServer.HasPersona("guard"), "a persona with a missing voice is kept");
    }

    private void TestListResource()
    {
        var list = new TtsPersonaListSettings();
        Expect(list.Personas.Count == 0, "TtsPersonaListSettings: no personas by default");
        var entry = new TtsPersonaSettings();
        entry.PersonaName = "narrator";
        list.Personas.Add(entry);
        ExpectEqual(list.Personas.Count, 1, "TtsPersonaListSettings: Personas holds the entries");
        Expect(TtsPersonaSettingsServer.Singleton.GetSettings() is TtsPersonaListSettings, "the settings resource is a TtsPersonaListSettings");
    }

    private void TestRestart()
    {
        if (!OS.HasFeature("pc"))
        {
            GD.Print("  SKIP  restart test (needs a desktop platform to start a second process)");
            return;
        }
        TtsPersonaSettingsServer.Singleton.Apply();
        ExpectEqual(TtsPersonaSettingsServer.Singleton.Save(), Error.Ok, "Save writes the player file");
        string[] args = ["--headless", "--path", ProjectSettings.GlobalizePath("res://"), ProbeScene];
        var output = new Godot.Collections.Array();
        int code = OS.Execute(OS.GetExecutablePath(), args, output, true);
        ExpectEqual(code, 0, "restart: the probe process ran");
        var probe = new Godot.Collections.Dictionary();
        foreach (string line in output[0].AsString().Split('\n'))
        {
            if (line.StartsWith("PROBE "))
            {
                probe = Json.ParseString(line["PROBE ".Length..]).AsGodotDictionary();
            }
        }
        Godot.Collections.Array names = (Godot.Collections.Array)probe["persona_names"];
        Expect(names.Contains("helper"), "restart: the saved helper persona is in TtsServer");
        Expect(names.Contains("guard"), "restart: the saved guard persona is in TtsServer");
        ExpectEqual((float)probe["helper_rate"], 1.5f, "restart: the saved rate is applied");
        ExpectEqual((string)probe["default_persona"], "helper", "restart: the saved default persona is applied");
    }
}
