extends ListSettingsServer
## List settings domain "check15_list" of the 15-feature-settings-server checks (not registered at startup).


func _get_domain() -> StringName:
	return &"check15_list"


func _create_settings() -> Resource:
	return Issue15CheckList.new()


func _get_entry_key() -> StringName:
	return &"entry_name"
