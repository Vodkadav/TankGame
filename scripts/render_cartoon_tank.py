"""Build and render a chunky CARTOON tank as two iso sprite layers — a hull and an independently
rotating turret — entirely from Blender primitives (no external model, no licence).

The owner asked for a brighter, stylised look in the spirit of the classic top-down cartoon tank-battle
games (rounded turret, stubby barrel, fat treads, bold colours) instead of the realistic military
Super_Tank. Rather than toon-shade a realistic mesh — which still reads as realistic — this models a
cartoon tank procedurally: a rounded box hull on two fat treads, a domed turret, and a short barrel.
The body is a light neutral tan so the in-engine team tint (Modulate) paints it bright per side; the
treads and barrel are dark. Flat, high-roughness shading + a soft sun gives the even cartoon look; the
bold black outline is added later in PIL (scripts/pack_cartoon_tank.py).

The hull and turret are SEPARATE objects from the start (no geometry split needed). The turret sits at
the world origin and spins about its own vertical axis, so any turret frame overlays any hull frame and
stays seated on the ring — and the hull's turret seat is also at the origin, so the layers composite
aligned. Camera, light, per-frame rotation and FACINGS match scripts/render_iso_tank.py exactly, so the
in-engine facing maths (TankView.FacingOffset, the iso anchor) are unchanged: the barrel rests pointing
along -Y (the model's "forward"), each frame rotates the layer i*step CCW about +Z.

Run with Blender (headless):
    blender --background --python scripts/render_cartoon_tank.py -- <out_dir>

Writes hull_NN.png + turret_NN.png frames; scripts/pack_cartoon_tank.py stitches + outlines them into
the committed IsoTankHull.png / IsoTankTurret.png. Deterministic: fixed geometry, camera, light.
"""
import bpy
import bmesh
import math
import sys
from mathutils import Vector

# --- tunables (eyeball-gated on the owner's playtest) -------------------------------------------
FACINGS = 16          # directional frames per layer — must match render_iso_tank.py / TankFacings
RES = 192             # square canvas per frame
ELEV_DEG = 30.0       # camera elevation — matches the iso tile art angle
AZ_DEG = -45.0        # camera yaw — matches IsoProjection's camera
CAM_TARGET_Z = 0.55   # aim the camera at the tank's vertical centre so it is framed, not clipped
ORTHO_PAD = 1.9       # ortho frustum = max footprint dimension * this

BODY = (0.90, 0.88, 0.84, 1.0)   # near-white neutral — team tint multiplies this to a bright per-side colour
DARK = (0.18, 0.18, 0.21, 1.0)   # tread band, barrel, hatch
WHEEL = (0.36, 0.36, 0.40, 1.0)  # road wheels — mid grey so they read against the dark track
ACCENT = (0.74, 0.72, 0.68, 1.0) # turret hatch / darker panel for a little contrast


def out_dir():
    argv = sys.argv
    return argv[argv.index("--") + 1] if "--" in argv else "."


def material(name, rgba):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = rgba
    bsdf.inputs["Roughness"].default_value = 0.95   # kill specular highlights -> flat cartoon shading
    if "Specular IOR Level" in bsdf.inputs:
        bsdf.inputs["Specular IOR Level"].default_value = 0.1
    return mat


def box(name, size, location, mat, bevel=0.07):
    """A rounded box (bevelled + smooth-shaded) — the chunky cartoon building block."""
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=location)
    obj = bpy.context.active_object
    obj.name = name
    obj.scale = (size[0], size[1], size[2])
    bpy.ops.object.transform_apply(scale=True)
    b = obj.modifiers.new("Bevel", "BEVEL")
    b.width = bevel
    b.segments = 3
    bpy.ops.object.modifier_apply(modifier=b.name)
    obj.data.materials.append(mat)
    return obj


def cylinder(name, radius, depth, location, mat, axis="Z", bevel=0.04):
    bpy.ops.mesh.primitive_cylinder_add(radius=radius, depth=depth, vertices=24, location=location)
    obj = bpy.context.active_object
    obj.name = name
    if axis == "Y":
        obj.rotation_euler = (math.radians(90), 0, 0)
        bpy.ops.object.transform_apply(rotation=True)
    b = obj.modifiers.new("Bevel", "BEVEL")
    b.width = bevel
    b.segments = 2
    bpy.ops.object.modifier_apply(modifier=b.name)
    obj.data.materials.append(mat)
    return obj


def sphere(name, radius, scale_z, location, mat):
    bpy.ops.mesh.primitive_uv_sphere_add(radius=radius, segments=24, ring_count=12, location=location)
    obj = bpy.context.active_object
    obj.name = name
    obj.scale = (1.0, 1.0, scale_z)
    bpy.ops.object.transform_apply(scale=True)
    obj.data.materials.append(mat)
    return obj


