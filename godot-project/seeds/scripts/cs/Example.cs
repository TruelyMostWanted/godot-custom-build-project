using Godot;

namespace TruelyInteractive.Seeds;

/// <summary>Minimal example of Seeds: the snippets of the wiki guide, in order. Same as scripts/gd/Example.gd.</summary>
public partial class Example : Node
{
    public override void _Ready()
    {
        // Give each system its own random stream.
        SeedServer.GlobalSeed = 12345;
        RandomNumberGenerator loot = SeedServer.GetRng("loot");
        RandomNumberGenerator weather = SeedServer.GetRng("weather");
        int item = loot.RandiRange(0, 99);
        bool rain = weather.Randf() < 0.3f;
        GD.Print("Loot item: ", item);
        GD.Print("Rain: ", rain);

        // Offer a daily challenge.
        string date = Time.GetDateStringFromSystem(true);
        long dailySeed = SeedServer.SetSeedFromText("Daily-" + date);
        GD.Print("Daily seed: ", dailySeed);

        // Seed a noise generator.
        var terrain = (FastNoiseLite)SeedServer.GetNoise("terrain");
        terrain.Frequency = 0.02f;
        var caves = new FastNoiseLite();
        caves.NoiseType = FastNoiseLite.NoiseTypeEnum.Cellular;
        SeedServer.RegisterNoise("caves", caves);
        GD.Print("Terrain noise seed: ", terrain.Seed);

        // Store the random state in a save game.
        using (var saveFile = FileAccess.Open("user://save.dat", FileAccess.ModeFlags.Write))
        {
            saveFile.StoreVar(SeedServer.GetState());
        }
        using var loadFile = FileAccess.Open("user://save.dat", FileAccess.ModeFlags.Read);
        Godot.Collections.Dictionary state = loadFile.GetVar().AsGodotDictionary();
        SeedServer.SetState(state);
        GD.Print("Restored global seed: ", SeedServer.GlobalSeed);

        // Seed randi() and randf().
        SeedServer.SeedGlobalFunctions = true;
        SeedServer.GlobalSeed = 4242;
        GD.Print("GD.Randi() after seeding: ", GD.Randi());

        SeedServer.SeedGlobalFunctions = false;
        SeedServer.Clear();
        GetTree().Quit();
    }
}
