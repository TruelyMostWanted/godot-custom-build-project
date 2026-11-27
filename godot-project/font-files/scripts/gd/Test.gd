extends FeatureTest
## Full API test of Font Files (runtime font loaders, FontFile saver, SystemFont.find, sysfont:// paths, shader files).
## Same tests as scripts/cs/Test.cs. Test fonts: a subset of JetBrains Mono (SIL OFL, see resources/LICENSE.JetBrainsMono.txt)
## as .ttf, .otf, .woff and .woff2 and a hand-made BMFont. They use the import mode Keep File, so they stay raw files.
## Named system fonts are checked on Windows (Arial, Courier New and Segoe UI ship with it); the Web has no system fonts.

const DIR := "user://font-files-test"
const RES := "res://font-files/resources"
const FONT_FILES := ["JetBrainsMono_Subset.ttf", "JetBrainsMono_Subset.otf", "JetBrainsMono_Subset.woff", "JetBrainsMono_Subset.woff2"]
const BMFONT_SIZE := 12
const BMFONT_STRING_WIDTH := 40.0
const MISSING := "Truely Missing Font"
const MISSING_PATH := "sysfont://Truely%20Missing%20Font"
const BOLD := 700
const SEMIBOLD := 600
const PERF_RUNS := 20


func _run_tests() -> void:
	DirAccess.make_dir_recursive_absolute(DIR + "/fonts")
	for file_name: String in FONT_FILES:
		DirAccess.copy_absolute("%s/fonts/%s" % [RES, file_name], "%s/fonts/%s" % [DIR, file_name])
	DirAccess.copy_absolute(RES + "/bmfont/TestBitmap.fnt", DIR + "/fonts/TestBitmap.fnt")
	DirAccess.copy_absolute(RES + "/bmfont/TestBitmap_0.png", DIR + "/fonts/TestBitmap_0.png")
	_test_font_files()
	_test_bmfont()
	_test_cache()
	_test_save()
	_test_save_errors()
	_test_importer_precedence()
	_test_missing_system_font()
	if OS.has_feature("web"):
		_test_web_has_no_system_fonts()
	else:
		_test_generic_system_font()
	if OS.get_name() == "Windows":
		_test_find()
		_test_find_first()
		_test_sysfont_path()
		_test_sysfont_theme()
		_test_find_performance()
	_test_shader_files()


func _load_font(path: String) -> FontFile:
	return ResourceLoader.load(path, "FontFile", ResourceLoader.CACHE_MODE_IGNORE) as FontFile


func _test_font_files() -> void:
	for file_name: String in FONT_FILES:
		var path: String = "%s/fonts/%s" % [DIR, file_name]
		var extension: String = file_name.get_extension()
		expect(ResourceLoader.exists(path), "ResourceLoader.exists finds the .%s" % extension)
		expect(ResourceLoader.exists(path, "Font"), "ResourceLoader.exists finds the .%s with the hint Font" % extension)
		expect(extension in ResourceLoader.get_recognized_extensions_for_type("FontFile"), "%s is a recognized FontFile extension" % extension)
		var font: FontFile = _load_font(path)
		expect(font != null, ".%s loads as FontFile" % extension)
		expect(font.data == FileAccess.get_file_as_bytes(path), ".%s: data holds the file bytes unchanged" % extension)
		expect(font.has_char("A".unicode_at(0)), ".%s font has the glyph A" % extension)
		var size: Vector2 = font.get_string_size("Hello", HORIZONTAL_ALIGNMENT_LEFT, -1, 16)
		expect(size.x > 0.0, ".%s font shapes text" % extension)


func _test_bmfont() -> void:
	var path: String = DIR + "/fonts/TestBitmap.fnt"
	expect(ResourceLoader.exists(path, "FontFile"), "ResourceLoader.exists finds the BMFont .fnt")
	var font: FontFile = _load_font(path)
	expect(font != null, "the BMFont loads as FontFile")
	expect(font.data.is_empty(), "a BMFont has no font data")
	expect_equal(font.fixed_size, BMFONT_SIZE, "fixed_size is the size of the info line")
	expect(font.has_char("A".unicode_at(0)), "the BMFont has the glyph A")
	expect_equal(font.get_texture_count(0, Vector2i(BMFONT_SIZE, 0)), 1, "the page texture is loaded from the folder of the .fnt")
	var size: Vector2 = font.get_string_size("Hello", HORIZONTAL_ALIGNMENT_LEFT, -1, BMFONT_SIZE)
	expect_equal(size.x, BMFONT_STRING_WIDTH, "BMFont advance: 5 characters of 8 px")


func _test_cache() -> void:
	var path: String = DIR + "/fonts/JetBrainsMono_Subset.ttf"
	var first: Resource = load(path)
	var second: Resource = load(path)
	expect(first == second, "load caches the font")
	expect_equal(first.resource_path, path, "resource_path is the loaded path")


