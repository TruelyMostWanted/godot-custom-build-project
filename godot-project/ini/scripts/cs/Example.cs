using Godot;

namespace TruelyInteractive.IniFormat;

/// <summary>Minimal example of INI: the snippets of the wiki guide, in order. Same as scripts/gd/Example.gd.</summary>
/// <remarks>Item is the GDScript class of text-serialization/scripts/gd/item.gd.</remarks>
public partial class Example : Node
{
    public override void _Ready()
    {
        // Using the API
        var settings = GD.Load<Ini>("res://ini/resources/settings.ini");
        GD.Print(settings.GetValue("video", "width", 1280));
        GD.Print(settings.GetSections());

        var mine = new Ini();
        mine.FromConfigFile(settings.ToConfigFile());
        mine.SetValue("audio", "music", 0.5);
        mine.Save("user://settings.ini");

        // Settings a launcher can edit
        ApplySettings();

        // Save your own resource as INI
        var itemScript = GD.Load<GDScript>("res://text-serialization/scripts/gd/item.gd");
        var sword = (Resource)itemScript.New();
        sword.Set("name", "Sword");
        ResourceSaver.Save(sword, "user://sword.ini");
        var loaded = GD.Load<Resource>("user://sword.ini");
        string text = Ini.Serialize(sword);
        var copy = Ini.Deserialize(text);
        GD.Print(loaded.Get("name"), " ", copy.Get("name"));
        GD.Print(text);
        GetTree().Quit();
    }

    private void ApplySettings()
    {
        var settings = new Ini();
        if (settings.Load("user://settings.ini") != Error.Ok)
            settings.Parse(FileAccess.GetFileAsString("res://ini/resources/settings.ini"));

        GetWindow().Size = new Vector2I((int)settings.GetValue("video", "width", 1280), (int)settings.GetValue("video", "height", 720));
        bool vsync = (bool)settings.GetValue("video", "vsync", true);
        DisplayServer.WindowSetVsyncMode(vsync ? DisplayServer.VSyncMode.Enabled : DisplayServer.VSyncMode.Disabled);

        settings.Options = new SerializerOptions { IniValueStyle = SerializerOptions.IniValueStyleEnum.Plain };
        settings.Save("user://settings.ini");
        GD.Print(FileAccess.GetFileAsString("user://settings.ini"));
    }
}
