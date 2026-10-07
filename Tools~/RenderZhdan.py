"""Render the supplied Blender mine in its deployed, vertical pose without saving the blend."""
import bpy, math
from pathlib import Path
from mathutils import Vector

root=Path(__file__).resolve().parent.parent
bpy.ops.wm.open_mainfile(filepath='F:/NCMod/Iron Gate/Zhdan_Mine.blend')
scene=bpy.context.scene
scene.frame_set(15)
body=bpy.data.objects['Zhdan_Mine_Container']
body.rotation_euler=(math.pi/2,0,0)
bpy.context.view_layer.update()
points=[obj.matrix_world @ Vector(corner) for obj in scene.objects if obj.type=='MESH' for corner in obj.bound_box]
low=Vector(tuple(min(p[i] for p in points) for i in range(3)))
high=Vector(tuple(max(p[i] for p in points) for i in range(3)))
center=(low+high)/2
for obj in list(scene.objects):
    if obj.type in {'CAMERA','LIGHT'}: bpy.data.objects.remove(obj,do_unlink=True)

camera_data=bpy.data.cameras.new('GalleryCamera')
camera=bpy.data.objects.new('GalleryCamera',camera_data)
scene.collection.objects.link(camera)
camera.location=center+Vector((.6,-.8,.35))
camera.rotation_euler=(center-camera.location).to_track_quat('-Z','Y').to_euler()
camera_data.type='ORTHO'
camera_data.ortho_scale=max((high-low).z*1.85,(high-low).length*1.5)
scene.camera=camera
for name,offset,power,size in [('Key',(.35,-.5,.55),7,.4),('Fill',(-.4,-.2,.15),3,.5),('Rim',(.2,.4,.4),6,.3)]:
    light_data=bpy.data.lights.new(name,'AREA');light_data.energy=power;light_data.shape='DISK';light_data.size=size
    light=bpy.data.objects.new(name,light_data);scene.collection.objects.link(light)
    light.location=center+Vector(offset);light.rotation_euler=(center-light.location).to_track_quat('-Z','Y').to_euler()
world=bpy.data.worlds.new('GalleryCharcoal')
world.use_nodes=True
world.node_tree.nodes['Background'].inputs['Color'].default_value=(.005,.008,.012,1)
world.node_tree.nodes['Background'].inputs['Strength'].default_value=.8
scene.world=world
scene.render.engine='CYCLES';scene.cycles.device='CPU';scene.cycles.samples=32;scene.cycles.use_denoising=True
scene.render.resolution_x=1600;scene.render.resolution_y=900;scene.render.resolution_percentage=100
scene.render.film_transparent=False
scene.view_settings.view_transform='Standard'
scene.view_settings.look='None'
scene.render.image_settings.file_format='PNG'
scene.render.filepath=str(root/'Validation~'/'Gallery'/'zhdan-mine.png')
bpy.ops.render.render(write_still=True)
print('ZHDAN_GALLERY_RENDER_OK')
