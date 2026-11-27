using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;
using GodotDictionary = Godot.Collections.Dictionary;

namespace TruelyInteractive.LocalizedText;

/// <summary>Full API test of Localized Text (LocalizedText): every method, property, signal and enum. Same tests as scripts/gd/Test.gd.</summary>
public partial class Test : FeatureTest
{
    private const string PluralRule = "nplurals=2; plural=(n != 1);";

    private readonly TextLocalizationCatalog _catalog = new();
    private int _ticks;
    private readonly List<string> _changedTexts = [];

    protected override async Task RunTests()
    {
        string previousLocale = TranslationServer.GetLocale();
        FillCatalog();
        TextLocalizationServer.Singleton.AddCatalog(_catalog, 100);
        TranslationServer.SetLocale("en");
        TextInsertionServer.SetValue("player_name", "Ada");
        TestDefaults();
        TestGetText();
        TestPlural();
        TestChannel();
        await TestBind();
        await TestMetadataBinding();
        await TestRefreshMode();
        TextInsertionServer.RemoveValue("player_name");
        TextInsertionServer.RemoveValue("enemy.count");
        TextLocalizationServer.Singleton.RemoveCatalog(_catalog);
        TranslationServer.SetLocale(previousLocale);
    }

    private void FillCatalog()
    {
        _catalog.SetText("NPC_GREETING", "en", "Hello {player_name}!");
        _catalog.SetText("NPC_GREETING", "de", "Hallo {player_name}!");
        _catalog.SetText("NPC_GREETING", "de", "Na, {player_name}?", "casual");
        _catalog.SetPluralRule("en", PluralRule);
        _catalog.SetPluralRule("de", PluralRule);
        _catalog.SetPluralTexts("ENEMIES_LEFT", "en", ["{enemy.count} enemy left", "{enemy.count} enemies left"]);
        _catalog.SetPluralTexts("ENEMIES_LEFT", "de", ["{enemy.count} Feind", "{enemy.count} Feinde"]);
        _catalog.SetText("TICK", "en", "Tick {tick}");
    }

    private void TestDefaults()
    {
        var text = new Godot.LocalizedText();
        ExpectEqual(text.Key.ToString(), "", "Key defaults to empty");
        ExpectEqual(text.Context.ToString(), "", "Context defaults to empty");
        Expect(text.Args.Count == 0, "Args defaults to empty");
        ExpectEqual(text.CountArgument.ToString(), "", "CountArgument defaults to empty");
        ExpectEqual(text.Channel.ToString(), "", "Channel defaults to empty");
        ExpectEqual(text.RefreshMode, Godot.LocalizedText.RefreshModeEnum.OnChange, "RefreshMode defaults to OnChange");
        ExpectEqual(text.GetText(), "", "GetText without a key is empty");
    }

    private void TestGetText()
    {
        var greeting = new Godot.LocalizedText();
        greeting.Key = "NPC_GREETING";
        ExpectEqual(greeting.GetText(), "Hello Ada!", "GetText fills placeholders from TextInsertionServer");
        greeting.Args = new GodotDictionary { { "player_name", "Bob" } };
        ExpectEqual(greeting.GetText(), "Hello Bob!", "args win over server values");
        greeting.Args = new GodotDictionary();
        TranslationServer.SetLocale("de");
        ExpectEqual(greeting.GetText(), "Hallo Ada!", "GetText follows the locale");
        greeting.Context = "casual";
        ExpectEqual(greeting.GetText(), "Na, Ada?", "Context picks the text of that context");
        TranslationServer.SetLocale("en");
    }

    private void TestPlural()
    {
        var enemies = new Godot.LocalizedText();
        enemies.Key = "ENEMIES_LEFT";
        enemies.CountArgument = "enemy.count";
        TextInsertionServer.SetValue("enemy.count", 1);
        ExpectEqual(enemies.GetText(), "1 enemy left", "CountArgument from a server value picks the singular");
        TextInsertionServer.SetValue("enemy.count", 4);
        ExpectEqual(enemies.GetText(), "4 enemies left", "CountArgument from a server value picks the plural");
        enemies.Args = new GodotDictionary { { "enemy.count", 1 } };
        ExpectEqual(enemies.GetText(), "1 enemy left", "CountArgument from args");
        enemies.Args = new GodotDictionary();
        TextInsertionServer.RegisterProvider("enemy.count", Callable.From(TwoEnemies));
        ExpectEqual(enemies.GetText(), "2 enemies left", "CountArgument from a provider picks the plural");
        TextInsertionServer.UnregisterProvider("enemy.count");
    }

    private void TestChannel()
    {
        TranslationServer.SetLocaleFor("subtitles", "de");
        var subtitle = new Godot.LocalizedText();
        subtitle.Key = "NPC_GREETING";
        subtitle.Channel = "subtitles";
        ExpectEqual(subtitle.GetText(), "Hallo Ada!", "an explicit channel uses the locale of that channel");
        TranslationServer.ClearLocaleFor("subtitles");
        ExpectEqual(subtitle.GetText(), "Hello Ada!", "a channel without its own locale follows the general one");
    }

