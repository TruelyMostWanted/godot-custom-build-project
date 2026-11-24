extends FeatureCheck
## GDScript checks of 96-feature-font-format-loaders. Check the same things as scripts/cs/Issue96FeatureFontFormatLoadersCheck.cs.
##
## Test fonts: an ASCII subset of JetBrains Mono (SIL Open Font License 1.1, see resources/LICENSE.JetBrainsMono.txt)
## as .ttf, .otf (CFF outlines), .woff and .woff2, and a hand-made BMFont (resources/bmfont). They use the import mode
## "Keep File", so they stay raw files; resources/imported holds one WOFF2 that the editor imports.

const DIR := "user://96-feature-font-format-loaders"
const RES := "res://96-feature-font-format-loaders/resources"
const DYNAMIC_FONTS := ["JetBrainsMono_Subset.ttf", "JetBrainsMono_Subset.otf", "JetBrainsMono_Subset.woff", "JetBrainsMono_Subset.woff2"]
const BMFONT_FILES := ["TestBitmap.fnt", "TestBitmap_0.png"]


func _get_check_name() -> String:
	return "96-feature-font-format-loaders"


func _run_checks() -> void:
	DirAccess.make_dir_recursive_absolute(DIR.path_join("fonts"))
	for file: String in DYNAMIC_FONTS:
		_copy(RES.path_join("fonts").path_join(file), DIR.path_join("fonts").path_join(file))
	for file in BMFONT_FILES:
		_copy(RES.path_join("bmfont").path_join(file), DIR.path_join("fonts").path_join(file))

	for file: String in DYNAMIC_FONTS:
		await _check_dynamic_font_from_user(file)
	await _check_bmfont_from_user()
	_check_cache()
	_check_save_same_bytes()
	_check_save_errors()
	_check_importer_precedence()
	_check_shader_files()


func _copy(from: String, to: String) -> void:
	var bytes := FileAccess.get_file_as_bytes(from)
	expect(not bytes.is_empty(), "read %s" % from)
	var file := FileAccess.open(to, FileAccess.WRITE)
	if expect(file != null, "open %s for writing" % to):
		file.store_buffer(bytes)
		file.close()


## Shows the font in a Label and returns the Label's minimum width (> 0 when the font shapes the text).
func _label_width(font: Font) -> float:
	var label := Label.new()
	label.text = "Hello, font!"
	label.add_theme_font_override("font", font)
	label.add_theme_font_size_override("font_size", 16)
	add_child(label)
	await get_tree().process_frame
	var width := label.get_minimum_size().x
	label.queue_free()
	return width


func _check_dynamic_font_from_user(file: String) -> void:
	var path := DIR.path_join("fonts").path_join(file)
	var ext := file.get_extension()
	expect(ResourceLoader.exists(path), "ResourceLoader.exists() finds a .%s in user://" % ext)
	expect(ResourceLoader.exists(path, "Font"), "exists() with type hint Font (.%s)" % ext)
	expect(ext in ResourceLoader.get_recognized_extensions_for_type("FontFile"), "%s is a recognized FontFile extension" % ext)

	var font := ResourceLoader.load(path, "", ResourceLoader.CACHE_MODE_IGNORE) as FontFile
	if not expect(font != null, "ResourceLoader.load() returns a FontFile from user:// (.%s)" % ext):
		return
	expect(font.data == FileAccess.get_file_as_bytes(path), "FontFile.data holds the file bytes unchanged (.%s)" % ext)
	expect(font.has_char("A".unicode_at(0)), "the .%s font has the glyph A" % ext)
	expect(font.get_string_size("Hello", HORIZONTAL_ALIGNMENT_LEFT, -1, 16).x > 0.0, "get_string_size() > 0 (.%s)" % ext)
	expect(await _label_width(font) > 0.0, "a Label renders text with the .%s font" % ext)


func _check_bmfont_from_user() -> void:
	var path := DIR.path_join("fonts/TestBitmap.fnt")
	expect(ResourceLoader.exists(path, "FontFile"), "ResourceLoader.exists() finds a BMFont .fnt in user://")
	var font := ResourceLoader.load(path, "", ResourceLoader.CACHE_MODE_IGNORE) as FontFile
	if not expect(font != null, "ResourceLoader.load() returns a FontFile from a BMFont in user://"):
		return
	expect(font.data.is_empty(), "a BMFont has no font data")
	expect_equal(font.fixed_size, 12, "BMFont size from the info line")
	expect(font.has_char("A".unicode_at(0)), "the BMFont has the glyph A")
	expect_equal(font.get_texture_count(0, Vector2i(12, 0)), 1, "the BMFont page texture is loaded from the folder of the .fnt")
	expect(is_equal_approx(font.get_string_size("Hello", HORIZONTAL_ALIGNMENT_LEFT, -1, 12).x, 40.0), "BMFont advance: 5 chars x 8 px")
	expect(await _label_width(font) > 0.0, "a Label renders text with the BMFont")


func _check_cache() -> void:
	var path := DIR.path_join("fonts/JetBrainsMono_Subset.ttf")
	var first := load(path)
	var second := load(path)
	expect(first != null and first == second, "load() caches the font")
	expect_equal(first.resource_path if first else "", path, "resource_path of the cached font")


