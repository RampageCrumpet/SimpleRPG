"""Build a measured, polygonal skeleton from BodyParts3D bone and cartilage surfaces.

Run from Blender's scripting workspace or with ``blender --background --python``.
The input directory contains the individually named OBJ reference surfaces.  Use
``--refs PATH`` after ``--`` to choose another directory.  The script also
downloads the selected references when that directory is empty.

The source OBJ vertices are measured in millimetres.  This script works on
their polygon surfaces, reduces each structure's topology individually, articulates
the arms into a neutral T pose, and stores the source ID and dimensions on
each result.  No Blender mesh primitives are used.
"""

from __future__ import annotations

import argparse
import csv
import json
import math
import os
from pathlib import Path
import re
import sys
import tempfile
from urllib.parse import quote
from urllib.request import Request, urlopen

import bpy
import bmesh
from mathutils import Matrix, Vector


PROJECT = Path(__file__).resolve().parents[2]
SCRIPT_DIR = Path(__file__).resolve().parent
SOURCE_REPO = "https://github.com/olivercase/body_parts_3d_api"
SOURCE_API = "https://api.github.com/repos/olivercase/body_parts_3d_api/git/trees/main?recursive=1"
SOURCE_MEDIA = "https://media.githubusercontent.com/media/olivercase/body_parts_3d_api/main/"
ATTRIBUTION = "BodyParts3D, © The Database Center for Life Science licensed under CC Attribution 4.0 International"
TARGET_HEIGHT_M = 1.78
MAX_TRIANGLES = 40_000
TEXTURE_DIR = PROJECT / "Assets" / "Art" / "Textures" / "Bone"
COSTAL_CARTILAGE_IDS = {
    "FJ3239", "FJ3242", "FJ3245", "FJ3248", "FJ3251", "FJ3254", "FJ3255",
    "FJ3333", "FJ3335", "FJ3337", "FJ3339", "FJ3341", "FJ3343", "FJ3345",
    "FJ3667", "FJ3667M", "FJ3668", "FJ3668M", "FJ3669", "FJ3669M",
}


def args() -> argparse.Namespace:
    after_dash = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--refs", type=Path, default=Path(tempfile.gettempdir()) / "SimpleRPG_BoneReferences" / "selected")
    parser.add_argument("--blend", type=Path, default=SCRIPT_DIR / "AnatomicalHumanSkeleton.blend")
    parser.add_argument("--fbx", type=Path, default=PROJECT / "Assets" / "Art" / "3d Models" / "Characters" / "Undead" / "AnatomicalHumanSkeleton.fbx")
    return parser.parse_args(after_dash)


def selected_id(path: str) -> bool:
    match = re.search(r"/FJ(\d+)", path)
    if not match:
        return False
    number = int(match.group(1))
    name = Path(path).name
    if name.split("_", 1)[0] in COSTAL_CARTILAGE_IDS:
        return True
    if 3152 <= number <= 3394:
        return not re.search(r"Intervertebral disk|cartilage|meniscus|ligament|muscle|fascia|nerve|artery|vein|gingiva|tendon", name, re.I)
    return 1254 <= number <= 1281 or number in {6309, 6383, 6412, 6470, 6486}


def ensure_references(directory: Path) -> list[Path]:
    directory.mkdir(parents=True, exist_ok=True)
    files = sorted(directory.glob("FJ*.obj"))
    available = {path.name.split("_", 1)[0] for path in files}
    if len(files) >= 250 and COSTAL_CARTILAGE_IDS <= available:
        return files
    req = Request(SOURCE_API, headers={"User-Agent": "SimpleRPG-skeleton-builder"})
    with urlopen(req, timeout=45) as response:
        tree = json.load(response)["tree"]
    paths = [item["path"] for item in tree if item["path"].startswith("meshes/") and selected_id(item["path"])]
    for index, relative in enumerate(paths, 1):
        destination = directory / Path(relative).name
        if destination.exists() and destination.stat().st_size > 100:
            continue
        url = SOURCE_MEDIA + quote(relative, safe="/")
        with urlopen(Request(url, headers={"User-Agent": "SimpleRPG-skeleton-builder"}), timeout=45) as response:
            destination.write_bytes(response.read())
        if index % 25 == 0:
            print(f"Downloaded {index}/{len(paths)} bone references")
    return sorted(directory.glob("FJ*.obj"))


