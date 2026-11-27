using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.__FEATURE_PASCAL__;

/// <summary>Full API test of __FEATURE_TITLE__. Same tests as scripts/gd/Test.gd.</summary>
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
