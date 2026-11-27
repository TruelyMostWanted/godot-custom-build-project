using Godot;

namespace TruelyInteractive.LocaleChannels;

/// <summary>The language menu of the wiki guide Locale Channels: one OptionButton per channel. Same as scripts/gd/LanguageMenu.gd.</summary>
public partial class LanguageMenu : Control
{
    public override void _Ready()
    {
        Fill(GetNode<OptionButton>("LanguageList"), "");
        Fill(GetNode<OptionButton>("InterfaceList"), "interface");
        Fill(GetNode<OptionButton>("AudioList"), "audio");
        Fill(GetNode<OptionButton>("SubtitlesList"), "subtitles");
    }

    private static void Fill(OptionButton list, StringName channel)
    {
        list.Clear();
        list.AddItem(channel == "" ? "System language" : "Same as language");
        list.SetItemMetadata(0, "");
        foreach (string locale in LanguageSettingsServer.GetAvailableLocalesFor(channel))
        {
            list.AddItem(TranslationServer.GetLocaleName(locale));
            list.SetItemMetadata(list.ItemCount - 1, locale);
            if (locale == LanguageSettingsServer.GetLocaleFor(channel))
            {
                list.Select(list.ItemCount - 1);
            }
        }
        list.ItemSelected += index =>
        {
            LanguageSettingsServer.SetLocaleFor(channel, list.GetItemMetadata((int)index).AsString());
            LanguageSettingsServer.Singleton.Save();
        };
    }
}
