using Godot;

namespace TruelyInteractive.DialogueGraphs;

/// <summary>
/// The dialogue box of the wiki guide (step 9): shows lines and choices of a FlowGraphPlayer through its signals.
/// Used by scenes/dialogue_box_cs.tscn. Same as scripts/gd/DialogueBox.gd.
/// </summary>
public partial class DialogueBox : VBoxContainer
{
    private FlowGraphPlayer _player;

    public Label SpeakerLabel { get; private set; }
    public Label TextLabel { get; private set; }
    public VBoxContainer ChoiceBox { get; private set; }
    public Button ContinueButton { get; private set; }

    public void Attach(FlowGraphPlayer player)
    {
        _player = player;
        SpeakerLabel = GetNode<Label>("Speaker");
        TextLabel = GetNode<Label>("Text");
        ChoiceBox = GetNode<VBoxContainer>("Choices");
        ContinueButton = GetNode<Button>("Continue");
        _player.NodeWaiting += OnNodeWaiting;
        _player.Finished += endId => Hide();
        ContinueButton.Pressed += () => _player.GetPlayback().Advance();
    }

    private void OnNodeWaiting(long nodeId, FlowNode node)
    {
        Show();
        foreach (Node child in ChoiceBox.GetChildren())
        {
            child.QueueFree();
        }
        ContinueButton.Visible = node is DialogueLineNode;
        if (node is DialogueLineNode line)
        {
            SpeakerLabel.Text = line.GetTranslatedSpeakerName();
            TextLabel.Text = FormatVariables(line.GetTranslatedText(), _player.GetPlayback().GetBlackboard().Variables);
        }
        else if (node is DialogueChoiceNode)
        {
            var exits = _player.GetPlayback().GetAvailableExits();
            for (int i = 0; i < exits.Count; i++)
            {
                if (!exits[i].Enabled)
                {
                    continue;
                }
                int index = i;
                var button = new Button { Text = exits[i].Label };
                button.Pressed += () => _player.GetPlayback().Choose(index);
                ChoiceBox.AddChild(button);
            }
        }
    }

    /// <summary>Replaces "{name}" with the variable values. C# strings have no Format(Dictionary) like GDScript's String.format().</summary>
    public static string FormatVariables(string text, Godot.Collections.Dictionary variables)
    {
        foreach ((Variant name, Variant value) in variables)
        {
            text = text.Replace("{" + name + "}", value.ToString());
        }
        return text;
    }
}
