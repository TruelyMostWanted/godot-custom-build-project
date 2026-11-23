using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.__FEATURE_PASCAL__;

/// <summary>C# checks of __FEATURE_NAME__. Check the same things as scripts/gd/check.gd.</summary>
public partial class __FEATURE_PASCAL__Check : FeatureCheckCs
{
    protected override string CheckName => "__FEATURE_NAME__";

    protected override Task RunChecks()
    {
        // Example: replace with checks of the feature's API.
        Expect((int)Engine.GetVersionInfo()["major"] == 4, "engine major version is 4");
        Expect(ClassDB.ClassExists("Node"), "ClassDB knows Node");
        return Task.CompletedTask;
    }
}
