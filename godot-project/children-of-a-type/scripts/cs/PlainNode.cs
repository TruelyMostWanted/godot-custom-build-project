using Godot;

namespace TruelyInteractive.ChildrenOfAType;

public interface IDamageable
{
    void TakeDamage(int amount);
}

/// <summary>A C# node class without [GlobalClass]: find_children() can't find it by name, FindChildren&lt;T&gt;() can.</summary>
public partial class PlainNode : Node2D, IDamageable
{
    public int Damage { get; private set; }

    public void TakeDamage(int amount)
    {
        Damage += amount;
    }
}
