extends FeatureTest
## The pieces of Localization working together: catalog -> TextLocalizationServer -> TextInsertionServer -> tr().
## The full API of each piece is tested in its own guide folder. Same tests as scripts/cs/Test.cs.

const CATALOG_PATH := "res://localization/resources/ui.csv"
const GREETING_DE := "Sei gegrüßt, Mara!"


func _run_tests() -> void:
	var catalog := TextLocalizationCatalog.new()
	catalog.load_from_file(CATALOG_PATH)
	TextLocalizationServer.add_catalog(catalog)
	TranslationServer.set_locale("de")
	_test_greeting()
	_test_tr_adapter()
	TextLocalizationServer.remove_catalog(catalog)
	TranslationServer.set_locale("en")


func _test_greeting() -> void:
	var args: Dictionary = { "player_name": "Mara" }
	var text: String = TextLocalizationServer.translate(&"npc.greeting")
	expect_equal(text, "Sei gegrüßt, {player_name}!", "the server returns the German text with its placeholder")
	var greeting: String = TextInsertionServer.insert(text, args)
	expect_equal(greeting, GREETING_DE, "the insertion server fills the placeholder")
	var args_greeting: String = TextLocalizationServer.translate(&"npc.greeting", &"", -1, args)
	expect_equal(args_greeting, GREETING_DE, "translate() with args fills the placeholder in one call")


func _test_tr_adapter() -> void:
	expect_equal(tr(&"menu.play"), "Spielen", "tr() reads the server's catalogs")
	TranslationServer.set_locale("en")
	expect_equal(tr(&"menu.play"), "Play", "tr() follows the locale of TranslationServer")
