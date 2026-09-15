from pathlib import Path
import math
import bpy

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'Assets' / 'Resources' / 'Models'
SOURCE = ROOT / 'ArtSource'
OUT.mkdir(parents=True, exist_ok=True)
SOURCE.mkdir(parents=True, exist_ok=True)
bpy.context.preferences.filepaths.save_version = 0

PALETTE = {
    'obsidian hide': (.07, .055, .09), 'hide ridge': (.14, .11, .19),
    'violet eyes': (.77, .23, .96), 'wing membrane': (.23, .13, .31),
    'bone horn': (.76, .73, .68), 'crystal core': (.82, .25, .87),
    'crystal frame': (.33, .15, .43), 'stone base': (.23, .25, .27),
    'villager skin': (.64, .43, .30), 'linen shirt': (.79, .74, .58),
    'brown coat': (.33, .22, .15), 'boots': (.14, .12, .11),
    'green eyes': (.20, .51, .30), 'black': (.04, .035, .025),
    'gold trim': (.88, .62, .17),
}

def reset():
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    for mesh in list(bpy.data.meshes):
        if mesh.users == 0:
            bpy.data.meshes.remove(mesh)

def material(name):
    m = bpy.data.materials.get(name)
    if m:
        return m
    m = bpy.data.materials.new(name)
    color = PALETTE[name]
    m.diffuse_color = (*color, 1)
    m.use_nodes = True
    shader = m.node_tree.nodes.get('Principled BSDF')
    shader.inputs['Base Color'].default_value = (*color, 1)
    shader.inputs['Roughness'].default_value = .85
    if name in ('violet eyes', 'crystal core'):
        shader.inputs['Emission Color'].default_value = (*color, 1)
        shader.inputs['Emission Strength'].default_value = .7
    return m

def joint(name, location=(0, 0, 0), parent=None):
    obj = bpy.data.objects.new(name, None)
    bpy.context.collection.objects.link(obj)
    obj.parent = parent
    obj.location = location
    return obj

def box(name, location, size, color, parent, rotation=(0, 0, 0)):
    bpy.ops.mesh.primitive_cube_add(size=1)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.parent = parent
    obj.location = location
    obj.rotation_euler = rotation
    obj.data.materials.append(material(color))
    return obj

def eyes(parent, width, forward, height, color='violet eyes'):
    for side in (-1, 1):
        box('eye', (side * width, forward, height), (.12, .028, .055), color, parent)

def enderman():
    root = joint('Enderman')
    box('body', (0, 0, 1.94), (.47, .27, .83), 'obsidian hide', root)
    head = joint('head_joint', (0, 0, 2.65), root)
    box('head', (0, 0, 0), (.49, .46, .46), 'obsidian hide', head)
    eyes(head, .135, -.243, .02)
    for side in (-1, 1):
        arm = joint(f'arm_{side}', (side * .32, 0, 2.3), root)
        box('arm_mesh', (0, 0, -.65), (.14, .15, 1.43), 'obsidian hide', arm)
        leg = joint(f'leg_{side}', (side * .145, 0, 1.55), root)
        box('leg_mesh', (0, 0, -.75), (.15, .16, 1.52), 'obsidian hide', leg)
    return root

def villager():
    root = joint('Villager')
    box('coat', (0, 0, 1.05), (.59, .36, .92), 'brown coat', root)
    box('shirt', (0, -.188, 1.22), (.33, .028, .48), 'linen shirt', root)
    box('belt', (0, -.01, .85), (.61, .38, .065), 'gold trim', root)
    head = joint('head_joint', (0, 0, 1.71), root)
    box('head', (0, 0, 0), (.5, .48, .52), 'villager skin', head)
    box('nose', (0, -.3, -.07), (.12, .17, .25), 'villager skin', head)
    eyes(head, .14, -.249, .065, 'green eyes')
    box('brow', (0, -.253, .12), (.39, .027, .045), 'brown coat', head)
    for side in (-1, 1):
        arm = joint(f'arm_{side}', (side * .365, 0, 1.39), root)
        box('sleeve', (0, -.10, -.22), (.2, .30, .5), 'brown coat', arm, (.25, 0, side * -.16))
        box('hand', (side * -.13, -.24, -.32), (.37, .19, .17), 'villager skin', arm)
        leg = joint(f'leg_{side}', (side * .155, 0, .64), root)
        box('leg_mesh', (0, 0, -.27), (.22, .25, .53), 'linen shirt', leg)
        box('foot', (0, -.055, -.56), (.24, .34, .15), 'boots', leg)
    return root

