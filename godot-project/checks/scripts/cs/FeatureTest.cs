using System.Threading.Tasks;
using Godot;

namespace TruelyInteractive.Checks;

/// <summary>
/// Base of every C# test (scripts/cs/Test.cs). Override <see cref="RunTests"/>, call <see cref="Expect"/> for each condition.
/// The scene quits with exit code 0 when every expectation passed, else 1.
/// </summary>
public abstract partial class FeatureTest : Node
{
    private int _passed;
    private int _failed;

    public override async void _Ready()
    {
        string feature = ((Script)GetScript()).ResourcePath.Split('/')[2];
        GD.Print($"== {feature} (C#)");
        try
        {
            await RunTests();
        }
        catch (System.Exception exception)
        {
            _failed++;
            GD.PrintErr($"  FAIL  unexpected exception: {exception}");
        }
        GD.Print($"== {feature}: {_passed} passed, {_failed} failed");
        GetTree().Quit(_failed == 0 ? 0 : 1);
    }

    /// <summary>The tests of the feature.</summary>
    protected abstract Task RunTests();

    protected bool Expect(bool condition, string message)
    {
        if (condition)
        {
            _passed++;
            GD.Print($"  PASS  {message}");
        }
        else
        {
            _failed++;
            GD.PrintErr($"  FAIL  {message}");
        }
        return condition;
    }

    protected bool ExpectEqual<T>(T actual, T expected, string message)
        => Expect(Equals(actual, expected), $"{message} (expected {expected}, got {actual})");
}