func _check_save_same_bytes() -> void:
	for file: String in DYNAMIC_FONTS:
		var source := DIR.path_join("fonts").path_join(file)
		var ext := file.get_extension()
		var font := ResourceLoader.load(source, "FontFile", ResourceLoader.CACHE_MODE_IGNORE) as FontFile
		if not expect(font != null, "load the .%s font to save it" % ext):
			continue
		var copy := DIR.path_join("copy." + ext)
		expect(ext in ResourceSaver.get_recognized_extensions(font), "ResourceSaver offers .%s for .%s data" % [ext, ext])
		expect_equal(ResourceSaver.save(font, copy), OK, "ResourceSaver.save() writes a .%s copy" % ext)
		expect(FileAccess.get_file_as_bytes(copy) == FileAccess.get_file_as_bytes(source), "the .%s copy has the same bytes as the source" % ext)
		var reloaded := ResourceLoader.load(copy, "FontFile", ResourceLoader.CACHE_MODE_IGNORE) as FontFile
		expect(reloaded != null and reloaded.data == font.data, "the .%s copy loads again" % ext)
	# TrueType data may also be stored as .otf (both are sfnt).
	var ttf := ResourceLoader.load(DIR.path_join("fonts/JetBrainsMono_Subset.ttf"), "FontFile", ResourceLoader.CACHE_MODE_IGNORE) as FontFile
	if ttf:
		expect_equal(ResourceSaver.save(ttf, DIR.path_join("ttf_as.otf")), OK, "sfnt data saves as .otf")


func _check_save_errors() -> void:
	var empty_path := DIR.path_join("empty.ttf")
	if FileAccess.file_exists(empty_path):
		DirAccess.remove_absolute(empty_path)
	var empty := FontFile.new()
	expect(ResourceSaver.get_recognized_extensions(empty).find("ttf") == -1, "no .ttf offered for a FontFile without data")
	expect_equal(ResourceSaver.save(empty, empty_path), ERR_INVALID_DATA, "a FontFile without data cannot be saved as .ttf")
	expect(not FileAccess.file_exists(empty_path), "nothing is written for a FontFile without data")

	var bmfont := ResourceLoader.load(DIR.path_join("fonts/TestBitmap.fnt"), "FontFile", ResourceLoader.CACHE_MODE_IGNORE) as FontFile
	if bmfont:
		expect_equal(ResourceSaver.save(bmfont, DIR.path_join("bmfont.ttf")), ERR_INVALID_DATA, "a BMFont cannot be saved as .ttf")
		expect_equal(ResourceSaver.save(bmfont, DIR.path_join("bmfont.tres")), OK, "a BMFont still saves as .tres")

	var ttf := ResourceLoader.load(DIR.path_join("fonts/JetBrainsMono_Subset.ttf"), "FontFile", ResourceLoader.CACHE_MODE_IGNORE) as FontFile
	if ttf:
		expect(not ("woff2" in ResourceSaver.get_recognized_extensions(ttf)), "no .woff2 offered for TrueType data")
		expect_equal(ResourceSaver.save(ttf, DIR.path_join("ttf_as.woff2")), ERR_INVALID_DATA, "TrueType data is not converted to .woff2")


func _check_importer_precedence() -> void:
	# The .import file of the imported WOFF2 sets antialiasing=0: only the importer applies it.
	var imported := load(RES.path_join("imported/JetBrainsMono_Imported.woff2")) as FontFile
	if expect(imported != null, "imported res:// font loads"):
		expect_equal(imported.antialiasing, TextServer.FONT_ANTIALIASING_NONE, "imported res:// font comes from the importer (antialiasing None)")
	# The raw fonts use the import mode "Keep File": the runtime loader reads them from res:// directly.
	var kept_path := RES.path_join("fonts/JetBrainsMono_Subset.ttf")
	var kept := ResourceLoader.load(kept_path, "", ResourceLoader.CACHE_MODE_IGNORE) as FontFile
	if expect(kept != null, "font with import mode Keep File loads from res://"):
		expect_equal(kept.antialiasing, TextServer.FONT_ANTIALIASING_GRAY, "Keep File font comes from the runtime loader (default antialiasing)")
		expect(kept.data == FileAccess.get_file_as_bytes(kept_path), "Keep File font data is the raw file")


func _check_shader_files() -> void:
	var include_path := DIR.path_join("tint.gdshaderinc")
	var shader_path := DIR.path_join("tinted.gdshader")

	var include := ShaderInclude.new()
	include.code = "uniform vec4 tint : source_color = vec4(1.0);\n"
	expect_equal(ResourceSaver.save(include, include_path), OK, "ResourceSaver.save() writes a .gdshaderinc to user://")
	var loaded_include := ResourceLoader.load(include_path, "", ResourceLoader.CACHE_MODE_IGNORE) as ShaderInclude
	if expect(loaded_include != null, "ResourceLoader.load() returns a ShaderInclude from user://"):
		expect_equal(loaded_include.code, include.code, "ShaderInclude code after save and load")

	var shader := Shader.new()
	shader.code = "shader_type canvas_item;\n#include \"%s\"\n\nvoid fragment() {\n\tCOLOR *= tint;\n}\n" % include_path
	expect_equal(ResourceSaver.save(shader, shader_path), OK, "ResourceSaver.save() writes a .gdshader to user://")
	var loaded_shader := ResourceLoader.load(shader_path, "", ResourceLoader.CACHE_MODE_IGNORE) as Shader
	if expect(loaded_shader != null, "ResourceLoader.load() returns a Shader from user://"):
		expect_equal(loaded_shader.code, shader.code, "Shader code after save and load")
		expect_equal(loaded_shader.get_mode(), Shader.MODE_CANVAS_ITEM, "shader mode from the loaded code")
