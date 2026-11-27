extends Node
## Minimal example of Localization Editor: the snippets of the wiki guide, in order.
## Wiki: https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Localization-Editor
## The table dock, the Inspector summary, the conversion and the export are editor UI and are not shown here.

const UI_CSV := "res://localization-editor/resources/ui.csv"
const UI_MISSING_CSV := "res://localization-editor/resources/ui_missing.csv"


func _ready() -> void:
	_using_the_api()
	_fill_missing_french()
	get_tree().quit()


func _using_the_api() -> void:
	# Using the API: read a key, change it, undo the change, check the catalog, write the template.
	var catalog := TextLocalizationCatalog.new()
	catalog.load_from_file(UI_CSV)

	var before: Dictionary = catalog.get_entry(&"menu.play")
	print(before)
	catalog.set_entry(&"menu.play", { "en": "Start", "de": "Starten" })
	catalog.set_entry(&"menu.play", before)

	catalog.set_entry(&"enemy.count", { "_plural": "enemy.count.plural", "en": ["{count} enemy", "{count} enemies"] })

	var entry: Dictionary = catalog.get_entry(&"menu.quit")
	catalog.remove_key(&"menu.quit")
	catalog.set_entry(&"menu.exit", entry)

	catalog.set_note(&"menu.play", "Main menu button")
	catalog.set_note(&"menu.play", "12", &"", &"max_length")
	print(catalog.get_note(&"menu.play"))

	var options := DataValidationOptions.new()
	options.max_issues = -1
	var result: DataValidation = catalog.validate(options)
	for i: int in result.get_issue_count():
		print(result.get_issue_path(i), " ", result.get_issue_field(i), " line ", result.get_issue_line(i))

	var csv_options := SerializerOptions.new()
	csv_options.csv_delimiter = ";"
	print(catalog.to_text(ResourceTextSerializer.FORMAT_CSV, csv_options))
	var pot_file: FileAccess = FileAccess.open("user://ui.pot", FileAccess.WRITE)
	pot_file.store_string(catalog.to_po())
	pot_file.close()


func _fill_missing_french() -> void:
	# A complete example: fill the missing French texts.
	var catalog := TextLocalizationCatalog.new()
	catalog.load_from_file(UI_MISSING_CSV)
	print("fr before: %d of %d translated" % [catalog.get_translated_count("fr"), catalog.key_count])

	for row: int in catalog.get_missing_rows("fr"):
		var key: StringName = catalog.get_key(row)
		var english: String = catalog.get_text(key, "en")
		catalog.set_text(key, "fr", "[TODO] " + english)
		print("filled ", key)

	print("fr after: %d of %d translated" % [catalog.get_translated_count("fr"), catalog.key_count])
	catalog.save_to_file("user://ui_filled.csv")
	var pot_file: FileAccess = FileAccess.open("user://ui.pot", FileAccess.WRITE)
	pot_file.store_string(catalog.to_po())
	pot_file.close()
