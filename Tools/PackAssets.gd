extends SceneTree
var packed: Dictionary = {}
func _initialize():
    var root = ProjectSettings.globalize_path("res://")
    DirAccess.make_dir_recursive_absolute(root+"build")
    collect(root+"PuniMod", "res://PuniMod")
    for file in DirAccess.get_files_at(root+".godot/imported"):
        if file.ends_with(".ctex") and not file.contains("-source."):
            packed["res://.godot/imported/"+file] = root+".godot/imported/"+file
    var packer = PCKPacker.new()
    if packer.pck_start(root+"build/PuniMod.pck") != OK:
        quit(1)
        return
    for resource_path in packed:
        if packer.add_file(resource_path, packed[resource_path]) != OK:
            quit(1)
            return
    if packer.flush() != OK:
        quit(1)
        return
    print("Packed PuniMod.pck: ", packed.size(), " files")
    quit(0)
func collect(physical: String, virtual_path: String):
    for file in DirAccess.get_files_at(physical):
        if not file.contains("-source."):
            packed[virtual_path+"/"+file] = physical+"/"+file
    for directory in DirAccess.get_directories_at(physical):
        collect(physical+"/"+directory, virtual_path+"/"+directory)
