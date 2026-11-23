extends FeatureCheck
## GDScript checks of 39-feature-dir-access-recursive-paths. Check the same things as scripts/cs/Issue39FeatureDirAccessRecursivePathsCheck.cs.

const ROOT := "user://dirtest_gd"
const BENCH_ROOT := "user://dirbench"
const PCK_ROOT := "user://dirtest_pck"
const FEATURE := "res://39-feature-dir-access-recursive-paths"


func _get_check_name() -> String:
	return "39-feature-dir-access-recursive-paths"


func _run_checks() -> void:
	_create_tree(ROOT)
	_check_file_paths()
	_check_directory_paths()
	_check_extensions()
	_check_instance_methods()
	_check_res_in_editor()
	_check_missing_folder()
	_check_folder_link()
	_check_exported_pck()
	_check_performance()
	_remove_tree(ROOT)


# A link to a folder (junction on Windows, which needs no extra privileges) is listed, but not followed.
func _check_folder_link() -> void:
	var target := ProjectSettings.globalize_path(ROOT + "/sub1")
	var link := ProjectSettings.globalize_path(ROOT + "/link")
	var code := -1
	if OS.get_name() == "Windows":
		code = OS.execute("cmd.exe", ["/c", "mklink", "/J", link.replace("/", "\\"), target.replace("/", "\\")])
	else:
		code = OS.execute("ln", ["-s", target, link])
	if not expect(code == 0, "folder link created (exit code %d)" % code):
		return
	expect_equal(DirAccess.get_directory_paths_at(ROOT, true), _paths(ROOT, ["link", "sub1", "sub1/sub2", "sub3"]), "a linked folder is listed, but not followed")
	expect_equal(DirAccess.get_file_paths_at(ROOT, true, ["json"]), _paths(ROOT, ["sub1/sub2/f.json"]), "files behind the link are not listed")
	DirAccess.remove_absolute(ROOT + "/link")


# 3 levels, mixed extensions in mixed case, a hidden file, an empty folder.
func _create_tree(root: String) -> void:
	_remove_tree(root)
	DirAccess.make_dir_recursive_absolute(root + "/sub1/sub2")
	DirAccess.make_dir_recursive_absolute(root + "/sub3")
	for file: String in ["a.PNG", "b.txt", ".hidden.png", "sub1/c.png", "sub1/d.WebP", "sub1/noext", "sub1/sub2/e.Tres", "sub1/sub2/f.json"]:
		var f := FileAccess.open(root.path_join(file), FileAccess.WRITE)
		f.store_string("x")
		f.close()
	# Hidden by name on Unix, by attribute on Windows.
	FileAccess.set_hidden_attribute(root.path_join(".hidden.png"), true)


func _remove_tree(root: String) -> void:
	if not DirAccess.dir_exists_absolute(root):
		return
	var da := DirAccess.open(root)
	da.include_hidden = true
	var dirs := da.get_directory_paths(true)
	for path: String in da.get_file_paths(true):
		DirAccess.remove_absolute(path)
	dirs.reverse()
	for dir: String in dirs:
		DirAccess.remove_absolute(dir)
	DirAccess.remove_absolute(root)


func _paths(base: String, names: Array) -> PackedStringArray:
	var ret := PackedStringArray()
	for name: String in names:
		ret.append(base.path_join(name))
	return ret


func _check_file_paths() -> void:
	expect_equal(DirAccess.get_file_paths_at(ROOT), _paths(ROOT, ["a.PNG", "b.txt"]), "get_file_paths_at() lists one folder, without hidden files")
	expect_equal(DirAccess.get_file_paths_at(ROOT, true),
			_paths(ROOT, ["a.PNG", "b.txt", "sub1/c.png", "sub1/d.WebP", "sub1/noext", "sub1/sub2/e.Tres", "sub1/sub2/f.json"]),
			"get_file_paths_at(recursive) lists all levels, sorted")
	expect_equal(DirAccess.get_file_paths_at(ROOT, true, ["png", "webp"]), _paths(ROOT, ["a.PNG", "sub1/c.png", "sub1/d.WebP"]),
			"extension filter is case-insensitive")
	expect_equal(DirAccess.get_file_paths_at(ROOT, true, [".PNG"]), _paths(ROOT, ["a.PNG", "sub1/c.png"]), "a leading dot in the filter is ignored")
	expect_equal(DirAccess.get_file_paths_at(ROOT + "/", true, ["json"]), _paths(ROOT, ["sub1/sub2/f.json"]), "a trailing slash in the input gives no double slash")