def read_obj(path: Path):
    vertices: list[Vector] = []
    faces: list[tuple[int, ...]] = []
    name = path.stem.split("_", 3)[-1]
    source_id = path.name.split("_", 1)[0]
    with path.open("r", encoding="utf-8") as stream:
        for line in stream:
            if line.startswith("# English name :"):
                name = line.split(":", 1)[1].strip()
            elif line.startswith("v "):
                _, x, y, z, *_ = line.split()
                vertices.append(Vector((float(x), float(y), float(z))))
            elif line.startswith("f "):
                faces.append(tuple(int(part.split("/")[0]) - 1 for part in line.split()[1:]))
    if not vertices or not faces:
        raise ValueError(f"Missing geometry in {path}")
    return {"name": name, "id": source_id, "vertices": vertices, "faces": faces, "path": path}


def group(name: str) -> str:
    n = name.lower()
    if "tooth" in n or any(word in n for word in (
        "frontal bone", "parietal bone", "occipital bone", "temporal bone", "maxilla", "mandible",
        "sphenoid", "ethmoid", "vomer", "palatine bone", "nasal bone", "lacrimal bone", "zygomatic bone",
        "inferior nasal concha")):
        return "01 Skull and Jaw"
    if any(word in n for word in ("vertebra", "atlas", "axis", "sacrum", "rib", "costal cartilage", "sternum", "manubrium", "xiphoid", "hyoid")):
        return "02 Spine and Ribcage"
    if any(word in n for word in ("clavicle", "scapula")):
        return "03 Shoulder Girdle"
    if any(word in n for word in ("humerus", "radius", "ulna", "metacarpal", "finger", "thumb",
                                      "capitate", "hamate", "lunate", "pisiform", "scaphoid", "trapezium",
                                      "trapezoid", "triquetral")):
        return "04 Arms and Hands"
    return "05 Pelvis Legs and Feet"


def arm_side(name: str) -> int:
    n = name.lower()
    if group(name) != "04 Arms and Hands":
        return 0
    if "left" in n:
        return 1
    if "right" in n:
        return -1
    return 0


def face_budget(name: str, count: int) -> int:
    n = name.lower()
    if "tooth" in n:
        budget = 70
    elif "frontal bone" in n:
        # The orbital roof is much thinner than the vault and must survive the
        # initial reduction before the skull is fused into one game mesh.
        budget = 1600
    elif any(x in n for x in ("parietal bone", "occipital bone")):
        budget = 1000
    elif "mandible" in n:
        budget = 900
    elif "maxilla" in n:
        budget = 1200
    elif "sphenoid" in n:
        budget = 1000
    elif "ethmoid" in n:
        budget = 700
    elif any(x in n for x in ("lacrimal bone", "palatine bone")):
        budget = 260
    elif "zygomatic bone" in n:
        budget = 420
    elif "temporal bone" in n:
        budget = 420
    elif group(name) == "01 Skull and Jaw":
        budget = 140
    elif "costal cartilage" in n:
        budget = 70
    elif "rib" in n:
        budget = 125
    elif "cervical vertebra" in n:
        budget = 700
    elif n in {"atlas", "axis"}:
        budget = 500
    elif "vertebra" in n:
        budget = 195
    elif "sacrum" in n:
        budget = 550
    elif any(x in n for x in ("sternum", "manubrium", "xiphoid")):
        budget = 260
    elif "scapula" in n:
        budget = 500
    elif "hip bone" in n:
        budget = 680
    elif "femur" in n or "humerus" in n:
        budget = 450
    elif any(x in n for x in ("tibia", "fibula", "radius", "ulna", "clavicle")):
        budget = 340
    elif "patella" in n:
        budget = 110
    elif "metacarpal" in n or "metatarsal" in n:
        budget = 110
    elif "phalanx" in n:
        budget = 30
    else:
        budget = 120
    return min(count, budget)


