using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;
using GodotDictionary = Godot.Collections.Dictionary;

namespace TruelyInteractive.Issue345FeatureLocalizedText;

/// <summary>C# checks of 345-feature-localized-text. Check the same things as scripts/gd/check.gd.</summary>
public partial class Issue345FeatureLocalizedTextCheck : FeatureCheckCs
{
    private const string NpcGreetingScene = "res://345-feature-localized-text/packed-scenes/npc_greeting.tscn";

    private int _ticks;
    private readonly List<string> _changedTexts = [];

    protected override string CheckName => "345-feature-localized-text";

    // Refreshes are deferred to the end of the frame.
    private async Task Settle()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private void OnTextChanged(string text) => _changedTexts.Add(text);

    private static TextLocalizationCatalog MakeCatalog()
    {
        var catalog = new TextLocalizationCatalog();
        catalog.SetText("NPC_GREETING", "en", "Hello {player_name}!");
        catalog.SetText("NPC_GREETING", "de", "Hallo {player_name}!");
        catalog.SetText("NPC_GREETING", "de", "Na, {player_name}?", "casual");
        catalog.SetPluralRule("en", "nplurals=2; plural=(n != 1);");
        catalog.SetPluralRule("de", "nplurals=2; plural=(n != 1);");
        catalog.SetPluralTexts("ENEMIES_LEFT", "en", ["{enemy.count} enemy left", "{enemy.count} enemies left"]);
        catalog.SetPluralTexts("ENEMIES_LEFT", "de", ["{enemy.count} Feind", "{enemy.count} Feinde"]);
        catalog.SetText("TICK", "en", "Tick {tick}");
        return catalog;
    }

    private static LocalizedText MakeText(StringName key) => new() { Key = key };

    protected override async Task RunChecks()
    {
        Expect(ClassDB.ClassExists("LocalizedText"), "LocalizedText is a registered class");
        string previousLocale = TranslationServer.GetLocale();
        TextLocalizationCatalog catalog = MakeCatalog();
        TextLocalizationServer.AddCatalog(catalog, 100);
        TranslationServer.SetLocale("en");
        TextInsertionServer.SetValue("player_name", "Ada");

        // Defaults, as shown in the inspector.
        var defaults = new LocalizedText();
        Expect(defaults.Key == "" && defaults.Context == "" && defaults.Args.Count == 0 && defaults.CountArgument == "" && defaults.Channel == "", "empty defaults");
        ExpectEqual(defaults.RefreshMode, LocalizedText.RefreshModeEnum.OnChange, "RefreshMode defaults to OnChange");
        ExpectEqual(defaults.GetText(), "", "no key, no text");

        // NPC greeting: key, values, args, context.
        LocalizedText greeting = MakeText("NPC_GREETING");
        ExpectEqual(greeting.GetText(), "Hello Ada!", "placeholder from TextInsertionServer");
        greeting.Args = new GodotDictionary { { "player_name", "Bob" } };
        ExpectEqual(greeting.GetText(), "Hello Bob!", "args win over server values");
        greeting.Args = new GodotDictionary();
        TranslationServer.SetLocale("de");
        ExpectEqual(greeting.GetText(), "Hallo Ada!", "follows the locale");
        greeting.Context = "casual";
        ExpectEqual(greeting.GetText(), "Na, Ada?", "context");
        greeting.Context = "";

        // HUD line with a plural from a value.
        LocalizedText enemies = MakeText("ENEMIES_LEFT");
        enemies.CountArgument = "enemy.count";
        TextInsertionServer.SetValue("enemy.count", 1);
        ExpectEqual(enemies.GetText(), "1 Feind", "plural from a server value (one)");
        TextInsertionServer.SetValue("enemy.count", 4);
        ExpectEqual(enemies.GetText(), "4 Feinde", "plural from a server value (other)");
        enemies.Args = new GodotDictionary { { "enemy.count", 1 } };
        ExpectEqual(enemies.GetText(), "1 Feind", "plural from args");

        // Channel with its own locale.
        TranslationServer.SetLocaleFor("subtitles", "en");
        LocalizedText subtitle = MakeText("NPC_GREETING");
        subtitle.Channel = "subtitles";
        ExpectEqual(subtitle.GetText(), "Hello Ada!", "explicit channel uses its locale");
        TranslationServer.ClearLocaleFor("subtitles");

        // Binding Label, Button and RichTextLabel.
        var label = new Label();
        var button = new Button();
        var rich = new RichTextLabel();
        AddChild(label);
        AddChild(button);
        AddChild(rich);
        greeting.Bind(label);
        greeting.Bind(button);
        greeting.Bind(button, "tooltip_text");
        greeting.Bind(rich);
        ExpectEqual(label.Text, "Hallo Ada!", "Bind sets the text at once");
        Expect(greeting.IsBound(label) && label.GetMeta("localized_text").AsGodotObject() == greeting, "binding is the metadata localized_text");
        ExpectEqual(label.AutoTranslateMode, Node.AutoTranslateModeEnum.Disabled, "bound node is not translated twice");
        ExpectEqual(button.TooltipText, "Hallo Ada!", "other properties bind too");
        var buttonMeta = button.GetMeta("localized_text").AsGodotDictionary();
        Expect(buttonMeta.Count == 2 && buttonMeta["tooltip_text"].AsGodotObject() == greeting, "two properties: the one key holds a dictionary");

        greeting.TextChanged += OnTextChanged;
        TextInsertionServer.SetValue("player_name", "Bob");
        ExpectEqual(label.Text, "Hallo Ada!", "refresh is deferred");
        await Settle();
        ExpectEqual(label.Text, "Hallo Bob!", "value change refreshes the label");
        ExpectEqual(button.Text, "Hallo Bob!", "value change refreshes the button");
        ExpectEqual(rich.Text, "Hallo Bob!", "value change refreshes the rich text label");
        Expect(_changedTexts.Count == 1 && _changedTexts[0] == "Hallo Bob!", "TextChanged once");
        TranslationServer.SetLocale("en");
        await Settle();
        ExpectEqual(label.Text, "Hello Bob!", "locale change refreshes");
        greeting.Unbind(label);
        Expect(!greeting.IsBound(label) && !label.HasMeta("localized_text"), "Unbind removes the metadata");
        ExpectEqual(label.AutoTranslateMode, Node.AutoTranslateModeEnum.Inherit, "Unbind restores auto translation");
        greeting.Unbind(button);
        greeting.Unbind(button, "tooltip_text");
        greeting.Unbind(rich);
        greeting.TextChanged -= OnTextChanged;

        // Inspector binding: metadata saved in a scene.
        var npc = GD.Load<PackedScene>(NpcGreetingScene).Instantiate<Label>();
        AddChild(npc);
        ExpectEqual(npc.Text, "Hello Bob!", "metadata binding applies on entering the tree");
        var npcText = npc.GetMeta("localized_text").As<LocalizedText>();
        Expect(npcText != null && npcText.IsBound(npc), "scene metadata holds a bound LocalizedText");
        var answer = npc.GetNode<Button>("Answer");
        Expect(answer.Text == "Hello Bob!" && answer.TooltipText == "Hello Bob!", "dictionary metadata binds text and tooltip_text");
        TextInsertionServer.SetValue("player_name", "Cy");
        await Settle();
        ExpectEqual(npc.Text, "Hello Cy!", "scene binding refreshes");
        ExpectEqual(answer.TooltipText, "Hello Cy!", "dictionary binding refreshes");

        // Only texts using a value refresh; EveryFrame for provider values.
        _ticks = 0;
        TextInsertionServer.RegisterProvider("tick", Callable.From(() => ++_ticks));
        LocalizedText ticker = MakeText("TICK");
        var tickLabel = new Label();
        AddChild(tickLabel);
        ticker.Bind(tickLabel);
        await Settle();
        int before = _ticks;
        TextInsertionServer.SetValue("player_name", "Dee");
        await Settle();
        ExpectEqual(_ticks, before, "a value change does not touch texts that do not use it");
        ticker.RefreshMode = LocalizedText.RefreshModeEnum.EveryFrame;
        await Settle();
        int frameBefore = _ticks;
        await Settle();
        Expect(_ticks >= frameBefore + 2, "EveryFrame refreshes every frame");
        ExpectEqual(tickLabel.Text, $"Tick {_ticks}", "EveryFrame sets the new text");
        TextInsertionServer.UnregisterProvider("tick");

        await Measure();

        foreach (Node node in new Node[] { label, button, rich, npc, tickLabel })
        {
            node.QueueFree();
        }
        TextInsertionServer.RemoveValue("player_name");
        TextInsertionServer.RemoveValue("enemy.count");
        TextLocalizationServer.RemoveCatalog(catalog);
        TranslationServer.SetLocale(previousLocale);
    }

