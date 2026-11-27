using Godot;

namespace TruelyInteractive.LanguageSettingsGuide;

public partial class LanguageButton : OptionButton
{
    public override void _Ready()
    {
        AddItem("System"); // The empty locale follows the operating system.
        SetItemMetadata(0, "");
        foreach (string locale in LanguageSettingsServer.GetAvailableLocales())
        {
            AddItem(TranslationServer.GetLocaleName(locale));
            SetItemMetadata(ItemCount - 1, locale);
        }
        SelectLocale(LanguageSettingsServer.GetLocale());
        ItemSelected += OnItemSelected;
        TranslationServer.LocaleChanged += OnLocaleChanged;
    }

    private void OnItemSelected(long index)
    {
        LanguageSettingsServer.SetLocale(GetItemMetadata((int)index).AsString());
        LanguageSettingsServer.Singleton.Save();
    }

    private void OnLocaleChanged(StringName channel, string locale, string previousLocale)
    {
        if (channel == "")
        {
            SelectLocale(locale);
        }
    }

    private void SelectLocale(string locale)
    {
        // Also called when a reset or a loaded file changed the locale.
        for (int i = 0; i < ItemCount; i++)
        {
            if (GetItemMetadata(i).AsString() == locale)
            {
                Select(i); // Select() doesn't emit ItemSelected: no loop
                return;
            }
        }
    }
}
