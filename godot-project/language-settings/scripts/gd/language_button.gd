class_name LanguageButton
extends OptionButton


func _ready() -> void:
	add_item("System") # The empty locale follows the operating system.
	set_item_metadata(0, "")
	for locale: String in LanguageSettingsServer.get_available_locales():
		add_item(TranslationServer.get_locale_name(locale))
		set_item_metadata(item_count - 1, locale)
	_select_locale(LanguageSettingsServer.get_locale())
	item_selected.connect(_on_item_selected)
	TranslationServer.locale_changed.connect(_on_locale_changed)


func _on_item_selected(index: int) -> void:
	LanguageSettingsServer.set_locale(get_item_metadata(index))
	LanguageSettingsServer.save()


func _on_locale_changed(channel: StringName, locale: String, _previous_locale: String) -> void:
	if channel == &"":
		_select_locale(locale)


func _select_locale(locale: String) -> void:
	# Also called when a reset or a loaded file changed the locale.
	for i in item_count:
		if get_item_metadata(i) == locale:
			select(i) # select() doesn't emit item_selected: no loop
			return
