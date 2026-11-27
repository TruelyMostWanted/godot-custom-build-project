using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.SettingsServers;

/// <summary>
/// Full API test of Settings Servers: SettingsServer, ListSettingsServer, SettingsServerManager. Same tests as scripts/gd/Test.gd.
/// The domains "test_gd" (GDScript) and "test_cs" (C#) are listed in settings/startup/custom_servers and started before the main scene.
/// Setting names are the C# property names (Volume, not volume).
/// </summary>
public partial class Test : FeatureTest
{
    private const string Dir = "user://settings-servers-test-cs";
    private const string ListScript = "res://settings-servers/scripts/gd/test_list_domain.gd";
    private const string EntryScript = "res://settings-servers/scripts/gd/test_list_entry.gd";
    private const string ProbeScene = "res://settings-servers/scenes/restart_probe.tscn";
    private const string PlayerFile = "user://settings/test_cs.ini";
    private const double DefaultVolume = 0.5;
    private const int DefaultQuality = 1;
    private const int MaxLevel = 10;

    private readonly List<string> _events = [];
    private int _boundValue;

    protected override async Task RunTests()
    {
        DirAccess.MakeDirRecursiveAbsolute(Dir);
        TestConstants();
        TestStartup();
        TestDomainBasics();
        TestValidation();
        TestValuesAndSignals();
        TestManualMode();
        TestFloatPrecision();
        TestSaveAndLoad();
        TestLoadFailures();
        TestMigration();
        await TestConfirmation();
        TestApplyDuringConfirmation();
        TestRestartRequired();
        TestBindings();
        TestSessionOverride();
        TestManager();
        TestListDomain();
        TestListFiles();
        TestListDefaultsAndValidation();
        TestProjectPaths();
        TestViewportTarget();
        await TestRestart();
        RemoveDir(Dir);
    }

    private static TestDomain NewDomain(string fileName)
    {
        var domain = new TestDomain();
        domain.ApplyMode = SettingsServer.ApplyModeEnum.Manual;
        domain.FilePath = Dir.PathJoin(fileName);
        return domain;
    }

    private static ListSettingsServer NewList()
    {
        var list = GD.Load<GDScript>(ListScript).New().As<ListSettingsServer>();
        list.ApplyMode = SettingsServer.ApplyModeEnum.Manual;
        return list;
    }

    private static Resource NewEntry(string entryName, int level)
    {
        var entry = GD.Load<GDScript>(EntryScript).New().As<Resource>();
        entry.Set("entry_name", entryName);
        entry.Set("level", level);
        return entry;
    }

    private void Log(string text)
    {
        _events.Add(text);
    }

    private string Events()
    {
        return string.Join("; ", _events);
    }

    private void TestConstants()
    {
        ExpectEqual((int)SettingsServer.ApplyModeEnum.Immediate, 0, "ApplyModeEnum.Immediate is 0");
        ExpectEqual((int)SettingsServer.ApplyModeEnum.Manual, 1, "ApplyModeEnum.Manual is 1");
        ExpectEqual((int)SettingsServer.ApplyPhase.None, -1, "ApplyPhase.None is -1");
        ExpectEqual((int)SettingsServer.ApplyPhase.PreDisplay, 0, "ApplyPhase.PreDisplay is 0");
        ExpectEqual((int)SettingsServer.ApplyPhase.Servers, 1, "ApplyPhase.Servers is 1");
        ExpectEqual((int)SettingsServer.ApplyPhase.Scene, 2, "ApplyPhase.Scene is 2");
        ExpectEqual((int)SettingsServer.ApplyPhase.Max, 3, "ApplyPhase.Max is 3");
        ExpectEqual((int)SettingsServer.SettingFlags.None, 0, "SettingFlags.None is 0");
        ExpectEqual((int)SettingsServer.SettingFlags.RestartRequired, 1, "SettingFlags.RestartRequired is 1");
        ExpectEqual((int)SettingsServer.SettingFlags.Confirm, 2, "SettingFlags.Confirm is 2");
        ExpectEqual((int)SettingsServer.SettingFlags.NoEngineTarget, 4, "SettingFlags.NoEngineTarget is 4");
        ExpectEqual((int)SettingsServer.ViewportTargetMode.RootOnly, 0, "ViewportTargetMode.RootOnly is 0");
        ExpectEqual((int)SettingsServer.ViewportTargetMode.Group, 1, "ViewportTargetMode.Group is 1");
        ExpectEqual((int)SettingsServer.ViewportTargetMode.AllViewports, 2, "ViewportTargetMode.AllViewports is 2");
        Expect(ClassDB.IsParentClass("ListSettingsServer", "SettingsServer"), "ListSettingsServer extends SettingsServer");
    }

