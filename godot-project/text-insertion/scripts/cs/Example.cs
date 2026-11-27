using Godot;
using Godot.Collections;

namespace TruelyInteractive.TextInsertion;

/// <summary>Minimal example of Text Insertion: the snippets of the wiki guide, in order. Same as scripts/gd/Example.gd.</summary>
public partial class Example : Node
{
    private const string TextValues = "res://text-insertion/resources/text_values.tres";

    private readonly TextInsertionTemplate _template = TextInsertionServer.Parse("Greetings, {player_name}!");
    private readonly Label _greetingLabel = new();
    private string _playerName = "";

    public string PlayerName
    {
        get => _playerName;
        set
        {
            _playerName = value;
            if (!TextInsertionServer.HasProvider("player_name"))
            {
                // From now on {player_name} comes from here.
                TextInsertionServer.RegisterProvider("player_name", Callable.From(() => _playerName));
            }
            TextInsertionServer.NotifyChanged("player_name");
        }
    }

    public override void _Ready()
    {
        // The values resource. The guide lists it under Project Settings > Default Values; this project adds it from code.
        var textValues = GD.Load<TextInsertionValues>(TextValues);
        TextInsertionServer.AddValues(textValues);

        // Using the API.
        GD.Print(TextInsertionServer.Insert("Welcome to {game_name}, {player_name}!"));
        GD.Print(TextInsertionServer.Insert("{count} enemies left", new Dictionary { { "count", 3 } }));
        TextInsertionServer.RegisterProvider("player_name", Callable.From(() => "Mara"));
        GD.Print(TextInsertionServer.Insert("Greetings, {player_name:upper}!"));
        TextInsertionTemplate template = TextInsertionServer.Parse("Level cap: {max_level:number}");
        GD.Print(TextInsertionServer.InsertTemplate(template));
        GD.Print(template.HasName("max_level"));
        TextInsertionServer.UnregisterProvider("player_name");

        // A greeting with the player's name: PlayerProfile and NameGreeting of the guide live in this class.
        AddChild(_greetingLabel);
        TextInsertionServer.ValueChanged += OnValueChanged;
        TextInsertionServer.ValuesChanged += Refresh;
        Refresh();
        GD.Print(_greetingLabel.Text);
        PlayerName = "Mara"; // What TextSubmitted of the LineEdit does.
        GD.Print(_greetingLabel.Text);

        // Modifiers of your own.
        TextInsertionServer.RegisterModifier("roman", Callable.From((Variant value) => ToRoman(value.AsInt32())));
        GD.Print(TextInsertionServer.Insert("Chapter {chapter:roman}", new Dictionary { { "chapter", 4 } }));

        // RichTextLabel.
        GD.Print(TextInsertionServer.Insert("[b]{player_name}[/b]", new Dictionary { { "player_name", "[i]Mara" } }, TextInsertionServer.Escape.Bbcode));

        // The server keeps the callables of this class: take them away before it is freed.
        TextInsertionServer.UnregisterProvider("player_name");
        TextInsertionServer.UnregisterModifier("roman");
        GetTree().Quit();
    }

    public override void _ExitTree()
    {
        TextInsertionServer.ValueChanged -= OnValueChanged;
        TextInsertionServer.ValuesChanged -= Refresh;
    }

    private static string ToRoman(int number)
    {
        string[] numerals = ["", "I", "II", "III", "IV", "V"];
        return numerals[number];
    }

    private void OnValueChanged(StringName valueName)
    {
        // Only names this text uses.
        if (_template.HasName(valueName))
        {
            Refresh();
        }
    }

    private void Refresh() => _greetingLabel.Text = TextInsertionServer.InsertTemplate(_template);
}