def dragon():
    root = joint('EndDragon')
    box('body', (0, 0, 1.2), (1.8, 3.5, 1.6), 'obsidian hide', root)
    box('belly', (0, -.1, .40), (1.2, 2.9, .14), 'hide ridge', root)
    for i in range(5):
        box('spine', (0, -1.2 + i * .62, 2.15), (.22, .29, .48), 'hide ridge', root)
    neck = joint('neck', (0, -1.58, 1.43), root)
    box('neck_mesh', (0, -.65, .03), (.82, 1.5, .79), 'obsidian hide', neck)
    head = joint('head_joint', (0, -1.45, .09), neck)
    box('head', (0, -.2, 0), (1.15, 1.18, .81), 'obsidian hide', head)
    box('snout', (0, -.97, -.12), (.89, .72, .48), 'obsidian hide', head)
    jaw = joint('jaw', (0, -.25, -.39), head)
    box('jaw_mesh', (0, -.50, -.06), (.88, 1.4, .19), 'hide ridge', jaw)
    for side in (-1, 1):
        box('eye', (side * .585, -.52, .16), (.03, .22, .13), 'violet eyes', head)
        box('horn', (side * .46, .2, .65), (.16, .2, .72), 'bone horn', head, (side * .08, side * -.25, 0))
        wing = joint(f'wing_{side}', (side * .81, -.45, 1.71), root)
        box('wing_upper', (side * 1.63, 0, .06), (3.35, .20, .24), 'hide ridge', wing)
        tip = joint(f'wingtip_{side}', (side * 3.2, 0, .06), wing)
        box('wing_outer', (side * 1.23, .06, 0), (2.6, .15, .17), 'hide ridge', tip)
        vertices = [(0, 0, 0), (side * 3.1, 0, .03), (side * 2.25, 2.5, -.12), (0, 1.8, -.1)]
        mesh = bpy.data.meshes.new('inner_membrane')
        mesh.from_pydata(vertices, [], [(0, 1, 2, 3), (3, 2, 1, 0)])
        obj = bpy.data.objects.new('wing_membrane', mesh)
        bpy.context.collection.objects.link(obj)
        obj.parent = wing
        mesh.materials.append(material('wing membrane'))
        vertices = [(0, 0, 0), (side * 2.6, 0, 0), (side * 1.9, 1.65, -.1), (side * -.9, 2.5, -.17)]
        mesh = bpy.data.meshes.new('outer_membrane')
        mesh.from_pydata(vertices, [], [(0, 1, 2, 3), (3, 2, 1, 0)])
        obj = bpy.data.objects.new('wing_tip_membrane', mesh)
        bpy.context.collection.objects.link(obj)
        obj.parent = tip
        mesh.materials.append(material('wing membrane'))
        for end in (-1, 1):
            leg = joint(f'leg_{side}_{end}', (side * .68, end * 1.02, .62), root)
            box('leg_mesh', (side * .22, .1, -.46), (.45, .60, .91), 'obsidian hide', leg)
            box('foot', (side * .25, -.10, -.88), (.48, .95, .21), 'hide ridge', leg)
    parent = root
    for i in range(6):
        tail = joint(f'tail_{i}', (0, 1.6, 1.15) if i == 0 else (0, 1.12, 0), parent)
        width = .9 - i * .12
        box('tail_mesh', (0, .56, 0), (width, 1.26, width * .85), 'obsidian hide', tail)
        box('tail_spine', (0, .50, width * .62), (.13, .21, .28), 'hide ridge', tail)
        parent = tail
    return root

def crystal():
    root = joint('EndCrystal')
    box('base', (0, 0, .12), (1, 1, .24), 'stone base', root)
    spin = joint('crystal_spin', (0, 0, 1.08), root)
    box('crystal', (0, 0, 0), (.64, .64, .64), 'crystal core', spin, (.61, .61, .61))
    for side in (-1, 1):
        for side2 in (-1, 1):
            box('frame_vertical', (side * .54, side2 * .54, 0), (.055, .055, 1.12), 'crystal frame', spin)
            box('frame_horizontal', (side * .54, 0, side2 * .54), (.055, 1.12, .055), 'crystal frame', spin)
            box('frame_cross', (0, side * .54, side2 * .54), (1.12, .055, .055), 'crystal frame', spin)
    return root

def export(name):
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.export_scene.fbx(filepath=str(OUT / f'{name}.fbx'), use_selection=True,
        object_types={'EMPTY', 'MESH', 'ARMATURE'}, apply_unit_scale=True,
        apply_scale_options='FBX_SCALE_UNITS', axis_forward='-Z', axis_up='Y',
        bake_anim=False, add_leaf_bones=False, mesh_smooth_type='OFF', use_mesh_modifiers=True)
    triangles = sum(len(obj.data.polygons) * 2 for obj in bpy.context.scene.objects if obj.type == 'MESH')
    print(f'EXPORTED {name}: {triangles} approximate triangles')

legacy = ROOT / 'Assets' / 'models'
if not legacy.exists():
    legacy = SOURCE / 'models'
for path in sorted(legacy.glob('*.glb')):
    reset()
    bpy.ops.import_scene.gltf(filepath=str(path))
    export(path.stem)

for name, factory in [('enderman', enderman), ('villager', villager), ('end_dragon', dragon), ('end_crystal', crystal)]:
    reset()
    factory()
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE / f'{name}_new.blend'))
    export(name)
print('Unity model conversion complete.')
