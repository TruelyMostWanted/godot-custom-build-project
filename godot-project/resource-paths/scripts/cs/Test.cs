using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.ResourcePaths;

/// <summary>Full API test of Resource Paths. Same tests as scripts/gd/Test.gd.</summary>
public partial class Test : FeatureTest
{
    protected override Task RunTests()
    {
        TestExample();
        return Task.CompletedTask;
    }

    private void TestExample()
    {
        var version = Engine.GetVersionInfo();
        ExpectEqual((int)version["major"], 4, "engine major version");
    }
}