func _test_save() -> void:
	for file_name: String in FONT_FILES:
		var source: String = "%s/fonts/%s" % [DIR, file_name]
		var extension: String = file_name.get_extension()
		var font: FontFile = _load_font(source)
		var copy: String = "%s/copy.%s" % [DIR, extension]
		expect(extension in ResourceSaver.get_recognized_extensions(font), "ResourceSaver offers .%s for .%s data" % [extension, extension])
		expect_equal(ResourceSaver.save(font, copy), OK, "ResourceSaver.save writes a .%s copy" % extension)
		expect(FileAccess.get_file_as_bytes(copy) == FileAccess.get_file_as_bytes(source), "the .%s copy has the same bytes" % extension)
		expect(_load_font(copy).data == font.data, "the .%s copy loads again" % extension)
	var ttf: FontFile = _load_font(DIR + "/fonts/JetBrainsMono_Subset.ttf")
	expect_equal(ResourceSaver.save(ttf, DIR + "/ttf_as.otf"), OK, "sfnt data saves as .otf")


func _test_save_errors() -> void:
	var empty := FontFile.new()
	expect(not ("ttf" in ResourceSaver.get_recognized_extensions(empty)), "no .ttf is offered for a FontFile without data")
	expect_equal(ResourceSaver.save(empty, DIR + "/empty.ttf"), ERR_INVALID_DATA, "a FontFile without data cannot be saved as .ttf")
	expect(not FileAccess.file_exists(DIR + "/empty.ttf"), "nothing is written for a FontFile without data")
	var bmfont: FontFile = _load_font(DIR + "/fonts/TestBitmap.fnt")
	expect_equal(ResourceSaver.save(bmfont, DIR + "/bmfont.ttf"), ERR_INVALID_DATA, "a BMFont cannot be saved as .ttf")
	expect_equal(ResourceSaver.save(bmfont, DIR + "/bmfont.tres"), OK, "a BMFont saves as .tres")
	var ttf: FontFile = _load_font(DIR + "/fonts/JetBrainsMono_Subset.ttf")
	expect(not ("woff2" in ResourceSaver.get_recognized_extensions(ttf)), "no .woff2 is offered for TrueType data")
	expect_equal(ResourceSaver.save(ttf, DIR + "/ttf_as.woff2"), ERR_INVALID_DATA, "TrueType data is not converted to .woff2")


# The .import file of the imported WOFF2 sets antialiasing to None: only the importer applies it.
func _test_importer_precedence() -> void:
	var imported: FontFile = load(RES + "/imported/JetBrainsMono_Imported.woff2") as FontFile
	expect_equal(imported.antialiasing, TextServer.FONT_ANTIALIASING_NONE, "an imported font comes from the importer (antialiasing None)")
	var kept_path: String = RES + "/fonts/JetBrainsMono_Subset.ttf"
	var kept: FontFile = _load_font(kept_path)
	expect_equal(kept.antialiasing, TextServer.FONT_ANTIALIASING_GRAY, "a Keep File font comes from the runtime loader (default antialiasing)")
	expect(kept.data == FileAccess.get_file_as_bytes(kept_path), "the Keep File font data is the raw file")


func _test_missing_system_font() -> void:
	expect(SystemFont.find(MISSING) == null, "find of a missing family returns null")
	expect(SystemFont.find_first([MISSING, ""]) == null, "find_first without an installed family returns null")
	expect(SystemFont.find_first([]) == null, "find_first of an empty list returns null")
	expect(not ResourceLoader.exists(MISSING_PATH), "ResourceLoader.exists is false for a missing sysfont:// font")
	expect(ResourceLoader.load(MISSING_PATH) == null, "load of a missing sysfont:// font returns null")


func _test_web_has_no_system_fonts() -> void:
	expect(SystemFont.find("sans-serif") == null, "the Web has no system fonts: find returns null")
	expect(not ResourceLoader.exists("sysfont://sans-serif"), "the Web has no system fonts: exists is false")


func _test_generic_system_font() -> void:
	expect(SystemFont.find("sans-serif") != null, "find(sans-serif) resolves the platform's default font")


func _test_find() -> void:
	var font: SystemFont = SystemFont.find("Arial")
	expect(font != null, "find(Arial) returns a SystemFont")
	expect_equal(font.font_names, PackedStringArray(["Arial"]), "font_names holds only the requested family")
	expect_equal(font.get_font_name(), "Arial", "the resolved face is Arial")
	expect(font.has_char("A".unicode_at(0)), "the found font has the glyph A")
	expect(SystemFont.find("arial") != null, "the family name is compared case-insensitively")
	var bold_italic: SystemFont = SystemFont.find("Arial", BOLD, true)
	expect_equal(bold_italic.font_weight, BOLD, "font_weight is the requested weight")
	expect(bold_italic.font_italic, "font_italic is set")
	expect((bold_italic.get_font_style() & TextServer.FONT_ITALIC) != 0, "the resolved face is italic")


