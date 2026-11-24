using System;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.Issue15FeatureSettingsServer;

/// <summary>
/// C# checks of 15-feature-settings-server. Check the same things as scripts/gd/check.gd.
/// The domains "check15_gd" (GDScript) and "check15_cs" (C#) are listed in the project setting
/// settings/startup/custom_servers. The restart check starts the project a second time as its own
/// process (desktop only) and reads what was applied before the first frame.
/// </summary>
public partial class Issue15FeatureSettingsServerCheck : FeatureCheckCs
{
    private const string Dir = "user://15-feature-settings-server-cs";
    private const string ProbeScene = "res://15-feature-settings-server/scenes/restart_probe.tscn";
    private const string PlayerFile = "user://settings/check15_cs.ini";

    protected override string CheckName => "15-feature-settings-server";

    protected override async Task RunChecks()
    {
        DirAccess.MakeDirRecursiveAbsolute(Dir);
        Expect(Engine.HasSingleton("SettingsServerManager"), "SettingsServerManager is a registered singleton");
        Expect(SettingsServerManager.Singleton is SettingsServerManagerInstance, "SettingsServerManager.Singleton is the instance type");
        Expect(ClassDB.CanInstantiate("SettingsServer"), "SettingsServer is a virtual class that C# extends");
        ExpectEqual(SettingsServerManager.GetStartupPhase(), SettingsServer.ApplyPhase.Scene, "all startup phases ran before the main scene");
        ExpectEqual(SettingsServerManager.GetStorageFormat(), ResourceTextSerializer.Format.Ini, "default storage format is ResourceTextSerializer.Format.Ini");
        Expect(!SettingsServerManager.HasDirtyServers(), "HasDirtyServers() is false after startup");
        Expect(SettingsServerManager.GetServer("check15_gd") is SettingsServer, "the GDScript domain is a SettingsServer in C#");

        var server = SettingsServerManager.GetServer("check15_cs") as Issue15CheckDomainCs;
        if (!Expect(server != null, "the C# domain from settings/startup/custom_servers is registered as Issue15CheckDomainCs"))
        {
            return;
        }
        Expect(server.IsStarted(), "the C# domain was started before the main scene");
        ExpectEqual(server.GetFilePath(), PlayerFile, "default file path");
        ExpectEqual(string.Join(",", server.GetSettingNames()), "Volume,Quality", "setting names come from the C# resource");

        CheckValidation();
        CheckRevert();
        CheckFormats();
        CheckBrokenFile();
        await CheckConfirmation();
        await CheckRestart(server);

        foreach (string file in DirAccess.GetFilesAt(Dir))
        {
            DirAccess.RemoveAbsolute(Dir.PathJoin(file));
        }
        DirAccess.RemoveAbsolute(Dir);
    }

    private static Issue15CheckDomainCs NewDomain(string path)
    {
        var domain = new Issue15CheckDomainCs();
        domain.ApplyMode = SettingsServer.ApplyModeEnum.Manual;
        domain.FilePath = path;
        return domain;
    }

    private void CheckValidation()
    {
        var domain = NewDomain(Dir.PathJoin("validation.ini"));
        domain.SetValue("Volume", 4.0);
        Expect(Mathf.IsEqualApprox(domain.GetValue("Volume").AsDouble(), 1.0), "a range hint clamps the value");
        domain.SetValue("Quality", 9);
        ExpectEqual(domain.GetValue("Quality").AsInt32(), 1, "an unknown enum value is rejected (an error is printed)");
        Expect(Mathf.IsEqualApprox(domain.GetDefaultValue("Volume").AsDouble(), 0.5), "default value from the resource");
        domain.Free();
    }

    private void CheckRevert()
    {
        var domain = NewDomain(Dir.PathJoin("revert.ini"));
        domain.SetValue("Volume", 0.8);
        ExpectEqual(domain.Apply(), Error.Ok, "Apply() in manual mode");
        domain.SetValue("Volume", 0.2);
        domain.SetValue("Quality", 2);
        Expect(domain.IsDirty(), "SetValue() in manual mode makes the domain dirty");
        ExpectEqual(string.Join(",", domain.GetDirtySettingNames()), "Volume,Quality", "GetDirtySettingNames() lists the changed settings");
        domain.Revert();
        Expect(!domain.IsDirty(), "Revert() clears the dirty state");
        Expect(Mathf.IsEqualApprox(domain.GetValue("Volume").AsDouble(), 0.8), "Revert() gives back the last applied volume");
        ExpectEqual(domain.GetValue("Quality").AsInt32(), 1, "Revert() gives back the last applied quality");
        domain.Free();
    }

