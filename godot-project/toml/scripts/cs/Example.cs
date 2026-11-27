using Godot;

namespace TruelyInteractive.TomlFormat;

/// <summary>Minimal example of TOML: the snippets of the wiki guide, in order. Same as scripts/gd/Example.gd.</summary>
/// <remarks>GameSettings, AudioSettings and PlayerProfile are the GDScript classes of text-serialization/scripts/gd.</remarks>
public partial class Example : Node
{
    private const string Scripts = "res://text-serialization/scripts/gd/";

    public override void _Ready()
    {
        // Using the API
        var config = GD.Load<Toml>("res://toml/resources/server.toml");
        GD.Print(config.GetValue("server", "port"));
        GD.Print(config.GetValue("server.limits", "players"));
        foreach (var map in config.Data.AsGodotDictionary()["maps"].AsGodotArray())
            GD.Print(map.AsGodotDictionary()["name"], ": ", map.AsGodotDictionary()["rounds"], " rounds");

        var mine = new Toml();
        mine.Parse(config.EncodeToText());
        mine.SetValue("server.limits", "players", 32);
        mine.Save("user://server.toml");

        // Start a server from its file
        StartServer();

        // Save your own resource as TOML
        Resource settings = MakeSettings();
        ResourceSaver.Save(settings, "user://settings.toml");
        var loaded = GD.Load<Resource>("user://settings.toml");
        string text = Toml.Serialize(settings);
        GD.Print(loaded.Get("title"));
        GD.Print(text);
        GetTree().Quit();
    }

    private void StartServer()
    {
        var config = new Toml();
        if (config.Load(OS.GetExecutablePath().GetBaseDir().PathJoin("server.toml")) != Error.Ok)
            config.Parse(FileAccess.GetFileAsString("res://toml/resources/server.toml"));

        var peer = new ENetMultiplayerPeer();
        int port = (int)config.GetValue("server", "port", 8080);
        int players = (int)config.GetValue("server.limits", "players", 16);
        peer.CreateServer(port, players);
        Multiplayer.MultiplayerPeer = peer;
        Engine.PhysicsTicksPerSecond = (int)(double)config.GetValue("server.limits", "tick_rate", 30.0);
        GD.Print("Server on port ", port, " for ", players, " players at ", Engine.PhysicsTicksPerSecond, " ticks");
        peer.Close();
        Multiplayer.MultiplayerPeer = null;
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