    private void TestStartup()
    {
        Expect(Engine.HasSingleton("SettingsServerManager"), "SettingsServerManager is a registered singleton");
        ExpectEqual(SettingsServerManager.GetStartupPhase(), SettingsServer.ApplyPhase.Scene, "all startup phases ran before the main scene");
        ExpectEqual(SettingsServerManager.GetStorageFolder(), "user://settings", "default storage folder");
        ExpectEqual(SettingsServerManager.GetStorageFormat(), ResourceTextSerializer.Format.Ini, "default storage format is Format.Ini");
        Expect(!SettingsServerManager.HasDirtyServers(), "HasDirtyServers is false after startup");
        Expect(SettingsServerManager.HasServer("test_gd"), "the GDScript domain from custom_servers is registered");
        var server = SettingsServerManager.GetServer("test_cs") as TestDomain;
        Expect(server != null, "the C# domain from custom_servers is registered as TestDomain");
        Expect(server.IsStarted(), "the domain was started before the main scene");
        ExpectEqual(server.GetApplyPhase(), SettingsServer.ApplyPhase.PreDisplay, "the domain asks for ApplyPhase.PreDisplay (a script domain still starts in ApplyPhase.Scene)");
        ExpectEqual(server.FilePath, PlayerFile, "default file path");
    }

    private void TestDomainBasics()
    {
        TestDomain domain = NewDomain("basics.ini");
        ExpectEqual(domain.GetDomain().ToString(), "test_cs", "GetDomain returns the domain name");
        Expect(!domain.IsStarted(), "a domain that is not registered is not started");
        ExpectEqual(domain.GetFormatVersion(), 2, "GetFormatVersion returns the version of the domain");
        ExpectEqual(string.Join(",", domain.GetSettingNames()), "Volume,Quality,Label,Hidden", "GetSettingNames comes from the resource");
        Expect(domain.HasSetting("Volume"), "HasSetting finds Volume");
        Expect(!domain.HasSetting("Missing"), "HasSetting does not find a missing name");
        Expect(domain.IsValueSupported("Volume"), "IsValueSupported is true for a supported setting");
        Expect(!domain.IsValueSupported("Hidden"), "IsValueSupported follows _IsValueSupported");
        Expect(!domain.IsValueSupported("Missing"), "IsValueSupported is false for a missing setting");
        ExpectEqual(domain.GetDefaultValue("Volume").AsDouble(), DefaultVolume, "GetDefaultValue comes from the resource");
        ExpectEqual(domain.GetDefaultValue("Label").AsString(), "engine", "the defaults capture _ReadFromEngine");
        Resource settings = domain.GetSettings();
        Resource applied = domain.GetAppliedSettings();
        Resource defaults = domain.GetDefaultSettings();
        Expect(settings is SettingsServersTestSettingsCs, "GetSettings returns the settings resource");
        Expect(applied != settings, "GetAppliedSettings is a separate copy");
        Expect(defaults != settings, "GetDefaultSettings is a separate copy");
        domain.Free();
    }

    private void TestValidation()
    {
        TestDomain domain = NewDomain("validation.ini");
        domain.SetValue("Volume", 4.0);
        ExpectEqual(domain.GetValue("Volume").AsDouble(), 1.0, "a range hint clamps the value");
        domain.SetValue("Quality", 9);
        ExpectEqual(domain.GetValue("Quality").AsInt32(), DefaultQuality, "an unknown enum value is rejected (an error is printed)");
        domain.SetValue("Label", "  trimmed  ");
        ExpectEqual(domain.GetValue("Label").AsString(), "trimmed", "_ValidateValue changes the value");
        domain.Free();
    }

    private void TestValuesAndSignals()
    {
        TestDomain domain = NewDomain("values.ini");
        domain.ApplyMode = SettingsServer.ApplyModeEnum.Immediate;
        _events.Clear();
        domain.ValueChanged += (name, value) => Log($"value_changed {name} {value}");
        domain.SettingsChanged += names => Log($"settings_changed {string.Join(",", names)}");
        domain.Applied += names => Log($"applied {string.Join(",", names)}");
        domain.Reset += () => Log("reset");
        domain.SetValue("Volume", 0.8);
        ExpectEqual(domain.GetValue("Volume").AsDouble(), 0.8, "SetValue changes the pending value");
        ExpectEqual(domain.GetAppliedValue("Volume").AsDouble(), 0.8, "immediate mode applies at once");
        ExpectEqual(Events(), "value_changed Volume 0.8; settings_changed Volume; applied Volume", "SetValue emits ValueChanged, SettingsChanged, Applied");
        _events.Clear();
        domain.SetValue("Volume", 0.8);
        Expect(_events.Count == 0, "the same value again is no change");
        domain.ResetValue("Volume");
        ExpectEqual(domain.GetValue("Volume").AsDouble(), DefaultVolume, "ResetValue goes back to the default");
        domain.SetValue("Quality", 2);
        _events.Clear();
        domain.ResetToDefaults();
        ExpectEqual(domain.GetValue("Quality").AsInt32(), DefaultQuality, "ResetToDefaults goes back to the defaults");
        Expect(_events.Contains("reset"), "ResetToDefaults emits Reset");
        domain.Free();
    }

