using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.Seeds;

/// <summary>Full API test of Seeds (SeedServer): every method, property and signal. Same tests as scripts/gd/Test.gd.</summary>
public partial class Test : FeatureTest
{
    // Golden values, computed outside the engine (FNV-1a 64, SplitMix64, PCG32). The same on every platform.
    private const string DailyText = "Daily-2026-11-23";
    private const long DailySeed = 1486713988171126959;
    private const long LootSeed = -5897437094724455890;
    private const uint LootFirstRandi = 1463604985;
    private const int TerrainNoiseSeed = 1699205639;

    private readonly List<long> _changedSeeds = [];

    protected override Task RunTests()
    {
        SeedServer.Clear();
        TestSeedFromText();
        TestSeedChangedSignal();
        TestStreams();
        TestStreamNames();
        TestState();
        TestNoise();
        TestSeedGlobalFunctions();
        TestRandomizeSeed();
        TestClear();
        return Task.CompletedTask;
    }

    private void TestSeedFromText()
    {
        long seedOnly = SeedServer.GetSeedFromText(DailyText);
        ExpectEqual(seedOnly, DailySeed, "GetSeedFromText returns the golden value");
        long seedSet = SeedServer.SetSeedFromText(DailyText);
        ExpectEqual(seedSet, DailySeed, "SetSeedFromText returns the seed");
        ExpectEqual(SeedServer.GlobalSeed, DailySeed, "SetSeedFromText sets GlobalSeed");
    }

    private void TestSeedChangedSignal()
    {
        SeedServer.SeedChanged += OnSeedChanged;
        SeedServer.GlobalSeed = 12345;
        SeedServer.SeedChanged -= OnSeedChanged;
        Expect(_changedSeeds.SequenceEqual([12345L]), "SeedChanged is emitted with the new seed");
    }

    private void TestStreams()
    {
        SeedServer.GlobalSeed = 12345;
        ExpectEqual(SeedServer.GetDerivedSeed("loot"), LootSeed, "GetDerivedSeed returns the golden value");
        RandomNumberGenerator loot = SeedServer.GetRng("loot");
        ExpectEqual((long)loot.Seed, LootSeed, "GetRng: the stream seed is the derived seed");
        ExpectEqual(loot.Randi(), LootFirstRandi, "GetRng: first Randi() is the golden value");
        Expect(SeedServer.GetRng("loot") == loot, "GetRng returns the same stream again");
        SeedServer.GlobalSeed = 12345;
        ExpectEqual(loot.Randi(), LootFirstRandi, "setting GlobalSeed restarts the stream");
        RandomNumberGenerator weather = SeedServer.GetRng("weather");
        Expect(weather.Seed != loot.Seed, "two names give two different seeds");
        SeedServer.GlobalSeed = 12346;
        Expect((long)loot.Seed != LootSeed, "another global seed gives another derived seed");
    }

    private void TestStreamNames()
    {
        Expect(SeedServer.HasRng("loot"), "HasRng finds the loot stream");
        Expect(SeedServer.HasRng("weather"), "HasRng finds the weather stream");
        Expect(SeedServer.GetRngNames().SequenceEqual(["loot", "weather"]), "GetRngNames is sorted");
        SeedServer.RemoveRng("weather");
        Expect(!SeedServer.HasRng("weather"), "RemoveRng removes a stream");
    }

    private void TestState()
    {
        SeedServer.GlobalSeed = 777;
        RandomNumberGenerator loot = SeedServer.GetRng("loot");
        loot.Randi();
        Godot.Collections.Dictionary state = SeedServer.GetState();
        uint expected = loot.Randi();
        SeedServer.GlobalSeed = 1;
        SeedServer.SetState(state);
        ExpectEqual(SeedServer.GlobalSeed, 777L, "SetState restores GlobalSeed");
        ExpectEqual(loot.Randi(), expected, "SetState restores the stream position");
        SeedServer.RemoveRng("loot");
        SeedServer.SetState(state);
        RandomNumberGenerator recreated = SeedServer.GetRng("loot");
        ExpectEqual(recreated.Randi(), expected, "SetState recreates a removed stream");
    }

    private void TestNoise()
    {
        SeedServer.GlobalSeed = 12345;
        var terrain = (FastNoiseLite)SeedServer.GetNoise("terrain");
        Expect(terrain != null, "GetNoise creates a FastNoiseLite");
        ExpectEqual(terrain.Seed, TerrainNoiseSeed, "GetNoise: the noise seed is the golden value");
        Expect(SeedServer.GetNoise("terrain") == terrain, "GetNoise returns the same noise again");
        var caves = new FastNoiseLite();
        SeedServer.RegisterNoise("caves", caves);
        int cavesSeed = (int)(SeedServer.GetDerivedSeed("caves") >> 32);
        ExpectEqual(caves.Seed, cavesSeed, "RegisterNoise sets the derived seed");
        Expect(SeedServer.HasNoise("caves"), "HasNoise finds a registered noise");
        Expect(SeedServer.GetNoiseNames().SequenceEqual(["caves", "terrain"]), "GetNoiseNames is sorted");
        SeedServer.GlobalSeed = 99;
        Expect(caves.Seed != cavesSeed, "a new global seed reseeds registered noise");
        SeedServer.UnregisterNoise("caves");
        Expect(!SeedServer.HasNoise("caves"), "UnregisterNoise removes a noise");
    }

    private void TestSeedGlobalFunctions()
    {
        SeedServer.SeedGlobalFunctions = true;
        SeedServer.GlobalSeed = 4242;
        uint first = GD.Randi();
        SeedServer.GlobalSeed = 4242;
        ExpectEqual(GD.Randi(), first, "SeedGlobalFunctions seeds GD.Randi()");
        SeedServer.SeedGlobalFunctions = false;
    }

    private void TestRandomizeSeed()
    {
        long newSeed = SeedServer.RandomizeSeed();
        ExpectEqual(SeedServer.GlobalSeed, newSeed, "RandomizeSeed sets and returns the global seed");
    }

    private void TestClear()
    {
        SeedServer.Clear();
        Expect(SeedServer.GetRngNames().Length == 0, "Clear removes all streams");
        Expect(SeedServer.GetNoiseNames().Length == 0, "Clear removes all noise");
    }

    private void OnSeedChanged(long globalSeed)
    {
        _changedSeeds.Add(globalSeed);
    }
}
