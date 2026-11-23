extends FeatureCheck
## GDScript checks of 10-feature-pck-package-server. Check the same things as scripts/cs/Issue10FeaturePckPackageServerCheck.cs.
## The wrong-key checks print engine errors on purpose.

const ROOT := "user://10-feature-pck-package-server/gd/"
const KEY_A := "00112233445566778899aabbccddeeff00112233445566778899aabbccddeeff"
const KEY_B := "ffeeddccbbaa99887766554433221100ffeeddccbbaa99887766554433221100"
const ZERO_KEY := "0000000000000000000000000000000000000000000000000000000000000000"

var _mounted: Array[String] = []
var _unmounted: Array[String] = []


func _get_check_name() -> String:
	return "10-feature-pck-package-server"


func _write_pack(path: String, files: Dictionary, key: String = ZERO_KEY, encrypt: bool = false) -> void:
	var packer := PCKPacker.new()
	packer.pck_start(path, 32, key, encrypt)
	for target in files:
		packer.add_file_from_buffer(target, String(files[target]).to_utf8_buffer(), encrypt)
	packer.flush()


func _run_checks() -> void:
	DirAccess.make_dir_recursive_absolute(ROOT)
	var server := PckPackageServer
	expect(Engine.has_singleton("PckPackageServer"), "PckPackageServer singleton exists")
	server.pack_mounted.connect(func(p: String): _mounted.append(p))
	server.pack_unmounted.connect(func(p: String): _unmounted.append(p))

	# The main pack of an exported project opens and lists its files.
	for pack in server.get_mounted():
		if server.is_main_pack(pack):
			var main := server.open(pack)
			expect(main != null and main.get_file_count() > 0, "main pack %s opens and lists files" % pack)

	# Open, list and read without mounting.
	var base := ROOT + "base.pck"
	_write_pack(base, {"pck10gd/a.txt": "Alpha", "pck10gd/b.txt": "Beta"})
	var package := server.open(base)
	expect(package != null, "open() returns a package")
	expect_equal(package.format_version, 4, "format_version is 4")
	expect(not package.engine_version.is_empty(), "engine_version is set")
	expect_equal(package.get_paths(), PackedStringArray(["res://pck10gd/a.txt", "res://pck10gd/b.txt"]), "get_paths() lists all files")
	var entries := package.get_entries()
	expect(entries.size() == 2 and entries[0].size == 5 and entries[0].md5 == "Alpha".md5_text(), "get_entries() has size and md5")
	expect_equal(server.read_file(package, "res://pck10gd/a.txt").get_string_from_utf8(), "Alpha", "read_file() without mounting")
	expect_equal(server.verify(package), OK, "verify()")
	expect(not server.is_mounted(base), "open() does not mount")
	expect(not FileAccess.file_exists("res://pck10gd/a.txt"), "file is not in res:// before mounting")

	# Edit and build: the new pack mounts and shows the changes.
	package.add_file_from_buffer("res://pck10gd/c.txt", "Gamma".to_utf8_buffer())
	expect_equal(package.remove_file("res://pck10gd/a.txt"), OK, "remove_file()")
	expect_equal(package.rename_file("res://pck10gd/b.txt", "res://pck10gd/b2.txt"), OK, "rename_file()")
	var edited := ROOT + "edited.pck"
	expect_equal(server.build(package, edited), OK, "build()")
	expect_equal(server.mount(edited), OK, "mount()")
	expect(server.is_mounted(edited) and server.get_mounted().has(edited), "is_mounted() and get_mounted()")
	expect(_mounted.has(edited), "pack_mounted emitted")
	expect_equal(FileAccess.get_file_as_string("res://pck10gd/c.txt"), "Gamma", "added file is in res://")
	expect_equal(FileAccess.get_file_as_string("res://pck10gd/b2.txt"), "Beta", "renamed file is in res://")
	expect(not FileAccess.file_exists("res://pck10gd/a.txt"), "removed file is not in res://")

	# Two packs provide the same path: get_pack_of() names the one that is used.
	var overlay := ROOT + "overlay.pck"
	_write_pack(overlay, {"pck10gd/c.txt": "Overlay", "pck10gd/only_overlay.txt": "Only"})
	expect(ProjectSettings.load_resource_pack(overlay), "load_resource_pack() still works")
	expect(server.is_mounted(overlay), "load_resource_pack() goes through the server")
	expect_equal(server.get_pack_of("res://pck10gd/c.txt"), overlay, "get_pack_of() names the later pack")
	expect_equal(server.get_pack_of("res://pck10gd/b2.txt"), edited, "get_pack_of() names the only pack")
	expect_equal(FileAccess.get_file_as_string("res://pck10gd/c.txt"), "Overlay", "later pack replaces the file")

	# Unmount: files of the pack disappear, replaced files come back.
	expect_equal(server.unmount(overlay), OK, "unmount()")
	expect(_unmounted.has(overlay), "pack_unmounted emitted")
	expect(not server.is_mounted(overlay), "is_mounted() false after unmount()")
	expect(not FileAccess.file_exists("res://pck10gd/only_overlay.txt"), "file of the unmounted pack is gone")
	expect_equal(server.get_pack_of("res://pck10gd/c.txt"), edited, "get_pack_of() names the remaining pack")
	expect_equal(FileAccess.get_file_as_string("res://pck10gd/c.txt"), "Gamma", "replaced file is back")
	expect_equal(server.unmount(overlay), ERR_DOES_NOT_EXIST, "unmount() twice: ERR_DOES_NOT_EXIST")
	expect_equal(server.unmount(edited), OK, "unmount() the edited pack")
	expect(not FileAccess.file_exists("res://pck10gd/c.txt"), "all files of the packs are gone")
	expect_equal(server.build(package, edited), OK, "build() onto an unmounted pack")

	# Encrypted packs.
	var secret := ROOT + "secret.pck"
	_write_pack(secret, {"pck10gd/secret.txt": "Hidden"}, KEY_A, true)
	var encrypted := server.open(secret, KEY_A)
	expect(encrypted != null and encrypted.encrypted_directory, "open() with the right key")
	expect_equal(server.read_file(encrypted, "res://pck10gd/secret.txt").get_string_from_utf8(), "Hidden", "read_file() decrypts")
	expect(server.open(secret, KEY_B) == null, "open() with a wrong key returns null")