    private void TestManualMode()
    {
        TestDomain domain = NewDomain("manual.ini");
        _events.Clear();
        domain.Applied += names => Log($"applied {string.Join(",", names)}");
        domain.SetValue("Volume", 0.8);
        ExpectEqual(domain.Apply(), Error.Ok, "Apply in manual mode");
        Expect(domain.HasUnsavedChanges(), "an applied change is unsaved");
        ExpectEqual(Events(), "applied Volume", "Apply emits Applied with the names");
        domain.SetValue("Volume", 0.2);
        domain.SetValue("Quality", 2);
        Expect(domain.IsDirty(), "SetValue in manual mode makes the domain dirty");
        ExpectEqual(string.Join(",", domain.GetDirtySettingNames()), "Volume,Quality", "GetDirtySettingNames lists the changed settings");
        ExpectEqual(domain.GetAppliedValue("Volume").AsDouble(), 0.8, "the applied value stays until Apply");
        domain.Revert();
        Expect(!domain.IsDirty(), "Revert clears the dirty state");
        ExpectEqual(domain.GetValue("Volume").AsDouble(), 0.8, "Revert gives back the last applied volume");
        ExpectEqual(domain.GetValue("Quality").AsInt32(), DefaultQuality, "Revert gives back the last applied quality");
        domain.Free();
    }

    private void TestFloatPrecision()
    {
        ExpectEqual(SettingsServer.SnapFloat(0.1 + 0.2), 0.3, "SnapFloat(0.1 + 0.2) is 0.3");
        ExpectEqual(SettingsServer.SnapFloat(1.23456), 1.235, "three decimals by default");
        ExpectEqual(SettingsServer.SnapFloat(-6.0206, 0.1), -6.0, "snapped to the step");
        ExpectEqual(SettingsServer.SnapFloat(0.12, 0.1, 0.05), 0.15, "snapped relative to the offset");
        ExpectEqual(SettingsServer.SnapFloat(0.0123, 0.0001), 0.012, "a step below 0.001 counts as 0.001");
        ExpectEqual(SettingsServer.SnapFloat(-0.0000014).ToString(), "0", "-0 becomes 0");
        TestDomain domain = NewDomain("precision.json");
        domain.SetValue("Volume", 0.123456);
        ExpectEqual(domain.GetValue("Volume").AsDouble(), 0.12, "SetValue snaps to the step of the range hint");
        domain.SetValue("Volume", 0.1 + 0.2);
        domain.Apply();
        domain.Save();
        string text = FileAccess.GetFileAsString(domain.FilePath);
        Expect(text.Contains("0.3"), "the file holds 0.3");
        Expect(!text.Contains("0000000"), "the file has no float noise");
        Expect(!text.Contains("9999999"), "the file has no 9999999 noise");
        domain.Free();
    }

    private void TestSaveAndLoad()
    {
        foreach (string extension in new[] { "ini", "json" })
        {
            TestDomain writer = NewDomain("single." + extension);
            writer.SetValue("Volume", 0.3);
            writer.SetValue("Quality", 0);
            writer.Apply();
            _events.Clear();
            writer.Saved += () => Log("saved");
            ExpectEqual(writer.Save(), Error.Ok, $"Save as {extension}");
            ExpectEqual(Events(), "saved", $"Save emits Saved ({extension})");
            Expect(!writer.HasUnsavedChanges(), $"Save clears HasUnsavedChanges ({extension})");
            string text = FileAccess.GetFileAsString(writer.FilePath);
            Expect(text.Contains("settings_version"), $"{extension} file has the settings version");
            TestDomain reader = NewDomain("single." + extension);
            reader.Loaded += () => Log("loaded");
            ExpectEqual(reader.Load(), Error.Ok, $"Load as {extension}");
            ExpectEqual(_events.Last(), "loaded", $"Load emits Loaded ({extension})");
            ExpectEqual(reader.GetValue("Volume").AsDouble(), 0.3, $"{extension}: volume loaded");
            ExpectEqual(reader.GetValue("Quality").AsInt32(), 0, $"{extension}: quality loaded");
            writer.Free();
            reader.Free();
        }
        TestDomain changed = NewDomain("only_changed.ini");
        changed.SetValue("Quality", 2);
        changed.Apply();
        changed.Save();
        string changedText = FileAccess.GetFileAsString(changed.FilePath);
        Expect(changedText.Contains("Quality"), "a changed value is written");
        Expect(!changedText.Contains("Volume"), "a value equal to the default is not written");
        changed.Free();
        string[] extensions = ["json", "ini", "csv", "xml", "toml", "yaml"];
        foreach (ResourceTextSerializer.Format format in new[] { ResourceTextSerializer.Format.Xml, ResourceTextSerializer.Format.Toml, ResourceTextSerializer.Format.Yaml })
        {
            string extension = extensions[(int)format];
            if (!ResourceTextSerializer.IsFormatAvailable(format))
            {
                GD.Print($"  SKIP  {extension} (module not in this build)");
                continue;
            }
            TestDomain writer = NewDomain("format." + extension);
            writer.SetValue("Quality", 2);
            writer.Apply();
            ExpectEqual(writer.Save(), Error.Ok, $"Save as {extension}");
            TestDomain reader = NewDomain("format." + extension);
            ExpectEqual(reader.Load(), Error.Ok, $"Load as {extension}");
            ExpectEqual(reader.GetValue("Quality").AsInt32(), 2, $"{extension}: quality loaded");
            writer.Free();
            reader.Free();
        }
    }

