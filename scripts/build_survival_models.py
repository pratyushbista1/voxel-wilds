"""Export the survival expansion's editable models and runtime GLBs."""
from pathlib import Path
import bpy
import math

ROOT = Path(__file__).resolve().parents[1]
bpy.context.preferences.filepaths.save_version = 0
COLORS = {
    'green': (.28, .47, .22), 'darkgreen': (.15, .27, .12),
    'shirt': (.12, .46, .47), 'pants': (.24, .23, .40),
    'black': (.045, .038, .035), 'white': (.88, .87, .77),
    'brown': (.26, .14, .08), 'pink': (.80, .44, .42),
    'lightpink': (.96, .65, .60), 'red': (.67, .10, .13),
    'wood': (.50, .29, .12), 'iron': (.64, .67, .62),
    'gold': (.95, .57, .08), 'orange': (.85, .30, .025),
}
M = {}
for name, color in COLORS.items():
    material = bpy.data.materials.new(name)
    material.diffuse_color = (*color, 1)
    material.use_nodes = True
    material.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value = (*color, 1)
    material.node_tree.nodes['Principled BSDF'].inputs['Roughness'].default_value = .85
    M[name] = material

def joint(name, position=(0, 0, 0), parent=None):
    obj = bpy.data.objects.new(name, None)
    bpy.context.collection.objects.link(obj)
    obj.parent = parent
    obj.location = position
    return obj

def box(name, position, size, color, parent):
    bpy.ops.mesh.primitive_cube_add(size=1)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.parent = parent
    obj.location = position
    obj.data.materials.append(M[color])
    return obj

def eyes(root, width, front, height):
    for side in [-1, 1]:
        box('eye', (side * width, front, height), (.07, .025, .075), 'black', root)

def humanoid(kind):
    root = joint(kind)
    skin = 'green' if kind == 'zombie' else 'white' if kind == 'skeleton' else 'pink'
    body = 'shirt' if kind == 'zombie' else 'white' if kind == 'skeleton' else 'brown'
    if kind == 'skeleton':
        box('spine', (0, .06, 1.12), (.13, .17, .67), 'white', root)
        for height in [1.03, 1.20, 1.37]:
            box('rib', (0, -.02, height), (.51, .24, .07), 'white', root)
        box('pelvis', (0, 0, .84), (.45, .23, .13), 'white', root)
    else:
        box('body', (0, 0, 1.12), (.53, .30, .64), body, root)
    box('head', (0, 0, 1.70), (.50, .47, .49), skin, root)
    eyes(root, .14, -.247, 1.76)
    if kind == 'skeleton':
        box('mouth', (0, -.247, 1.58), (.23, .025, .04), 'black', root)
    if kind == 'piglin':
        box('snout', (0, -.30, 1.60), (.31, .19, .20), 'lightpink', root)
        for side in [-1, 1]:
            box('ear', (side * .32, 0, 1.83), (.18, .13, .25), 'pink', root)
            box('tusk', (side * .16, -.37, 1.62), (.06, .07, .14), 'white', root)
        box('belt', (0, -.01, .93), (.56, .33, .08), 'gold', root)
    for side in [-1, 1]:
        arm = joint(f'arm_{side}', (side * .38, 0, 1.40), root)
        box('arm_mesh', (0, 0, -.32), (.12, .13, .68) if kind == 'skeleton' else (.19, .23, .68), skin, arm)
        leg = joint(f'leg_{side}', (side * .15, 0, .82), root)
        box('leg_mesh', (0, 0, -.37), (.12, .13, .74) if kind == 'skeleton' else (.22, .25, .74), 'pants' if kind == 'zombie' else body, leg)
        box('foot', (0, -.045, -.76), (.16, .26, .10) if kind == 'skeleton' else (.24, .34, .12), 'white' if kind == 'skeleton' else 'black', leg)
    return root