    private void CheckFormats()
    {
        foreach (string extension in new[] { "ini", "json" })
        {
            string path = Dir.PathJoin("single." + extension);
            var writer = NewDomain(path);
            writer.SetValue("Volume", 0.3);
            writer.SetValue("Quality", 0);
            writer.Apply();
            ExpectEqual(writer.Save(), Error.Ok, $"Save() as {extension}");
            writer.Free();

            var reader = NewDomain(path);
            ExpectEqual(reader.Load(), Error.Ok, $"Load() as {extension}");
            Expect(Mathf.IsEqualApprox(reader.GetValue("Volume").AsDouble(), 0.3), $"{extension}: volume loaded");
            ExpectEqual(reader.GetValue("Quality").AsInt32(), 0, $"{extension}: quality loaded");
            reader.Free();
        }
    }

    private void CheckBrokenFile()
    {
        string path = Dir.PathJoin("broken.json");
        const string broken = "{ \"Volume\": 0.25, ";
        using (var file = FileAccess.Open(path, FileAccess.ModeFlags.Write))
        {
            file.StoreString(broken);
        }
        var domain = NewDomain(path);
        Expect(domain.Load() != Error.Ok, "a broken file is reported (a warning is printed)");
        Expect(Mathf.IsEqualApprox(domain.GetValue("Volume").AsDouble(), 0.5), "a broken file falls back to the defaults");
        ExpectEqual(FileAccess.GetFileAsString(path), broken, "a broken file is not overwritten by Load()");
        domain.Free();
    }

    private async Task CheckConfirmation()
    {
        var domain = NewDomain(Dir.PathJoin("confirm.ini"));
        domain.SetValue("Quality", 2);
        ExpectEqual(domain.ApplyWithConfirmation(0.2), Error.Ok, "ApplyWithConfirmation()");
        Expect(domain.IsConfirmationPending(), "a confirmation is pending");
        await ToSignal(GetTree().CreateTimer(0.6), SceneTreeTimer.SignalName.Timeout);
        Expect(!domain.IsConfirmationPending(), "the confirmation timed out");
        ExpectEqual(domain.GetValue("Quality").AsInt32(), 1, "without ConfirmChanges() the old value comes back");

        domain.SetValue("Quality", 0);
        domain.ApplyWithConfirmation(5.0);
        domain.ConfirmChanges();
        ExpectEqual(domain.GetAppliedValue("Quality").AsInt32(), 0, "ConfirmChanges() keeps the new value");

        domain.SetValue("Quality", 2);
        domain.ApplyWithConfirmation(5.0);
        domain.CancelConfirmation();
        Expect(!domain.IsConfirmationPending(), "CancelConfirmation() ends the confirmation");
        ExpectEqual(domain.GetAppliedValue("Quality").AsInt32(), 0, "CancelConfirmation() gives back the confirmed value");

        var finished = false;
        domain.ReadFromEngineFinished += () => finished = true;
        domain.ReadFromEngine();
        Expect(finished, "ReadFromEngine() emits ReadFromEngineFinished");
        domain.Free();
    }

    private async Task CheckRestart(Issue15CheckDomainCs server)
    {
        if (!OS.HasFeature("pc"))
        {
            GD.Print("  SKIP  restart check (needs a desktop platform to start a second process)");
            return;
        }
        double volume = Math.Round(GD.RandRange(0.1, 0.9), 2);
        server.SetValue("Volume", volume);
        server.SetValue("Quality", 2);
        Expect(!server.IsDirty(), "immediate mode applies SetValue() at once");
        ExpectEqual(server.Save(), Error.Ok, "Save() writes the player file");
        Expect(FileAccess.FileExists(PlayerFile), "the player file is in user://settings/");

        Godot.Collections.Dictionary probe = RunProbe();
        Expect(probe.ContainsKey("cs_started") && probe["cs_started"].AsBool(), "restart: the domain was started");
        var applied = probe.ContainsKey("cs_applied") ? probe["cs_applied"].AsGodotDictionary() : new Godot.Collections.Dictionary();
        Expect(applied.ContainsKey("volume") && Mathf.IsEqualApprox(applied["volume"].AsDouble(), volume), "restart: the saved volume was applied");
        ExpectEqual(applied.ContainsKey("quality") ? applied["quality"].AsInt32() : -1, 2, "restart: the saved quality was applied");
        ExpectEqual(applied.ContainsKey("frame") ? applied["frame"].AsInt32() : -1, 0, "restart: applied before the first frame");

        // Leave no player file behind for later runs.
        server.ResetToDefaults();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        DirAccess.RemoveAbsolute(PlayerFile);
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
                if (line.StartsWith("CHECK15_PROBE "))
                {
                    Variant parsed = Json.ParseString(line["CHECK15_PROBE ".Length..]);
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
