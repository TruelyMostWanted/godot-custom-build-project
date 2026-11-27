using Godot;

namespace TruelyInteractive.XmlFormat;

/// <summary>Minimal example of XML: the snippets of the wiki guide, in order. Same as scripts/gd/Example.gd.</summary>
/// <remarks>Item is the GDScript class of text-serialization/scripts/gd/item.gd.</remarks>
public partial class Example : Node2D
{
    [Export] public Xml LevelFile { get; set; } = GD.Load<Xml>("res://xml/resources/level.xml");
    [Export] public Godot.Collections.Dictionary<string, PackedScene> EnemyScenes { get; set; } = [];

    public override void _Ready()
    {
        // Using the API
        XmlElement root = LevelFile.GetRoot();
        GD.Print(root.GetAttribute("name"));
        GD.Print(LevelFile.FindFirst("music").Text);
        foreach (XmlElement spawn in LevelFile.FindAll("spawn"))
            GD.Print(spawn.GetAttribute("enemy"), " at ", spawn.GetAttribute("x"), ",", spawn.GetAttribute("y"));

        var save = new Xml();
        XmlElement customRoot = save.CreateRoot("level");
        customRoot.SetAttribute("name", "Custom");
        var customSpawn = new XmlElement { Name = "spawn" };
        customSpawn.SetAttribute("enemy", "slime");
        customRoot.AddChild(customSpawn);
        save.Save("user://custom_level.xml");
        GD.Print(FileAccess.GetFileAsString("user://custom_level.xml"));

        // Spawn the enemies of a level: one empty Node2D scene stands in for goblin.tscn and orc.tscn.
        var enemyScene = new PackedScene();
        var template = new Node2D();
        enemyScene.Pack(template);
        template.Free();
        EnemyScenes["goblin"] = enemyScene;
        EnemyScenes["orc"] = enemyScene;
        foreach (XmlElement spawn in LevelFile.FindAll("spawn"))
        {
            var enemy = EnemyScenes[spawn.GetAttribute("enemy")].Instantiate<Node2D>();
            enemy.Position = new Vector2(spawn.GetAttribute("x").ToInt(), spawn.GetAttribute("y").ToInt()) * 16;
            AddChild(enemy);
            GD.Print("Spawned at ", enemy.Position);
        }

        // Save your own resource as XML
        var itemScript = GD.Load<GDScript>("res://text-serialization/scripts/gd/item.gd");
        var sword = (Resource)itemScript.New();
        sword.Set("name", "Sword");
        ResourceSaver.Save(sword, "user://sword.xml");
        var loaded = GD.Load<Resource>("user://sword.xml");
        string text = Xml.Serialize(sword);
        GD.Print(loaded.Get("name"));
        GD.Print(text);
        GetTree().Quit();
    }
}
