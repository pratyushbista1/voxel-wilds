"""Build and export the model collection. Run with Blender's --python option."""
from pathlib import Path
import math
import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'assets' / 'models'
BLENDS = ROOT / 'assets' / 'blender'
OUT.mkdir(parents=True, exist_ok=True)
BLENDS.mkdir(parents=True, exist_ok=True)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
bpy.context.preferences.filepaths.save_version = 0

def material(name, color, emission=0):
    m = bpy.data.materials.new(name)
    m.diffuse_color = (*color, 1)
    m.use_nodes = True
    bsdf = m.node_tree.nodes.get('Principled BSDF')
    bsdf.inputs['Base Color'].default_value = (*color, 1)
    bsdf.inputs['Roughness'].default_value = .88
    if emission:
        bsdf.inputs['Emission Color'].default_value = (*color, 1)
        bsdf.inputs['Emission Strength'].default_value = emission
    return m

M = {name: material(name, color, emission) for name, color, emission in [
    ('ivory wool', (.86, .81, .65), 0), ('wool highlight', (.98, .93, .77), 0),
    ('warm skin', (.40, .31, .23), 0), ('dark hooves', (.12, .14, .12), 0),
    ('deep eyes', (.025, .045, .04), 0), ('eye glint', (.95, .98, .87), 0),
    ('fox orange', (.70, .30, .105), 0), ('fox highlight', (.91, .43, .15), 0),
    ('cream fur', (.92, .84, .63), 0), ('earth brown', (.25, .16, .09), 0),
    ('wood grain', (.45, .28, .12), 0), ('cut wood', (.72, .50, .25), 0),
    ('stone', (.32, .39, .36), 0), ('stone light', (.47, .55, .50), 0),
    ('stone dark', (.19, .25, .24), 0), ('moss', (.35, .49, .18), 0),
    ('iron', (.66, .73, .69), 0), ('iron edge', (.85, .89, .79), 0),
    ('bronze', (.39, .29, .15), 0), ('warm glow', (1, .57, .16), 1.8),
    ('flame gold', (1, .30, .035), 1.2), ('flame core', (1, .78, .32), 2.2),
    ('crystal glow', (.23, .90, .80), 1.8),
]}

def empty(name, loc=(0, 0, 0), parent=None):
    ob = bpy.data.objects.new(name, None)
    bpy.context.collection.objects.link(ob)
    ob.location = loc
    if parent: ob.parent = parent
    return ob

def cube(name, loc, size, mat, parent=None, rotation=(0, 0, 0)):
    bpy.ops.mesh.primitive_cube_add(size=1, location=(0, 0, 0))
    ob = bpy.context.object
    ob.name = name
    ob.dimensions = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    ob.data.materials.append(M[mat])
    if parent: ob.parent = parent
    ob.location = loc
    ob.rotation_euler = rotation
    return ob

models = {}
def sheep():
    root = empty('Sheep')
    cube('fleece', (0, .06, .92), (.84, 1.16, .66), 'ivory wool', root)
    cube('fleece_top', (0, .07, 1.24), (.77, 1.06, .12), 'wool highlight', root)
    cube('head', (0, -.64, 1.01), (.46, .44, .49), 'warm skin', root)
    cube('fringe', (0, -.57, 1.25), (.51, .42, .21), 'wool highlight', root)
    cube('muzzle', (0, -.89, .91), (.35, .12, .19), 'ivory wool', root)
    for x in [-1, 1]:
        cube('ear', (x * .30, -.60, 1.06), (.20, .20, .11), 'warm skin', root)
        cube('eye', (x * .17, -.868, 1.09), (.07, .024, .07), 'deep eyes', root)
        cube('glint', (x * .17 - .014, -.883, 1.105), (.023, .012, .023), 'eye glint', root)
        for y in [-1, 1]:
            leg = empty(f'leg_{x}_{y}', (x * .28, y * .38, .64), root)
            cube('leg_mesh', (0, 0, -.23), (.17, .18, .46), 'warm skin', leg)
            cube('hoof', (0, -.014, -.52), (.20, .23, .14), 'dark hooves', leg)
    cube('tail', (0, .70, .97), (.24, .22, .24), 'wool highlight', root)
    return root

