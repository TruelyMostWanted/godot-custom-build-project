extends FeatureTest
## Full API test of Recursive Directory Listing (DirAccess.get_file_paths_at and its siblings). Same tests as scripts/cs/Test.cs.

const ROOT := "user://dir_listing_test"
const PCK_ROOT := "user://dir_listing_pack"
const FEATURE := "res://recursive-directory-listing"
# The files of the tree: 3 levels, mixed extensions in mixed case, a hidden file, an empty folder (sub3).
const FILES: Array[String] = ["a.PNG", "b.txt", ".hidden.png", "sub1/c.png", "sub1/d.WebP", "sub1/noext", "sub1/sub2/e.Tres", "sub1/sub2/f.json"]
const ALL_EXTENSIONS := ["json", "png", "tres", "txt", "webp"]
# A project that prints the listings of its own res:// folder, run on a pack as the main pack.
const PACK_SCRIPT := """extends SceneTree

func _initialize() -> void:
	print("PACK_FILES=", ",".join(DirAccess.get_file_paths_at("res://data", true)))
	print("PACK_IMPORT=", ",".join(DirAccess.get_file_paths_at("res://data", true, ["IMPORT"])))
	print("PACK_DIRS=", ",".join(DirAccess.get_directory_paths_at("res://data", true)))
	print("PACK_EXTENSIONS=", ",".join(DirAccess.get_file_extensions_at("res://data", true)))
	quit(0)
"""


func _run_tests() -> void:
	_remove_tree(ROOT)
	_create_tree()
	_test_file_paths()
	_test_directory_paths()
	_test_extensions()
	_test_instance_methods()
	_test_res_folder()
	_test_missing_folder()
	_test_folder_link()
	_test_pack()
	_remove_tree(ROOT)


func _create_tree() -> void:
	DirAccess.make_dir_recursive_absolute(ROOT + "/sub1/sub2")
	DirAccess.make_dir_recursive_absolute(ROOT + "/sub3")
	for relative: String in FILES:
		var file: FileAccess = FileAccess.open(ROOT.path_join(relative), FileAccess.WRITE)
		file.store_string("x")
		file.close()
	# Hidden by name on Unix, by attribute on Windows.
	FileAccess.set_hidden_attribute(ROOT + "/.hidden.png", true)


func _remove_tree(root: String) -> void:
	if not DirAccess.dir_exists_absolute(root):
		return
	var dir: DirAccess = DirAccess.open(root)
	dir.include_hidden = true
	var folders: PackedStringArray = dir.get_directory_paths(true)
	for path: String in dir.get_file_paths(true):
		DirAccess.remove_absolute(path)
	folders.reverse()
	for folder: String in folders:
		DirAccess.remove_absolute(folder)
	DirAccess.remove_absolute(root)


func _paths(base: String, entries: Array[String]) -> PackedStringArray:
	var paths := PackedStringArray()
	for entry: String in entries:
		paths.append(base.path_join(entry))
	return paths


func _test_file_paths() -> void:
	expect_equal(DirAccess.get_file_paths_at(ROOT), _paths(ROOT, ["a.PNG", "b.txt"]), "get_file_paths_at lists one folder, without hidden files")
	var all: Array[String] = ["a.PNG", "b.txt", "sub1/c.png", "sub1/d.WebP", "sub1/noext", "sub1/sub2/e.Tres", "sub1/sub2/f.json"]
	expect_equal(DirAccess.get_file_paths_at(ROOT, true), _paths(ROOT, all), "get_file_paths_at(recursive) lists all levels, sorted")
	var images: Array[String] = ["a.PNG", "sub1/c.png", "sub1/d.WebP"]
	expect_equal(DirAccess.get_file_paths_at(ROOT, true, ["png", "webp"]), _paths(ROOT, images), "the extension filter ignores case")
	expect_equal(DirAccess.get_file_paths_at(ROOT, true, [".PNG"]), _paths(ROOT, ["a.PNG", "sub1/c.png"]), "a leading dot in the filter is ignored")
	expect_equal(DirAccess.get_file_paths_at(ROOT + "/", true, ["json"]), _paths(ROOT, ["sub1/sub2/f.json"]), "a trailing slash gives no double slash")


func _test_directory_paths() -> void:
	expect_equal(DirAccess.get_directory_paths_at(ROOT), _paths(ROOT, ["sub1", "sub3"]), "get_directory_paths_at lists one folder")
	expect_equal(DirAccess.get_directory_paths_at(ROOT, true), _paths(ROOT, ["sub1", "sub1/sub2", "sub3"]), "get_directory_paths_at(recursive) lists all levels")
	expect_equal(DirAccess.get_directory_paths_at(ROOT + "/sub3", true), PackedStringArray(), "an empty folder gives an empty array")


