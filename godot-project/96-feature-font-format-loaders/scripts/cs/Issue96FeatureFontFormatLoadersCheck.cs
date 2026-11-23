using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.Issue96FeatureFontFormatLoaders;

/// <summary>C# checks of 96-feature-font-format-loaders. Check the same things as scripts/gd/check.gd.</summary>
public partial class Issue96FeatureFontFormatLoadersCheck : FeatureCheckCs
{
    protected override string CheckName => "96-feature-font-format-loaders";

    protected override Task RunChecks()
    {
        // Example: replace with checks of the feature's API.
        Expect((int)Engine.GetVersionInfo()["major"] == 4, "engine major version is 4");
        Expect(ClassDB.ClassExists("Node"), "ClassDB knows Node");
        return Task.CompletedTask;
    }
}