    private void TestLoadFailures()
    {
        string brokenPath = Dir.PathJoin("broken.json");
        const string broken = "{ \"Volume\": 0.25, ";
        using (var file = FileAccess.Open(brokenPath, FileAccess.ModeFlags.Write))
        {
            file.StoreString(broken);
        }
        TestDomain domain = NewDomain("broken.json");
        _events.Clear();
        domain.LoadFailed += (error, path) => Log($"load_failed {error} {path}");
        Error loadError = domain.Load();
        Expect(loadError != Error.Ok, "a broken file is reported (a warning is printed)");
        ExpectEqual(_events[0], $"load_failed {loadError} {brokenPath}", "LoadFailed carries the error and the path");
        ExpectEqual(domain.GetValue("Volume").AsDouble(), DefaultVolume, "a broken file falls back to the defaults");
        ExpectEqual(FileAccess.GetFileAsString(brokenPath), broken, "Load does not overwrite a broken file");
        TestDomain missing = NewDomain("missing.ini");
        missing.LoadFailed += (error, path) => Log("load_failed");
        _events.Clear();
        ExpectEqual(missing.Load(), Error.FileNotFound, "a missing file keeps the defaults");
        Expect(_events.Count == 0, "a missing file emits no LoadFailed");
        string blocker = Dir.PathJoin("blocker.txt");
        using (var file = FileAccess.Open(blocker, FileAccess.ModeFlags.Write))
        {
            file.StoreString("not a folder");
        }
        string badPath = blocker.PathJoin("sub").PathJoin("signals.ini");
        domain.FilePath = badPath;
        domain.SaveFailed += (error, path) => Log($"save_failed {error} {path}");
        _events.Clear();
        Error saveError = domain.Save();
        Expect(saveError != Error.Ok, "Save into a folder that can't be created fails (an error is printed)");
        ExpectEqual(Events(), $"save_failed {saveError} {badPath}", "SaveFailed carries the error and the path");
        domain.Free();
        missing.Free();
    }

    private void TestMigration()
    {
        string path = Dir.PathJoin("migrate.json");
        using (var file = FileAccess.Open(path, FileAccess.ModeFlags.Write))
        {
            file.StoreString("{\"LegacyVolume\": 0.25, \"metadata/settings_version\": 1}");
        }
        TestDomain domain = NewDomain("migrate.json");
        ExpectEqual(domain.Load(), Error.Ok, "Load of an old file");
        ExpectEqual(domain.GetValue("Volume").AsDouble(), 0.25, "_Migrate renamed LegacyVolume to Volume");
        domain.Free();
    }

    private async Task TestConfirmation()
    {
        TestDomain domain = NewDomain("confirm.ini");
        _events.Clear();
        domain.ConfirmationRequested += seconds => Log($"confirmation_requested {seconds}");
        domain.ConfirmationFinished += kept => Log($"confirmation_finished {kept}");
        domain.SetValue("Quality", 2);
        ExpectEqual(domain.ApplyWithConfirmation(0.2), Error.Ok, "ApplyWithConfirmation");
        Expect(domain.IsConfirmationPending(), "a confirmation is pending");
        Expect(domain.GetConfirmationTimeLeft() > 0.0, "GetConfirmationTimeLeft counts down");
        ExpectEqual(Events(), "confirmation_requested 0.2", "ConfirmationRequested carries the time");
        ExpectEqual(domain.GetAppliedValue("Quality").AsInt32(), 2, "the new value is applied while waiting");
        await ToSignal(GetTree().CreateTimer(0.6), SceneTreeTimer.SignalName.Timeout);
        Expect(!domain.IsConfirmationPending(), "the confirmation timed out");
        ExpectEqual(domain.GetValue("Quality").AsInt32(), DefaultQuality, "without ConfirmChanges the old value comes back");
        ExpectEqual(_events.Last(), "confirmation_finished False", "a timeout emits ConfirmationFinished(false)");
        ExpectEqual(domain.GetConfirmationTimeLeft(), 0.0, "no time left without a confirmation");
        domain.SetValue("Quality", 0);
        domain.ApplyWithConfirmation(5.0);
        domain.ConfirmChanges();
        ExpectEqual(domain.GetAppliedValue("Quality").AsInt32(), 0, "ConfirmChanges keeps the new value");
        ExpectEqual(_events.Last(), "confirmation_finished True", "ConfirmChanges emits ConfirmationFinished(true)");
        domain.SetValue("Quality", 2);
        domain.ApplyWithConfirmation(5.0);
        domain.CancelConfirmation();
        Expect(!domain.IsConfirmationPending(), "CancelConfirmation ends the confirmation");
        ExpectEqual(domain.GetAppliedValue("Quality").AsInt32(), 0, "CancelConfirmation gives back the confirmed value");
        _events.Clear();
        domain.ReadFromEngineFinished += () => Log("read_from_engine_finished");
        domain.ReadFromEngine();
        ExpectEqual(_events.Last(), "read_from_engine_finished", "ReadFromEngine emits ReadFromEngineFinished");
        ExpectEqual(domain.GetValue("Label").AsString(), "engine", "ReadFromEngine calls _ReadFromEngine");
        domain.Free();
    }

