from pathlib import Path
import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[1]
bpy.context.preferences.filepaths.save_version = 0
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)

def material(name, color):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*color, 1)
    return mat

ground_mat = material('Forest background', (.045, .070, .064))
plinth_mat = material('Slate platforms', (.105, .145, .125))
label_mat = material('Warm white labels', (.82, .80, .68))
names = ['zombie', 'skeleton', 'piglin', 'blaze', 'cow', 'pig', 'sheep', 'creeper', 'chicken', 'spider', 'bed', 'chest']
for i, name in enumerate(names):
    x, y = (i % 4 - 1.5) * 2.65, (1 - i // 4) * 2.85
    for model in (['bed_head', 'bed_foot'] if name == 'bed' else [name]):
        before = set(bpy.data.objects)
        bpy.ops.import_scene.gltf(filepath=str(ROOT / 'assets' / 'models' / f'{model}.glb'))
        imported = set(bpy.data.objects) - before
        for obj in imported:
            if not obj.parent:
                obj.location += Vector((x, y + (.5 if model == 'bed_head' else -.5 if model == 'bed_foot' else 0), .09))
    bpy.ops.mesh.primitive_cube_add(size=1, location=(x, y, 0))
    plinth = bpy.context.object
    plinth.name = name + ' platform'
    plinth.dimensions = (2.3, 2.55, .16)
    plinth.data.materials.append(plinth_mat)
    bpy.ops.object.text_add(location=(x, y - 1.21, .085))
    label = bpy.context.object
    label.name = name + ' label'
    label.data.body = name.replace('_', ' ').upper()
    label.data.align_x = 'CENTER'
    label.data.size = .21
    label.data.extrude = .001
    label.data.materials.append(label_mat)

bpy.ops.mesh.primitive_plane_add(size=200, location=(0, 0, -.1))
bpy.context.object.data.materials.append(ground_mat)
bpy.ops.object.camera_add(location=(7, -13, 15))
camera = bpy.context.object
camera.rotation_euler = (Vector((0, 0, .5)) - camera.location).to_track_quat('-Z', 'Y').to_euler()
camera.data.type = 'ORTHO'
camera.data.ortho_scale = 13.8
bpy.context.scene.camera = camera
for location, energy, size in [((1, -4, 10), 2300, 8), ((-5, 4, 7), 1800, 6)]:
    bpy.ops.object.light_add(type='AREA', location=location)
    light = bpy.context.object
    light.data.energy = energy
    light.data.shape = 'DISK'
    light.data.size = size
    light.rotation_euler = (-light.location).to_track_quat('-Z', 'Y').to_euler()
scene = bpy.context.scene
scene.render.engine = 'CYCLES'
scene.cycles.samples = 24
scene.render.resolution_x = 1600
scene.render.resolution_y = 1150
scene.render.resolution_percentage = 100
scene.world.color = (.25, .25, .25)
scene.view_settings.view_transform = 'AgX'
scene.render.image_settings.file_format = 'PNG'
scene.render.filepath = str(ROOT / 'assets' / 'survival-model-gallery.png')
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT / 'assets' / 'blender' / 'survival-gallery.blend'), compress=True)
bpy.ops.render.render(write_still=True)
