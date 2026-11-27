using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

namespace TruelyInteractive.LocalizedText;

/// <summary>Minimal example of Localized Text: the snippets of the wiki guide, in order. Same as scripts/gd/Example.gd.</summary>
public partial class Example : Node
{
    private const string HudCsv = "res://localized-text/resources/hud.csv";

    private readonly List<string> _enemiesAlive = ["wolf", "bear"];

    public override async void _Ready()
    {
        // The texts. The guide lists hud.csv in a catalog manifest; this project adds it from code.
        var catalog = new TextLocalizationCatalog();
        catalog.LoadFromFile(HudCsv);
        TextLocalizationServer.Singleton.AddCatalog(catalog);
        TranslationServer.SetLocale("en");

        // The file: SceneGreetingLabel in this scene has the metadata localized_text.
        await Settle();
        GD.Print(GetNode<Label>("SceneGreetingLabel").Text);

        // Bind and set values.
        var greeting = new Godot.LocalizedText();
        greeting.Key = "NPC_GREETING";
        greeting.Context = "casual";
        greeting.Bind(GetNode<Label>("GreetingLabel"));
        TextInsertionServer.SetValue("player_name", "Ada");
        await Settle();
        GD.Print(GetNode<Label>("GreetingLabel").Text);
        greeting.Args = new Godot.Collections.Dictionary { { "player_name", "Lin" } };
        GD.Print(greeting.GetText());
        greeting.Unbind(GetNode<Label>("GreetingLabel"));

        // Plurals.
        var enemies = new Godot.LocalizedText();
        enemies.Key = "ENEMIES_LEFT";
        enemies.CountArgument = "enemy.count";
        enemies.Bind(GetNode<Label>("EnemiesLabel"));
        TextInsertionServer.SetValue("enemy.count", 3);
        await Settle();
        GD.Print(GetNode<Label>("EnemiesLabel").Text);
        TextInsertionServer.SetValue("enemy.count", 1);
        await Settle();
        GD.Print(GetNode<Label>("EnemiesLabel").Text);

        // Values that change on their own.
        TextInsertionServer.RegisterProvider("enemy.count", Callable.From(() => _enemiesAlive.Count));
        enemies.RefreshMode = Godot.LocalizedText.RefreshModeEnum.EveryFrame;
        await Settle();
        GD.Print(GetNode<Label>("EnemiesLabel").Text);

        // Other properties and channels.
        var tip = new Godot.LocalizedText();
        tip.Key = "NPC_GREETING_TIP";
        tip.Bind(GetNode<Label>("GreetingLabel"), "tooltip_text");
        var line = new Godot.LocalizedText();
        line.Key = "CUTSCENE_01";
        line.Channel = "subtitles";
        line.Bind(GetNode<Label>("SubtitleLabel"));
        TranslationServer.SetLocaleFor("subtitles", "ja");
        await Settle();
        GD.Print(GetNode<Label>("GreetingLabel").TooltipText);
        GD.Print(GetNode<Label>("SubtitleLabel").Text);

        // A complete example: a HUD. The same bindings as above, driven by buttons: not repeated here.

        // The server keeps the callable of this class: take it away before it is freed.
        TextInsertionServer.UnregisterProvider("enemy.count");
        GetTree().Quit();
    }

    private async Task Settle()
    {
        // Refreshes run at the end of the frame.
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }
}
