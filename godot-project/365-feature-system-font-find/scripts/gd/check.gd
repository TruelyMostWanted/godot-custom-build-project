extends FeatureCheck
## GDScript checks of 365-feature-system-font-find. Check the same things as scripts/cs/Issue365FeatureSystemFontFindCheck.cs.
##
## Installed fonts differ per machine: the checks with named fonts run on Windows (Arial, Courier New and Segoe UI ship
## with every Windows), the other desktop platforms check the generic "sans-serif" family, the Web expects null.

const MISSING := "Truely Missing Font 365"
const DIR := "user://365-feature-system-font-find"


func _get_check_name() -> String:
	return "365-feature-system-font-find"


func _run_checks() -> void:
	_check_missing_font()
	if OS.has_feature("web"):
		expect(SystemFont.find("sans-serif") == null, "the Web has no system fonts: find() returns null")
		expect(not ResourceLoader.exists("sysfont://sans-serif"), "the Web has no system fonts: exists() is false")
		return
	expect(SystemFont.find("sans-serif") != null, "find(sans-serif) resolves the platform's default font")
	if OS.get_name() != "Windows":
		return
	await _check_find()
	_check_find_first()
	_check_loader()
	_check_theme()
	_check_performance()


func _check_missing_font() -> void:
	expect(SystemFont.find(MISSING) == null, "find() of a missing family returns null")
	expect(SystemFont.find_first([MISSING, ""]) == null, "find_first() without an installed family returns null")
	expect(SystemFont.find_first([]) == null, "find_first([]) returns null")
	expect(not ResourceLoader.exists("sysfont://Truely%20Missing%20Font%20365"), "ResourceLoader.exists() is false for a missing font")
	expect(ResourceLoader.load("sysfont://Truely%20Missing%20Font%20365") == null, "load() of a missing font returns null")


func _check_find() -> void:
	var font := SystemFont.find("Arial")
	if not expect(font != null, "find(Arial) returns a SystemFont"):
		return
	expect_equal(font.font_names, PackedStringArray(["Arial"]), "font_names holds only the requested family")
	expect_equal(font.get_font_name(), "Arial", "the resolved face is Arial")
	expect(font.has_char("A".unicode_at(0)), "the found font has the glyph A")
	expect(SystemFont.find("arial") != null, "the family name is compared case-insensitively")

	var bold_italic := SystemFont.find("Arial", 700, true)
	if expect(bold_italic != null, "find(Arial, 700, true) returns a SystemFont"):
		expect_equal(bold_italic.font_weight, 700, "font_weight is the requested weight")
		expect(bold_italic.font_italic, "font_italic is set")
		expect((bold_italic.get_font_style() & TextServer.FONT_ITALIC) != 0, "the resolved face is italic")

	var label := Label.new()
	label.text = "Hello, system font!"
	label.add_theme_font_override("font", font)
	add_child(label)
	await get_tree().process_frame
	expect(label.get_minimum_size().x > 0.0, "a Label renders text with the found font")
	label.queue_free()


func _check_find_first() -> void:
	var font := SystemFont.find_first([MISSING, "Segoe UI", "Arial"])
	if expect(font != null, "find_first() skips the missing family"):
		expect_equal(font.font_names, PackedStringArray(["Segoe UI"]), "find_first() returns the first installed family")


func _check_loader() -> void:
	var path := "sysfont://%s,Courier%%20New?weight=700&italic=true" % MISSING.uri_encode()
	expect(ResourceLoader.exists("sysfont://Arial"), "ResourceLoader.exists(sysfont://Arial)")
	expect(ResourceLoader.exists("sysfont://Arial", "Font"), "exists() with type hint Font")
	var font := load(path) as SystemFont
	if not expect(font != null, "load(%s) returns a SystemFont" % path):
		return
	expect_equal(font.font_names, PackedStringArray(["Courier New"]), "the first installed family of the path is used")
	expect_equal(font.font_weight, 700, "weight from the query")
	expect(font.font_italic, "italic from the query")
	expect_equal(font.resource_path, path, "resource_path is the sysfont:// path")
	expect(is_same(load(path), font), "a second load() returns the cached font")
	expect(not is_same(ResourceLoader.load(path, "", ResourceLoader.CACHE_MODE_IGNORE), font), "CACHE_MODE_IGNORE loads a new font")
	expect(load("sysfont://Arial?size=16") == null, "an unknown query option fails")


func _check_theme() -> void:
	DirAccess.make_dir_recursive_absolute(DIR)
	var theme := Theme.new()
	theme.default_font = load("sysfont://Segoe%20UI?weight=600") as Font
	expect(ResourceSaver.save(theme, DIR.path_join("theme.tres")) == OK, "save a Theme that uses a sysfont:// font")
	expect(FileAccess.get_file_as_string(DIR.path_join("theme.tres")).contains("sysfont://Segoe%20UI?weight=600"), "the Theme file references the sysfont:// path")
	var loaded := ResourceLoader.load(DIR.path_join("theme.tres"), "", ResourceLoader.CACHE_MODE_IGNORE) as Theme
	var loaded_font: SystemFont = (loaded.default_font as SystemFont) if loaded else null
	if expect(loaded_font != null, "the Theme loads with its system font"):
		expect_equal(loaded_font.font_names, PackedStringArray(["Segoe UI"]), "the Theme's font is Segoe UI")


## Prints the timings for the wiki's Measurements section.
func _check_performance() -> void:
	const RUNS := 20
	var start := Time.get_ticks_usec()
	for i in RUNS:
		SystemFont.find("Arial")
	var find_ms := (Time.get_ticks_usec() - start) / 1000.0 / RUNS
	start = Time.get_ticks_usec()
	for i in RUNS:
		SystemFont.find(MISSING)
	var missing_ms := (Time.get_ticks_usec() - start) / 1000.0 / RUNS
	start = Time.get_ticks_usec()
	for i in RUNS:
		ResourceLoader.load("sysfont://Arial", "", ResourceLoader.CACHE_MODE_IGNORE)
	var load_ms := (Time.get_ticks_usec() - start) / 1000.0 / RUNS
	var kept := load("sysfont://Arial")
	start = Time.get_ticks_usec()
	for i in 1000:
		load("sysfont://Arial")
	var cached_us := (Time.get_ticks_usec() - start) / 1000.0
	print("PERF gd: find(Arial) = %.3f ms, find(missing) = %.3f ms, load(sysfont://Arial) uncached = %.3f ms, cached = %.2f us" % [find_ms, missing_ms, load_ms, cached_us])
	expect(kept != null and find_ms < 1000.0, "find() takes less than 1 s")