def join(name, objs, smooth=False):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.join()
    merged = bpy.context.active_object
    merged.name = name
    # Hull stays flat for crisp chunky faces; the turret dome is smoothed so it reads round.
    bpy.ops.object.shade_smooth() if smooth else bpy.ops.object.shade_flat()
    return merged


def build_hull(body_mat, dark_mat, wheel_mat):
    # Two fat tracks down the sides (longer than the body, so the silhouette reads as a tank) with a row
    # of road wheels, and a bulky rounded body over them. Forward = -Y.
    parts = []
    for sign in (-1, 1):
        x = sign * 0.66
        parts.append(box(f"Track{sign}", (0.34, 2.50, 0.52), (x, 0.0, 0.26), dark_mat, bevel=0.12))
        for wy in (-0.84, -0.42, 0.0, 0.42, 0.84):   # road wheels poking out both faces of the track
            parts.append(cylinder(f"Wheel{sign}_{wy}", 0.25, 0.46, (x, wy, 0.25), wheel_mat, axis="X", bevel=0.05))
    hull = box("HullBody", (1.28, 1.95, 0.64), (0.0, 0.0, 0.80), body_mat, bevel=0.16)
    return join("hull", parts + [hull])


def build_turret(body_mat, dark_mat, accent_mat):
    # Big round domed turret centred on the world origin (its spin axis), a short stubby barrel forward (-Y).
    dome = sphere("Dome", 0.66, 0.62, (0.0, 0.0, 1.14), body_mat)
    hatch = cylinder("Hatch", 0.30, 0.10, (0.0, 0.12, 1.52), accent_mat, axis="Z", bevel=0.05)
    barrel = cylinder("Barrel", 0.16, 0.85, (0.0, -0.78, 1.10), dark_mat, axis="Y", bevel=0.05)
    muzzle = cylinder("Muzzle", 0.21, 0.20, (0.0, -1.18, 1.10), dark_mat, axis="Y", bevel=0.06)
    return join("turret", [dome, hatch, barrel, muzzle], smooth=True)


def setup_camera(max_dim):
    cam_data = bpy.data.cameras.new("IsoCam")
    cam_data.type = "ORTHO"
    cam_data.ortho_scale = max_dim * ORTHO_PAD
    cam = bpy.data.objects.new("IsoCam", cam_data)
    bpy.context.collection.objects.link(cam)
    target = Vector((0.0, 0.0, CAM_TARGET_Z))
    e, a = math.radians(ELEV_DEG), math.radians(AZ_DEG)
    direction = Vector((math.cos(e) * math.sin(a), -math.cos(e) * math.cos(a), math.sin(e)))
    cam.location = target + direction * max_dim * 4.0
    cam.rotation_euler = (-direction).to_track_quat("-Z", "Y").to_euler()
    bpy.context.scene.camera = cam


def setup_light():
    light_data = bpy.data.lights.new("Sun", "SUN")
    light_data.energy = 3.2
    sun = bpy.data.objects.new("Sun", light_data)
    bpy.context.collection.objects.link(sun)
    sun.rotation_euler = (math.radians(50), 0, math.radians(40))
    world = bpy.context.scene.world
    world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    if bg:
        bg.inputs[1].default_value = 0.75   # generous ambient -> even, cartoon-flat fill


def clear_scene():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete()
    for block in (bpy.data.meshes, bpy.data.materials, bpy.data.cameras, bpy.data.lights):
        for b in list(block):
            block.remove(b)


def main():
    clear_scene()
    body_mat = material("CartoonBody", BODY)
    dark_mat = material("CartoonDark", DARK)
    wheel_mat = material("CartoonWheel", WHEEL)
    accent_mat = material("CartoonAccent", ACCENT)

    hull = build_hull(body_mat, dark_mat, wheel_mat)
    turret = build_turret(body_mat, dark_mat, accent_mat)
    max_dim = 2.6   # tread length plus margin; fixed so framing is identical regardless of geometry

    setup_camera(max_dim)
    setup_light()

    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = RES
    scene.render.resolution_y = RES
    scene.render.film_transparent = True
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"

    out = out_dir()
    for layer_name, layer, other in (("hull", hull, turret), ("turret", turret, hull)):
        other.hide_render = True
        layer.hide_render = False
        for i in range(FACINGS):
            layer.rotation_euler = (0.0, 0.0, i * 2.0 * math.pi / FACINGS)
            scene.render.filepath = f"{out}/{layer_name}_{i:02d}.png"
            bpy.ops.render.render(write_still=True)
        print(f"RENDERED {layer_name} x{FACINGS}")


main()