    private async Task Measure()
    {
        const int count = 1000;
        var labels = new List<Label>();
        var texts = new List<LocalizedText>();
        for (int idx = 0; idx < count; idx++)
        {
            var label = new Label();
            AddChild(label);
            labels.Add(label);
            LocalizedText text = MakeText(idx == 0 ? "ENEMIES_LEFT" : "NPC_GREETING");
            text.CountArgument = idx == 0 ? "enemy.count" : "";
            texts.Add(text);
        }
        await Settle();

        ulong start = Time.GetTicksUsec();
        for (int idx = 0; idx < count; idx++)
        {
            texts[idx].Bind(labels[idx]);
        }
        ulong bindUsec = Time.GetTicksUsec() - start;

        // Frame times include the engine's frame work; the idle frame is the baseline.
        start = Time.GetTicksUsec();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        ulong idleUsec = Time.GetTicksUsec() - start;
        start = Time.GetTicksUsec();
        TranslationServer.SetLocale("de");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        ulong localeUsec = Time.GetTicksUsec() - start;
        start = Time.GetTicksUsec();
        TextInsertionServer.SetValue("enemy.count", 7);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        ulong oneUsec = Time.GetTicksUsec() - start;
        start = Time.GetTicksUsec();
        TextInsertionServer.SetValue("player_name", "Eve");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        ulong allUsec = Time.GetTicksUsec() - start;
        await Settle();
        ExpectEqual(labels[count - 1].Text, "Hallo Eve!", "1 000 labels refreshed");
        ExpectEqual(labels[0].Text, "7 Feinde", "the plural label refreshed");
        foreach (LocalizedText text in texts)
        {
            text.RefreshMode = LocalizedText.RefreshModeEnum.EveryFrame;
        }
        await Settle();
        start = Time.GetTicksUsec();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        ulong frameUsec = Time.GetTicksUsec() - start;
        GD.Print($"345 timing (C#, {count} labels, whole frames): bind {bindUsec} us, idle frame {idleUsec} us, locale switch {localeUsec} us, value for 1 label {oneUsec} us, for {count - 1} labels {allUsec} us, EveryFrame frame {frameUsec} us");
        TranslationServer.SetLocale("en");
        foreach (Label label in labels)
        {
            label.QueueFree();
        }
    }
}
