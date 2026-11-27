using System.Linq;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.DialogueGraphs;

using FlowGraphPlayback = Godot.FlowGraphPlayback;

/// <summary>Full API test of Dialogue Graphs (DialogueLineNode, DialogueChoiceNode, DialogueOption). Same tests as scripts/gd/Test.gd.</summary>
public partial class Test : FeatureTest
{
    private const string BoxScene = "res://dialogue-graphs/scenes/dialogue_box_cs.tscn";
    private const string SamplePath = "res://dialogue-graphs/resources/shop.tres";
    private const string GraphPath = "user://dialogue_graphs_shop_cs.tres";
    private const string StatePath = "user://dialogue_graphs_state_cs.tres";
    private const string NewsText = "The king is coming, you have {gold} gold.";

    protected override async Task RunTests()
    {
        TestLineNode();
        TestOption();
        TestChoiceNode();
        TestTranslation();
        TestConditionsAndOnce();
        TestTranslatableStrings();
        TestSaveAndRestore();
        TestSampleGraph();
        await TestDialogueBox();
    }

    // Start(0) -> Line(1) -> Choice(2) [buy: gold >= 10 -> End "bought"(3), chat (once) -> MoodLineNodeCs(4) -> Choice, bye -> End "bye"(5)].
    private static FlowGraph MakeShopGraph()
    {
        var graph = new FlowGraph
        {
            Variables = [new FlowVariable { Name = "gold", Type = Variant.Type.Int, DefaultValue = 5 }],
        };
        int start = graph.AddNode(new FlowStartNode());
        int hello = graph.AddNode(new DialogueLineNode { Speaker = "merchant", SpeakerName = "Merchant", Text = "Welcome, traveler!" });
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
        int news = graph.AddNode(new MoodLineNodeCs { Speaker = "merchant", Text = NewsText, Mood = "worried" });
        int left = graph.AddNode(new FlowEndNode { EndName = "bye" });
        graph.ConnectNodes(start, "out", hello, "in");
        graph.ConnectNodes(hello, "out", choiceId, "in");
        graph.ConnectNodes(choiceId, "buy", bought, "in");
        graph.ConnectNodes(choiceId, "chat", news, "in");
        graph.ConnectNodes(news, "out", choiceId, "in");
        graph.ConnectNodes(choiceId, choice.GetOptionPort(2), left, "in");
        return graph;
    }

    private void TestLineNode()
    {
        var line = new DialogueLineNode();
        ExpectEqual(line.GetTitle(), "Dialogue Line", "the line title");
        Expect(line.GetInlineProperties().SequenceEqual(["speaker", "text"]), "Speaker and Text are inline properties");
        line.Speaker = "hero";
        ExpectEqual(line.GetTranslatedSpeakerName(), "hero", "GetTranslatedSpeakerName falls back to Speaker");
        line.SpeakerName = "Hero";
        ExpectEqual(line.GetTranslatedSpeakerName(), "Hero", "GetTranslatedSpeakerName returns SpeakerName");
        var voice = new AudioStreamWav();
        line.Voice = voice;
        Expect(line.Voice == voice, "Voice keeps the audio stream");
        var subtitle = new Subtitle();
        line.Subtitle = subtitle;
        Expect(line.Subtitle == subtitle, "Subtitle keeps the subtitle");
    }

    private void TestOption()
    {
        var option = new DialogueOption();
        ExpectEqual(option.Port, (StringName)"", "Port defaults to empty");
        ExpectEqual(option.Text, "", "Text defaults to empty");
        ExpectEqual(option.Condition, "", "Condition defaults to empty");
        Expect(!option.Once, "Once defaults to false");
    }

    private void TestChoiceNode()
    {
        var choice = new DialogueChoiceNode { Options = [new DialogueOption { Port = "buy" }, new DialogueOption()] };
        ExpectEqual(choice.GetOptionPort(0), (StringName)"buy", "GetOptionPort returns the port of a named option");
        ExpectEqual(choice.GetOptionPort(1), (StringName)"option_1", "GetOptionPort returns option_<index> for an unnamed option");
        string[] ports = choice.GetOutputPorts().Select(port => port.Name.ToString()).ToArray();
        Expect(ports.SequenceEqual(["buy", "option_1"]), "one flow output per option");
    }

    private void TestTranslation()
    {
        string locale = TranslationServer.GetLocale();
        var catalog = new TextLocalizationCatalog();
        catalog.SetText("DLG_HELLO", locale, "Welcome!", "shop");
        catalog.SetText("DLG_BYE", locale, "Goodbye!", "shop");
        TextLocalizationServer.Singleton.AddCatalog(catalog, 100);
        var line = new DialogueLineNode { Text = "DLG_HELLO" };
        ExpectEqual(line.GetTranslatedText(), "DLG_HELLO", "without context the key stays");
        line.TranslationContext = "shop";
        ExpectEqual(line.GetTranslatedText(), "Welcome!", "GetTranslatedText uses TranslationContext");
        var choice = new DialogueChoiceNode { Options = [new DialogueOption { Text = "DLG_BYE" }], TranslationContext = "shop" };
        var graph = new FlowGraph();
        int start = graph.AddNode(new FlowStartNode());
        int choiceId = graph.AddNode(choice);
        graph.ConnectNodes(start, "out", choiceId, "in");
        var playback = new FlowGraphPlayback();
        playback.Start(graph);
        var exits = playback.GetAvailableExits();
        ExpectEqual(exits[0].Label, "Goodbye!", "option labels are translated with the choice context");
        TextLocalizationServer.Singleton.RemoveCatalog(catalog);
    }

