using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;
using GodotDictionary = Godot.Collections.Dictionary;

namespace TruelyInteractive.Issue342FeatureTextInsertionServer;

/// <summary>C# checks of 342-feature-text-insertion-server. Check the same things as scripts/gd/check.gd.</summary>
public partial class Issue342FeatureTextInsertionServerCheck : FeatureCheckCs
{
    private const string NpcValuesPath = "res://342-feature-text-insertion-server/resources/npc_values.tres";

    private int _gold = 1250;
    private readonly List<StringName> _changedNames = [];
    private int _valuesChanged;

    protected override string CheckName => "342-feature-text-insertion-server";

    private void OnValueChanged(StringName name) => _changedNames.Add(name);

    private void OnValuesChanged() => _valuesChanged++;

    protected override Task RunChecks()
    {
        Expect(Engine.HasSingleton("TextInsertionServer"), "TextInsertionServer is a registered singleton");
        string previousLocale = TranslationServer.GetLocale();
        TranslationServer.SetLocale("en");

        // NPC greeting with the player name from call args.
        ExpectEqual(TextInsertionServer.Insert("Hello {player}, welcome to {town}!", new GodotDictionary { { "player", "Mara" }, { "town", "Ashford" } }),
            "Hello Mara, welcome to Ashford!", "Insert fills placeholders from args");
        ExpectEqual(TextInsertionServer.Insert("{{player}} {player}}}", new GodotDictionary { { "player", "Mara" } }), "{player} Mara}", "{{ and }} are literal braces");
        ExpectEqual(TextInsertionServer.Insert("No placeholders"), "No placeholders", "text without braces is unchanged");

        // Inventory gold via a provider, read on every insertion.
        TextInsertionServer.RegisterProvider("gold", Callable.From(() => _gold));
        Expect(TextInsertionServer.HasProvider("gold"), "RegisterProvider registers");
        ExpectEqual(TextInsertionServer.Insert("You have {gold:number} gold."), "You have 1,250 gold.", "provider value with number modifier");
        _gold = 1500000;
        ExpectEqual(TextInsertionServer.Insert("You have {gold:number} gold."), "You have 1,500,000 gold.", "provider is called on every insertion");
        ExpectEqual(TextInsertionServer.Insert("{gold}", new GodotDictionary { { "gold", 1 } }), "1", "args win over providers");

        // Values and resources by priority.
        var npcValues = GD.Load<TextInsertionValues>(NpcValuesPath);
        Expect(npcValues != null && npcValues.GetValue("npc").AsString() == "Smith", "TextInsertionValues loads from .tres");
        var modValues = new TextInsertionValues();
        modValues.Values = new Godot.Collections.Dictionary<StringName, Variant> { { "npc", "Modded Smith" } };
        TextInsertionServer.AddValues(npcValues);
        TextInsertionServer.AddValues(modValues);
        ExpectEqual(TextInsertionServer.Insert("{npc} of {town}"), "Smith of Ashford", "higher priority resource wins");
        modValues.Priority = 10;
        ExpectEqual(TextInsertionServer.Insert("{npc}"), "Modded Smith", "priority change reorders resources");
        TextInsertionServer.SetValue("npc", "Value Smith");
        ExpectEqual(TextInsertionServer.Insert("{npc}"), "Value Smith", "SetValue wins over resources");
        Expect(TextInsertionServer.HasValue("npc") && TextInsertionServer.GetValue("npc").AsString() == "Value Smith", "GetValue and HasValue");
        TextInsertionServer.RemoveValue("npc");
        TextInsertionServer.RemoveValues(modValues);
        TextInsertionServer.RemoveValues(npcValues);

        // Translated item name.
        var translation = new Translation { Locale = "en" };
        translation.AddMessage("ITEM_SWORD", "Sword");
        TranslationServer.AddTranslation(translation);
        ExpectEqual(TextInsertionServer.Insert("You found a {item:t}. {item:t:upper}!", new GodotDictionary { { "item", "ITEM_SWORD" } }),
            "You found a Sword. SWORD!", "t translates, modifiers chain");
        TranslationServer.RemoveTranslation(translation);

        // Other modifiers.
        ExpectEqual(TextInsertionServer.Insert("{n:upper} {n:lower} {n:lower:capitalize}", new GodotDictionary { { "n", "mara STONE" } }),
            "MARA STONE mara stone Mara Stone", "upper, lower, capitalize");
        TranslationServer.SetLocale("de");
        ExpectEqual(TextInsertionServer.Insert("{x:number}", new GodotDictionary { { "x", -1234.5 } }), "-1.234,5", "number uses German separators");
        TranslationServer.SetLocale("en");
        TextInsertionServer.RegisterModifier("shout", Callable.From((Variant value) => value.AsString() + "!"));
        ExpectEqual(TextInsertionServer.Insert("{n:shout}", new GodotDictionary { { "n", "hey" } }), "hey!", "custom modifier");
        TextInsertionServer.UnregisterModifier("shout");
        Expect(!TextInsertionServer.HasModifier("shout"), "UnregisterModifier removes");
        var nested = new GodotDictionary { { "intro", "Hi {player}" }, { "player", "Mara" } };
        ExpectEqual(TextInsertionServer.Insert("{intro:f}", nested), "Hi Mara", "f inserts into the value");
        ExpectEqual(TextInsertionServer.Insert("{intro}", nested), "Hi {player}", "no recursion without f");

        // Missing value: placeholder kept (a warning is printed once).
        ExpectEqual(TextInsertionServer.Insert("Hi {nobody_342}!"), "Hi {nobody_342}!", "missing value keeps the placeholder");

        // BBCode escaping of values, not of the text.
        ExpectEqual(TextInsertionServer.Insert("[b]{p}[/b]", new GodotDictionary { { "p", "[i]x" } }, TextInsertionServer.Escape.Bbcode), "[b][lb]i]x[/b]", "Escape.Bbcode escapes values only");

        // Templates.
        TextInsertionTemplate template = TextInsertionServer.Parse("{player} has {gold} gold, {player}.");
        Expect(template.GetNames().Select(n => n.ToString()).SequenceEqual(["player", "gold"]), "template names, each once");
        Expect(template.HasName("gold") && !template.HasName("npc"), "HasName");
        ExpectEqual(TextInsertionServer.InsertTemplate(template, new GodotDictionary { { "player", "Mara" } }), "Mara has 1500000 gold, Mara.", "InsertTemplate");

        // Signals.
        TextInsertionServer.ValueChanged += OnValueChanged;
        TextInsertionServer.ValuesChanged += OnValuesChanged;
        TextInsertionServer.SetValue("hp", 3);
        TextInsertionServer.NotifyChanged("gold");
        TextInsertionServer.UnregisterProvider("gold");
        TextInsertionServer.RemoveValue("hp");
        Expect(_changedNames.Select(n => n.ToString()).SequenceEqual(["hp", "gold", "hp"]), "value_changed from SetValue, NotifyChanged, RemoveValue");
        ExpectEqual(_valuesChanged, 1, "values_changed from UnregisterProvider");
        TextInsertionServer.ValueChanged -= OnValueChanged;
        TextInsertionServer.ValuesChanged -= OnValuesChanged;

        // Timing of a parsed template.
        const string text = "Hello {name}, you have {gold} gold and {items} items.";
        var args = new GodotDictionary { { "name", "Mara" }, { "gold", "1250" }, { "items", "7" } };
        TextInsertionTemplate parsed = TextInsertionServer.Parse(text);
        const int count = 20000;
        ulong begin = Time.GetTicksUsec();
        string result = "";
        for (int idx = 0; idx < count; idx++)
        {
            result = TextInsertionServer.InsertTemplate(parsed, args);
        }
        double templateNs = (Time.GetTicksUsec() - begin) * 1000.0 / count;
        ExpectEqual(result, "Hello Mara, you have 1250 gold and 7 items.", "InsertTemplate result");
        GD.Print($"342 timing (C#): InsertTemplate {(int)templateNs} ns per call");

        TranslationServer.SetLocale(previousLocale);
        return Task.CompletedTask;
    }
}
