extends Node
## Minimal example of the Database dock: the snippets of the wiki guide, in order.
## Wiki: https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Database-Dock

const SHOP := "res://database-dock/resources/shop.db"
const SAVE := "user://shop_save.db"


func _ready() -> void:
	# Read the rows you edited in the dock.
	var shop := SQLiteDatabase.new()
	shop.open(SHOP, SQLiteDatabase.OPEN_MODE_READ_ONLY)
	print(shop.query("SELECT name, price FROM items ORDER BY id;"))

	# Read a view.
	print(shop.query("SELECT name FROM cheap_items ORDER BY name;"))
	shop.close()

	# Write like Save: all edits in one transaction.
	DirAccess.copy_absolute(SHOP, SAVE)
	var save := SQLiteDatabase.new()
	save.open(SAVE)
	save.begin_transaction()
	save.execute("UPDATE items SET price = ? WHERE id = ?;", [0.75, 1])
	save.execute("INSERT INTO items (name) VALUES (?);", ["Bread"])
	if save.commit() != OK:
		save.rollback()
	print(save.query("SELECT name, price FROM items ORDER BY id;"))
	save.close()
	DirAccess.remove_absolute(SAVE)

	get_tree().quit()
