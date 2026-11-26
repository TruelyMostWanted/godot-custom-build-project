using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.Issue259FeatureFlowGraphDialogue;

/// <summary>C# checks of 259-feature-flow-graph-dialogue. Check the same things as scripts/gd/check.gd.</summary>
public partial class Issue259FeatureFlowGraphDialogueCheck : FeatureCheckCs
{
    private const string MoodLineScript = "res://259-feature-flow-graph-dialogue/scripts/gd/issue259_mood_line_node.gd";
    private const string GraphPath = "user://259_dialogue_cs.tres";
    private const string StatePath = "user://259_dialogue_state_cs.tres";

    protected override string CheckName => "259-feature-flow-graph-dialogue";

    protected override async Task RunChecks()
    {
        foreach (string cls in new[] { "DialogueLineNode", "DialogueChoiceNode", "DialogueOption" })
        {
            Expect(ClassDB.ClassExists(cls), $"ClassDB knows {cls}");
        }
        CheckTranslation();
        CheckDialogue();
        CheckStringsAndState();
        await CheckPlayerLabels();
    }

    // Start(0) -> Line(1) -> Choice(2) [buy: gold >= 10 -> End "bought"(3), chat (once) -> News(4) -> Choice, bye -> End "bye"(5)].
    // The news line is the GDScript node type Issue259MoodLineNode with its own translatable string.
    private static FlowGraph MakeShopGraph()
    {
        var graph = new FlowGraph
        {
            Variables = new Godot.Collections.Array<FlowVariable> { new FlowVariable { Name = "gold", Type = Variant.Type.Int, DefaultValue = 5 } },
        };
        int start = graph.AddNode(new FlowStartNode());
        int hello = graph.AddNode(new DialogueLineNode { Speaker = "merchant", SpeakerName = "Merchant", Text = "DLG_259_HELLO" });
        var choice = new DialogueChoiceNode
        {
            Options = new Godot.Collections.Array<DialogueOption>
            {
                new DialogueOption { Port = "buy", Text = "Buy a sword", Condition = "gold >= 10" },
                new DialogueOption { Port = "chat", Text = "Any news?", Once = true },
                new DialogueOption { Text = "Bye" },
            },
        };
        int choiceId = graph.AddNode(choice);
        int bought = graph.AddNode(new FlowEndNode { EndName = "bought" });
        var news = (DialogueLineNode)GD.Load<GDScript>(MoodLineScript).New();
        news.Speaker = "merchant";
        news.Text = "The king is coming, you have {gold} gold.";
        news.Set("mood", "worried");
        int newsId = graph.AddNode(news);
        int bye = graph.AddNode(new FlowEndNode { EndName = "bye" });
        graph.ConnectNodes(start, "out", hello, "in");
        graph.ConnectNodes(hello, "out", choiceId, "in");
        graph.ConnectNodes(choiceId, "buy", bought, "in");
        graph.ConnectNodes(choiceId, "chat", newsId, "in");
        graph.ConnectNodes(newsId, "out", choiceId, "in");
        graph.ConnectNodes(choiceId, choice.GetOptionPort(2), bye, "in");
        return graph;
    }

    private void CheckTranslation()
    {
        var line = new DialogueLineNode { Text = "DLG_259_KEY", TranslationContext = "shop" };
        ExpectEqual(line.GetTranslatedText(), "DLG_259_KEY", "untranslated key");
        var translation = new Translation { Locale = TranslationServer.GetLocale() };
        translation.AddMessage("DLG_259_KEY", "Welcome!", "shop");
        TranslationServer.AddTranslation(translation);
        ExpectEqual(line.GetTranslatedText(), "Welcome!", "translated through the context");
        TranslationServer.RemoveTranslation(translation);
        line.Speaker = "hero";
        ExpectEqual(line.GetTranslatedSpeakerName(), "hero", "speaker id when no speaker name");
        Expect(line.GetInlineProperties().SequenceEqual(new[] { "speaker", "text" }), "inline properties");
        ExpectEqual(line.GetTitle(), "Dialogue Line", "title");
    }