    private void TestConditionsAndOnce()
    {
        var playback = new FlowGraphPlayback();
        var finished = new System.Collections.Generic.List<long>();
        playback.Finished += endId => finished.Add(endId);
        ExpectEqual(playback.Start(MakeShopGraph()), Error.Ok, "start the shop dialogue");
        Expect(playback.GetCurrentNode() is DialogueLineNode, "waits at the first line");
        ExpectEqual(playback.Advance(), Error.Ok, "Advance leaves a line");
        var exits = playback.GetAvailableExits();
        ExpectEqual(exits.Count, 3, "one exit per option");
        Expect(!exits[0].Enabled, "condition gold >= 10 is false with 5 gold");
        ExpectEqual(exits[1].Label, "Any news?", "the exit label is the option text");
        ExpectEqual(exits[1].NodeId, 4, "the exit leads to the connected node");
        ExpectEqual(exits[2].Port, (StringName)"option_2", "the exit port of an unnamed option");
        ExpectEqual(playback.Choose(0), Error.Unavailable, "Choose on a disabled option fails");
        ExpectEqual(playback.Choose(1), Error.Ok, "Choose an enabled option");
        ExpectEqual(playback.GetCurrentNodeId(), 4, "the chosen option leads to the news line");
        playback.Advance();
        exits = playback.GetAvailableExits();
        Expect(!exits[1].Enabled, "a once option is disabled after it was taken");
        playback.SetVariable("gold", 20);
        exits = playback.GetAvailableExits();
        Expect(exits[0].Enabled, "the condition sees the new variable value");
        ExpectEqual(playback.Choose(0), Error.Ok, "Choose buy");
        Expect(finished.SequenceEqual([3L]), "the dialogue finishes at the bought end");
    }

    private void TestTranslatableStrings()
    {
        string[] ids = MakeShopGraph().GetTranslatableStrings().Select(entry => entry[0]).ToArray();
        foreach (string text in new[] { "Welcome, traveler!", "Merchant", "Buy a sword", "Any news?", "Bye", NewsText, "worried" })
        {
            Expect(ids.Contains(text), $"translatable string: {text}");
        }
    }

    private void TestSaveAndRestore()
    {
        ExpectEqual(ResourceSaver.Save(MakeShopGraph(), GraphPath), Error.Ok, "save the graph as .tres");
        var graph = ResourceLoader.Load(GraphPath, "", ResourceLoader.CacheMode.Replace) as FlowGraph;
        var playback = new FlowGraphPlayback();
        playback.Start(graph);
        playback.Advance();
        playback.Choose(1);
        playback.Advance();
        ExpectEqual(ResourceSaver.Save(playback.GetState(), StatePath), Error.Ok, "save the state");
        var state = ResourceLoader.Load(StatePath, "", ResourceLoader.CacheMode.Ignore) as FlowGraphState;
        var restored = new FlowGraphPlayback();
        var waiting = new System.Collections.Generic.List<long>();
        restored.NodeWaiting += (id, node) => waiting.Add(id);
        ExpectEqual(restored.SetState(state), Error.Ok, "SetState restores the dialogue");
        Expect(waiting.SequenceEqual([2L]), "SetState emits NodeWaiting for the choice again");
        var exits = restored.GetAvailableExits();
        Expect(!exits[1].Enabled, "a taken once option stays taken after the restore");
        ExpectEqual(restored.Choose(2), Error.Ok, "Choose bye after the restore");
        Expect(!restored.IsPlaying(), "the restored dialogue finishes");
    }

    private void TestSampleGraph()
    {
        var graph = GD.Load<FlowGraph>(SamplePath);
        Expect(graph.Validate().IsValid(), "the sample shop.tres is a valid graph");
        var choice = (DialogueChoiceNode)graph.GetNode(2);
        ExpectEqual(choice.Options.Count, 3, "the sample choice has three options");
    }

    private async Task TestDialogueBox()
    {
        var player = new FlowGraphPlayer { Graph = MakeShopGraph() };
        AddChild(player);
        var box = GD.Load<PackedScene>(BoxScene).Instantiate<DialogueBox>();
        AddChild(box);
        box.Attach(player);
        ExpectEqual(player.Play(), Error.Ok, "Play");
        ExpectEqual(box.SpeakerLabel.Text, "Merchant", "the box shows the speaker");
        ExpectEqual(box.TextLabel.Text, "Welcome, traveler!", "the box shows the line");
        box.ContinueButton.EmitSignal(BaseButton.SignalName.Pressed);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        ExpectEqual(box.ChoiceBox.GetChildCount(), 2, "the box shows the two enabled options");
        var chatButton = box.ChoiceBox.GetChild<Button>(0);
        chatButton.EmitSignal(BaseButton.SignalName.Pressed);
        ExpectEqual(box.TextLabel.Text, "The king is coming, you have 5 gold.", "the box formats variables into the line");
        box.ContinueButton.EmitSignal(BaseButton.SignalName.Pressed);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        ExpectEqual(box.ChoiceBox.GetChildCount(), 1, "only bye is left");
        var byeButton = box.ChoiceBox.GetChild<Button>(0);
        byeButton.EmitSignal(BaseButton.SignalName.Pressed);
        Expect(!player.IsPlaying(), "the dialogue finished");
        Expect(!box.Visible, "the box hides at the end");
        box.QueueFree();
        player.QueueFree();
    }
}