func _test_extensions() -> void:
	expect_equal(DirAccess.get_file_extensions_at(ROOT), PackedStringArray(["png", "txt"]), "get_file_extensions_at lists one folder")
	expect_equal(DirAccess.get_file_extensions_at(ROOT, true), PackedStringArray(ALL_EXTENSIONS), "get_file_extensions_at(recursive) gives distinct lower-case extensions, sorted")


func _test_instance_methods() -> void:
	var dir: DirAccess = DirAccess.open(ROOT)
	var base: String = dir.get_current_dir()
	expect_equal(dir.get_file_paths(false, ["png"]), _paths(base, ["a.PNG"]), "get_file_paths skips hidden files by default")
	dir.include_hidden = true
	expect_equal(dir.get_file_paths(true, ["png"]), _paths(base, [".hidden.png", "a.PNG", "sub1/c.png"]), "get_file_paths honors include_hidden")
	dir.include_navigational = true
	expect_equal(dir.get_directory_paths(true), _paths(base, ["sub1", "sub1/sub2", "sub3"]), "get_directory_paths never returns . and ..")
	expect_equal(dir.get_file_extensions(true), PackedStringArray(ALL_EXTENSIONS), "get_file_extensions works on the instance")


func _test_res_folder() -> void:
	var scripts: Array[String] = ["/scripts/gd/Example.gd", "/scripts/gd/Test.gd"]
	expect_equal(DirAccess.get_file_paths_at(FEATURE, true, ["gd"]), _paths(FEATURE, scripts), "res:// keeps the res:// prefix")
	expect(DirAccess.get_directory_paths_at(FEATURE, true).has(FEATURE + "/scripts/gd"), "res:// directory paths include nested folders")


func _test_missing_folder() -> void:
	expect_equal(DirAccess.get_file_paths_at(ROOT + "/missing", true), PackedStringArray(), "a missing folder gives an empty array")


# A link to a folder (a junction on Windows, which needs no extra privileges) is listed, but not entered.
func _test_folder_link() -> void:
	var target: String = ProjectSettings.globalize_path(ROOT + "/sub1")
	var link: String = ProjectSettings.globalize_path(ROOT + "/link")
	var code: int = 0
	if OS.get_name() == "Windows":
		code = OS.execute("cmd.exe", ["/c", "mklink", "/J", link.replace("/", "\\"), target.replace("/", "\\")])
	else:
		code = OS.execute("ln", ["-s", target, link])
	expect_equal(code, 0, "the folder link is created")
	expect_equal(DirAccess.get_directory_paths_at(ROOT, true), _paths(ROOT, ["link", "sub1", "sub1/sub2", "sub3"]), "a linked folder is listed, but not entered")
	expect_equal(DirAccess.get_file_paths_at(ROOT, true, ["json"]), _paths(ROOT, ["sub1/sub2/f.json"]), "the files behind the link are not listed")
	DirAccess.remove_absolute(ROOT + "/link")


# Runs this engine with a pack as the main pack, so res:// is served by the pack as in an exported game.
func _test_pack() -> void:
	_remove_tree(PCK_ROOT)
	DirAccess.make_dir_recursive_absolute(PCK_ROOT)
	var sources: Dictionary = {
		"project.godot": "config_version=5\n\n[application]\n\nconfig/name=\"pack-test\"\n",
		"pack_test.gd": PACK_SCRIPT,
		"data/icon.svg.import": "x",
		"data/sub/x.tres.remap": "x",
		"data/sub/deep/y.json": "{}",
	}
	var packer := PCKPacker.new()
	var pack: String = ProjectSettings.globalize_path(PCK_ROOT + "/test.pck")
	expect_equal(packer.pck_start(pack), OK, "PCKPacker starts")
	for target: String in sources:
		var content: String = sources[target]
		packer.add_file_from_buffer(target, content.to_utf8_buffer())
	expect_equal(packer.flush(), OK, "PCKPacker writes the pack")
	var output: Array = []
	var code: int = OS.execute(OS.get_executable_path(), ["--headless", "--main-pack", pack, "--script", "res://pack_test.gd"], output, true)
	var text: String = "\n".join(output)
	expect_equal(code, 0, "the engine runs on the pack")
	expect(text.contains("PACK_FILES=res://data/icon.svg.import,res://data/sub/deep/y.json,res://data/sub/x.tres.remap"), "pack: get_file_paths_at(recursive) returns the raw entries (.import, .remap)")
	expect(text.contains("PACK_IMPORT=res://data/icon.svg.import"), "pack: the extension filter works")
	expect(text.contains("PACK_DIRS=res://data/sub,res://data/sub/deep"), "pack: get_directory_paths_at(recursive)")
	expect(text.contains("PACK_EXTENSIONS=import,json,remap"), "pack: get_file_extensions_at(recursive)")
	_remove_tree(PCK_ROOT)