func _check_directory_paths() -> void:
	expect_equal(DirAccess.get_directory_paths_at(ROOT), _paths(ROOT, ["sub1", "sub3"]), "get_directory_paths_at() lists one folder")
	expect_equal(DirAccess.get_directory_paths_at(ROOT, true), _paths(ROOT, ["sub1", "sub1/sub2", "sub3"]), "get_directory_paths_at(recursive) lists all levels")
	expect_equal(DirAccess.get_directory_paths_at(ROOT + "/sub3", true), PackedStringArray(), "empty folder gives an empty array")


func _check_extensions() -> void:
	expect_equal(DirAccess.get_file_extensions_at(ROOT), PackedStringArray(["png", "txt"]), "get_file_extensions_at() lists one folder")
	expect_equal(DirAccess.get_file_extensions_at(ROOT, true), PackedStringArray(["json", "png", "tres", "txt", "webp"]),
			"get_file_extensions_at(recursive) gives distinct lower-case extensions, sorted")


func _check_instance_methods() -> void:
	var da := DirAccess.open(ROOT)
	var base := da.get_current_dir()
	expect_equal(da.get_file_paths(false, ["png"]), _paths(base, ["a.PNG"]), "get_file_paths() skips hidden files by default")
	da.include_hidden = true
	expect_equal(da.get_file_paths(true, ["png"]), _paths(base, [".hidden.png", "a.PNG", "sub1/c.png"]), "get_file_paths() respects include_hidden")
	da.include_navigational = true
	expect_equal(da.get_directory_paths(true), _paths(base, ["sub1", "sub1/sub2", "sub3"]), "get_directory_paths() never returns . and ..")
	expect_equal(da.get_file_extensions(true), PackedStringArray(["json", "png", "tres", "txt", "webp"]), "get_file_extensions() works on the instance")


func _check_res_in_editor() -> void:
	expect_equal(DirAccess.get_file_paths_at(FEATURE, true, ["gd"]), PackedStringArray([FEATURE + "/scripts/gd/check.gd"]), "res:// in the editor keeps the res:// prefix")
	expect_equal(DirAccess.get_file_paths_at(FEATURE + "/scripts", true, ["CS"]), PackedStringArray([FEATURE + "/scripts/cs/Issue39FeatureDirAccessRecursivePathsCheck.cs"]),
			"res:// filter by extension")
	expect(DirAccess.get_directory_paths_at(FEATURE, true).has(FEATURE + "/scripts/gd"), "res:// directory paths include nested folders")


func _check_missing_folder() -> void:
	expect_equal(DirAccess.get_file_paths_at(ROOT + "/missing", true), PackedStringArray(), "missing folder gives an empty array (with an error)")


