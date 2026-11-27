using Godot;

namespace TruelyInteractive.DialogueGraphs;

using FlowGraphPlayback = Godot.FlowGraphPlayback;

/// <summary>Minimal example of Dialogue Graphs: the snippets of the wiki guide, in order. Same as scripts/gd/Example.gd.</summary>
public partial class Example : Control
{
    public override void _Ready()
    {
        // Full example (with MakeShopGraph() and the DialogueBox of "First dialogue in 10 steps")
        var player = GetNode<FlowGraphPlayer>("FlowGraphPlayer");
        var box = GetNode<DialogueBox>("DialogueBox");
        player.Graph = MakeShopGraph();
        box.Attach(player);
        player.Play();
        player.GetPlayback().SetVariable("gold", 20);
        GD.Print(box.SpeakerLabel.Text, ": ", box.TextLabel.Text);

        // Lines (Voice needs an audio file: left out)
        var line = new DialogueLineNode
        {
            Speaker = "merchant",
            SpeakerName = "Merchant",
            Text = "Welcome, traveler!",
        };
        GD.Print("Line speaker: ", line.GetTranslatedSpeakerName());

        // Choices and options
        var choice = new DialogueChoiceNode
        {
            Options =
            [
                new DialogueOption { Port = "buy", Text = "Buy a sword", Condition = "gold >= 10" },
                new DialogueOption { Text = "Bye" },
            ],
        };
        GD.Print("Ports: ", choice.GetOptionPort(0), ", ", choice.GetOptionPort(1));

        // Save and restore
        ResourceSaver.Save(player.GetPlayback().GetState(), "user://dialogue_state_cs.tres");
        player.GetPlayback().SetState(GD.Load<FlowGraphState>("user://dialogue_state_cs.tres"));
        GD.Print("Restored: ", box.TextLabel.Text);

        // Localization (MoodLineNodeCs: scripts/cs/MoodLineNodeCs.cs)
        TranslationServer.SetLocale("de");
        FlowGraphPlayback playback = player.GetPlayback();
        box.TextLabel.Text = DialogueBox.FormatVariables(line.GetTranslatedText(), playback.GetBlackboard().Variables);
        GD.Print("Translated: ", box.TextLabel.Text);

        GetTree().Quit();
    }

    public static FlowGraph MakeShopGraph()
    {
        var graph = new FlowGraph
        {
            Variables = [new FlowVariable { Name = "gold", Type = Variant.Type.Int, DefaultValue = 5 }],
        };
        int start = graph.AddNode(new FlowStartNode());
        int hello = graph.AddNode(new DialogueLineNode
        {
            Speaker = "merchant",
            SpeakerName = "Merchant",
            Text = "Welcome, traveler!",
        });
        var choice = new DialogueChoiceNode
        {
            Options =
            [
                new DialogueOption { Port = "buy", Text = "Buy a sword", Condition = "gold >= 10" },
                new DialogueOption { Port = "chat", Text = "Any news?", Once = true },
                new DialogueOption { Text = "Bye" },
            ],
        };
        int choiceId = graph.AddNode(choice);
        int bought = graph.AddNode(new FlowEndNode { EndName = "bought" });
        int left = graph.AddNode(new FlowEndNode { EndName = "bye" });
        int news = graph.AddNode(new DialogueLineNode { Speaker = "merchant", Text = "The king is coming." });

        graph.ConnectNodes(start, "out", hello, "in");
        graph.ConnectNodes(hello, "out", choiceId, "in");
        graph.ConnectNodes(choiceId, "buy", bought, "in");
        graph.ConnectNodes(choiceId, "chat", news, "in"); // a node can't connect to itself: back through a line
        graph.ConnectNodes(news, "out", choiceId, "in");
        graph.ConnectNodes(choiceId, choice.GetOptionPort(2), left, "in");
        return graph;
    }
}
