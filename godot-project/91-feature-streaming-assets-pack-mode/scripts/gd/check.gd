extends FeatureCheck
## GDScript checks of 91-feature-streaming-assets-pack-mode. Check the same things as scripts/cs/Issue91FeatureStreamingAssetsPackModeCheck.cs.
## An export can't run here: these checks cover the bound rules of the pack mode (placement, pack path, the
## description and plan of the streaming pack) and the runtime side: a prepared pack with files of a test folder
## is mounted the way StreamingAssetsServer mounts the streaming pack in MODE_PACK, and the files load by res://.
## The full export (Windows, mode=pack) is a manual step, described in the wiki guide and in the issue.

const CLASS := "StreamingAssetsPackBuilder"
const SAMPLE := "res://91-feature-streaming-assets-pack-mode/resources/streaming_sample"
const PACK := "user://91_streaming_pack/Game.streaming.pck"
const METHODS: Array[String] = ["build", "get_pack_path", "make_description"]


func _get_check_name() -> String:
	return "91-feature-streaming-assets-pack-mode"


func _run_checks() -> void:
	_check_bindings()
	_check_placement()
	_check_pack_path()
	_check_description_and_plan()
	_check_refusals()
	_check_mount_prepared_pack()


func _check_bindings() -> void:
	expect(ClassDB.class_exists(CLASS), "%s exists in the editor build" % CLASS)
	expect_equal(ClassDB.get_parent_class(CLASS), &"Object", "%s extends Object" % CLASS)
	expect(not ClassDB.can_instantiate(CLASS), "%s can't be instantiated from scripts" % CLASS)
	for method in METHODS:
		expect(ClassDB.class_has_method(CLASS, method, true), "%s.%s() is bound" % [CLASS, method])
	expect_equal(StreamingAssetsExportPlugin.PLACEMENT_PACK, 4, "PLACEMENT_PACK")
	expect(ProjectSettings.has_setting("filesystem/streaming_assets/pack_file"), "the pack_file setting is registered")


func _check_placement() -> void:
	var windows := PackedStringArray(["pc", "windows", "x86_64"])
	var linux := PackedStringArray(["pc", "linux", "linuxbsd", "x86_64"])
	var macos := PackedStringArray(["pc", "macos", "arm64"])
	var android := PackedStringArray(["mobile", "android", "arm64"])
	var web := PackedStringArray(["web"])
	var embed := StreamingAssetsExportPlugin.FALLBACK_EMBED
	var pack := StreamingAssetsExportPlugin.MODE_PACK
	expect_equal(StreamingAssetsExportPlugin.resolve_placement(windows, "C:/build/Game.exe", embed, pack), StreamingAssetsExportPlugin.PLACEMENT_PACK, "Windows: a pack next to the executable")
	expect_equal(StreamingAssetsExportPlugin.resolve_placement(linux, "/build/Game.pck", embed, pack), StreamingAssetsExportPlugin.PLACEMENT_PACK, "Linux: a pack next to the main pack")
	expect_equal(StreamingAssetsExportPlugin.resolve_placement(windows, "C:/build/Game.zip", embed, pack), StreamingAssetsExportPlugin.PLACEMENT_EMBED, ".zip: the fallback")
	expect_equal(StreamingAssetsExportPlugin.resolve_placement(macos, "/build/Game.app", embed, pack), StreamingAssetsExportPlugin.PLACEMENT_EMBED, "macOS: the fallback")
	expect_equal(StreamingAssetsExportPlugin.resolve_placement(android, "/build/Game.apk", embed, pack), StreamingAssetsExportPlugin.PLACEMENT_EMBED, "Android: the fallback")
	expect_equal(StreamingAssetsExportPlugin.resolve_placement(web, "/build/index.html", StreamingAssetsExportPlugin.FALLBACK_NONE, pack), StreamingAssetsExportPlugin.PLACEMENT_NONE, "Web: the fallback none")
	expect_equal(StreamingAssetsExportPlugin.resolve_placement(windows, "C:/build/Game.exe", embed), StreamingAssetsExportPlugin.PLACEMENT_EXTERNAL, "the default mode is raw")


func _check_pack_path() -> void:
	expect_equal(StreamingAssetsPackBuilder.get_pack_path("C:/build/Game.exe", "Game.streaming.pck"), "C:/build/Game.streaming.pck", "the pack is next to the executable")
	expect_equal(StreamingAssetsPackBuilder.get_pack_path("/build/Game.pck", StreamingAssetsServer.expand_template("{exe_name}.streaming.pck", "Game")), "/build/Game.streaming.pck", "the default pack file")
	expect_equal(StreamingAssetsPackBuilder.get_pack_path("/build/Game.pck", "sub/a.pck"), "", "a pack file with a folder is refused")
	expect_equal(StreamingAssetsPackBuilder.get_pack_path("", "a.pck"), "", "an empty export path is refused")


func _check_description_and_plan() -> void:
	var description := StreamingAssetsPackBuilder.make_description(SAMPLE)
	expect(description != null, "make_description() returns a PCKPackage")
	if description == null:
		return
	var expected := PackedStringArray([
		"res://91-feature-streaming-assets-pack-mode/resources/streaming_sample/data/levels.json",
		"res://91-feature-streaming-assets-pack-mode/resources/streaming_sample/icon.svg",
	])
	var paths := description.get_file_paths()
	paths.sort()
	expect_equal(paths, expected, "one entry per file, at its res:// path; .import files are left out")
	var plan := PCKPackageExporter.plan_description(description)
	expect(plan.valid, "the streaming pack can be built")
	var sources := plan.export_sources
	sources.sort()
	expect_equal(sources, PackedStringArray([SAMPLE + "/data/levels.json", SAMPLE + "/icon.svg"]), "every file goes through the export pipeline")
	expect(plan.raw_files.is_empty(), "nothing is copied as it is")


func _check_refusals() -> void:
	expect(StreamingAssetsPackBuilder.make_description("user://streaming") == null, "a folder outside res:// is refused")
	expect_equal(StreamingAssetsPackBuilder.build(null, SAMPLE, "user://a.pck"), ERR_INVALID_PARAMETER, "build() without a preset")


func _check_mount_prepared_pack() -> void:
	# A prepared streaming pack: a file that exists only in the pack, below the sample folder.
	DirAccess.make_dir_recursive_absolute(PACK.get_base_dir())
	var source := "user://91_streaming_pack/pack_only.txt"
	var file := FileAccess.open(source, FileAccess.WRITE)
	file.store_string("Hello from the streaming pack")
	file.close()
	var packer := PCKPacker.new()
	expect_equal(packer.pck_start(PACK), OK, "pck_start()")
	expect_equal(packer.add_file(SAMPLE + "/pack_only.txt", source), OK, "add_file()")
	expect_equal(packer.flush(), OK, "flush()")

	# StreamingAssetsServer mounts the streaming pack like this in MODE_PACK.
	var pack_path := ProjectSettings.globalize_path(PACK)
	expect_equal(PCKPackageServer.mount(pack_path, true), OK, "the streaming pack mounts")
	expect(PCKPackageServer.is_mounted(pack_path), "is_mounted()")
	expect_equal(FileAccess.get_file_as_string(SAMPLE + "/pack_only.txt"), "Hello from the streaming pack", "the file loads by its res:// path")
	expect_equal(PCKPackageServer.unmount(pack_path), OK, "unmount()")
	expect(not FileAccess.file_exists(SAMPLE + "/pack_only.txt"), "the file is gone after unmount()")