    private async Task TestBind()
    {
        var greeting = new Godot.LocalizedText();
        greeting.Key = "NPC_GREETING";
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
        ExpectEqual(label.Text, "Hello Ada!", "Bind sets the text at once");
        Expect(greeting.IsBound(label), "IsBound finds the binding");
        Expect(label.GetMeta("localized_text").AsGodotObject() == greeting, "the binding is the metadata localized_text");
        ExpectEqual(label.AutoTranslateMode, AutoTranslateModeEnum.Disabled, "a bound node is not translated twice");
        Expect(greeting.IsBound(button, "tooltip_text"), "IsBound finds another property");
        ExpectEqual(button.TooltipText, "Hello Ada!", "Bind sets another property");
        GodotDictionary buttonMeta = button.GetMeta("localized_text").AsGodotDictionary();
        ExpectEqual(buttonMeta.Count, 2, "with two properties the metadata is a dictionary");
        Expect(buttonMeta["tooltip_text"].AsGodotObject() == greeting, "the dictionary maps the property to the LocalizedText");

        greeting.TextChanged += OnTextChanged;
        TextInsertionServer.SetValue("player_name", "Bob");
        ExpectEqual(label.Text, "Hello Ada!", "a refresh waits for the end of the frame");
        await Settle();
        ExpectEqual(label.Text, "Hello Bob!", "a value change refreshes a Label");
        ExpectEqual(button.Text, "Hello Bob!", "a value change refreshes a Button");
        ExpectEqual(rich.Text, "Hello Bob!", "a value change refreshes a RichTextLabel");
        Expect(_changedTexts.SequenceEqual(["Hello Bob!"]), "TextChanged is emitted once");
        TranslationServer.SetLocale("de");
        await Settle();
        ExpectEqual(label.Text, "Hallo Bob!", "a locale change refreshes");
        TranslationServer.SetLocale("en");
        greeting.Key = "ENEMIES_LEFT";
        greeting.CountArgument = "enemy.count";
        await Settle();
        ExpectEqual(label.Text, "4 enemies left", "a property change refreshes");
        greeting.TextChanged -= OnTextChanged;

        greeting.Unbind(label);
        Expect(!greeting.IsBound(label), "Unbind removes the binding");
        Expect(!label.HasMeta("localized_text"), "Unbind removes the metadata");
        ExpectEqual(label.AutoTranslateMode, AutoTranslateModeEnum.Inherit, "Unbind restores auto translation");
        greeting.Unbind(button);
        greeting.Unbind(button, "tooltip_text");
        greeting.Unbind(rich);
        label.Free();
        button.Free();
        rich.Free();
    }

    private async Task TestMetadataBinding()
    {
        var greeting = new Godot.LocalizedText();
        greeting.Key = "NPC_GREETING";
        var npc = new Label();
        npc.SetMeta("localized_text", greeting);
        var answer = new Button();
        answer.SetMeta("localized_text", new GodotDictionary { { "text", greeting }, { "tooltip_text", greeting } });
        AddChild(npc);
        AddChild(answer);
        ExpectEqual(npc.Text, "Hello Bob!", "metadata binds when the node enters the tree");
        Expect(greeting.IsBound(npc), "the metadata LocalizedText is bound");
        ExpectEqual(answer.TooltipText, "Hello Bob!", "dictionary metadata binds each property");
        TextInsertionServer.SetValue("player_name", "Cy");
        await Settle();
        ExpectEqual(npc.Text, "Hello Cy!", "a metadata binding refreshes");
        ExpectEqual(answer.TooltipText, "Hello Cy!", "a dictionary binding refreshes");
        npc.Free();
        answer.Free();
    }

    private async Task TestRefreshMode()
    {
        TextInsertionServer.RegisterProvider("tick", Callable.From(Tick));
        var ticker = new Godot.LocalizedText();
        ticker.Key = "TICK";
        var label = new Label();
        AddChild(label);
        ticker.Bind(label);
        await Settle();
        int before = _ticks;
        TextInsertionServer.SetValue("player_name", "Dee");
        await Settle();
        ExpectEqual(_ticks, before, "a value change does not refresh texts that do not use it");
        ticker.RefreshMode = Godot.LocalizedText.RefreshModeEnum.EveryFrame;
        await Settle();
        int frameBefore = _ticks;
        await Settle();
        Expect(_ticks >= frameBefore + 2, "EveryFrame refreshes every frame");
        ExpectEqual(label.Text, $"Tick {_ticks}", "EveryFrame sets the new text");
        ticker.Unbind(label);
        TextInsertionServer.UnregisterProvider("tick");
        var enemies = new Godot.LocalizedText();
        enemies.Key = "ENEMIES_LEFT";
        enemies.CountArgument = "enemy.count";
        enemies.RefreshMode = Godot.LocalizedText.RefreshModeEnum.EveryFrame;
        TextInsertionServer.SetValue("enemy.count", 1);
        enemies.Bind(label);
        TextInsertionServer.RegisterProvider("enemy.count", Callable.From(TwoEnemies));
        await Settle();
        ExpectEqual(label.Text, "2 enemies left", "EveryFrame picks the plural form of a provider count");
        enemies.Unbind(label);
        TextInsertionServer.UnregisterProvider("enemy.count");
        label.Free();
    }

    private async Task Settle()
    {
        // Refreshes run at the end of the frame.
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private static int TwoEnemies() => 2;

    private int Tick()
    {
        _ticks++;
        return _ticks;
    }

    private void OnTextChanged(string text) => _changedTexts.Add(text);
}
