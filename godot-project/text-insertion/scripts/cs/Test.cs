using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;
using GodotDictionary = Godot.Collections.Dictionary;

namespace TruelyInteractive.TextInsertion;

/// <summary>Full API test of Text Insertion (TextInsertionServer, TextInsertionValues, TextInsertionTemplate). Same tests as scripts/gd/Test.gd.</summary>
public partial class Test : FeatureTest
{
    private const string TextValues = "res://text-insertion/resources/text_values.tres";
    private const string SwordKey = "item.sword";

    private int _gold = 1250;
    private readonly List<string> _changedNames = [];
    private int _valuesChanged;

    protected override Task RunTests()
    {
        string previousLocale = TranslationServer.GetLocale();
        TranslationServer.SetLocale("en");
        TestInsert();
        TestProviders();
        TestValues();
        TestValuesResource();
        TestValuesPriority();
        TestTranslateModifier();
        TestBuiltinModifiers();
        TestCustomModifiers();
        TestEscape();
        TestTemplate();
        TestSignals();
        TranslationServer.SetLocale(previousLocale);
        return Task.CompletedTask;
    }

    private void TestInsert()
    {
        string filled = TextInsertionServer.Insert("Hello {player}, welcome to {town}!", new GodotDictionary { { "player", "Mara" }, { "town", "Ashford" } });
        ExpectEqual(filled, "Hello Mara, welcome to Ashford!", "Insert fills placeholders from args");
        string braces = TextInsertionServer.Insert("{{player}} {player}}}", new GodotDictionary { { "player", "Mara" } });
        ExpectEqual(braces, "{player} Mara}", "{{ and }} are literal braces");
        string plain = TextInsertionServer.Insert("No placeholders");
        ExpectEqual(plain, "No placeholders", "a text without braces stays the same");
        string missing = TextInsertionServer.Insert("Hi {nobody}!");
        ExpectEqual(missing, "Hi {nobody}!", "a placeholder without a value stays as written");
    }

    private void TestProviders()
    {
        TextInsertionServer.RegisterProvider("gold", Callable.From(GetGold));
        Expect(TextInsertionServer.HasProvider("gold"), "HasProvider finds a registered provider");
        ExpectEqual(TextInsertionServer.Insert("{gold}"), "1250", "Insert reads the provider");
        _gold = 1500;
        ExpectEqual(TextInsertionServer.Insert("{gold}"), "1500", "the provider is called on every insertion");
        ExpectEqual(TextInsertionServer.Insert("{gold}", new GodotDictionary { { "gold", 1 } }), "1", "args win over providers");
        TextInsertionServer.SetValue("gold", 7);
        ExpectEqual(TextInsertionServer.Insert("{gold}"), "1500", "providers win over SetValue");
        TextInsertionServer.RemoveValue("gold");
        TextInsertionServer.UnregisterProvider("gold");
        Expect(!TextInsertionServer.HasProvider("gold"), "UnregisterProvider removes the provider");
    }

    private void TestValues()
    {
        TextInsertionServer.SetValue("hp", 30);
        Expect(TextInsertionServer.HasValue("hp"), "HasValue finds a set value");
        ExpectEqual(TextInsertionServer.GetValue("hp").AsInt32(), 30, "GetValue returns the value");
        ExpectEqual(TextInsertionServer.Insert("{hp} HP"), "30 HP", "Insert reads SetValue");
        TextInsertionServer.RemoveValue("hp");
        Expect(!TextInsertionServer.HasValue("hp"), "RemoveValue removes the value");
        ExpectEqual(TextInsertionServer.GetValue("hp", 5).AsInt32(), 5, "GetValue returns the default without a value");
    }

    private void TestValuesResource()
    {
        var textValues = GD.Load<TextInsertionValues>(TextValues);
        ExpectEqual(textValues.GetValue("game_name").AsString(), "Ashfall", "TextInsertionValues loads from .tres");
        ExpectEqual(textValues.Priority, 0, "Priority defaults to 0");
        var values = new TextInsertionValues();
        values.SetValue("npc", "Smith");
        Expect(values.HasValue("npc"), "TextInsertionValues.HasValue finds a set value");
        ExpectEqual(values.Values["npc"].AsString(), "Smith", "Values holds the set value");
        values.RemoveValue("npc");
        Expect(!values.HasValue("npc"), "TextInsertionValues.RemoveValue removes the value");
        ExpectEqual(values.GetValue("npc", "none").AsString(), "none", "TextInsertionValues.GetValue returns the default");
    }

    private void TestValuesPriority()
    {
        var gameValues = new TextInsertionValues();
        gameValues.SetValue("npc", "Smith");
        var modValues = new TextInsertionValues();
        modValues.SetValue("npc", "Modded Smith");
        TextInsertionServer.AddValues(modValues);
        TextInsertionServer.AddValues(gameValues);
        ExpectEqual(TextInsertionServer.Insert("{npc}"), "Smith", "with equal priority, the resource added later wins");
        modValues.Priority = 10;
        ExpectEqual(TextInsertionServer.Insert("{npc}"), "Modded Smith", "a higher priority wins");
        TextInsertionServer.SetValue("npc", "Value Smith");
        ExpectEqual(TextInsertionServer.Insert("{npc}"), "Value Smith", "SetValue wins over resources");
        TextInsertionServer.RemoveValue("npc");
        TextInsertionServer.RemoveValues(modValues);
        ExpectEqual(TextInsertionServer.Insert("{npc}"), "Smith", "RemoveValues removes a resource");
        TextInsertionServer.RemoveValues(gameValues);
    }

