using System;
using Godot;

namespace TruelyInteractive.SettingsServers;

/// <summary>Minimal example of Settings Servers: the snippets of the wiki guide, in order. Same as scripts/gd/Example.gd.</summary>
public partial class Example : Node
{
    private const string File = "user://settings-servers-example/game.ini";

    private SettingsServer _game;

    public override void _Ready()
    {
        // Write a settings domain for your game (a game lists the script in settings/startup/custom_servers).
        DirAccess.MakeDirRecursiveAbsolute(File.GetBaseDir());
        DirAccess.RemoveAbsolute(File);
        var game = new GameSettings();
        game.FilePath = File;
        SettingsServerManager.RegisterServer(game);
        GD.Print("Settings: ", string.Join(", ", game.GetSettingNames()));

        // Change a value from the options menu.
        _game = SettingsServerManager.GetServer("game");
        _game.SettingsChanged += Refresh;
        _game.SetValue("Difficulty", 2);
        GD.Print("Difficulty: ", _game.GetValue("Difficulty"));

        // React when a value reaches the engine.
        game.DifficultyChanged += OnDifficultyChanged;
        _game.SetValue("Difficulty", 0);

        // Add Apply and Cancel buttons.
        _game.ApplyMode = SettingsServer.ApplyModeEnum.Manual;
        _game.SetValue("Difficulty", 2);
        GD.Print("Dirty: ", _game.IsDirty());
        _game.Revert();
        GD.Print("Dirty after revert: ", _game.IsDirty());
        _game.SetValue("Difficulty", 1);
        _game.Apply();

        // Ask the player to keep a risky change (the dialog is left out).
        _game.SetValue("ShowHints", false);
        _game.ApplyWithConfirmation(10.0);
        GD.Print("Confirmation pending: ", _game.IsConfirmationPending());
        _game.ConfirmChanges();
        GD.Print("Confirmation pending after confirm: ", _game.IsConfirmationPending());

        // Push values to the engine with bindings.
        _game.SetValue("MaxFps", 60);
        _game.Apply();
        GD.Print("Engine.MaxFps: ", Engine.MaxFps);
        _game.ResetValue("MaxFps");
        _game.Apply();

        // Float values have three decimals.
        GD.Print(SettingsServer.SnapFloat(0.30000000000000004)); // 0.3
        GD.Print(SettingsServer.SnapFloat(-6.0206, 0.1)); // -6

        // Choose where and in which format settings are stored.
        _game.FilePath = "user://settings-servers-example/game.json";
        _game.Save();
        _game.Load();
        _game.Apply();

        // React when a file can't be loaded or saved.
        foreach (SettingsServer server in SettingsServerManager.GetServers())
        {
            server.LoadFailed += (err, path) => GD.Print($"Settings reset: {path} is broken");
            server.SaveFailed += (err, path) => GD.Print($"Could not save {path} ({err})");
        }

        // Override a value for one run (the snippet reads the command line argument --hard).
        _game.SetSessionOverride("Difficulty", 2);
        GD.Print("Session override: ", _game.GetValue("Difficulty"));
        _game.ClearSessionOverride("Difficulty");

        // Write a list domain.
        var voices = GD.Load<GDScript>("res://settings-servers/scripts/gd/test_list_domain.gd").New().As<ListSettingsServer>();
        voices.FilePath = "user://settings-servers-example/voices.ini";
        SettingsServerManager.RegisterServer(voices);
        var guard = GD.Load<GDScript>("res://settings-servers/scripts/gd/test_list_entry.gd").New().As<Resource>();
        guard.Set("entry_name", "guard");
        voices.AddEntry(guard);
        int index = voices.FindEntry("guard");
        voices.SetEntryValue(index, "level", 7);
        GD.Print("Guard level: ", voices.GetEntryValue(index, "level"));

        voices.Free();
        game.Free();
        GetTree().Quit();
    }

    private void Refresh(string[] names)
    {
        GD.Print("Refresh: ", string.Join(", ", names));
    }

    private void OnDifficultyChanged(int difficulty)
    {
        GD.Print("Difficulty reached the engine: ", difficulty);
    }
}