# Runs this engine on a PCK as main pack, so res:// is served by the pack (as in an exported game).
func _check_exported_pck() -> void:
	_remove_tree(PCK_ROOT)
	DirAccess.make_dir_recursive_absolute(PCK_ROOT + "/src")
	var sources := {
		"project.godot": "config_version=5\n\n[application]\n\nconfig/name=\"pck-check\"\n",
		"pck_check.gd": "extends SceneTree\n\nfunc _initialize() -> void:\n\tprint(\"PCK_FILES=\" + \",\".join(DirAccess.get_file_paths_at(\"res://data\", true)))\n\tprint(\"PCK_IMPORT=\" + \",\".join(DirAccess.get_file_paths_at(\"res://data\", true, [\"IMPORT\"])))\n\tprint(\"PCK_DIRS=\" + \",\".join(DirAccess.get_directory_paths_at(\"res://data\", true)))\n\tprint(\"PCK_EXTS=\" + \",\".join(DirAccess.get_file_extensions_at(\"res://data\", true)))\n\tquit(0)\n",
		"data/icon.svg.import": "x",
		"data/sub/x.tres.remap": "x",
		"data/sub/deep/y.json": "{}",
	}
	var packer := PCKPacker.new()
	var pck := ProjectSettings.globalize_path(PCK_ROOT + "/check.pck")
	expect(packer.pck_start(pck) == OK, "PCKPacker starts")
	var index := 0
	for target: String in sources:
		var source := PCK_ROOT + "/src/%d.txt" % index
		index += 1
		var f := FileAccess.open(source, FileAccess.WRITE)
		f.store_string(sources[target])
		f.close()
		packer.add_file("res://" + target, source)
	packer.flush()
	var output := []
	var code := OS.execute(OS.get_executable_path(), ["--headless", "--main-pack", pck, "--script", "res://pck_check.gd"], output, true)
	var text := "\n".join(output)
	expect(code == 0, "engine runs on the PCK (exit code %d)" % code)
	expect(text.contains("PCK_FILES=res://data/icon.svg.import,res://data/sub/deep/y.json,res://data/sub/x.tres.remap"),
			"PCK: get_file_paths_at(recursive) returns the raw entries (.import, .remap)")
	expect(text.contains("PCK_IMPORT=res://data/icon.svg.import\n") or text.contains("PCK_IMPORT=res://data/icon.svg.import\r"), "PCK: extension filter")
	expect(text.contains("PCK_DIRS=res://data/sub,res://data/sub/deep"), "PCK: get_directory_paths_at(recursive)")
	expect(text.contains("PCK_EXTS=import,json,remap"), "PCK: get_file_extensions_at(recursive)")
	if not text.contains("PCK_FILES="):
		printerr(text)
	_remove_tree(PCK_ROOT)


# 10 000 files in 500 folders: 20 folders with 24 subfolders each, 20 files per folder.
static func create_bench_tree() -> void:
	if FileAccess.file_exists(BENCH_ROOT + "/done"):
		return
	for i in 20:
		var top := BENCH_ROOT + "/d%02d" % i
		for j in 24:
			DirAccess.make_dir_recursive_absolute(top + "/s%02d" % j)
		for j in 20:
			FileAccess.open(top + "/f%02d.txt" % j, FileAccess.WRITE).store_string("x")
		for j in 24:
			for k in 20:
				FileAccess.open(top + "/s%02d/f%02d.%s" % [j, k, "png" if k % 2 == 0 else "txt"], FileAccess.WRITE).store_string("x")
	FileAccess.open(BENCH_ROOT + "/done", FileAccess.WRITE).store_string("x")


static func _walk(path: String, out: PackedStringArray) -> void:
	for file: String in DirAccess.get_files_at(path):
		out.append(path.path_join(file))
	for dir: String in DirAccess.get_directories_at(path):
		_walk(path.path_join(dir), out)


func _check_performance() -> void:
	create_bench_tree()
	var best_native := INF
	var best_script := INF
	var native := PackedStringArray()
	var script := PackedStringArray()
	for run in 3:
		var t := Time.get_ticks_usec()
		native = DirAccess.get_file_paths_at(BENCH_ROOT, true)
		best_native = minf(best_native, (Time.get_ticks_usec() - t) / 1000.0)
		t = Time.get_ticks_usec()
		script = PackedStringArray()
		_walk(BENCH_ROOT, script)
		best_script = minf(best_script, (Time.get_ticks_usec() - t) / 1000.0)
	expect_equal(native.size(), 10001, "benchmark tree has 10 000 files (+ marker)")
	script.sort()
	expect(native == script, "C++ result equals the GDScript loop")
	var t2 := Time.get_ticks_usec()
	var pngs := DirAccess.get_file_paths_at(BENCH_ROOT, true, ["png"])
	var filtered_ms := (Time.get_ticks_usec() - t2) / 1000.0
	expect_equal(pngs.size(), 4800, "filtered benchmark")
	print("  PERF  10k files/500 folders: get_file_paths_at %.1f ms, GDScript loop %.1f ms, filtered png %.1f ms" % [best_native, best_script, filtered_ms])