    private void TestApplyDuringConfirmation()
    {
        TestDomain domain = NewDomain("confirm_flag.ini");
        domain.SetSettingFlags("Quality", SettingsServer.SettingFlags.Confirm);
        _events.Clear();
        domain.ConfirmationFinished += kept => Log($"confirmation_finished {kept}");
        domain.SetValue("Quality", 2);
        domain.Apply();
        Expect(domain.IsConfirmationPending(), "a value with SettingFlags.Confirm starts a confirmation");
        domain.SetValue("Volume", 0.3);
        ExpectEqual(domain.Apply(), Error.Ok, "Apply during a pending confirmation");
        Expect(!domain.IsConfirmationPending(), "Apply kept the pending confirmation first");
        ExpectEqual(Events(), "confirmation_finished True", "the kept confirmation emits ConfirmationFinished(true)");
        ExpectEqual(domain.GetAppliedValue("Quality").AsInt32(), 2, "the kept value stays applied");
        domain.SetValue("Quality", 0);
        domain.ApplyWithConfirmation(5.0);
        domain.SetValue("Quality", 1);
        ExpectEqual(domain.ApplyWithConfirmation(5.0), Error.Ok, "ApplyWithConfirmation during a pending one");
        domain.CancelConfirmation();
        ExpectEqual(domain.GetAppliedValue("Quality").AsInt32(), 0, "a cancel only reverts the values of the new confirmation");
        ExpectEqual(domain.GetAppliedValue("Volume").AsDouble(), 0.3, "values applied before stay");
        domain.Free();
    }

    private void TestRestartRequired()
    {
        TestDomain domain = NewDomain("restart.ini");
        domain.SetSettingFlags("Quality", SettingsServer.SettingFlags.RestartRequired);
        ExpectEqual(domain.GetSettingFlags("Quality"), SettingsServer.SettingFlags.RestartRequired, "GetSettingFlags returns the flags");
        ExpectEqual(domain.GetSettingFlags("Volume"), SettingsServer.SettingFlags.None, "a setting without flags has SettingFlags.None");
        _events.Clear();
        domain.RestartRequired += names => Log($"restart_required [{string.Join(",", names)}]");
        domain.SetValue("Quality", 2);
        domain.Apply();
        ExpectEqual(string.Join(",", domain.GetRestartRequiredNames()), "Quality", "a changed restart value is listed");
        domain.SetValue("Quality", DefaultQuality);
        domain.Apply();
        Expect(domain.GetRestartRequiredNames().Length == 0, "the value set back to the start value is no longer listed");
        ExpectEqual(Events(), "restart_required [Quality]; restart_required []", "RestartRequired carries the whole list");
        domain.Free();
    }

    private void SetBoundValue(int value)
    {
        _boundValue = value;
    }

    private int GetBoundValue()
    {
        return _boundValue;
    }

    private void TestBindings()
    {
        var node = new Node();
        TestDomain propertyDomain = NewDomain("bind_property.ini");
        propertyDomain.BindSettingProperty("Quality", node, "process_priority");
        Expect(propertyDomain.IsSettingBound("Quality"), "IsSettingBound after BindSettingProperty");
        Expect(!propertyDomain.IsSettingBound("Volume"), "an unbound setting is not bound");
        propertyDomain.SetValue("Quality", 2);
        propertyDomain.Apply();
        ExpectEqual(node.ProcessPriority, 2, "BindSettingProperty pushes the value to the property");
        node.ProcessPriority = 1;
        propertyDomain.ReadFromEngine();
        ExpectEqual(propertyDomain.GetValue("Quality").AsInt32(), 1, "ReadFromEngine reads the bound property");
        propertyDomain.UnbindSetting("Quality");
        Expect(!propertyDomain.IsSettingBound("Quality"), "UnbindSetting removes the binding");
        propertyDomain.Free();
        node.Free();

        TestDomain singletonDomain = NewDomain("bind_singleton.ini");
        singletonDomain.BindSettingSingletonProperty("Volume", "Engine", "time_scale");
        singletonDomain.SetValue("Volume", 0.5);
        singletonDomain.Apply();
        ExpectEqual(Engine.TimeScale, 0.5, "BindSettingSingletonProperty pushes the value to the singleton");
        Engine.TimeScale = 1.0;
        singletonDomain.Free();

        TestDomain callbackDomain = NewDomain("bind_callbacks.ini");
        callbackDomain.BindSettingCallbacks("Quality", Callable.From<int>(SetBoundValue), Callable.From(GetBoundValue), SettingsServer.SettingFlags.RestartRequired);
        ExpectEqual(callbackDomain.GetSettingFlags("Quality"), SettingsServer.SettingFlags.RestartRequired, "BindSettingCallbacks sets the flags");
        callbackDomain.SetValue("Quality", 2);
        callbackDomain.Apply();
        ExpectEqual(_boundValue, 2, "BindSettingCallbacks calls the setter");
        _boundValue = 0;
        callbackDomain.ReadFromEngine();
        ExpectEqual(callbackDomain.GetValue("Quality").AsInt32(), 0, "ReadFromEngine calls the getter");
        callbackDomain.UnbindSetting("Quality");
        ExpectEqual(callbackDomain.GetSettingFlags("Quality"), SettingsServer.SettingFlags.None, "UnbindSetting removes the flags");
        callbackDomain.Free();
    }