def fox():
    root = empty('Fox')
    cube('body', (0, .12, .55), (.48, .85, .37), 'fox orange', root)
    cube('back', (0, .10, .75), (.40, .72, .09), 'fox highlight', root)
    cube('chest', (0, -.24, .53), (.38, .25, .33), 'cream fur', root)
    cube('head', (0, -.48, .72), (.52, .43, .40), 'fox orange', root)
    cube('jaw', (0, -.58, .59), (.46, .40, .15), 'cream fur', root)
    cube('snout', (0, -.77, .67), (.24, .22, .17), 'cream fur', root)
    cube('nose', (0, -.90, .70), (.14, .06, .09), 'deep eyes', root)
    for side in [-1, 1]:
        cube('ear', (side * .17, -.42, 1.00), (.15, .18, .30), 'earth brown', root)
        cube('ear_inner', (side * .17, -.522, 1.005), (.09, .016, .16), 'fox highlight', root)
        cube('eye', (side * .175, -.707, .79), (.06, .025, .06), 'deep eyes', root)
        for y in [-1, 1]:
            leg = empty(f'leg_{side}_{y}', (side * .17, .12 + y * .30, .42), root)
            cube('shin', (0, 0, -.14), (.12, .15, .28), 'fox orange', leg)
            cube('sock', (0, -.015, -.33), (.13, .19, .14), 'earth brown', leg)
    tail = empty('tail', (0, .51, .59), root)
    cube('tail_base', (0, .29, .03), (.29, .65, .30), 'fox orange', tail, (math.radians(15), 0, 0))
    cube('tail_tip', (0, .67, .13), (.27, .28, .27), 'cream fur', tail)
    return root

def sentinel():
    root = empty('Sentinel')
    cube('body', (0, 0, 1.2), (.80, .46, .76), 'stone', root)
    cube('breastplate', (0, -.27, 1.25), (.57, .12, .47), 'stone light', root)
    cube('heart', (0, -.345, 1.28), (.16, .035, .16), 'crystal glow', root)
    cube('head', (0, -.015, 1.86), (.65, .53, .55), 'stone dark', root)
    cube('brow', (0, -.31, 2.02), (.72, .11, .16), 'stone', root)
    cube('eyes', (0, -.292, 1.91), (.41, .032, .07), 'crystal glow', root)
    cube('nose', (0, -.335, 1.81), (.12, .10, .21), 'stone', root)
    for side in [-1, 1]:
        arm = empty(f'arm_{side}', (side * .57, 0, 1.50), root)
        cube('arm_mesh', (0, 0, -.39), (.31, .40, .87), 'stone', arm)
        cube('fist', (0, -.01, -.85), (.36, .44, .29), 'stone dark', arm)
        leg = empty(f'leg_{side}', (side * .23, 0, .85), root)
        cube('leg_mesh', (0, 0, -.35), (.30, .35, .70), 'stone dark', leg)
        cube('foot', (0, -.07, -.75), (.35, .50, .20), 'stone', leg)
    for x, y, z, w in [(-.21,-.275,1.51,.19),(.26,-.28,1.00,.13),(-.19,-.287,2.10,.25),(.63,-.21,1.50,.24)]:
        cube('moss_patch', (x,y,z), (w,.025,.12), 'moss', root)
    return root

def campfire():
    root=empty('Campfire')
    for i in range(8):
        a=i*math.tau/8
        cube('hearth_stone',(math.cos(a)*.39,math.sin(a)*.39,.095),(.24,.22,.19),'stone',root,(0,0,a))
    for y in [-.15,.15]:
        cube('log',(0,y,.19),(.72,.15,.16),'earth brown',root)
        cube('log_end',(-.367,y,.19),(.025,.12,.13),'cut wood',root)
        cube('log_end',(.367,y,.19),(.025,.12,.13),'cut wood',root)
    for x in [-.15,.15]:cube('cross_log',(x,0,.32),(.15,.68,.15),'wood grain',root)
    flame=empty('flame',(0,0,.32),root)
    for x,y,z,w,h,mat in [(-.16,0,.12,.18,.30,'flame gold'),(.13,.03,.16,.22,.38,'warm glow'),(0,-.05,.23,.17,.52,'flame core'),(.03,.04,.46,.10,.24,'warm glow')]:
        cube('fire',(x,y,z),(w,w,h),mat,flame)
    return root

def lantern():
    root=empty('Lantern')
    cube('base',(0,0,.09),(.49,.49,.12),'bronze',root)
    cube('light',(0,0,.35),(.31,.31,.42),'warm glow',root)
    cube('core',(0,-.16,.35),(.13,.016,.29),'flame core',root)
    for x in [-.20,.20]:
        for y in [-.20,.20]:cube('frame',(x,y,.35),(.055,.055,.48),'bronze',root)
    cube('roof',(0,0,.61),(.50,.50,.12),'bronze',root)
    for x in [-.12,.12]:cube('handle',(x,0,.77),(.045,.06,.24),'iron',root)
    cube('handle_top',(0,0,.885),(.28,.06,.045),'iron',root)
    return root

