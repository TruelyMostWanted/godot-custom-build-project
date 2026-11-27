using Godot;

namespace TruelyInteractive.DialogueGraphs;

/// <summary>
/// Script node type of the wiki guide (Localization): a dialogue line with a translatable mood.
/// Named MoodLineNodeCs because the GDScript class MoodLineNode has the same global name.
/// </summary>
[Tool]
[GlobalClass]
public partial class MoodLineNodeCs : DialogueLineNode
{
    [Export] public string Mood { get; set; } = "";

    public override Godot.Collections.Array<string[]> _GetTranslatableStrings()
    {
        if (Mood.Length == 0)
        {
            return [];
        }
        return [new[] { Mood, "", "", "Mood of a line" }];
    }
}
