using System.Collections.Generic;
using Godot;

namespace TruelyInteractive.ChildrenOfAType;

/// <summary>Minimal example of Children of a Type: the snippets of the wiki guide, in order. Same idea as scripts/gd/Example.gd.</summary>
public partial class Example : Node
{
    public override void _Ready()
    {
        // Build a level with plain C# nodes (the guide's Enemy is any node class).
        var level = new Node { Name = "Level" };
        AddChild(level);
        level.AddChild(new PlainNode());
        level.AddChild(new PlainNode());
        var menu = new Node { Name = "Menu" };
        AddChild(menu);
        menu.AddChild(new Button());

        // All enemies below the level, typed.
        foreach (PlainNode enemy in GetNode("Level").FindChildren<PlainNode>())
        {
            GD.Print("Found: ", enemy.Name);
        }

        // Interfaces work too, also for classes without [GlobalClass].
        foreach (IDamageable target in GetNode("Level").FindChildren<IDamageable>())
        {
            target.TakeDamage(10);
        }

        // Only the direct children.
        List<Button> buttons = GetNode("Menu").GetChildren<Button>();
        GD.Print("Buttons: ", buttons.Count);
        GetTree().Quit();
    }
}