    private void TestTranslateModifier()
    {
        var catalog = new TextLocalizationCatalog();
        catalog.SetText(SwordKey, "en", "Sword");
        TextLocalizationServer.Singleton.AddCatalog(catalog);
        string found = TextInsertionServer.Insert("You found a {item:t}. {item:t:upper}!", new GodotDictionary { { "item", SwordKey } });
        ExpectEqual(found, "You found a Sword. SWORD!", "t translates the value, modifiers chain");
        TextLocalizationServer.Singleton.RemoveCatalog(catalog);
    }

    private void TestBuiltinModifiers()
    {
        string cases = TextInsertionServer.Insert("{n:upper} {n:lower} {n:lower:capitalize}", new GodotDictionary { { "n", "mara STONE" } });
        ExpectEqual(cases, "MARA STONE mara stone Mara Stone", "upper, lower and capitalize");
        ExpectEqual(TextInsertionServer.Insert("{x:number}", new GodotDictionary { { "x", 1234567 } }), "1,234,567", "number uses English separators");
        TranslationServer.SetLocale("de");
        ExpectEqual(TextInsertionServer.Insert("{x:number}", new GodotDictionary { { "x", -1234.5 } }), "-1.234,5", "number uses German separators");
        TranslationServer.SetLocale("en");
        var args = new GodotDictionary { { "intro", "Hi {player}" }, { "player", "Mara" } };
        ExpectEqual(TextInsertionServer.Insert("{intro:f}", args), "Hi Mara", "f inserts placeholders inside the value");
        ExpectEqual(TextInsertionServer.Insert("{intro}", args), "Hi {player}", "without f a value is not expanded");
    }

    private void TestCustomModifiers()
    {
        TextInsertionServer.RegisterModifier("shout", Callable.From<Variant, string>(Shout));
        Expect(TextInsertionServer.HasModifier("shout"), "HasModifier finds a registered modifier");
        ExpectEqual(TextInsertionServer.Insert("{n:shout}", new GodotDictionary { { "n", "hey" } }), "hey!", "a custom modifier changes the value");
        TextInsertionServer.UnregisterModifier("shout");
        Expect(!TextInsertionServer.HasModifier("shout"), "UnregisterModifier removes the modifier");
        Expect(!TextInsertionServer.HasModifier("upper"), "HasModifier does not list built-in modifiers");
    }

    private void TestEscape()
    {
        var args = new GodotDictionary { { "p", "[i]x" } };
        string escaped = TextInsertionServer.Insert("[b]{p}[/b]", args, TextInsertionServer.Escape.Bbcode);
        ExpectEqual(escaped, "[b][lb]i]x[/b]", "Escape.Bbcode escapes the values, not the text");
        string raw = TextInsertionServer.Insert("[b]{p}[/b]", args, TextInsertionServer.Escape.None);
        ExpectEqual(raw, "[b][i]x[/b]", "Escape.None keeps the values as they are");
        string byDefault = TextInsertionServer.Insert("[b]{p}[/b]", args, TextInsertionServer.Escape.Default);
        ExpectEqual(byDefault, "[b][i]x[/b]", "Escape.Default follows the project setting (off)");
    }

    private void TestTemplate()
    {
        string source = "{player} has {gold} gold, {player}.";
        TextInsertionTemplate template = TextInsertionServer.Parse(source);
        ExpectEqual(template.GetSource(), source, "GetSource returns the parsed text");
        IEnumerable<string> names = template.GetNames().Select(name => name.ToString());
        Expect(names.SequenceEqual(["player", "gold"]), "GetNames lists each name once");
        Expect(template.HasName("gold"), "HasName finds a used name");
        Expect(!template.HasName("npc"), "HasName misses an unused name");
        string filled = TextInsertionServer.InsertTemplate(template, new GodotDictionary { { "player", "Mara" }, { "gold", 3 } });
        ExpectEqual(filled, "Mara has 3 gold, Mara.", "InsertTemplate fills the template");
        var bracketArgs = new GodotDictionary { { "player", "[b]" }, { "gold", 3 } };
        string escaped = TextInsertionServer.InsertTemplate(template, bracketArgs, TextInsertionServer.Escape.Bbcode);
        ExpectEqual(escaped, "[lb]b] has 3 gold, [lb]b].", "InsertTemplate escapes with Escape.Bbcode");
    }

    private void TestSignals()
    {
        TextInsertionServer.ValueChanged += OnValueChanged;
        TextInsertionServer.ValuesChanged += OnValuesChanged;
        TextInsertionServer.RegisterProvider("gold", Callable.From(GetGold));
        TextInsertionServer.SetValue("hp", 3);
        TextInsertionServer.NotifyChanged("gold");
        TextInsertionServer.RemoveValue("hp");
        TextInsertionServer.UnregisterProvider("gold");
        TextInsertionServer.ValueChanged -= OnValueChanged;
        TextInsertionServer.ValuesChanged -= OnValuesChanged;
        Expect(_changedNames.SequenceEqual(["hp", "gold", "hp"]), "ValueChanged from SetValue, NotifyChanged and RemoveValue");
        ExpectEqual(_valuesChanged, 2, "ValuesChanged from RegisterProvider and UnregisterProvider");
    }

    private int GetGold() => _gold;

    private static string Shout(Variant value) => value.AsString() + "!";

    private void OnValueChanged(StringName valueName) => _changedNames.Add(valueName);

    private void OnValuesChanged() => _valuesChanged++;
}
