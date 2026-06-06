"""Render an isometric tank as two directional sprite layers — a hull and an independently
rotating turret — from the CC BY 4.0 low-poly Super_Tank model (Zsky_01, military_vehicles_lp).

The flat top-down tank is the last clearly-non-iso element of the arena; this is Phase 3 of the
isometric pivot. The model is one fused mesh, so we split the rotating turret (dome + gun) from the
hull by geometry: the turret is the central superstructure above the hull deck (Z above DECK, |X|
inside XLIM), everything else is the hull (tracks, fenders, stowage). Both layers are then rotated
about a single vertical axis through the turret-ring centroid — which we move to the world origin and
aim the orthographic iso camera at — so any rendered turret facing overlays any hull facing and stays
seated on the ring. The hull frame is chosen in-game from the chassis heading, the turret frame from
the aim, reproducing the independent turret the flat sprite had.

Run with Blender (headless):
    blender --background <Super_Tank.blend> --python scripts/render_iso_tank.py -- <out_dir>

It writes hull_NN.png and turret_NN.png frames (N = FACINGS) into <out_dir>; scripts/pack_iso_tank.py
then stitches them into the two committed spritesheets. Deterministic: fixed camera, light, and seed.
"""
import bpy
import bmesh
import math
import sys
from mathutils import Vector

# --- tunables (eyeball-gated on the owner's playtest) -------------------------------------------
FACINGS = 16          # directional frames per layer (chassis + turret each get this many)
RES = 192             # square canvas per frame
ELEV_DEG = 30.0       # camera elevation — matches the iso tile art angle
AZ_DEG = -45.0        # camera yaw — matches IsoProjection's camera (looks toward world +X,+Y)
DECK_Z = 0.13         # turret/hull seam height (a clean gap in the Super_Tank vertex Z histogram)
TURRET_XLIM = 0.62    # turret is central; fenders/stowage sit beyond this |X| and stay on the hull
ORTHO_PAD = 1.7       # ortho frustum = max model dimension * this

# Recolour the cool-green body to a neutral tan so the in-engine team tint (Modulate) reads cleanly,
# matching the approved neutral Kenney hull. Tracks/details keep their dark colours.
NEUTRAL = {
    "Green_SuperTank": (0.80, 0.74, 0.58, 1.0),
    "DarkGreen_SuperTank": (0.55, 0.50, 0.38, 1.0),
}


def out_dir():
    argv = sys.argv
    return argv[argv.index("--") + 1] if "--" in argv else "."


def neutralise_body():
    for name, rgba in NEUTRAL.items():
        mat = bpy.data.materials.get(name)
        if mat and mat.use_nodes:
            for node in mat.node_tree.nodes:
                if node.type == "BSDF_PRINCIPLED":
                    node.inputs["Base Color"].default_value = rgba
        elif mat:
            mat.diffuse_color = rgba


def turret_vert(co):
    return co.z > DECK_Z and abs(co.x) < TURRET_XLIM


def split(src, want_turret):
    """Copy src, keep only the turret (or only the hull) verts, return the new object."""
    dup = src.copy()
    dup.data = src.data.copy()
    bpy.context.collection.objects.link(dup)
    bm = bmesh.new()
    bm.from_mesh(dup.data)
    mw = dup.matrix_world
    dead = [v for v in bm.verts if turret_vert(mw @ v.co) != want_turret]
    bmesh.ops.delete(bm, geom=dead, context="VERTS")
    bm.to_mesh(dup.data)
    bm.free()
    return dup


def ring_centre(src):
    """Centroid (X,Y) of the turret geometry — the vertical axis the turret rotates about."""
    mw = src.matrix_world
    pts = [mw @ v.co for v in src.data.vertices if turret_vert(mw @ v.co)]
    x = sum(p.x for p in pts) / len(pts)
    y = sum(p.y for p in pts) / len(pts)
    return Vector((x, y))


def setup_camera(max_dim):
    cam_data = bpy.data.cameras.new("IsoCam")
    cam_data.type = "ORTHO"
    cam_data.ortho_scale = max_dim * ORTHO_PAD
    cam = bpy.data.objects.new("IsoCam", cam_data)
    bpy.context.collection.objects.link(cam)
    e, a = math.radians(ELEV_DEG), math.radians(AZ_DEG)
    direction = Vector((math.cos(e) * math.sin(a), -math.cos(e) * math.cos(a), math.sin(e)))
    cam.location = direction * max_dim * 4.0          # aimed at the world origin = turret ring
    cam.rotation_euler = (-direction).to_track_quat("-Z", "Y").to_euler()
    bpy.context.scene.camera = cam


def setup_light():
    light_data = bpy.data.lights.new("Sun", "SUN")
    light_data.energy = 3.0
    sun = bpy.data.objects.new("Sun", light_data)
    bpy.context.collection.objects.link(sun)
    sun.rotation_euler = (math.radians(50), 0, math.radians(40))
    world = bpy.context.scene.world
    world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    if bg:
        bg.inputs[1].default_value = 0.6                # soft ambient fill


def main():
    mesh = next(o for o in bpy.data.objects if o.type == "MESH")
    bpy.context.view_layer.update()

    neutralise_body()
    centre = ring_centre(mesh)
    max_dim = max(mesh.dimensions)

    turret = split(mesh, True)
    hull = split(mesh, False)
    mesh.hide_render = True

    # Move the turret ring to the world origin so both layers rotate about it and the camera frames it.
    offset = Vector((-centre.x, -centre.y, 0.0))
    for obj in (hull, turret):
        obj.location = offset

    for obj in list(bpy.data.objects):
        if obj.type in {"CAMERA", "LIGHT"}:
            bpy.data.objects.remove(obj, do_unlink=True)
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
    print(f"RING_CENTRE x={centre.x:.3f} y={centre.y:.3f} MAX_DIM={max_dim:.3f}")


main()
