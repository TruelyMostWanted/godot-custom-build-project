using Godot;

namespace TruelyInteractive.TextSerialization;

/// <summary>Minimal example of Text Serialization: the snippets of the wiki guide, in order. Same as scripts/gd/Example.gd.</summary>
/// <remarks>Item is the GDScript class of scripts/gd/item.gd: one class name can't be a GDScript and a C# global class.</remarks>
public partial class Example : Node
{
    public override void _Ready()
    {
        // Data files as resources
        var server = GD.Load<Toml>("res://toml/resources/server.toml");
        GD.Print(server.GetValue("server.limits", "players"));

        var saves = new Ini();
        if (saves.Load("user://saves.ini") != Error.Ok)
            saves.SetValue("slot1", "level", 1);
        saves.Save("user://saves.ini");
        GD.Print("Saves: ", saves.Data);

        var sections = Ini.ParseString("[a]\nb=1\n");
        GD.Print("Sections: ", sections);

        // Save your own resource in any format
        var itemScript = GD.Load<GDScript>("res://text-serialization/scripts/gd/item.gd");
        var sword = (Resource)itemScript.New();
        sword.Set("name", "Sword");
        sword.Set("rarity", 3);
        sword.Set("offset", new Vector2(0.5f, -2.0f));
        sword.Set("code", "007");
        ResourceSaver.Save(sword, "user://sword.json");
        var loaded = GD.Load<Resource>("user://sword.json");
        GD.Print("Loaded: ", loaded.Get("name"));

        string text = ResourceTextSerializer.Serialize(sword, ResourceTextSerializer.Format.Yaml);
        var copy = ResourceTextSerializer.Deserialize(text, ResourceTextSerializer.Format.Yaml);
        GD.Print("Copy: ", copy.Get("name"));

        string path = "user://sword.yml";
        var format = ResourceTextSerializer.GetFormatFromExtension(path.GetExtension());
        if (format != ResourceTextSerializer.Format.Max && ResourceTextSerializer.IsFormatAvailable(format))
        {
            using var file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
            file.StoreString(ResourceTextSerializer.Serialize(sword, format));
        }
        GD.Print(path, ":\n", FileAccess.GetFileAsString(path));

        // Options
        var options = new SerializerOptions { Indent = "", SortKeys = true };
        GD.Print(ResourceTextSerializer.Serialize(sword, ResourceTextSerializer.Format.Json, options));

        // Add a format in a C++ module: engine modules only, no script example.
        GetTree().Quit();
    }
}