    private void TestSessionOverride()
    {
        TestDomain domain = NewDomain("override.ini");
        _boundValue = DefaultQuality;
        domain.BindSettingCallbacks("Quality", Callable.From<int>(SetBoundValue), Callable.From(GetBoundValue));
        domain.SetSessionOverride("Quality", 2);
        Expect(domain.HasSessionOverride("Quality"), "HasSessionOverride after SetSessionOverride");
        ExpectEqual(domain.GetValue("Quality").AsInt32(), DefaultQuality, "an override does not change GetValue");
        ExpectEqual(_boundValue, 2, "an override is pushed to the engine");
        domain.Save();
        Expect(!FileAccess.GetFileAsString(domain.FilePath).Contains("Quality"), "an override is never saved");
        domain.ClearSessionOverride("Quality");
        Expect(!domain.HasSessionOverride("Quality"), "ClearSessionOverride removes the override");
        ExpectEqual(_boundValue, DefaultQuality, "ClearSessionOverride pushes the applied value again");
        domain.SetSessionOverride("Quality", 2);
        domain.SetValue("Quality", 0);
        Expect(!domain.HasSessionOverride("Quality"), "SetValue removes the override");
        domain.Free();
    }

    private void TestManager()
    {
        string[] domains = SettingsServerManager.GetDomains();
        Expect(domains.Contains("test_cs"), "GetDomains lists test_cs");
        ExpectEqual(SettingsServerManager.GetServers().Count, domains.Length, "GetServers has one server per domain");
        Expect(!SettingsServerManager.HasServer("missing"), "HasServer is false for a missing domain");
        Expect(SettingsServerManager.GetServer("missing") == null, "GetServer returns null for a missing domain");
        ListSettingsServer list = NewList();
        list.FilePath = Dir.PathJoin("manager_list.ini");
        _events.Clear();
        SettingsServerManager.ServerRegisteredEventHandler onRegistered = domain => Log($"server_registered {domain}");
        SettingsServerManager.ServerUnregisteredEventHandler onUnregistered = domain => Log($"server_unregistered {domain}");
        SettingsServerManager.ServerRegistered += onRegistered;
        SettingsServerManager.ServerUnregistered += onUnregistered;
        ExpectEqual(SettingsServerManager.RegisterServer(list), Error.Ok, "RegisterServer");
        Expect(SettingsServerManager.GetServer("test_list") == list, "GetServer returns the registered server");
        Expect(list.IsStarted(), "RegisterServer starts the server at once");
        ExpectEqual(SettingsServerManager.RegisterServer(null), Error.InvalidParameter, "RegisterServer rejects null");
        ListSettingsServer duplicate = NewList();
        ExpectEqual(SettingsServerManager.RegisterServer(duplicate), Error.AlreadyExists, "RegisterServer rejects a taken domain");
        duplicate.Free();
        list.AddEntry(NewEntry("a", 1));
        Expect(SettingsServerManager.HasDirtyServers(), "HasDirtyServers is true with a pending entry");
        SettingsServerManager.RevertAll();
        Expect(!SettingsServerManager.HasDirtyServers(), "RevertAll drops pending changes");
        list.AddEntry(NewEntry("a", 1));
        ExpectEqual(SettingsServerManager.ApplyAll(), Error.Ok, "ApplyAll");
        ExpectEqual(SettingsServerManager.SaveAll(), Error.Ok, "SaveAll");
        Expect(FileAccess.FileExists(list.FilePath), "SaveAll wrote the file of the list domain");
        list.ClearEntries();
        list.Apply();
        ExpectEqual(SettingsServerManager.LoadAll(), Error.Ok, "LoadAll");
        SettingsServerManager.ApplyAll();
        ExpectEqual(list.GetEntryCount(), 1, "LoadAll read the saved entry");
        SettingsServerManager.ResetAllToDefaults();
        ExpectEqual(list.GetEntryCount(), 0, "ResetAllToDefaults goes back to the defaults");
        SettingsServerManager.UnregisterServer("test_list");
        Expect(!SettingsServerManager.HasServer("test_list"), "UnregisterServer removes the server");
        ExpectEqual(Events(), "server_registered test_list; server_unregistered test_list", "ServerRegistered and ServerUnregistered carry the domain");
        SettingsServerManager.ServerRegistered -= onRegistered;
        SettingsServerManager.ServerUnregistered -= onUnregistered;
        list.Free();
    }

    private void TestListDomain()
    {
        ListSettingsServer list = NewList();
        ExpectEqual(list.GetEntryKey().ToString(), "entry_name", "GetEntryKey returns the key property");
        ExpectEqual(list.GetListProperty().ToString(), "entries", "GetListProperty is the first Array property");
        list.AddEntry(NewEntry("a", 1));
        list.AddEntry(NewEntry("b", 2));
        list.AddEntry(NewEntry("c", 3));
        list.AddEntry(NewEntry("first", 4), 0);
        ExpectEqual(list.GetEntryCount(), 4, "AddEntry adds at the end and at an index");
        ExpectEqual(list.GetEntryValue(0, "entry_name").AsString(), "first", "AddEntry at index 0 inserts first");
        list.RemoveEntry(0);
        list.MoveEntry(2, 0);
        ExpectEqual(list.GetEntryValue(0, "entry_name").AsString(), "c", "MoveEntry moves an entry");
        list.RemoveEntry(1);
        ExpectEqual(list.GetEntryCount(), 2, "RemoveEntry removes an entry");
        ExpectEqual(list.FindEntry("c"), 0, "FindEntry finds by key");
        ExpectEqual(list.FindEntry("missing"), -1, "FindEntry returns -1 for a missing key");
        list.SetEntryValue(1, "level", 5);
        ExpectEqual(list.GetEntryValue(1, "level").AsInt32(), 5, "SetEntryValue changes a value");
        Resource entry = list.GetEntry(1);
        ExpectEqual(entry.Get("level").AsInt32(), 5, "GetEntry returns the entry");
        ExpectEqual(list.GetEntries().Count, 2, "GetEntries returns a copy of the list");
        list.ClearEntries();
        ExpectEqual(list.GetEntryCount(), 0, "ClearEntries removes all entries");
        list.Free();
    }