func _test_find_first() -> void:
	var font: SystemFont = SystemFont.find_first([MISSING, "Segoe UI", "Arial"])
	expect_equal(font.font_names, PackedStringArray(["Segoe UI"]), "find_first returns the first installed family")


func _test_sysfont_path() -> void:
	var path: String = "sysfont://%s,Courier%%20New?weight=700&italic=true" % MISSING.uri_encode()
	expect(ResourceLoader.exists("sysfont://Arial"), "ResourceLoader.exists finds sysfont://Arial")
	expect(ResourceLoader.exists("sysfont://Arial", "Font"), "ResourceLoader.exists finds sysfont://Arial with the hint Font")
	var font: SystemFont = load(path) as SystemFont
	expect_equal(font.font_names, PackedStringArray(["Courier New"]), "the first installed family of the path is used")
	expect_equal(font.font_weight, BOLD, "weight comes from the query")
	expect(font.font_italic, "italic comes from the query")
	expect_equal(font.resource_path, path, "resource_path is the sysfont:// path")
	expect(is_same(load(path), font), "a second load returns the cached font")
	expect(not is_same(ResourceLoader.load(path, "", ResourceLoader.CACHE_MODE_IGNORE), font), "CACHE_MODE_IGNORE loads a new font")
	expect(load("sysfont://Arial?size=16") == null, "an unknown query option fails")


func _test_sysfont_theme() -> void:
	var theme_path: String = DIR + "/theme.tres"
	var theme := Theme.new()
	theme.default_font = load("sysfont://Segoe%20UI?weight=600") as Font
	expect_equal(ResourceSaver.save(theme, theme_path), OK, "a Theme with a sysfont:// font saves")
	expect(FileAccess.get_file_as_string(theme_path).contains("sysfont://Segoe%20UI?weight=600"), "the Theme file references the sysfont:// path")
	var loaded: Theme = ResourceLoader.load(theme_path, "", ResourceLoader.CACHE_MODE_IGNORE) as Theme
	var font: SystemFont = loaded.default_font as SystemFont
	expect_equal(font.font_names, PackedStringArray(["Segoe UI"]), "the loaded Theme finds its system font again")


# Prints the timings for the wiki's Measurements section.
func _test_find_performance() -> void:
	var start: int = Time.get_ticks_usec()
	for i in PERF_RUNS:
		SystemFont.find("Arial")
	var find_ms: float = (Time.get_ticks_usec() - start) / 1000.0 / PERF_RUNS
	start = Time.get_ticks_usec()
	for i in PERF_RUNS:
		SystemFont.find(MISSING)
	var missing_ms: float = (Time.get_ticks_usec() - start) / 1000.0 / PERF_RUNS
	start = Time.get_ticks_usec()
	for i in PERF_RUNS:
		ResourceLoader.load("sysfont://Arial", "", ResourceLoader.CACHE_MODE_IGNORE)
	var load_ms: float = (Time.get_ticks_usec() - start) / 1000.0 / PERF_RUNS
	print("PERF gd: find(Arial) = %.3f ms, find(missing) = %.3f ms, load(sysfont://Arial) uncached = %.3f ms" % [find_ms, missing_ms, load_ms])
	expect(find_ms < 1000.0, "find takes less than 1 s")


func _test_shader_files() -> void:
	var include_path: String = DIR + "/tint.gdshaderinc"
	var shader_path: String = DIR + "/tinted.gdshader"
	var include := ShaderInclude.new()
	include.code = "uniform vec4 tint : source_color = vec4(1.0);\n"
	expect_equal(ResourceSaver.save(include, include_path), OK, "ResourceSaver.save writes a .gdshaderinc to user://")
	var loaded_include: ShaderInclude = ResourceLoader.load(include_path, "", ResourceLoader.CACHE_MODE_IGNORE) as ShaderInclude
	expect_equal(loaded_include.code, include.code, "the ShaderInclude code survives save and load")
	var shader := Shader.new()
	shader.code = "shader_type canvas_item;\n#include \"%s\"\nvoid fragment() { COLOR *= tint; }\n" % include_path
	expect_equal(ResourceSaver.save(shader, shader_path), OK, "ResourceSaver.save writes a .gdshader to user://")
	var loaded_shader: Shader = ResourceLoader.load(shader_path, "", ResourceLoader.CACHE_MODE_IGNORE) as Shader
	expect_equal(loaded_shader.code, shader.code, "the Shader code survives save and load")
	expect_equal(loaded_shader.get_mode(), Shader.MODE_CANVAS_ITEM, "the shader mode comes from the loaded code")
