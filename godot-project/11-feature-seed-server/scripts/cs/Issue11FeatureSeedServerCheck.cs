using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.Issue11FeatureSeedServer;

/// <summary>C# checks of 11-feature-seed-server. Check the same things as scripts/gd/check.gd.</summary>
public partial class Issue11FeatureSeedServerCheck : FeatureCheckCs
{
    // Golden values, computed outside the engine (FNV-1a 64 + SplitMix64 + PCG32).
    // They are the same on every platform and in every language.
    private const string DailyText = "Daily-2026-11-23";
    private const long DailySeed = 1486713988171126959;
    private const long LootSeed = -5897437094724455890;
    private static readonly uint[] LootFirst = [1463604985, 632838076, 1232541005, 3482282562, 3706291731];
    private const int TerrainNoiseSeed = 1699205639;

    private readonly List<long> _signalSeeds = [];

    protected override string CheckName => "11-feature-seed-server";

    private static uint[] Draw(RandomNumberGenerator rng, int count)
    {
        var values = new uint[count];
        for (int i = 0; i < count; i++)
        {
            values[i] = rng.Randi();
        }
        return values;
    }

    private void OnSeedChanged(long globalSeed) => _signalSeeds.Add(globalSeed);

    protected override Task RunChecks()
    {
        Expect(Engine.HasSingleton("SeedServer"), "SeedServer is a registered singleton");
        SeedServer.Clear();

        // Seed from text: stable value.
        Expect(SeedServer.GetSeedFromText(DailyText) == DailySeed, "GetSeedFromText gives the golden value");
        Expect(SeedServer.SetSeedFromText(DailyText) == DailySeed, "set_seed_from_text returns the seed");
        Expect(SeedServer.GlobalSeed == DailySeed, "set_seed_from_text sets global_seed");

        // Signal.
        SeedServer.SeedChanged += OnSeedChanged;
        SeedServer.GlobalSeed = 12345;
        Expect(_signalSeeds.SequenceEqual([12345L]), "seed_changed is emitted with the new seed");
        SeedServer.SeedChanged -= OnSeedChanged;

        // Derived stream: golden values.
        Expect(SeedServer.GetDerivedSeed("loot") == LootSeed, "derived seed of 'loot' is the golden value");
        RandomNumberGenerator loot = SeedServer.GetRng("loot");
        Expect(loot != null, "get_rng creates a stream");
        Expect((long)loot.Seed == LootSeed, "stream seed is the derived seed");
        Expect(Draw(loot, 5).SequenceEqual(LootFirst), "first five randi() of 'loot' are the golden values");
        Expect(SeedServer.GetRng("loot") == loot, "get_rng returns the same stream again");

        // Same seed, same numbers.
        SeedServer.GlobalSeed = 12345;
        Expect(Draw(loot, 5).SequenceEqual(LootFirst), "setting the seed again restarts the stream");

        // Two names give different numbers.
        RandomNumberGenerator other = SeedServer.GetRng("weather");
        SeedServer.GlobalSeed = 12345;
        Expect(!Draw(SeedServer.GetRng("loot"), 10).SequenceEqual(Draw(other, 10)), "two names give different numbers");

        // Different global seeds give different numbers.
        SeedServer.GlobalSeed = 12345;
        uint[] a = Draw(loot, 10);
        SeedServer.GlobalSeed = 12346;
        Expect(!Draw(loot, 10).SequenceEqual(a), "another global seed gives other numbers");

        // Bookkeeping.
        Expect(SeedServer.HasRng("loot") && SeedServer.HasRng("weather"), "has_rng knows both streams");
        Expect(SeedServer.GetRngNames().SequenceEqual(["loot", "weather"]), "get_rng_names is sorted");
        SeedServer.RemoveRng("weather");
        Expect(!SeedServer.HasRng("weather"), "remove_rng removes a stream");

        // Save and restore.
        SeedServer.GlobalSeed = 777;
        Draw(loot, 13);
        Godot.Collections.Dictionary state = SeedServer.GetState();
        uint[] first = Draw(loot, 100);
        SeedServer.GlobalSeed = 1;
        Draw(loot, 7);
        SeedServer.SetState(state);
        Expect(SeedServer.GlobalSeed == 777, "set_state restores global_seed");
        Expect(Draw(loot, 100).SequenceEqual(first), "get_state, draw 100, set_state: the same 100 numbers");
        // A stream that does not exist yet is created by set_state.
        SeedServer.RemoveRng("loot");
        SeedServer.SetState(state);
        Expect(Draw(SeedServer.GetRng("loot"), 100).SequenceEqual(first), "set_state recreates a removed stream");

        // Noise.
        SeedServer.GlobalSeed = 12345;
        var noise = SeedServer.GetNoise("terrain") as FastNoiseLite;
        Expect(noise != null, "get_noise creates a FastNoiseLite");
        Expect(noise?.Seed == TerrainNoiseSeed, "noise seed is the golden derived value");
        Expect(SeedServer.GetNoise("terrain") == noise, "get_noise returns the same noise again");
        var custom = new FastNoiseLite();
        SeedServer.RegisterNoise("caves", custom);
        Expect(custom.Seed == (int)(SeedServer.GetDerivedSeed("caves") >> 32), "register_noise sets the derived seed");
        int before = custom.Seed;
        SeedServer.GlobalSeed = 99;
        Expect(custom.Seed != before, "changing the global seed reseeds registered noise");
        Expect(SeedServer.GetNoiseNames().SequenceEqual(["caves", "terrain"]), "get_noise_names is sorted");
        SeedServer.UnregisterNoise("caves");
        Expect(!SeedServer.HasNoise("caves"), "UnregisterNoise removes a noise");

        // Global functions.
        SeedServer.SeedGlobalFunctions = true;
        SeedServer.GlobalSeed = 4242;
        uint g1 = GD.Randi();
        SeedServer.GlobalSeed = 4242;
        Expect(GD.Randi() == g1, "seed_global_functions seeds randi()");
        SeedServer.SeedGlobalFunctions = false;

        // randomize_seed.
        long r = SeedServer.RandomizeSeed();
        Expect(SeedServer.GlobalSeed == r, "randomize_seed sets and returns the seed");

        SeedServer.Clear();
        Expect(SeedServer.GetRngNames().Length == 0 && SeedServer.GetNoiseNames().Length == 0, "clear removes streams and noise");
        return Task.CompletedTask;
    }
}