    private void TestListFiles()
    {
        ListSettingsServer list = NewList();
        list.AddEntry(NewEntry("c", 3));
        list.AddEntry(NewEntry("b", 5));
        list.Apply();
        foreach (string extension in new[] { "csv", "json", "ini" })
        {
            list.FilePath = Dir.PathJoin("list." + extension);
            ExpectEqual(list.Save(), Error.Ok, $"list: Save as {extension}");
            ListSettingsServer reader = NewList();
            reader.FilePath = list.FilePath;
            ExpectEqual(reader.Load(), Error.Ok, $"list: Load as {extension}");
            ExpectEqual(reader.GetEntryCount(), 2, $"list {extension}: entry count");
            ExpectEqual(reader.GetEntryValue(0, "entry_name").AsString(), "c", $"list {extension}: first entry");
            ExpectEqual(reader.GetEntryValue(1, "level").AsInt32(), 5, $"list {extension}: level of the second entry");
            reader.Free();
        }
        list.Free();
    }

    private void TestListDefaultsAndValidation()
    {
        ListSettingsServer list = NewList();
        list.ApplyMode = SettingsServer.ApplyModeEnum.Immediate;
        list.FilePath = Dir.PathJoin("list_defaults.ini");
        Resource defaults = list.GetDefaultSettings();
        Godot.Collections.Array defaultEntries = defaults.Get("entries").AsGodotArray();
        defaultEntries.Add(NewEntry("a", 1));
        list.ResetToDefaults();
        ExpectEqual(list.GetEntryCount(), 1, "the list has the default entry");
        Expect(list.GetEntry(0) != defaultEntries[0].AsGodotObject(), "the entry is a copy of the default entry");
        ExpectEqual(list.Save(), Error.Ok, "Save of an unchanged list");
Expect(!FileAccess.GetFileAsString(list.FilePath).Contains("entries"), "entries equal to the defaults are not written");
        list.SetEntryValue(0, "level", 4);
        list.Save();
        Expect(FileAccess.GetFileAsString(list.FilePath).Contains("entries"), "a changed entry writes the list");
        list.SetEntryValue(0, "level", 50);
        ExpectEqual(list.GetEntryValue(0, "level").AsInt32(), MaxLevel, "SetEntryValue clamps to the range hint");
        list.SetEntryValue(0, "level", -3);
        ExpectEqual(list.GetEntryValue(0, "level").AsInt32(), 0, "SetEntryValue clamps the lower end");
        list.SetEntryValue(0, "level", 7);
        list.Apply();
        string path = Dir.PathJoin("list_clamp.json");
        list.FilePath = path;
        list.Save();
        string text = FileAccess.GetFileAsString(path);
        using (var file = FileAccess.Open(path, FileAccess.ModeFlags.Write))
        {
            file.StoreString(text.Replace("7", "99"));
        }
        ListSettingsServer reader = NewList();
        reader.FilePath = path;
        ExpectEqual(reader.Load(), Error.Ok, "Load of a hand-edited list");
        ExpectEqual(reader.GetEntryValue(0, "level").AsInt32(), MaxLevel, "hand-edited entry values are clamped on Load");
        reader.Free();
        list.Free();
    }

