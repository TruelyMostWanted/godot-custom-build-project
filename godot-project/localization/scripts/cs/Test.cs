using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.Localization;

/// <summary>
/// The pieces of Localization working together: catalog -> TextLocalizationServer -> TextInsertionServer -> Tr().
/// The full API of each piece is tested in its own guide folder. Same tests as scripts/gd/Test.gd.
/// </summary>
public partial class Test : FeatureTest
{
    private const string CatalogPath = "res://localization/resources/ui.csv";
    private const string GreetingDe = "Sei gegrüßt, Mara!";

    protected override Task RunTests()
    {
        var catalog = new TextLocalizationCatalog();
        catalog.LoadFromFile(CatalogPath);
        TextLocalizationServer.Singleton.AddCatalog(catalog);
        TranslationServer.SetLocale("de");
        TestGreeting();
        TestTrAdapter();
        TextLocalizationServer.Singleton.RemoveCatalog(catalog);
        TranslationServer.SetLocale("en");
        return Task.CompletedTask;
    }

    private void TestGreeting()
    {
        var args = new Godot.Collections.Dictionary { { "player_name", "Mara" } };
        string text = TextLocalizationServer.Translate("npc.greeting");
        ExpectEqual(text, "Sei gegrüßt, {player_name}!", "the server returns the German text with its placeholder");
        string greeting = TextInsertionServer.Insert(text, args);
        ExpectEqual(greeting, GreetingDe, "the insertion server fills the placeholder");
        string argsGreeting = TextLocalizationServer.Translate("npc.greeting", "", -1, args);
        ExpectEqual(argsGreeting, GreetingDe, "Translate() with args fills the placeholder in one call");
    }

    private void TestTrAdapter()
    {
        ExpectEqual(Tr("menu.play"), "Spielen", "Tr() reads the server's catalogs");
        TranslationServer.SetLocale("en");
        ExpectEqual(Tr("menu.play"), "Play", "Tr() follows the locale of TranslationServer");
    }
}