    private void CheckDialogue()
    {
        FlowGraph graph = MakeShopGraph();
        var playback = new FlowGraphPlayback();
        var finished = new List<long>();
        playback.Finished += endId => finished.Add(endId);
        ExpectEqual(playback.Start(graph), Error.Ok, "start");
        Expect(playback.IsWaiting() && playback.GetCurrentNode() is DialogueLineNode, "waits at the first line");
        ExpectEqual(playback.Advance(), Error.Ok, "advance the line");
        ExpectEqual(playback.GetCurrentNodeId(), 2, "at the choice");
        var exits = playback.GetAvailableExits();
        ExpectEqual(exits.Count, 3, "three options");
        if (exits.Count == 3)
        {
            Expect(!exits[0].Enabled, "buy is disabled: gold 5 < 10");
            ExpectEqual(exits[1].Label, "Any news?", "option label");
            ExpectEqual(exits[1].NodeId, 4, "chat leads to the news line");
            ExpectEqual(exits[2].Port.ToString(), "option_2", "default port name");
        }
        ExpectEqual(playback.Choose(1), Error.Ok, "choose chat");
        ExpectEqual(playback.GetCurrentNodeId(), 4, "news line");
        ExpectEqual(playback.Advance(), Error.Ok, "advance the news");
        Expect(!playback.GetAvailableExits()[1].Enabled, "chat is taken once");
        playback.SetVariable("gold", 20);
        Expect(playback.GetAvailableExits()[0].Enabled, "buy is enabled with 20 gold");
        ExpectEqual(playback.Choose(0), Error.Ok, "choose buy");
        ExpectEqual(string.Join(",", finished), "3", "finished at bought");
    }

    private void CheckStringsAndState()
    {
        ExpectEqual(ResourceSaver.Save(MakeShopGraph(), GraphPath), Error.Ok, "save the graph as .tres");
        var graph = ResourceLoader.Load(GraphPath, "", ResourceLoader.CacheMode.Replace) as FlowGraph;
        if (!Expect(graph != null, "load the graph"))
        {
            return;
        }
        var ids = graph.GetTranslatableStrings().Select(entry => entry[0]).ToList();
        foreach (string text in new[] { "DLG_259_HELLO", "Merchant", "Buy a sword", "Any news?", "Bye", "The king is coming, you have {gold} gold.", "worried" })
        {
            Expect(ids.Contains(text), $"translatable string: {text}");
        }

        var playback = new FlowGraphPlayback();
        playback.Start(graph);
        playback.Advance();
        playback.Choose(1);
        playback.Advance();
        ExpectEqual(ResourceSaver.Save(playback.GetState(), StatePath), Error.Ok, "save the state");
        var state = ResourceLoader.Load(StatePath, "", ResourceLoader.CacheMode.Ignore) as FlowGraphState;
        if (!Expect(state != null, "load the state"))
        {
            return;
        }
        var restored = new FlowGraphPlayback();
        var waiting = new List<long>();
        restored.NodeWaiting += (id, node) => waiting.Add(id);
        ExpectEqual(restored.SetState(state), Error.Ok, "restore the state");
        ExpectEqual(string.Join(",", waiting), "2", "the choice is shown again");
        Expect(!restored.GetAvailableExits()[1].Enabled, "the taken option stays taken");
        ExpectEqual(restored.Choose(2), Error.Ok, "choose bye after the restore");
        Expect(!restored.IsPlaying(), "finished after the restore");
    }

    // A FlowGraphPlayer wired to labels through node_waiting, as a dialogue UI would do.
    private async Task CheckPlayerLabels()
    {
        var player = new FlowGraphPlayer { Graph = MakeShopGraph() };
        AddChild(player);
        var textLabel = new Label();
        AddChild(textLabel);
        var options = new List<string>();
        player.NodeWaiting += (id, node) =>
        {
            if (node is DialogueLineNode line)
            {
                textLabel.Text = line.GetTranslatedText().Format(player.GetPlayback().GetBlackboard().Variables);
            }
            else if (node is DialogueChoiceNode)
            {
                options = player.GetPlayback().GetAvailableExits().Where(e => e.Enabled).Select(e => e.Label).ToList();
            }
        };
        ExpectEqual(player.Play(), Error.Ok, "play");
        ExpectEqual(textLabel.Text, "DLG_259_HELLO", "the label shows the line");
        player.GetPlayback().Advance();
        ExpectEqual(string.Join("|", options), "Any news?|Bye", "available options (buy is hidden)");
        player.GetPlayback().Choose(1);
        ExpectEqual(textLabel.Text, "The king is coming, you have 5 gold.", "variables formatted into the line");
        player.GetPlayback().Advance();
        ExpectEqual(string.Join("|", options), "Bye", "only bye is left");
        player.GetPlayback().Choose(2);
        Expect(!player.IsPlaying(), "the dialogue finished");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        textLabel.QueueFree();
        player.QueueFree();
    }
}
