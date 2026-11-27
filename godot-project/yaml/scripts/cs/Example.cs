using Godot;

namespace TruelyInteractive.YamlFormat;

/// <summary>Minimal example of YAML: the snippets of the wiki guide, in order. Same as scripts/gd/Example.gd.</summary>
/// <remarks>GameSettings, AudioSettings and PlayerProfile are the GDScript classes of text-serialization/scripts/gd.</remarks>
public partial class Example : Node
{
    private const string Scripts = "res://text-serialization/scripts/gd/";
    private static readonly Yaml Stats = GD.Load<Yaml>("res://yaml/resources/enemies.yaml");

    [Export] public string EnemyName { get; set; } = "Slime";
    public int Hp { get; private set; }
    public float Speed { get; private set; }

    public override void _Ready()
    {
        // Using the API
        var enemies = GD.Load<Yaml>("res://yaml/resources/enemies.yaml");
        GD.Print(enemies.GetValue("enemies/0/name"));
        GD.Print(enemies.GetValue("enemies/1/flying", false));

        var mine = new Yaml();
        mine.Parse(enemies.EncodeToText());
        mine.SetValue("enemies/2", new Godot.Collections.Dictionary { { "name", "Ghost" }, { "hp", 20 } });
        mine.Save("user://enemies.yaml");

        // Enemy stats from the file: the guide's Enemy is a CharacterBody2D, here this node reads the stats.
        EnemyName = "Bat";
        ReadStats();
        GD.Print(EnemyName, ": ", Hp, " hp, speed ", Speed);

        // Save your own resource as YAML
        Resource settings = MakeSettings();
        ResourceSaver.Save(settings, "user://settings.yaml");
        var loaded = GD.Load<Resource>("user://settings.yaml");
        string text = Yaml.Serialize(settings);
        GD.Print(loaded.Get("title"));
        GD.Print(text);
        GetTree().Quit();
    }

    private void ReadStats()
    {
        foreach (var entry in Stats.Data.AsGodotDictionary()["enemies"].AsGodotArray())
        {
            var stats = entry.AsGodotDictionary();
            if ((string)stats["name"] == EnemyName)
            {
                Hp = (int)stats["hp"];
                Speed = (float)stats["speed"];
                return;
            }
        }
        GD.PushError($"No stats for {EnemyName} in enemies.yaml");
    }

    /// <summary>The GameSettings of the guide's file.</summary>
    private static Resource MakeSettings()
    {
        Resource settings = New("game_settings.gd");
        settings.Set("title", "Space Trader");
        settings.Set("quality", 2);
        settings.Set("resolution", new Vector2I(1920, 1080));
        settings.Set("fullscreen", true);
        settings.Set("audio", New("audio_settings.gd"));
        Resource ann = New("player_profile.gd");
        ann.Set("name", "Ann");
        ann.Set("level", 12);
        var profiles = settings.Get("profiles").AsGodotArray();
        profiles.Add(ann);
        return settings;
    }

    private static Resource New(string script)
    {
        var gdScript = GD.Load<GDScript>(Scripts + script);
        return (Resource)gdScript.New();
    }
}
