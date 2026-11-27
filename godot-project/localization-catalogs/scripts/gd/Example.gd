extends Node
## Minimal example of Localization Catalogs: the snippets of the wiki guide, in order.
## Wiki: https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Localization-Catalogs
## The guide's res://localization/ui.csv is resources/ui.csv here; files are written to user:// instead of res://.


func _ready() -> void:
	# Using the API
	var catalog := TextLocalizationCatalog.new()
	catalog.load_from_file("res://localization-catalogs/resources/ui.csv")
	print(catalog.key_count, " ", catalog.locales)
	print(catalog.get_text(&"menu.play", "de"))
	print(catalog.get_plural_text(&"enemy.count", "en", 3))
	catalog.set_text(&"menu.options", "en", "Options")
	print(catalog.get_translated_count("de"))
	catalog.save_to_file("user://ui.lcat")

	# A translation report (an EditorScript in the guide: the body of _run()).
	var report := TextLocalizationCatalog.new()
	report.load_from_file("user://ui.lcat")
	for locale: String in report.locales:
		var missing := PackedStringArray()
		for row: int in report.get_missing_rows(locale):
			missing.append(report.get_key(row))
		print("%s: %d of %d translated, missing %s" % [locale, report.get_translated_count(locale), report.key_count, missing])

	get_tree().quit()