def pickaxe():
    root=empty('Pickaxe')
    cube('handle',(0,0,.42),(.085,.09,.84),'wood grain',root)
    cube('grip',(0,-.051,.36),(.040,.015,.66),'cut wood',root)
    cube('head',(0,0,.83),(.61,.15,.12),'iron',root)
    cube('top_edge',(0,-.005,.90),(.54,.15,.032),'iron edge',root)
    for s in [-1,1]:
        cube('pick_tip',(s*.30,0,.76),(.10,.15,.13),'iron',root)
        cube('pick_tip_end',(s*.34,0,.66),(.075,.13,.12),'iron edge',root)
    cube('binding',(0,0,.81),(.14,.18,.15),'bronze',root)
    return root

def sword():
    root=empty('Sword')
    cube('grip',(0,0,.16),(.10,.11,.31),'wood grain',root)
    cube('pommel',(0,0,.025),(.15,.15,.09),'bronze',root)
    cube('guard',(0,0,.34),(.39,.14,.075),'bronze',root)
    cube('blade',(0,0,.70),(.13,.065,.66),'iron',root)
    cube('edge',(-.046,-.039,.70),(.045,.018,.67),'iron edge',root)
    cube('tip',(0,0,1.075),(.08,.065,.10),'iron edge',root)
    return root

gallery_scene=bpy.context.scene
gallery_scene.name='Voxel Wilds • Model Gallery'
builders={'sheep':sheep,'fox':fox,'sentinel':sentinel,'campfire':campfire,'lantern':lantern,'pickaxe':pickaxe,'sword':sword}
for name, build in builders.items():
    asset_scene=bpy.data.scenes.new(name.title())
    bpy.context.window.scene=asset_scene
    root=build()
    models[name]=root
    bpy.ops.object.select_all(action='DESELECT')
    def select_tree(obj):
        obj.select_set(True)
        for child in obj.children:select_tree(child)
    select_tree(root)
    bpy.context.view_layer.objects.active=root
    bpy.ops.export_scene.gltf(filepath=str(OUT/f'{name}.glb'),export_format='GLB',use_selection=True,export_yup=True,export_apply=True,export_animations=False,export_extras=True)
    for area in bpy.context.screen.areas:
        if area.type=='VIEW_3D':
            area.spaces.active.shading.color_type='MATERIAL'
            area.spaces.active.region_3d.view_distance=3.2
            area.spaces.active.region_3d.view_location=(0,0,.65)
    bpy.ops.wm.save_as_mainfile(filepath=str(BLENDS/f'{name}.blend'),compress=True)

bpy.context.window.scene=gallery_scene
for root in models.values():
    for ob in [root,*root.children_recursive]:gallery_scene.collection.objects.link(ob)
placements=[(-2,0,0),(0,0,0),(2.2,.3,0),(-1.3,-1.65,0),(.1,-1.6,0),(1.1,-1.6,0),(2,-1.6,0)]
for (name,root),loc in zip(models.items(),placements):
    root.location=loc
    root.hide_set(False)
    root.hide_render=False
    if name in ['pickaxe','sword']:root.rotation_euler[1]=-.4

bpy.ops.mesh.primitive_plane_add(size=200)
ground=bpy.context.object;ground.name='Gallery ground'
ground.data.materials.append(material('gallery background',(.085,.125,.105)))
bpy.ops.object.camera_add(location=(7,-10,7))
cam=bpy.context.object;cam.name='Gallery camera'
cam.rotation_euler=(Vector((0,-.2,.8))-cam.location).to_track_quat('-Z','Y').to_euler()
cam.data.type='ORTHO';cam.data.ortho_scale=7.3
bpy.context.scene.camera=cam
for name,loc,power,size in [('Soft key',(0,-4,8),1500,7),('Warm rim',(-4,2,5),1100,5)]:
    bpy.ops.object.light_add(type='AREA',location=loc)
    light=bpy.context.object;light.name=name;light.data.energy=power;light.data.shape='DISK';light.data.size=size
    light.rotation_euler=(-light.location).to_track_quat('-Z','Y').to_euler()
scene=bpy.context.scene
scene.render.engine='CYCLES';scene.cycles.samples=24
scene.render.resolution_x=1400;scene.render.resolution_y=920;scene.render.resolution_percentage=100
scene.world.color=(.3,.3,.3)
scene.view_settings.view_transform='AgX'
scene.render.image_settings.file_format='PNG'
scene.render.filepath=str(ROOT/'assets'/'model-gallery.png')
bpy.ops.wm.save_as_mainfile(filepath=str(BLENDS/'voxel-wilds-gallery.blend'))
bpy.ops.render.render(write_still=True)
print('EXPORTED 7 ORIGINAL MODELS, 8 BLEND FILES, AND MODEL GALLERY')