def animal(kind):
    root = joint(kind)
    pig = kind == 'pig'
    chicken = kind == 'chicken'
    skin = 'pink' if pig else 'white' if chicken else 'brown'
    scale = .55 if chicken else 1
    box('body', (0, .1 * scale, .84 * scale), (.70 * scale, 1.08 * scale, .62 * scale), skin, root)
    box('head', (0, -.60 * scale, 1.0 * scale), (.47 * scale, .43 * scale, .47 * scale), skin, root)
    eyes(root, .15 * scale, -.824 * scale, 1.08 * scale)
    box('snout', (0, -.86 * scale, .91 * scale), (.33 * scale, .16 * scale, .19 * scale), 'gold' if chicken else 'lightpink', root)
    if chicken:
        box('comb', (0, -.32, .70), (.07, .19, .14), 'red', root)
        for side in [-1, 1]:
            arm = joint(f'arm_{side}', (side * .24, 0, .53), root)
            box('wing', (0, 0, -.13), (.10, .35, .30), 'white', arm)
    if kind == 'cow':
        for x, y, z in [(-.36, -.14, .89), (.36, .26, .85), (0, .17, 1.16)]:
            box('patch', (x, y, z), (.025 if x else .40, .39, .31 if x else .025), 'white', root)
        for side in [-1, 1]:
            box('horn', (side * .24, -.48, 1.34), (.09, .11, .23), 'white', root)
    for side in [-1, 1]:
        for longitudinal in ([0] if chicken else [-1, 1]):
            leg = joint(f'leg_{side}_{longitudinal}', (side * .25 * scale, longitudinal * .36 * scale, .55 * scale), root)
            box('leg_mesh', (0, 0, -.24 * scale), (.15 * scale, .17 * scale, .49 * scale), 'gold' if chicken else skin, leg)
    return root

def creeper():
    root = joint('Creeper')
    box('body', (0, 0, .95), (.43, .36, .83), 'green', root)
    box('head', (0, 0, 1.53), (.57, .54, .52), 'green', root)
    eyes(root, .16, -.28, 1.63)
    box('mouth', (0, -.28, 1.42), (.24, .03, .18), 'black', root)
    for side in [-1, 1]:
        for y in [-1, 1]:
            leg = joint(f'leg_{side}_{y}', (side * .2, y * .18, .50), root)
            box('leg_mesh', (0, 0, -.20), (.21, .30, .40), 'darkgreen', leg)
    return root

def spider():
    root = joint('Spider')
    box('body', (0, .25, .43), (.67, .80, .40), 'brown', root)
    box('head', (0, -.34, .42), (.48, .38, .31), 'black', root)
    for x in [-.18, -.06, .06, .18]: box('eye', (x, -.54, .47), (.045, .025, .055), 'red', root)
    for side in [-1, 1]:
        for i in range(4):
            leg = joint(f'leg_{side}_{i}', (side * .25, -.2 + i * .17, .36), root)
            ob = box('leg_mesh', (side * .36, 0, -.07), (.78, .065, .075), 'black', leg)
            ob.rotation_euler[1] = side * .2
            ob.rotation_euler[2] = side * (i - 1.5) * .3
    return root

def blaze():
    root = joint('Blaze')
    box('head', (0, 0, 1.25), (.44, .43, .44), 'gold', root)
    eyes(root, .12, -.23, 1.31)
    for i in range(12):
        a = (i % 4) * math.tau / 4 + (i // 4) * .5
        box('rod', (math.sin(a) * .48, math.cos(a) * .48, .42 + (i // 4) * .38), (.10, .10, .35), 'gold', root)
    return root

def bed(head):
    root = joint('Bed head' if head else 'Bed foot')
    box('frame', (0, 0, .23), (.96, 1, .16), 'wood', root)
    box('mattress', (0, 0, .43), (.94, 1, .265), 'red', root)
    if head: box('pillow', (0, .24, .557), (.85, .42, .035), 'white', root)
    for side in [-1, 1]: box('leg', (side * .39, .39 if head else -.39, .10), (.16, .16, .20), 'wood', root)
    return root

def chest():
    root = joint('Chest')
    box('box', (0, 0, .39), (.88, .88, .74), 'wood', root)
    box('lid', (0, 0, .80), (.91, .91, .15), 'brown', root)
    box('latch', (0, -.46, .60), (.10, .05, .19), 'iron', root)
    for side in [-1, 1]: box('band', (side * .35, -.451, .40), (.065, .022, .74), 'brown', root)
    return root

builders = {'zombie': lambda: humanoid('zombie'), 'skeleton': lambda: humanoid('skeleton'),
    'piglin': lambda: humanoid('piglin'), 'cow': lambda: animal('cow'), 'pig': lambda: animal('pig'),
    'chicken': lambda: animal('chicken'), 'creeper': creeper, 'spider': spider, 'blaze': blaze,
    'bed_head': lambda: bed(True), 'bed_foot': lambda: bed(False), 'chest': chest}
for name, build in builders.items():
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    root = build()
    bpy.ops.object.select_all(action='SELECT')
    bpy.context.view_layer.objects.active = root
    bpy.ops.export_scene.gltf(filepath=str(ROOT / 'assets' / 'models' / f'{name}.glb'), export_format='GLB', use_selection=True, export_yup=True, export_animations=False)
    bpy.ops.wm.save_as_mainfile(filepath=str(ROOT / 'assets' / 'blender' / f'{name}.blend'), compress=True)
print('Exported 12 survival models and editable Blender sources.')