def shoulder_pivots(records):
    pivots = {}
    for side, side_word in ((-1, "Right"), (1, "Left")):
        humerus = next(record for record in records if record["name"] == f"{side_word} humerus")
        points = humerus["vertices"]
        top = sorted(points, key=lambda point: point.z, reverse=True)[: max(20, len(points) // 50)]
        pivots[side] = sum(top, Vector()) / len(top)
    return pivots


def tooth_alveolar_crests(records):
    """Find local jaw crests near each tooth in the measured source surfaces."""
    by_name = {record["name"]: record for record in records}
    crests = {}
    for tooth in (record for record in records if "tooth" in record["name"].lower()):
        name = tooth["name"]
        lower = "lower" in name.lower()
        if lower:
            jaw = by_name["Mandible"]
        else:
            side = "Left" if name.startswith("Left") else "Right"
            jaw = by_name[f"{side} maxilla"]
        points = tooth["vertices"]
        cx = sum(point.x for point in points) / len(points)
        cy = sum(point.y for point in points) / len(points)
        nearby = sorted(point.z for point in jaw["vertices"]
                        if (point.x - cx) ** 2 + (point.y - cy) ** 2 < 36)
        if len(nearby) < 10:
            raise RuntimeError(f"Cannot locate alveolar crest for {name}")
        index = max(1, len(nearby) // 10)
        crests[name] = nearby[-index] if lower else nearby[index]
    return crests


def transform_vertex(point: Vector, side: int, pivots, scale: float, min_z: float) -> Vector:
    p = point.copy()
    if side:
        pivot = pivots[side]
        angle = math.radians(77 * (-side))
        rotation = Matrix.Rotation(angle, 3, "Y")
        p = pivot + rotation @ (p - pivot)
    return Vector((p.x * scale, p.y * scale, (p.z - min_z) * scale))


def make_mesh(record, collection, pivots, scale, min_z, tooth_crest_mm=None):
    side = arm_side(record["name"])
    points = [transform_vertex(p, side, pivots, scale, min_z) for p in record["vertices"]]
    center = sum(points, Vector()) / len(points)
    local = [point - center for point in points]
    safe_name = record["name"].replace(" ", "_")
    mesh = bpy.data.meshes.new(safe_name + "_LowPoly")
    mesh.from_pydata(local, [], record["faces"])
    # BodyParts3D OBJ files duplicate many coincident vertices at normal seams.
    # Weld those seams before simplifying, so each bone remains one continuous
    # polygon shell instead of a set of visually touching triangle islands.
    welded = bmesh.new()
    welded.from_mesh(mesh)
    bmesh.ops.remove_doubles(welded, verts=welded.verts, dist=0.00001)
    welded.to_mesh(mesh)
    welded.free()
    if tooth_crest_mm is not None:
        # Keep the scanned crown plus 1 mm below the alveolar rim. Hidden roots
        # consume polygon budget without contributing to the game silhouette.
        lower = "lower" in record["name"].lower()
        crest_world_z = (tooth_crest_mm - min_z) * scale
        plane_z = crest_world_z + (-.001 if lower else .001) - center.z
        cropped = bmesh.new()
        cropped.from_mesh(mesh)
        bmesh.ops.bisect_plane(
            cropped, geom=list(cropped.verts) + list(cropped.edges) + list(cropped.faces),
            dist=.00001, plane_co=Vector((0, 0, plane_z)),
            plane_no=Vector((0, 0, 1)), clear_inner=lower, clear_outer=not lower,
        )
        cut_points = [vertex.co + center for vertex in cropped.verts
                      if vertex.is_boundary and abs(vertex.co.z - plane_z) < .00002]
        if len(cut_points) < 3:
            raise RuntimeError(f"No crown contour for {record['name']}")
        record["socket_contour_xy"] = [(point.x, point.y) for point in cut_points]
        bmesh.ops.holes_fill(cropped, edges=[edge for edge in cropped.edges if edge.is_boundary], sides=0)
        bmesh.ops.recalc_face_normals(cropped, faces=cropped.faces)
        cropped.to_mesh(mesh)
        cropped.free()
    mesh.validate(verbose=False)
    mesh.update()
    obj = bpy.data.objects.new(safe_name, mesh)
    obj.location = center
    collection.objects.link(obj)
    obj["source_fj_id"] = record["id"]
    obj["anatomical_name"] = record["name"]
    obj["reference_units"] = "millimetres"
    obj["source_model"] = "BodyParts3D 4.3"
    obj["source_license_url"] = "https://dbarchive.biosciencedbc.jp/en/bodyparts3d/lic.html"
    obj["modeling_process"] = "individual anatomical polygon surface adaptation and topology reduction"
    obj["raw_triangle_count"] = len(record["faces"])
    if tooth_crest_mm is not None:
        obj["alveolar_crest_reference_mm"] = round(tooth_crest_mm, 3)
        obj["modeling_process"] = "scanned anatomical tooth crown, cropped at measured alveolar rim and polygon reduced"
    for polygon in mesh.polygons:
        polygon.use_smooth = True
    budget = face_budget(record["name"], len(mesh.polygons))
    if len(mesh.polygons) > budget * 1.05:
        modifier = obj.modifiers.new("BonePolygonReduction", "DECIMATE")
        modifier.decimate_type = "COLLAPSE"
        modifier.ratio = max(.01, budget / len(mesh.polygons))
        bpy.context.view_layer.objects.active = obj
        obj.select_set(True)
        bpy.ops.object.modifier_apply(modifier=modifier.name)
        obj.select_set(False)
    # The supplied 8th-10th cartilage scans have open edge chains. Rebuild a
    # closed polygon shell from their reduced anatomical surfaces, then return
    # to a small game mesh; this is not a new tube or primitive approximation.
    cartilage_name = record["name"].lower()
    if "costal cartilage" in cartilage_name and any(ordinal in cartilage_name for ordinal in ("eighth", "ninth", "tenth")):
        bpy.context.view_layer.objects.active = obj
        obj.select_set(True)
        obj.data.remesh_voxel_size = .0015
        bpy.ops.object.voxel_remesh()
        triangle_count = sum(len(poly.vertices) - 2 for poly in obj.data.polygons)
        modifier = obj.modifiers.new("CartilagePolygonReduction", "DECIMATE")
        modifier.ratio = min(1.0, 80 / triangle_count)
        bpy.ops.object.modifier_apply(modifier=modifier.name)
        obj.select_set(False)
    # C3-C6 carry thin bifid spinous processes. Reduction makes their posterior
    # tips read as blunt planks, so taper the existing scan topology there.
    if "cervical vertebra" in record["name"].lower() and "Seventh" not in record["name"]:
        y_values = [vertex.co.y for vertex in obj.data.vertices]
        y_min, y_max = min(y_values), max(y_values)
        start = y_min + .62 * (y_max - y_min)
        for vertex in obj.data.vertices:
            if vertex.co.y > start:
                t = (vertex.co.y - start) / (y_max - start)
                vertex.co.x *= 1 - .35 * t * t
                vertex.co.z *= 1 - .12 * t * t
                vertex.co.y = start + (vertex.co.y - start) * (1 - .10 * t * t)
    cleanup = bmesh.new()
    cleanup.from_mesh(obj.data)
    loose_edges = [edge for edge in cleanup.edges if not edge.link_faces]
    if loose_edges:
        bmesh.ops.delete(cleanup, geom=loose_edges, context="EDGES")
    loose_vertices = [vertex for vertex in cleanup.verts if not vertex.link_edges]
    if loose_vertices:
        bmesh.ops.delete(cleanup, geom=loose_vertices, context="VERTS")
    bmesh.ops.recalc_face_normals(cleanup, faces=cleanup.faces)
    cleanup.to_mesh(obj.data)
    cleanup.free()
    obj.data.update()
    if "costal cartilage" in cartilage_name and any(ordinal in cartilage_name for ordinal in ("eighth", "ninth", "tenth")):
        check = bmesh.new()
        check.from_mesh(obj.data)
        open_edges = sum(not edge.is_manifold for edge in check.edges)
        check.free()
        if open_edges:
            raise RuntimeError(f"Open cartilage mesh after remesh: {record['name']} ({open_edges} edges)")
    obj["final_triangle_count"] = sum(len(poly.vertices) - 2 for poly in obj.data.polygons)
    return obj


def make_coccyx(sacrum, collection):
    # Coccyx is absent from the reference set: extend a custom tapered polygon cage
    # from the measured inferior end of the sacrum, with anterior curvature.
    bottom = min((sacrum.matrix_world @ vertex.co for vertex in sacrum.data.vertices), key=lambda point: point.z)
    rings = [(0.0, .013), (-.012, .011), (-.025, .008), (-.038, .004)]
    vertices = []
    for index, (dz, radius) in enumerate(rings):
        for segment in range(9):
            angle = 2 * math.pi * segment / 9
            vertices.append((bottom.x + radius * math.cos(angle), bottom.y - index * .003 + radius * math.sin(angle) * .7, bottom.z + dz))
    faces = []
    for ring in range(len(rings) - 1):
        for segment in range(9):
            a = ring * 9 + segment
            b = ring * 9 + (segment + 1) % 9
            faces.append((a, b, b + 9, a + 9))
    faces += [tuple(reversed(range(9))), tuple(27 + index for index in range(9))]
    mesh = bpy.data.meshes.new("Coccyx_QuadCage")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new("Coccyx", mesh)
    collection.objects.link(obj)
    obj["anatomical_name"] = "Coccyx"
    obj["modeling_process"] = "custom quad cage extruded from measured sacral apex"
    return obj


def convex_hull_xy(points):
    """Keep the actual crown's outer contour, without fitting a generic oval."""
    points = sorted(set((round(x, 7), round(y, 7)) for x, y in points))
    def cross(a, b, c):
        return (b[0] - a[0]) * (c[1] - a[1]) - (b[1] - a[1]) * (c[0] - a[0])
    lower = []
    for point in points:
        while len(lower) >= 2 and cross(lower[-2], lower[-1], point) <= 0:
            lower.pop()
        lower.append(point)
    upper = []
    for point in reversed(points):
        while len(upper) >= 2 and cross(upper[-2], upper[-1], point) <= 0:
            upper.pop()
        upper.append(point)
    return lower[:-1] + upper[:-1]


def contour_samples(points, count=10):
    hull = convex_hull_xy(points)
    if len(hull) < 3:
        raise RuntimeError("Tooth contour has fewer than three points")
    lengths = [math.dist(hull[i], hull[(i + 1) % len(hull)]) for i in range(len(hull))]
    perimeter = sum(lengths)
    sampled = []
    for sample in range(count):
        distance = sample * perimeter / count
        for i, length in enumerate(lengths):
            if distance <= length or i == len(lengths) - 1:
                a, b = hull[i], hull[(i + 1) % len(hull)]
                t = distance / length if length else 0
                sampled.append((a[0] * (1 - t) + b[0] * t,
                                a[1] * (1 - t) + b[1] * t))
                break
            distance -= length
    return sampled


def make_socket_rim(tooth, crest_mm, scale, min_z, arch_center_y, collection):
    """Loft a closed alveolar lip around the measured tooth-neck polygon."""
    contour = contour_samples(tooth["socket_contour_xy"])
    cx = sum(point[0] for point in contour) / len(contour)
    cy = sum(point[1] for point in contour) / len(contour)
    lower = "lower" in tooth["name"].lower()
    direction = 1 if lower else -1
    crest_z = (crest_mm - min_z) * scale
    # The rim rises between adjacent crowns and dips across their labial face.
    # This measured-arch tangent produces an anatomical scallop, not a flat
    # washer around each tooth. The outer edge sinks into the jaw surface.
    radial_x, radial_y = cx, cy - arch_center_y
    radial_length = math.hypot(radial_x, radial_y)
    tangent_x, tangent_y = -radial_y / radial_length, radial_x / radial_length
    rings = ((.00015, .00015, .0015),
             (.00065, .00005, .0014),
             (.0014, -.0013, .0003))
    vertices = []
    for radial_offset, height, side_rise in rings:
        for x, y in contour:
            dx, dy = x - cx, y - cy
            radius = math.hypot(dx, dy)
            side = min(1, abs(dx * tangent_x + dy * tangent_y) / radius)
            vertices.append((x + radial_offset * dx / radius,
                             y + radial_offset * dy / radius,
                             crest_z + direction * (height + side_rise * side * side)))
    count = len(contour)
    faces = []
    for ring in range(3):
        next_ring = (ring + 1) % 3
        for i in range(count):
            j = (i + 1) % count
            faces.append((ring * count + i, ring * count + j,
                          next_ring * count + j, next_ring * count + i))
    mesh = bpy.data.meshes.new("Alveolar_Socket_Rim_LowPoly")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    name = "Alveolar_socket_" + tooth["name"].replace(" ", "_")
    obj = bpy.data.objects.new(name, mesh)
    collection.objects.link(obj)
    obj["anatomical_name"] = "Socket rim of " + tooth["name"]
    obj["derived_from_fj_id"] = tooth["id"]
    obj["modeling_process"] = "closed polygonal alveolar rim lofted from measured tooth-neck contour"
    for polygon in mesh.polygons:
        polygon.use_smooth = True
    return obj


def build_pbr_materials():
    """Use external game texture maps; keep enamel smoother than porous bone."""
    paths = {name: TEXTURE_DIR / f"Bone_{name}.png"
             for name in ("BaseColor", "Normal", "Roughness")}
    missing = [str(path) for path in paths.values() if not path.is_file()]
    if missing:
        raise FileNotFoundError(f"Missing bone PBR maps: {missing}")
    bone = bpy.data.materials.get("Bone_PBR") or bpy.data.materials.new("Bone_PBR")
    bone.use_nodes = True
    bone.diffuse_color = (.78, .70, .57, 1)
    bone.node_tree.nodes.clear()
    nodes = bone.node_tree.nodes
    links = bone.node_tree.links
    output = nodes.new("ShaderNodeOutputMaterial")
    output.location = (600, 0)
    shader = nodes.new("ShaderNodeBsdfPrincipled")
    shader.location = (350, 0)
    shader.inputs["Metallic"].default_value = 0
    links.new(shader.outputs["BSDF"], output.inputs["Surface"])
    for index, name in enumerate(("BaseColor", "Roughness", "Normal")):
        node = nodes.new("ShaderNodeTexImage")
        node.label = "Bone " + name
        node.location = (-500, 260 - index * 300)
        node.extension = "REPEAT"
        node.image = bpy.data.images.load(str(paths[name]), check_existing=True)
        node.image.filepath = bpy.path.relpath(str(paths[name]))
        if name != "BaseColor":
            node.image.colorspace_settings.name = "Non-Color"
        if name == "BaseColor":
            links.new(node.outputs["Color"], shader.inputs["Base Color"])
        elif name == "Roughness":
            links.new(node.outputs["Color"], shader.inputs["Roughness"])
        else:
            normal = nodes.new("ShaderNodeNormalMap")
            normal.location = (50, -360)
            normal.inputs["Strength"].default_value = .32
            links.new(node.outputs["Color"], normal.inputs["Color"])
            links.new(normal.outputs["Normal"], shader.inputs["Normal"])
    enamel = bpy.data.materials.get("Tooth_Enamel") or bpy.data.materials.new("Tooth_Enamel")
    enamel.use_nodes = True
    enamel.diffuse_color = (.89, .85, .76, 1)
    enamel_shader = next(node for node in enamel.node_tree.nodes if node.type == "BSDF_PRINCIPLED")
    enamel_shader.inputs["Base Color"].default_value = (.89, .85, .76, 1)
    enamel_shader.inputs["Metallic"].default_value = 0
    enamel_shader.inputs["Roughness"].default_value = .34
    return bone, enamel


def assign_world_box_uvs(objects, tile_size=.10):
    """Stable metre-scaled UVs let one repeatable tile cover every bone."""
    for obj in objects:
        mesh = obj.data
        uv = mesh.uv_layers.get("BoneUV") or mesh.uv_layers.new(name="BoneUV")
        normal_matrix = obj.matrix_world.to_3x3()
        for polygon in mesh.polygons:
            normal = normal_matrix @ polygon.normal
            axis = max(range(3), key=lambda index: abs(normal[index]))
            for loop_index in polygon.loop_indices:
                vertex = mesh.vertices[mesh.loops[loop_index].vertex_index]
                point = obj.matrix_world @ vertex.co
                if axis == 0:
                    uv.data[loop_index].uv = (point.y / tile_size, point.z / tile_size)
                elif axis == 1:
                    uv.data[loop_index].uv = (point.x / tile_size, point.z / tile_size)
                else:
                    uv.data[loop_index].uv = (point.x / tile_size, point.y / tile_size)


def set_origin_world(obj, pivot: Vector):
    """Move an object origin to a joint without moving its polygon surface."""
    delta = pivot - obj.location
    for vertex in obj.data.vertices:
        vertex.co -= delta
    obj.location = pivot
    obj.data.update()


def join_anatomical_group(members, name: str, pivot: Vector, union_target: int | None = None,
                         voxel_size: float = .0015):
    """Produce one game mesh from separately measured bone/teeth surfaces."""
    assert members
    source_ids = []
    component_count = 0
    for member in members:
        if member.get("source_fj_ids"):
            source_ids.extend(json.loads(member["source_fj_ids"]))
        elif member.get("source_fj_id"):
            source_ids.append(member["source_fj_id"])
        component_count += int(member.get("component_count", 1))
    bpy.ops.object.select_all(action="DESELECT")
    for member in members:
        member.select_set(True)
    active = next((member for member in members if member.name in {"Frontal_bone", "Cranial_Shell"}), members[0])
    if name == "Mandible_with_Lower_Teeth":
        active = next(member for member in members if member.name.startswith("Mandible"))
    bpy.context.view_layer.objects.active = active
    bpy.ops.object.join()
    combined = bpy.context.object
    if union_target is not None:
        # Fuse cranial and facial bone shells into a single continuous game
        # surface. Teeth are joined afterwards to preserve their individual
        # crowns and spacing.
        combined.data.remesh_voxel_size = voxel_size
        bpy.ops.object.voxel_remesh()
        triangle_count = sum(len(poly.vertices) - 2 for poly in combined.data.polygons)
        modifier = combined.modifiers.new("CranialPolygonReduction", "DECIMATE")
        modifier.ratio = min(1.0, union_target / triangle_count)
        bpy.ops.object.modifier_apply(modifier=modifier.name)
        for polygon in combined.data.polygons:
            polygon.use_smooth = True
    combined.name = name
    combined.data.name = name + "_LowPoly"
    combined["anatomical_name"] = name.replace("_", " ")
    combined["source_fj_ids"] = json.dumps(source_ids)
    combined["component_count"] = component_count
    combined["modeling_process"] = "joined anatomical polygon surfaces for game mesh"
    set_origin_world(combined, pivot)
    return combined


def main():
    options = args()
    paths = ensure_references(options.refs)
    records = [read_obj(path) for path in paths if selected_id("meshes/" + path.name)]
    if len(records) < 250:
        raise RuntimeError(f"Expected at least 250 bone and cartilage reference surfaces; found {len(records)}")
    names = {record["name"] for record in records}
    required = {
        "Frontal bone", "Mandible", "Third cervical vertebra", "Fifth thoracic vertebra",
        "Third lumbar vertebra", "Sacrum", "Left third rib", "Left sixth rib", "Left tenth rib",
        "Manubrium", "Body of sternum", "Xiphoid process", "Left clavicle", "Left scapula",
        "Left first costal cartilage", "Left seventh costal cartilage", "Left tenth costal cartilage",
        "Right first costal cartilage", "Right seventh costal cartilage", "Right tenth costal cartilage",
        "Left humerus", "Left radius", "Left ulna", "Left hip bone", "Left femur",
        "Left patella", "Left tibia", "Left fibula", "Left third metacarpal bone",
        "Left third metatarsal bone", "Proximal phalanx of left middle finger",
        "Middle phalanx of left middle finger", "Distal phalanx of left middle finger",
        "Proximal phalanx of left third toe",
    }
    missing = required - names
    if missing:
        raise RuntimeError(f"Missing required anatomical references: {sorted(missing)}")
    min_z = min(point.z for record in records for point in record["vertices"])
    max_z = max(point.z for record in records for point in record["vertices"])
    scale = TARGET_HEIGHT_M / ((max_z - min_z) / 1000.0) / 1000.0
    pivots = shoulder_pivots(records)
    tooth_crests = tooth_alveolar_crests(records)
    for old_obj in list(bpy.data.objects):
        bpy.data.objects.remove(old_obj, do_unlink=True)
    for old in list(bpy.data.collections):
        if old.name.startswith("Human_Skeleton") or old.name.split(".", 1)[0] in {
            "01 Skull and Jaw", "02 Spine and Ribcage", "03 Shoulder Girdle",
            "04 Arms and Hands", "05 Pelvis Legs and Feet",
        }:
            bpy.data.collections.remove(old)
    root = bpy.data.collections.new("Human_Skeleton_Anatomical_1_78m")
    bpy.context.scene.collection.children.link(root)
    collections = {}
    for label in ("01 Skull and Jaw", "02 Spine and Ribcage", "03 Shoulder Girdle", "04 Arms and Hands", "05 Pelvis Legs and Feet"):
        collection = bpy.data.collections.new(label)
        root.children.link(collection)
        collections[label] = collection
    material, enamel_material = build_pbr_materials()
    objects = []
    for index, record in enumerate(records, 1):
        obj = make_mesh(record, collections[group(record["name"])], pivots, scale, min_z,
                        tooth_crests.get(record["name"]))
        obj.data.materials.append(enamel_material if "tooth" in record["name"].lower() else material)
        objects.append(obj)
        if index % 30 == 0:
            print(f"Modeled {index}/{len(records)} bones")
    measurements = SCRIPT_DIR / "AnatomicalHumanSkeleton_measurements.csv"
    with measurements.open("w", newline="", encoding="utf-8") as stream:
        writer = csv.writer(stream)
        writer.writerow(("bone", "reference_FJ_ID", "source_size_mm_x", "source_size_mm_y",
                         "source_size_mm_z", "final_size_m_x", "final_size_m_y", "final_size_m_z", "triangles"))
        for record, obj in zip(records, objects):
            raw_min = Vector(min(point[i] for point in record["vertices"]) for i in range(3))
            raw_max = Vector(max(point[i] for point in record["vertices"]) for i in range(3))
            world = [obj.matrix_world @ vertex.co for vertex in obj.data.vertices]
            final_min = Vector(min(point[i] for point in world) for i in range(3))
            final_max = Vector(max(point[i] for point in world) for i in range(3))
            writer.writerow((record["name"], record["id"],
                             *(round(value, 3) for value in raw_max - raw_min),
                             *(round(value, 5) for value in final_max - final_min),
                             obj["final_triangle_count"]))
    sacrum = next(obj for obj in objects if obj.name == "Sacrum")
    coccyx = make_coccyx(sacrum, collections["02 Spine and Ribcage"])
    coccyx.data.materials.append(material)
    objects.append(coccyx)
    second_molars = [mol for mol in records if "second lower molar tooth" in mol["name"].lower()]
    arch_center_y = sum(sum(point.y for point in mol["vertices"]) / len(mol["vertices"])
                        for mol in second_molars) / len(second_molars) * scale
    for record in records:
        if record["name"] in tooth_crests:
            socket = make_socket_rim(record, tooth_crests[record["name"]], scale, min_z,
                                     arch_center_y,
                                     collections["01 Skull and Jaw"])
            socket.data.materials.append(material)
            objects.append(socket)
    # Fuse the lofted rims into their alveolar bone rather than leaving visible
    # washer-like rings around crowns. Keep the teeth separate until afterward.
    socket_parts = [obj for obj in objects if obj.name.startswith("Alveolar_socket_")]
    mandible_bone = next(obj for obj in objects if obj.name == "Mandible")
    lower_sockets = [obj for obj in socket_parts if "_lower_" in obj.name]
    maxilla_bones = {side: next(obj for obj in objects if obj.name == f"{side}_maxilla")
                     for side in ("Left", "Right")}
    upper_sockets = {side: [obj for obj in socket_parts
                            if obj.name.startswith(f"Alveolar_socket_{side}_") and "_upper_" in obj.name]
                     for side in ("Left", "Right")}
    untouched = [obj for obj in objects if obj not in socket_parts
                 and obj != mandible_bone and obj not in maxilla_bones.values()]
    fused_jaw = join_anatomical_group([mandible_bone] + lower_sockets,
                                      "Mandible_with_Alveolar_Sockets", Vector((0, 0, 0)),
                                      union_target=1700, voxel_size=.0006)
    fused_maxillae = []
    for side in ("Left", "Right"):
        fused = join_anatomical_group([maxilla_bones[side]] + upper_sockets[side],
                                      f"{side}_maxilla_with_Alveolar_Sockets", Vector((0, 0, 0)),
                                      union_target=1300, voxel_size=.0006)
        fused_maxillae.append(fused)
    objects = untouched + [fused_jaw] + fused_maxillae
    skull_parts = [obj for obj in objects if obj.get("anatomical_name") and group(obj["anatomical_name"]) == "01 Skull and Jaw"]
    jaw_parts = [obj for obj in skull_parts if obj.name.startswith("Mandible") or "lower" in obj["anatomical_name"].lower() and "tooth" in obj["anatomical_name"].lower()]
    upper_parts = [obj for obj in skull_parts if obj not in jaw_parts]
    upper_teeth = [obj for obj in upper_parts if "tooth" in obj["anatomical_name"].lower()]
    maxilla_parts = [obj for obj in upper_parts if "maxilla" in obj["anatomical_name"].lower()]
    cranial_parts = [obj for obj in upper_parts if obj not in upper_teeth and obj not in maxilla_parts]
    atlas = next(obj for obj in objects if obj.name == "Atlas")
    atlas_world = [atlas.matrix_world @ vertex.co for vertex in atlas.data.vertices]
    head_pivot = Vector((0, sum(p.y for p in atlas_world) / len(atlas_world), max(p.z for p in atlas_world) + .008))
    mandible = fused_jaw
    jaw_world = [mandible.matrix_world @ vertex.co for vertex in mandible.data.vertices]
    jaw_pivot = Vector((0, max(p.y for p in jaw_world) - .006, max(p.z for p in jaw_world) - .008))
    remaining = [obj for obj in objects if obj not in skull_parts]
    # Fuse the measured orbital bones and maxillae in the same fine-resolution
    # voxel pass. Merely joining their closed islands left visible gaps between
    # the orbital floor, nasal sidewall and upper alveolar process.
    cranial_shell = join_anatomical_group(cranial_parts + maxilla_parts,
                                          "Cranial_Shell", head_pivot,
                                          union_target=6700, voxel_size=.0008)
    # Crowns remain separate surfaces within the one skull object so the enamel
    # material and the scalloped socket lips survive the cranial union.
    skull = join_anatomical_group([cranial_shell] + upper_teeth,
                                  "Skull_with_Upper_Teeth", head_pivot)
    jaw = join_anatomical_group(jaw_parts, "Mandible_with_Lower_Teeth", jaw_pivot)
    objects = remaining + [skull, jaw]
    anchor = bpy.data.objects.new("SKELETON_ROOT", None)
    root.objects.link(anchor)
    for obj in objects:
        obj.parent = anchor
        obj.matrix_parent_inverse = anchor.matrix_world.inverted()
    assign_world_box_uvs(objects)
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.length_unit = "METERS"
    scene["source_attribution"] = ATTRIBUTION
    scene["source_url"] = SOURCE_REPO
    scene["bone_albedo_source"] = "Image-generated tileable diffuse albedo; periodic technical normal and roughness maps"
    scene["bone_texture_tile_size_m"] = .10
    scene["intended_height_m"] = TARGET_HEIGHT_M
    scene["construction"] = "Measured bone and costal cartilage polygon surfaces, joined skull and jaw, articulated T pose"
    total = sum(sum(len(poly.vertices) - 2 for poly in obj.data.polygons) for obj in objects)
    if total > MAX_TRIANGLES:
        raise RuntimeError(f"Triangle budget exceeded: {total} > {MAX_TRIANGLES}")
    scene["triangle_count"] = total
    scene["reference_count"] = len(records)
    options.blend.parent.mkdir(parents=True, exist_ok=True)
    options.fbx.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    bpy.context.view_layer.objects.active = None
    bpy.ops.wm.save_as_mainfile(filepath=str(options.blend))
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.export_scene.fbx(filepath=str(options.fbx), use_selection=True, object_types={"MESH"}, apply_unit_scale=True, add_leaf_bones=False, use_custom_props=True, path_mode="COPY")
    bpy.ops.object.select_all(action="DESELECT")
    bpy.context.view_layer.objects.active = None
    print(json.dumps({"meshes": len(objects), "triangles": total, "height_m": TARGET_HEIGHT_M,
                      "blend": str(options.blend), "fbx": str(options.fbx),
                      "measurements": str(measurements)}, indent=2))


if __name__ == "__main__":
    main()
