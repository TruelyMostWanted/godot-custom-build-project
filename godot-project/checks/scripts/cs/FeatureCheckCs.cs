using System.Threading.Tasks;
using Godot;

namespace TruelyInteractive.Checks;

/// <summary>
/// Base of every C# feature check. Override <see cref="RunChecks"/>, call <see cref="Expect"/> for each condition.
/// The scene quits with exit code 0 when every expectation passed, else 1.
/// </summary>
public abstract partial class FeatureCheckCs : Node
{
    private int _passed;
    private int _failed;

    /// <summary>The name printed in the log. Default: the name of the root node.</summary>
    protected virtual string CheckName => Name;

    public override async void _Ready()
    {
        GD.Print($"== {CheckName} (C#)");
        try
        {
            await RunChecks();
        }
        catch (System.Exception exception)
        {
            _failed++;
            GD.PrintErr($"  FAIL  unexpected exception: {exception}");
        }
        GD.Print($"== {CheckName}: {_passed} passed, {_failed} failed");
        GetTree().Quit(_failed == 0 ? 0 : 1);
    }

    /// <summary>The checks of the feature.</summary>
    protected abstract Task RunChecks();

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