    private void TestProjectPaths()
    {
        foreach (string domainName in SettingsServerManager.GetDomains())
        {
            foreach (string key in new[] { "file_name", "override_format", "format" })
            {
                string settingName = $"settings/{domainName}/{key}";
                Expect(ProjectSettings.HasSetting(settingName), $"project setting {settingName} exists");
            }
        }
        foreach (string general in new[] { "folder", "default_format", "only_changed_values", "autosave", "confirmation_timeout" })
        {
            Expect(ProjectSettings.HasSetting("settings/general/" + general), $"settings/general/{general} exists");
        }
        const string fileNameSetting = "settings/test_cs/file_name";
        const string overrideSetting = "settings/test_cs/override_format";
        const string formatSetting = "settings/test_cs/format";
        ExpectEqual(ProjectSettings.GetSetting(fileNameSetting).AsString(), "test_cs", "the file name defaults to the domain name");
        ExpectEqual(ProjectSettings.GetSetting(overrideSetting).AsBool(), false, "the format is not overridden by default");
        var domain = new TestDomain();
        ExpectEqual(domain.FilePath, PlayerFile, "path: <folder>/<file_name>.<extension of default_format>");
        ProjectSettings.SetSetting("settings/general/folder", Dir);
        ProjectSettings.SetSetting(fileNameSetting, "profiles/test_cs");
        ExpectEqual(domain.FilePath, Dir.PathJoin("profiles/test_cs.ini"), "a relative subfolder in the file name");
        ProjectSettings.SetSetting(formatSetting, (int)ResourceTextSerializer.Format.Json);
        ExpectEqual(domain.FilePath, Dir.PathJoin("profiles/test_cs.ini"), "format without override_format is ignored");
        ProjectSettings.SetSetting(overrideSetting, true);
        ExpectEqual(domain.FilePath, Dir.PathJoin("profiles/test_cs.json"), "override_format uses the format of the domain");
        ProjectSettings.SetSetting("settings/general/default_format", (int)ResourceTextSerializer.Format.Xml);
        ExpectEqual(domain.FilePath, Dir.PathJoin("profiles/test_cs.json"), "override_format wins over default_format");
        ProjectSettings.SetSetting(overrideSetting, false);
        ExpectEqual(domain.FilePath, Dir.PathJoin("profiles/test_cs.xml"), "default_format picks the extension");
        ProjectSettings.SetSetting("settings/general/default_format", (int)ResourceTextSerializer.Format.Ini);
        ProjectSettings.SetSetting(overrideSetting, true);
        domain.ApplyMode = SettingsServer.ApplyModeEnum.Manual;
        domain.SetValue("Quality", 2);
        domain.Apply();
        ExpectEqual(domain.Save(), Error.Ok, "Save creates the subfolder");
        string written = FileAccess.GetFileAsString(Dir.PathJoin("profiles/test_cs.json"));
        Expect(written.StripEdges().StartsWith('{'), "the composed .json path is written as JSON");
        foreach (string invalid in new[] { "../outside", "/absolute", "res://inside", "a//b" })
        {
            ProjectSettings.SetSetting(fileNameSetting, invalid);
            ExpectEqual(domain.FilePath, Dir.PathJoin("test_cs.json"), $"invalid file name {invalid} falls back to the domain name (an error is printed)");
        }
        ProjectSettings.SetSetting(fileNameSetting, "test_cs");
        string runtimePath = Dir.PathJoin("runtime.ini");
        domain.FilePath = runtimePath;
        ExpectEqual(domain.FilePath, runtimePath, "FilePath set at runtime wins over the project settings");
        domain.FilePath = "";
        ExpectEqual(domain.FilePath, Dir.PathJoin("test_cs.json"), "an empty FilePath gives the composed path back");
        string unknown = Dir.PathJoin("runtime.cfg");
        domain.FilePath = unknown;
        ExpectEqual(domain.Save(), Error.Ok, "an unknown extension is saved in the format of the domain (an error is printed)");
        Expect(FileAccess.GetFileAsString(unknown).StripEdges().StartsWith('{'), "an unknown extension is written as JSON");
        ProjectSettings.SetSetting("settings/general/folder", "user://settings");
        ProjectSettings.SetSetting(overrideSetting, false);
        ProjectSettings.SetSetting(formatSetting, (int)ResourceTextSerializer.Format.Ini);
        domain.Free();
        DirAccess.RemoveAbsolute(Dir.PathJoin("profiles/test_cs.json"));
        DirAccess.RemoveAbsolute(Dir.PathJoin("profiles"));
    }

    private void TestViewportTarget()
    {
        ExpectEqual(RenderQualitySettingsServer.ViewportTarget, SettingsServer.ViewportTargetMode.Group, "ViewportTarget defaults to Group");
        RenderQualitySettingsServer.ViewportTarget = SettingsServer.ViewportTargetMode.AllViewports;
        ExpectEqual(RenderQualitySettingsServer.ViewportTarget, SettingsServer.ViewportTargetMode.AllViewports, "ViewportTarget can be changed");
        RenderQualitySettingsServer.ViewportTarget = SettingsServer.ViewportTargetMode.Group;
    }

    private async Task TestRestart()
    {
        if (!OS.HasFeature("pc"))
        {
            GD.Print("  SKIP  restart test (needs a desktop platform to start a second process)");
            return;
        }
        SettingsServer server = SettingsServerManager.GetServer("test_cs");
        double volume = GD.RandRange(10, 90) / 100.0;
        server.SetValue("Volume", volume);
        server.SetValue("Quality", 2);
        Expect(!server.IsDirty(), "immediate mode applies SetValue at once");
        ExpectEqual(server.Save(), Error.Ok, "Save writes the player file");
        Godot.Collections.Dictionary probe = RunProbe();
        Godot.Collections.Dictionary csProbe = probe["test_cs"].AsGodotDictionary();
        Godot.Collections.Dictionary applied = csProbe["applied"].AsGodotDictionary();
        Expect(csProbe["started"].AsBool(), "restart: the domain was started");
        ExpectEqual(applied["volume"].AsDouble(), volume, "restart: the saved volume was applied");
        ExpectEqual(applied["quality"].AsInt32(), 2, "restart: the saved quality was applied");
        ExpectEqual(applied["frame"].AsInt32(), 0, "restart: applied before the first frame");
        ExpectEqual(probe["ready_frame"].AsInt32(), 0, "restart: the main scene was ready before the first frame");
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
                if (line.StartsWith("PROBE "))
                {
                    return Json.ParseString(line["PROBE ".Length..]).AsGodotDictionary();
                }
            }
        }
        Expect(false, "restart: the probe printed its result");
        return [];
    }

    private static void RemoveDir(string path)
    {
        foreach (string fileName in DirAccess.GetFilesAt(path))
        {
            DirAccess.RemoveAbsolute(path.PathJoin(fileName));
        }
        DirAccess.RemoveAbsolute(path);
    }
}
