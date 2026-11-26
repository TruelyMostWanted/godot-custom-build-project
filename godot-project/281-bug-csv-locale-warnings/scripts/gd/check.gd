extends FeatureCheck
## GDScript checks of 281-bug-csv-locale-warnings. Check the same things as scripts/cs/Issue281BugCsvLocaleWarningsCheck.cs.
##
## The editor imports resources/units.csv (data, header name,health,speed) and resources/greetings.csv
## (translation, header keys,en,de) with the default "CSV Translation" importer before the checks run.

const DIR := "res://281-bug-csv-locale-warnings/resources/"


func _get_check_name() -> String:
	return "281-bug-csv-locale-warnings"


func _run_checks() -> void:
	# Data CSV: no column is a locale, so no bogus translations are created.
	expect(not ResourceLoader.exists(DIR + "units.health.translation"), "data CSV creates no 'health' translation")
	expect(not ResourceLoader.exists(DIR + "units.speed.translation"), "data CSV creates no 'speed' translation")
	var units := FileAccess.get_file_as_string(DIR + "units.csv")
	expect(units.begins_with("name,health,speed"), "data CSV stays readable as a plain file")

	# Translation CSV: still imported, one file per locale.
	expect(ResourceLoader.exists(DIR + "greetings.en.translation"), "translation CSV creates the 'en' translation")
	expect(ResourceLoader.exists(DIR + "greetings.de.translation"), "translation CSV creates the 'de' translation")
	var de := load(DIR + "greetings.de.translation") as Translation
	expect(de != null and de.locale == "de", "'de' translation has locale 'de'")
	expect(de != null and de.get_message("HELLO") == "Hallo", "'de' translation contains HELLO")
