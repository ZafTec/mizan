"""One procedural badge scene. Run in Blender to render, in Python to package.

blender -b badges.blend --python render_badges.py -- --out ./deliverables
python render_badges.py --postprocess --out ./deliverables --oxipng oxipng
"""

import argparse
import hashlib
import json
import math
from pathlib import Path
import struct
import subprocess
import sys
import time
import uuid

try:
    import bpy
    from mathutils import Vector
except ImportError:
    bpy = None

ROOT = Path(__file__).resolve().parent
PALETTE = {
    "verdigris": "1F6F68", "ink": "1A1815", "cream": "FBFAF7",
    "amber": "8B5F18", "clay": "AA5132", "bronze": "9A6B3F",
    "silver": "A8ABB0", "gold": "C9A227", "platinum": "B9C4CC",
    "pulp": "D6C9AC", "paper": "E9E2D3",
}
MATERIALS = {}
PARTS = []


def catalog():
    source = ROOT / "catalog.json"
    if source.exists():
        return json.loads(source.read_text(encoding="utf-8"))
    if bpy and "catalog.json" in bpy.data.texts:
        return json.loads(bpy.data.texts["catalog.json"].as_string())
    raise FileNotFoundError(source)


def filename(row):
    return f"{'level' if row['category'] == 'level' else 'achievement'}-{row['key']}.png"


def linear(hex_color):
    channels = [int(hex_color[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    return tuple(c / 12.92 if c <= .04045 else ((c + .055) / 1.055) ** 2.4 for c in channels)


def material(name):
    if name in MATERIALS:
        return MATERIALS[name]
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    shader = nodes.get("Principled BSDF")
    base = linear(PALETTE[name])
    shader.inputs["Roughness"].default_value = .82
    shader.inputs["Metallic"].default_value = 0
    shader.inputs["Specular IOR Level"].default_value = .16
    shader.inputs["Sheen Weight"].default_value = .12
    coords = nodes.new("ShaderNodeTexCoord")
    # Object coordinates retain grain scale across every part, after joining.
    grain = nodes.new("ShaderNodeTexNoise")
    grain.inputs["Scale"].default_value = 185
    grain.inputs["Detail"].default_value = 2
    grain.inputs["Roughness"].default_value = .75
    mat.node_tree.links.new(coords.outputs["Object"], grain.inputs["Vector"])
    color = nodes.new("ShaderNodeValToRGB")
    color.color_ramp.elements[0].position = .17
    color.color_ramp.elements[0].color = tuple(c * .65 for c in base) + (1,)
    color.color_ramp.elements[1].position = .82
    color.color_ramp.elements[1].color = tuple(min(.95, c * 1.14 + .012) for c in base) + (1,)
    mat.node_tree.links.new(grain.outputs["Fac"], color.inputs["Fac"])
    # Stretched fine noise models the random pulp fibers catching the key light.
    stretch = nodes.new("ShaderNodeVectorMath")
    stretch.operation = "MULTIPLY"
    stretch.inputs[1].default_value = (1.1, 13, 2.3)
    mat.node_tree.links.new(coords.outputs["Object"], stretch.inputs[0])
    fibers = nodes.new("ShaderNodeTexNoise")
    fibers.inputs["Scale"].default_value = 95
    fibers.inputs["Detail"].default_value = 2
    mat.node_tree.links.new(stretch.outputs["Vector"], fibers.inputs["Vector"])
    bump = nodes.new("ShaderNodeBump")
    bump.inputs["Strength"].default_value = .42
    bump.inputs["Distance"].default_value = .013
    mat.node_tree.links.new(fibers.outputs["Fac"], bump.inputs["Height"])
    wrinkles = nodes.new("ShaderNodeTexNoise")
    wrinkles.inputs["Scale"].default_value = 21
    wrinkles.inputs["Detail"].default_value = 3
    mat.node_tree.links.new(coords.outputs["Object"], wrinkles.inputs["Vector"])
    broad_bump = nodes.new("ShaderNodeBump")
    broad_bump.inputs["Strength"].default_value = .24
    broad_bump.inputs["Distance"].default_value = .009
    mat.node_tree.links.new(wrinkles.outputs["Fac"], broad_bump.inputs["Height"])
    mat.node_tree.links.new(bump.outputs["Normal"], broad_bump.inputs["Normal"])
    mat.node_tree.links.new(broad_bump.outputs["Normal"], shader.inputs["Normal"])
    tooth = nodes.new("ShaderNodeMapRange")
    tooth.inputs["To Min"].default_value = .72
    tooth.inputs["To Max"].default_value = .92
    mat.node_tree.links.new(grain.outputs["Fac"], tooth.inputs["Value"])
    mat.node_tree.links.new(tooth.outputs["Result"], shader.inputs["Roughness"])
    ao = nodes.new("ShaderNodeAmbientOcclusion")
    mat.node_tree.links.new(color.outputs["Color"], ao.inputs["Color"])
    ao.inputs["Distance"].default_value = .11
    ao.samples = 16
    mat.node_tree.links.new(ao.outputs["Color"], shader.inputs["Base Color"])
    MATERIALS[name] = mat
    return mat


def finish(obj, name, color, bevel=.004, smooth=False):
    obj.name = name
    obj.data.materials.append(material(color))
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if bevel:
        mod = obj.modifiers.new("Crisp edge", "BEVEL")
        mod.width = bevel
        mod.segments = 2
    if smooth:
        for face in obj.data.polygons:
            face.use_smooth = True
    else:
        mod = obj.modifiers.new("Face normals", "WEIGHTED_NORMAL")
        mod.keep_sharp = True
    PARTS.append(obj)
    return obj


def sheet(name, verts, faces, color, thickness=.012, reverse="pulp"):
    """Thin paper shell, dyed face, warm uncoated cut edges and reverse."""
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.data.materials.append(material(color))
    obj.data.materials.append(material(reverse))
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    mod = obj.modifiers.new("Paper stock thickness", "SOLIDIFY")
    mod.thickness = thickness
    mod.offset = 0
    mod.material_offset = 1
    mod.material_offset_rim = 1
    mod = obj.modifiers.new("Pressed edge", "BEVEL")
    mod.width = .0025
    mod.segments = 2
    PARTS.append(obj)
    return obj


def folded_panel(name, corners, color, crease=.018):
    # Triangulated ridge makes a true crease with small imperfect paper planes.
    center = sum((Vector(v) for v in corners), Vector()) / len(corners)
    normal = (Vector(corners[1])-Vector(corners[0])).cross(Vector(corners[2])-Vector(corners[0])).normalized()
    center += normal * crease
    verts = list(corners) + [center]
    return sheet(name, verts, [(i,(i+1)%len(corners),len(corners)) for i in range(len(corners))], color)


def paper_box(name, center, size, color):
    x,y,z = center
    w,d,h = (v/2 for v in size)
    # Six folded faces, an overlapping triangular flap on the top, no soft corners.
    verts=[(x-w,y-d,z-h),(x+w,y-d,z-h),(x+w,y+d,z-h),(x-w,y+d,z-h),
           (x-w,y-d,z+h),(x+w,y-d,z+h),(x+w,y+d,z+h),(x-w,y+d,z+h),
           (x+.02,y-.025,z+h+.016)]
    faces=[(0,3,2,1),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7),
           (4,5,8),(5,6,8),(6,7,8),(7,4,8)]
    obj=sheet(name,verts,faces,color)
    folded_panel(name+" tucked seam",[(x-w*.96,y-d*.96,z+h+.017),
                 (x+w*.93,y-d*.96,z+h+.017),(x+.025,y+.015,z+h+.036)],color,.002)
    return obj


def paper_tube(name,start,end,radius,color,sides=8):
    direction=Vector(end)-Vector(start)
    length=direction.length
    rot=direction.to_track_quat("Z","Y")
    verts=[]
    for h in (0,length):
        for i in range(sides):
            angle=i*math.tau/sides
            verts.append(Vector(start)+rot@Vector((radius*math.cos(angle),radius*math.sin(angle),h)))
    faces=[(i,(i+1)%sides,sides+(i+1)%sides,sides+i) for i in range(sides)]
    obj=sheet(name,verts,faces,color,.014)
    # An overlap running the length of the rolled sheet visibly breaks the surface.
    a=Vector(start)+rot@Vector((radius*1.016,0,0))
    b=Vector(end)+rot@Vector((radius*1.016,0,0))
    c=Vector(end)+rot@Vector((radius*1.006,radius*.22,0))
    d=Vector(start)+rot@Vector((radius*1.006,radius*.22,0))
    sheet(name+" overlap",[a,b,c,d],[(0,1,2,3)],color,.008)
    return obj


def ring_shell(name,center,profile,color,sides=8,phase=math.pi/8):
    """Faceted folded vessel or rolled cardboard shell, not a smooth lathe."""
    x,y,z=center
    verts=[]
    for j,(r,h) in enumerate(profile):
        for i in range(sides):
            theta=i*math.tau/sides+phase
            wobble=.007*math.sin(i*3.71+j*.97)
            verts.append((x+r*math.cos(theta),y+r*math.sin(theta),z+h+wobble))
    faces=[]
    for j in range(len(profile)-1):
        for i in range(sides):
            ni=(i+1)%sides
            a,b=j*sides+i,j*sides+ni
            c,d=(j+1)*sides+ni,(j+1)*sides+i
            faces.extend(((a,b,c),(a,c,d)))
    return sheet(name,verts,faces,color,.013)


def vessel(name,color,radius=.84,height=.48,sides=8):
    ring_shell(name,(0,0,0),[(.001,.035),(.43*radius,.035),
               (.69*radius,height*.30),(radius,height),(.96*radius,height+.025)],color,sides)
    # Contrasting inner reverse and triangular folded corners remain visible at 128.
    ring_shell(name+" inner reverse",(0,0,0),[(.001,.048),(.43*radius,.048),
               (.69*radius,height*.30+.016),(.96*radius,height-.012)],"paper",sides)
    for i in range(0,sides,2):
        t=i*math.tau/sides+math.pi/8
        t2=t+math.tau/sides
        folded_panel("Corner tuck",[(radius*math.cos(t),radius*math.sin(t),height+.03),
                     (radius*math.cos(t2),radius*math.sin(t2),height+.03),
                     (.66*radius*math.cos(t+.3),.66*radius*math.sin(t+.3),height*.51)],color,.014)


def leaf(name,center,size,color,angle=0):
    x,y,z=center
    length,width=size
    local=[(-length/2,0,0),(0,-width/2,.035),(length/2,0,.03),(0,width/2,.035),(0,0,.16)]
    verts=[(x+a*math.cos(angle)-b*math.sin(angle),y+a*math.sin(angle)+b*math.cos(angle),z+c) for a,b,c in local]
    return sheet(name,verts,[(0,1,4),(1,2,4),(2,3,4),(3,0,4)],color,.012)


def spoon():
    paper_tube("Rolled spoon handle",(.31,.08,.18),(.54,.10,1.08),.042,"paper",6)
    folded_panel("Folded spoon cup",[(.40,.04,1.12),(.49,.06,1.42),(.68,.10,1.34),(.69,.08,1.09)],"paper",.065)


def nutrition(row):
    color,motif=row["pigment"],row["motif"]
    if motif=="empty-bowl":
        vessel("Shallow folded bowl",color,.85,.28)
    elif motif=="plate":
        vessel("Wide folded plate",color,1.12,.055,12)
        before=len(PARTS)
        vessel("Bowl on plate",color,.56,.56)
        for obj in PARTS[before:]:
            obj.location.z=.105
    elif motif in ("filled-bowl","harvest-bowl"):
        count=2 if motif=="filled-bowl" else 3
        for i in range(count):
            before=len(PARTS)
            vessel(f"Stacked folded bowl {i+1}",color,.84-i*.075,.46)
            for obj in PARTS[before:]:
                obj.location.z=i*.43
    elif motif=="pot":
        ring_shell("Tall folded pot",(0,0,0),[(.001,.02),(.56,.02),(.74,.20),(.77,.85),(.69,.91)],color,8)
        ring_shell("Pot lid",(0,0,.86),[(.001,.22),(.40,.19),(.80,.02)],"paper",8)
        paper_box("Lid folded handle",(0,0,1.12),(.28,.15,.18),color)
        for sign in (-1,1):
            corners=[(sign*.71,-.14,.71),(sign*1.06,-.14,.75),
                     (sign*1.06,.13,.75),(sign*.71,.13,.71)]
            folded_panel("Pot wing handle",corners,color,.055)
    else:
        vessel("Folded bowl",color,.83,.51)
        if motif=="recipe-bowl":
            spoon()


def steps(row):
    count=row["tier"]
    total,height,depth=((1.35,.34,.62),(1.72,.65,.64),(1.90,1.06,.66),
                        (2.20,1.40,.70),(2.65,.83,.76))[count-1]
    # An open paper strip: every peak is a fold, with genuine air between pleats.
    path=[(-total/2+i*total/(2*count),height if i%2 else 0)
          for i in range(2*count+1)]
    upright=count==5
    def point(x,y,z):
        return (z-height/2,y,x+total/2) if upright else (x,y,z)
    verts=[point(x,y,z) for x,z in path for y in (-depth/2,depth/2)]
    faces=[(2*i,2*i+2,2*i+3,2*i+1) for i in range(len(path)-1)]
    sheet(f"{count}-fold paper accordion",verts,faces,row["pigment"],.021)
    if count>=4:
        # A narrow folded paper belt and its tucked end bind the later tiers.
        width=height if upright else total
        z=total*.48 if upright else height*.30
        half=width/2+.025
        d=depth/2+.018
        band=.17
        verts=[(x,y,h) for h in (z-band/2,z+band/2)
               for x,y in ((-half,-d),(half,-d),(half,d),(-half,d))]
        sheet("Paper binding band",verts,[(0,1,5,4),(1,2,6,5),
              (2,3,7,6),(3,0,4,7)],"paper",.016)
        folded_panel("Binding tucked end",[(half-.13,-d-.012,z-band/2),
                     (half+.11,-d-.012,z-band/2),
                     (half+.11,-d-.012,z+band/2),(half-.13,-d-.012,z+band/2)],"paper",.008)


def paper_weight(name,x,z,radius,depth,color,sides=10,y=0):
    # Octagonal cardboard weight with a thick folded perimeter and radial pleats.
    profile=[(.09,-depth/2),(.72*radius,-depth/2),
             (radius,-depth/2+.018),(radius,depth/2-.018),
             (.72*radius,depth/2),(.09,depth/2)]
    before=len(PARTS)
    ring_shell(name,(0,0,0),profile,color,sides)
    for obj in PARTS[before:]:
        obj.rotation_euler.y=math.pi/2
        obj.location=(x,y,z)


def barbell(row):
    color,motif=row["pigment"],row["motif"]
    if motif=="plate-stack":
        for i in range(3):
            ring_shell("Rolled program stack",(0,0,.12+i*.21),[(.08,-.085),(.64-i*.05,-.085),
                       (.64-i*.05,.085),(.08,.085)],color,10)
        paper_tube("Upright rolled bar",(0,0,.12),(0,0,1.22),.075,"paper")
        paper_tube("Cross grip",(-.30,0,1.13),(.30,0,1.13),.08,"paper")
        return
    if motif=="dumbbell":
        # Upright short dumbbell differs from every horizontal bar in grayscale.
        paper_tube("Short rolled grip",(0,0,.20),(0,0,1.30),.10,"paper")
        for sign in (-1,1):
            h=.75+sign*.48
            ring_shell("Hexagonal dumbbell",(0,0,h),[(.09,-.15),(.39,-.15),
                       (.39,.15),(.09,.15)],color,6)
        return
    if motif=="rack":
        for sign in (-1,1):
            paper_tube("Rack upright",(sign*.61,0,.08),(sign*.61,0,1.68),.095,color,4)
            paper_tube("Rack foot",(sign*.61,-.35,.08),(sign*.61,.39,.08),.10,color,4)
        paper_tube("Rack top",(-.61,0,1.58),(.61,0,1.58),.10,color,4)
        paper_tube("Racked bar",(-1.01,-.07,.94),(1.01,-.07,.94),.065,"paper")
        for sign in (-1,1):
            paper_weight("Racked plate",sign*.80,.94,.29,.10,color,10,-.07)
        return
    if motif=="raised-bar":
        z=.48
        paper_tube("Trap grip",(-1.05,0,z),(1.05,0,z),.10,"paper")
        for sign in (-1,1):
            paper_weight("Trap plate",sign*.83,z,.28,.12,color)
            paper_tube("Trap diagonal",(sign*.72,0,z),(sign*.35,-.52,.15),.115,color,4)
        paper_tube("Trap front",(-.35,-.52,.15),(.35,-.52,.15),.115,color,4)
        return
    z=.69 if motif=="large-plate-bar" else .48
    paper_tube("Long rolled bar",(-1.14,0,z),(1.14,0,z),.12 if motif=="barbell" else .066,"paper")
    for sign in (-1,1):
        if motif=="large-plate-bar":
            paper_weight("Large broad disc",sign*.83,z,.66,.19,color,12)
        elif motif=="loaded-bar":
            # Broad deep pack, with open grip slots distinct from a simple bar.
            paper_weight("Deep plate pack",sign*.74,z,.43,.41,color,8)
            paper_weight("Pack outer crease",sign*.98,z,.30,.10,color,8)
        else:
            paper_weight("Single modest plate",sign*.79,z,.29,.085,color,10)


def tape(row):
    color,tier=row["pigment"],row["tier"]
    # Open rolled ribbon reveals a real spiral instead of a solid toy spool.
    points=128
    verts=[]
    for i in range(points):
        t=i/(points-1)
        angle=t*math.tau*2.2
        radius=.13+.32*t
        for y in (-.17,.17):
            verts.append((-.39+radius*math.cos(angle),y,.48+radius*math.sin(angle)))
    sheet("Paper measuring ribbon",verts,[(2*i,2*i+1,2*i+3,2*i+2) for i in range(points-1)],color,.012)
    length=.79 if tier==1 else 1.30
    path=[(-.32,.045),(-.05,.043),(length-.42,.043)]
    if tier==2:
        path += [(length-.36,.12),(length-.36,.57)]
    verts=[(x,y,z) for x,z in path for y in (-.17,.17)]
    sheet("Unrolled measure",verts,[(2*i,2*i+2,2*i+3,2*i+1) for i in range(len(path)-1)],"paper",.015)
    for i in range(3 if tier==1 else 6):
        x=-.08+i*.15
        sheet("Printed measure",[(x,-.17,.056),(x+.025,-.17,.056),
              (x+.025,-.01 if i%2 else .06,.056),(x,-.01 if i%2 else .06,.056)],[(0,1,2,3)],"ink",.001)


def scale(row):
    tier,color=row["tier"],row["pigment"]
    paper_box("Folded scale",(0,0,.15),(1.34,1.06,.28),color)
    if tier==1:
        folded_panel("Paper readout",[(-.24,.06,.312),(.24,.06,.312),(.24,.32,.312),(-.24,.32,.312)],"paper",.003)
    else:
        h=.34 if tier==2 else .70
        w=.52 if tier==2 else .83
        paper_box("Folded scale display",(0,.29,.30+h/2),(w,.16,h),color)
        folded_panel("Scale display face",[(-w*.37,.198,.30+h*.26),(w*.37,.198,.30+h*.26),
                     (w*.37,.198,.30+h*.78),(-w*.37,.198,.30+h*.78)],"paper",.003)
    for i in range(tier):
        leaf("Measurement crease",((i-(tier-1)/2)*.26,-.28,.31),(.46,.17),"verdigris",math.pi/2)


def person(x,y,z,color,size=1):
    # Origami human: two folded coat planes, lapel ridge, diamond head and neck.
    s=size
    sheet("Folded person",[(x-.24*s,y,z),(x+.24*s,y,z),(x+.20*s,y,z+.53*s),
          (x,y-.11*s,z+.72*s),(x-.20*s,y,z+.53*s),(x,y-.16*s,z+.31*s)],
          [(0,1,5),(1,2,5),(2,3,5),(3,4,5),(4,0,5)],color,.019)
    sheet("Back coat fold",[(x-.24*s,y,z),(x-.17*s,y+.16*s,z+.56*s),
          (x,y+.09*s,z+.70*s),(x+.17*s,y+.16*s,z+.56*s),(x+.24*s,y,z)],[(0,1,2),(0,2,4),(2,3,4)],color,.018)
    paper_tube("Paper neck",(x,y,z+.67*s),(x,y,z+.80*s),.047*s,"paper",4)
    verts=[(x,y-.06*s,z+1.14*s),(x-.17*s,y-.06*s,z+.96*s),
           (x,y-.06*s,z+.77*s),(x+.17*s,y-.06*s,z+.96*s),(x,y-.14*s,z+.97*s)]
    sheet("Folded diamond head",verts,[(0,1,4),(1,2,4),(2,3,4),(3,0,4)],color,.022)


def people(row):
    motif,tier,color=row["motif"],row["tier"],row["pigment"]
    if motif=="people-follow":
        person(-.36,0,0,color)
        person(.38,.23,.12 if tier==1 else .30,"paper",.63 if tier==1 else 1)
        if tier==2:
            sheet("Joined folded ribbon",[(-.36,0,.08),(-.30,-.2,.04),(.30,-.2,.32),(.38,.23,.38)],[(0,1,2),(0,2,3)],color,.016)
    elif motif=="people-share":
        person(-.47,0,0,color,.86)
        person(.47,0,.05 if tier==1 else .19,"paper",.86)
        z=.42 if tier==1 else .79
        paper_tube("Shared training bar",(-.37,-.19,z),(.37,-.19,z),.061,"paper",6)
        for sign in (-1,1):
            paper_weight("Shared folded disc",sign*.20,z,.15 if tier==1 else .22,.085,color,8,-.19)
        if tier==2:
            paper_tube("Lifting left sleeve",(-.47,-.05,.45),(-.18,-.19,z),.071,color,4)
            paper_tube("Lifting right sleeve",(.47,-.05,.64),(.18,-.19,z),.071,"paper",4)
    elif motif=="people-highfive":
        person(-.41,0,0,color,.89)
        person(.41,0,0,"paper",.89)
        paper_tube("Raised left sleeve",(-.36,-.02,.53),(0,0,1.31),.075,color,4)
        paper_tube("Raised right sleeve",(.36,-.02,.53),(0,0,1.31),.075,"paper",4)
    else:
        person(-.37,0,0,color,.86)
        person(.40,.07,0,"paper",.86)
        folded_panel("Dialogue folded card",[(-.35,-.05,1.16),(.36,-.05,1.16),
                     (.36,-.05,1.48),(-.35,-.05,1.48)],color,.07)
        sheet("Dialogue tail",[(.12,-.05,1.16),(.12,-.05,1.02),(-.04,-.05,1.16)],[(0,1,2)],color,.012)


def level(row):
    tier,color=row["tier"],row["pigment"]
    for i in range(tier):
        x=(i-(tier-1)/2)*.39
        h=.85+i*.19
        # Standing folded bookmark with open sides and a visible paper reverse.
        sheet("Level folded tally",[(x-.15,0,0),(x, -.15,0),(x+.15,0,0),
              (x-.15,0,h),(x,-.15,h+.06),(x+.15,0,h)],[(0,1,4,3),(1,2,5,4)],color,.019)
        sheet("Tally folded tip",[(x-.15,0,h),(x,-.15,h+.06),(x+.15,0,h),
              (x,.11,h-.12)],[(0,1,3),(1,2,3)],color,.017)


def build(row):
    PARTS.clear()
    for obj in list(bpy.data.objects):
        if obj.type == "MESH":
            bpy.data.objects.remove(obj, do_unlink=True)
    builders = {"empty-bowl": nutrition, "plate": nutrition, "filled-bowl": nutrition,
                "harvest-bowl": nutrition, "recipe-bowl": nutrition, "pot": nutrition, "steps": steps,
                "dumbbell": barbell, "barbell": barbell, "loaded-bar": barbell,
                "large-plate-bar": barbell, "rack": barbell, "raised-bar": barbell,
                "plate-stack": barbell, "tape": tape, "scale": scale,
                "people-share": people, "people-follow": people,
                "people-highfive": people, "people-talk": people, "level": level}
    builders[row["motif"]](row)
    bpy.ops.object.select_all(action="DESELECT")
    for obj in PARTS:
        bpy.context.view_layer.objects.active = obj
        obj.select_set(True)
        for mod in list(obj.modifiers):
            bpy.ops.object.modifier_apply(modifier=mod.name)
        obj.select_set(False)
    for obj in PARTS:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = PARTS[0]
    bpy.ops.object.join()
    obj = bpy.context.object
    obj.name = filename(row).removesuffix(".png")
    obj["key"] = row["key"]
    obj["motif"] = row["motif"]
    obj["tier"] = row["tier"]
    frame(obj, row)
    # Avoid accumulating unused meshes in a long catalog render.
    for mesh in list(bpy.data.meshes):
        if mesh.users == 0:
            bpy.data.meshes.remove(mesh)
    return obj


def frame(obj, row):
    camera = bpy.context.scene.camera
    yaw, elevation = math.radians(35), math.radians(30)
    direction = Vector((math.sin(yaw) * math.cos(elevation), -math.cos(yaw) * math.cos(elevation), math.sin(elevation)))
    rotation = (-direction).to_track_quat("-Z", "Y")
    inverse = rotation.inverted()
    verts = [inverse @ (obj.matrix_world @ v.co) for v in obj.data.vertices]
    lows = Vector(tuple(min(v[i] for v in verts) for i in range(3)))
    highs = Vector(tuple(max(v[i] for v in verts) for i in range(3)))
    target = rotation @ ((lows + highs) / 2)
    camera.rotation_euler = rotation.to_euler()
    camera.location = target + direction * 8
    camera.data.ortho_scale = max(highs.x - lows.x, highs.y - lows.y) / .80
    for name, offset in (("Soft key", (-3.0, -4.5, 5)), ("Weak fill", (4, 1, 2.5))):
        light = bpy.data.objects[name]
        light.location = target + Vector(offset)
        light.rotation_euler = (target - light.location).to_track_quat("-Z", "Y").to_euler()


def setup(samples):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    MATERIALS.clear()
    scene = bpy.context.scene
    scene.name = "Mizan badges"
    scene.render.engine = "CYCLES"
    prefs = bpy.context.preferences.addons["cycles"].preferences
    prefs.compute_device_type = "OPTIX"
    prefs.get_devices()
    gpu_names = []
    for device in prefs.devices:
        device.use = device.type == "OPTIX"
        if device.use:
            gpu_names.append(device.name)
    if not gpu_names:
        raise RuntimeError("An OptiX GPU is required; no CPU fallback is selected.")
    scene.cycles.device = "GPU"
    scene.cycles.samples = samples
    scene.cycles.use_denoising = True
    scene.cycles.denoiser = "OPTIX"
    scene.cycles.adaptive_threshold = .012
    scene.cycles.max_bounces = 6
    scene.render.film_transparent = True
    scene.render.resolution_x = scene.render.resolution_y = 512
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.image_settings.color_depth = "8"
    scene.render.image_settings.compression = 80
    scene.view_settings.view_transform = "Standard"
    scene.view_settings.look = "None"
    scene.view_settings.exposure = -.18
    scene.view_settings.gamma = 1
    scene.render.dither_intensity = 0
    world = bpy.data.worlds.new("Soft ambient")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (*linear("FBFAF7"), 1)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = .21
    scene.world = world
    camera_data = bpy.data.cameras.new("Shared orthographic camera")
    camera_data.type = "ORTHO"
    camera = bpy.data.objects.new("Shared orthographic camera", camera_data)
    scene.collection.objects.link(camera)
    scene.camera = camera
    for name, energy, size in (("Soft key", 620, 2.6), ("Weak fill", 115, 3.2)):
        data = bpy.data.lights.new(name, "AREA")
        data.energy = energy
        data.shape = "DISK"
        data.size = size
        obj = bpy.data.objects.new(name, data)
        scene.collection.objects.link(obj)
    scene["device"] = ", ".join(gpu_names)
    scene["camera_elevation"] = 30
    scene["camera_yaw"] = 35
    return scene


def save_blend(path, rows):
    for name, content in (("render_badges.py", Path(__file__).read_text(encoding="utf-8")),
                          ("catalog.json", json.dumps(rows, indent=2, ensure_ascii=False))):
        text = bpy.data.texts.get(name) or bpy.data.texts.new(name)
        text.clear()
        text.write(content)
    bpy.context.scene["catalog_rows"] = len(rows)
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(path.resolve()), compress=True)


def render(args, rows):
    scene = setup(args.samples)
    selected = [row for row in rows if not args.keys or row["key"] in args.keys.split(",")]
    args.out.mkdir(parents=True, exist_ok=True)
    reports = []
    start = time.perf_counter()
    for row in selected:
        build(row)
        if args.prepare_only:
            break
        scene.render.filepath = str((args.out / filename(row)).resolve())
        scene.render.image_settings.color_mode = "RGBA"
        mark = time.perf_counter()
        bpy.ops.render.render(write_still=True)
        reports.append({"key": row["key"], "seconds": round(time.perf_counter() - mark, 3)})
        print(f"BADGE {row['key']} {reports[-1]['seconds']}s", flush=True)
    if args.save_blend:
        # Open the blend on a representative bowl, retaining exactly one scene/object.
        build(rows[2])
        save_blend(args.save_blend, rows)
    if not args.prepare_only:
        report_path = args.out / "render-report.json"
        if args.keys and report_path.exists():
            previous = json.loads(report_path.read_text(encoding="utf-8"))
            updated_keys = {r["key"] for r in reports}
            reports = [r for r in previous["renders"] if r["key"] not in updated_keys] + reports
        report_path.write_text(json.dumps({"blender": bpy.app.version_string,
            "device": scene["device"], "samples": args.samples,
            "seconds": round(sum(r["seconds"] for r in reports), 3),
            "renders": reports}, indent=2), encoding="utf-8")


def silhouette_geometry(args, rows):
    """Project the evaluated mesh only; this mode never starts a Cycles render."""
    setup(args.samples)
    args.out.mkdir(parents=True, exist_ok=True)
    glyphs=[]
    for row in rows:
        obj=build(row)
        if args.glb:
            export_glb(obj,row,args.glb)
        bpy.context.view_layer.update()
        camera=bpy.context.scene.camera
        matrix=camera.matrix_world.inverted() @ obj.matrix_world
        scale=camera.data.ortho_scale
        points=[matrix @ v.co for v in obj.data.vertices]
        projected=[(round(512*(.5+v.x/scale),4),round(512*(.5-v.y/scale),4)) for v in points]
        obj.data.calc_loop_triangles()
        glyphs.append({**row,"file":filename(row),"points":projected,
                       "triangles":[list(t.vertices) for t in obj.data.loop_triangles],
                       "coverage":round(max(max(p[i] for p in projected)-min(p[i] for p in projected)
                                            for i in (0,1))/512,6)})
    (args.out/"silhouette-geometry.json").write_text(json.dumps(glyphs,separators=(",",":")),encoding="utf-8")
    if args.save_blend:
        build(rows[2])
        save_blend(args.save_blend,rows)
    print(json.dumps({"silhouettes":len(glyphs),"coverage":[[g["key"],g["coverage"]] for g in glyphs]}),flush=True)


def export_glb(obj,row,output):
    """Export the same mesh/camera, with unbaked PBR paper and Draco compression.

    glTF has no procedural noise/bump or area-light shader representation. Keep
    their exact Blender settings as scene metadata and retain the source shader.
    """
    output.mkdir(parents=True,exist_ok=True)
    scene=bpy.context.scene
    original=list(obj.data.materials)
    substitutes=[]
    metadata={"proceduralPaper":{
        "fiberNoiseScale":95,"fiberStretch":[1.1,13,2.3],"fiberBumpStrength":.42,
        "fiberBumpDistance":.013,"grainNoiseScale":185,"wrinkleNoiseScale":21,
        "wrinkleBumpStrength":.24,"wrinkleBumpDistance":.009,
        "roughnessRange":[.72,.92],"aoDistance":.11},
        "blenderAreaLights":[{"name":name,"energy":bpy.data.objects[name].data.energy,
             "size":bpy.data.objects[name].data.size,"shape":"DISK",
             "position":list(bpy.data.objects[name].location),
             "rotationEuler":list(bpy.data.objects[name].rotation_euler)}
             for name in ("Soft key","Weak fill")],
        "ambient":{"color":"FBFAF7","strength":.21},
        "viewTransform":"Standard","exposure":-.18}
    prior=scene.get("origamiSource")
    scene["origamiSource"]=metadata
    try:
        for index,source in enumerate(original):
            mat=bpy.data.materials.new(source.name+" GLB paper")
            mat.use_nodes=True
            shader=mat.node_tree.nodes.get("Principled BSDF")
            shader.inputs["Base Color"].default_value=(*linear(PALETTE[source.name]),1)
            shader.inputs["Roughness"].default_value=.82
            shader.inputs["Metallic"].default_value=0
            shader.inputs["Specular IOR Level"].default_value=.16
            shader.inputs["Sheen Weight"].default_value=.12
            obj.data.materials[index]=mat
            substitutes.append(mat)
        bpy.ops.object.select_all(action="DESELECT")
        obj.select_set(True)
        scene.camera.select_set(True)
        path=output/(filename(row).removesuffix(".png")+".glb")
        bpy.ops.export_scene.gltf(filepath=str(path.resolve()),export_format="GLB",
             use_selection=True,export_cameras=True,export_lights=False,
             export_extras=True,export_animations=False,export_texcoords=False,
             export_normals=True,export_materials="EXPORT",
             export_draco_mesh_compression_enable=True,export_draco_mesh_compression_level=6,
             export_draco_position_quantization=14,export_draco_normal_quantization=10)
        if path.stat().st_size>=80000:
            raise ValueError(f"GLB exceeds 80 KB: {path} ({path.stat().st_size})")
        data=path.read_bytes()
        json_length,chunk_type=struct.unpack_from("<II",data,12)
        doc=json.loads(data[20:20+json_length])
        if doc.get("textures") or doc.get("images"):
            raise ValueError(f"Unexpected baked texture in {path}")
        print(f"GLB {row['key']} {len(data)} bytes",flush=True)
    finally:
        for index,source in enumerate(original):
            obj.data.materials[index]=source
        for mat in substitutes:
            bpy.data.materials.remove(mat)
        if prior is None:
            del scene["origamiSource"]
        else:
            scene["origamiSource"]=prior
        scene.camera.select_set(False)


def export_all_glbs(args,rows):
    setup(args.samples)
    for row in rows:
        if not args.keys or row["key"] in args.keys.split(","):
            export_glb(build(row),row,args.glb)


def silhouette_preview(args):
    """Antialiased geometry proof on both papers, without color or illumination."""
    from PIL import Image,ImageDraw,ImageFont
    glyphs=json.loads((args.out/"silhouette-geometry.json").read_text(encoding="utf-8"))
    font_path=args.font or Path("C:/Windows/Fonts/arial.ttf")
    font=ImageFont.truetype(str(font_path),14)
    small=ImageFont.truetype(str(font_path),12)
    title=ImageFont.truetype(str(font_path),25)
    changed=set(args.keys.split(",")) if args.keys else {g["key"] for g in glyphs}
    sheet=Image.new("RGB",(1080,132+47*len(glyphs)),"#FBFAF7")
    draw=ImageDraw.Draw(sheet)
    draw.rectangle((540,0,1080,sheet.height),fill="#14161A")
    masks=[]
    for glyph in glyphs:
        mask=Image.new("L",(1024,1024),0)
        md=ImageDraw.Draw(mask)
        points=[(x*2,y*2) for x,y in glyph["points"]]
        for face in glyph["triangles"]:
            md.polygon([points[i] for i in face],fill=255)
        mask=mask.resize((512,512),Image.Resampling.LANCZOS)
        mask.save(args.out/glyph["file"])
        masks.append(mask)
    for offset,ink,muted,rule in ((0,"#1A1815","#55514A","#E3DFD6"),
                                 (540,"#E9E2D3","#C0BCB4","#2A2E34")):
        draw.text((offset+24,22),"Mizan / Silhouette review",font=title,fill=ink)
        draw.text((offset+24,62),"28 px · projected geometry · 80% frame",font=font,fill=muted)
        draw.text((offset+24,87),"Outlined names: revised glyphs · no full render yet",font=small,fill=muted)
        for i,(glyph,mask) in enumerate(zip(glyphs,masks)):
            top=122+i*47
            mark=Image.new("RGB",(28,28),ink)
            sheet.paste(mark,(offset+24,top+5),mask.resize((28,28),Image.Resampling.LANCZOS))
            draw.text((offset+76,top+7),glyph["name"],font=font,fill=ink)
            if glyph["key"] in changed:
                draw.rectangle((offset+68,top+1,offset+508,top+34),outline=muted,width=1)
            draw.line((offset+24,top+46,offset+516,top+46),fill=rule)
    sheet.save(args.out/"silhouette-sheet.png",optimize=True)
    # A second view enlarges only the requested edits; the first is the 28px gate.
    selected=[(g,m) for g,m in zip(glyphs,masks) if g["key"] in changed]
    detail=Image.new("RGB",(1080,116+len(selected)*98),"#FBFAF7")
    dd=ImageDraw.Draw(detail)
    dd.rectangle((540,0,1080,detail.height),fill="#14161A")
    for offset,ink,muted in ((0,"#1A1815","#55514A"),(540,"#E9E2D3","#C0BCB4")):
        dd.text((offset+24,22),"Revised outlines / 28 + 84 px",font=title,fill=ink)
        dd.text((offset+24,62),"Fold counts and stacked bowls · geometry only",font=font,fill=muted)
        for i,(glyph,mask) in enumerate(selected):
            top=106+i*98
            for size,x in ((28,24),(84,68)):
                detail.paste(Image.new("RGB",(size,size),ink),(offset+x,top+(84-size)//2),
                             mask.resize((size,size),Image.Resampling.LANCZOS))
            dd.text((offset+176,top+24),glyph["name"],font=font,fill=ink)
            dd.text((offset+176,top+46),glyph["key"],font=small,fill=muted)
    detail.save(args.out/"silhouette-detail.png",optimize=True)


def png_chunks(path):
    data = path.read_bytes()
    offset, chunks = 8, []
    while offset < len(data):
        length = struct.unpack_from(">I", data, offset)[0]
        chunks.append(data[offset + 4:offset + 8].decode("ascii"))
        offset += length + 12
    return chunks


def optimize(path, executable):
    subprocess.run([executable, "-o4", "--strip", "all", "--nc", "--nb", str(path)],
                   check=True, capture_output=True, text=True)


def postprocess(args, rows):
    """Package the raw Blender renders in --out into the app's served badge folder.

    The raw 512 px renders stay in --out and are not committed. The optimized
    512 px and 128 px files are written once, under --assets (the frontend's
    public/badges), which is the only committed copy. Review sheets and the
    manifest stay in --out.
    """
    from PIL import Image, ImageDraw, ImageFont
    output = args.out
    assets = args.assets
    (assets / "512").mkdir(parents=True, exist_ok=True)
    (assets / "128").mkdir(parents=True, exist_ok=True)
    manifest = []
    prior_manifest = output / "manifest.json"
    prior = {Path(r["file"]).name: r for r in json.loads(prior_manifest.read_text(encoding="utf-8"))["assets"]} if prior_manifest.exists() else {}
    selected=set(args.keys.split(",")) if args.keys else None
    for row in rows:
        raw = output / filename(row)
        path = assets / "512" / filename(row)
        preview = assets / "128" / filename(row)
        if selected is not None and row["key"] not in selected:
            previous=prior.get(path.name)
            if previous is None:
                raise ValueError(f"Missing manifest entry for unchanged badge: {path}")
            if previous["sha256"] != hashlib.sha256(path.read_bytes()).hexdigest():
                raise ValueError(f"Unchanged badge was modified: {path}")
            manifest.append(previous)
            continue
        if not raw.exists():
            if args.keys:
                continue
            raise FileNotFoundError(raw)
        previous = prior.get(path.name, {})
        same_source = previous.get("sha256") == hashlib.sha256(raw.read_bytes()).hexdigest()
        # Pillow resizes RGBA through premultiplied channels, then returns straight
        # alpha. This avoids dark fringes on paper at both smaller sizes.
        im = Image.open(raw).convert("RGBA")
        if im.size != (512, 512):
            raise ValueError(f"Unexpected resolution: {raw}")
        # Standard view highlights may clip a cream edge. Keep the export inside
        # the product's near-white range, including antialiased boundary colors.
        r, g, b, alpha = im.split()
        near_white = lambda c: min(254, max(1, c))
        im = Image.merge("RGBA", (r.point(near_white), g.point(near_white), b.point(near_white), alpha))
        small = im.resize((128, 128), Image.Resampling.LANCZOS)
        sr, sg, sb, sa = small.split()
        small = Image.merge("RGBA", (sr.point(near_white), sg.point(near_white), sb.point(near_white), sa))
        im.save(path, optimize=True, compress_level=9)
        small.save(preview, optimize=True, compress_level=9)
        optimize(path, args.oxipng)
        # Coarsen RGB by at most a few code values when lossless optimization
        # cannot meet the budget. Thousands of shades and full alpha are retained.
        # No indexed PNG or baked background is exported.
        quantized = previous.get("quantizedColors") if same_source else None
        channel_step = previous.get("rgbChannelStep", 1) if same_source else 1
        if path.stat().st_size >= 60000:
            for step in (2, 3, 4, 5, 6, 8, 10):
                lut = [min(254, max(1, round(c / step) * step)) for c in range(256)]
                reduced = Image.merge("RGBA", (r.point(lut), g.point(lut), b.point(lut), alpha))
                reduced.save(path, optimize=True, compress_level=9)
                optimize(path, args.oxipng)
                if path.stat().st_size < 60000:
                    channel_step = step
                    break
        if path.stat().st_size >= 60000:
            for colors in (192, 128, 96):
                im.quantize(colors=colors, method=Image.Quantize.FASTOCTREE,
                            dither=Image.Dither.NONE).convert("RGBA").save(path, optimize=True)
                optimize(path, args.oxipng)
                if path.stat().st_size < 60000:
                    quantized = colors
                    break
        optimize(preview, args.oxipng)
        for candidate, expected in ((path, (512, 512)), (preview, (128, 128))):
            check = Image.open(candidate)
            if check.mode != "RGBA" or check.size != expected or candidate.stat().st_size >= 60000:
                raise ValueError(f"PNG contract failed: {candidate}")
            if set(png_chunks(candidate)) - {"IHDR", "IDAT", "IEND"}:
                raise ValueError(f"Metadata remains: {candidate}")
        bbox = Image.open(path).getchannel("A").getbbox()
        if not bbox or min(bbox[:2]) < 20 or max(bbox[2:]) > 492:
            raise ValueError(f"Badge clipped or empty: {path} {bbox}")
        ident = None if row["category"] == "level" else (
            f"ac000000-0000-0000-0000-{row['seed']:012}" if "seed" in row else
            str(uuid.UUID(hashlib.md5(f"mizan-achievement:{row['key']}".encode()).hexdigest())))
        manifest.append({**row, "id": ident, "file": f"512/{path.name}", "preview": f"128/{path.name}",
                         "dimensions": [512, 512], "previewDimensions": [128, 128], "quantizedColors": quantized,
                         "rgbChannelStep": channel_step,
                         "bytes": path.stat().st_size, "previewBytes": preview.stat().st_size,
                         "alphaBounds": bbox, "sha256": hashlib.sha256(path.read_bytes()).hexdigest()})
    # Both earned and CSS-equivalent locked states at real size, beside 14px labels.
    font_path = args.font or Path("C:/Windows/Fonts/arial.ttf")
    font = ImageFont.truetype(str(font_path), 14)
    title_font = ImageFont.truetype(str(font_path), 25)
    small_font = ImageFont.truetype(str(font_path), 12)
    width, row_height = 1080, 47
    height = 155 + row_height * len(manifest)
    sheet = Image.new("RGB", (width, height), "#FBFAF7")
    draw = ImageDraw.Draw(sheet)
    draw.rectangle((width // 2, 0, width, height), fill="#14161A")
    for offset, paper, ink, muted, rule in ((0, "#FBFAF7", "#1A1815", "#55514A", "#E3DFD6"),
                                         (540, "#14161A", "#F2F0EB", "#C0BCB4", "#2A2E34")):
        draw.text((offset + 24, 23), "Mizan / Achievement glyphs", font=title_font, fill=ink)
        draw.text((offset + 24, 64), f"{len(manifest)} glyphs · 28 px · {paper} paper", font=font, fill=muted)
        draw.text((offset + 24, 94), "Earned / still to earn", font=small_font, fill=muted)
        draw.text((offset + 116, 123), "Achievement / level", font=small_font, fill=muted)
        for i, item in enumerate(manifest):
            top = 151 + i * row_height
            im = Image.open(assets / item["file"]).convert("RGBA").resize((28, 28), Image.Resampling.LANCZOS)
            sheet.paste(im, (offset + 24, top + 7), im)
            locked = im.convert("L").convert("RGBA")
            locked.putalpha(im.getchannel("A").point(lambda a: round(a * .5)))
            sheet.paste(locked, (offset + 72, top + 7), locked)
            draw.text((offset + 116, top + 6), item["name"], font=font, fill=ink)
            draw.text((offset + 116, top + 24), f"{item['category']} / {item['key']}", font=small_font, fill=muted)
            draw.line((offset + 24, top + row_height - 1, offset + 516, top + row_height - 1), fill=rule)
    sheet.save(output / "contact-sheet.png", optimize=True)
    # A pure silhouette proof makes shape collisions visible without pigment or
    # illumination helping. All marks use identical ink and retain actual sizing.
    proof = Image.new("RGB", (540, 108 + row_height * len(manifest)), "#FBFAF7")
    proof_draw = ImageDraw.Draw(proof)
    proof_draw.text((24, 22), "Mizan / Shape proof", font=title_font, fill="#1A1815")
    proof_draw.text((24, 62), "28 px · one ink · alpha silhouette", font=font, fill="#55514A")
    for i,item in enumerate(manifest):
        top=100+i*row_height
        mark=Image.open(assets/item["file"]).convert("RGBA").resize((28,28),Image.Resampling.LANCZOS)
        mask=Image.new("RGBA",(28,28),"#55514A")
        mask.putalpha(mark.getchannel("A"))
        proof.paste(mask,(24,top+5),mask)
        proof_draw.text((76,top+7),item["name"],font=font,fill="#1A1815")
        proof_draw.line((24,top+row_height-1,516,top+row_height-1),fill="#E3DFD6")
    proof.save(output/"silhouette-sheet.png",optimize=True)
    # A large sheet assists inspecting edge quality without changing the real-size proof.
    columns, cell = 8, 160
    detail = Image.new("RGB", (columns * cell, math.ceil(len(manifest) / columns) * 190), "#FBFAF7")
    detail_draw = ImageDraw.Draw(detail)
    for i, item in enumerate(manifest):
        x, y = (i % columns) * cell, (i // columns) * 190
        im = Image.open(assets / item["preview"])
        detail.paste(im, (x + 16, y + 8), im)
        detail_draw.text((x + 9, y + 142), item["key"], font=small_font, fill="#55514A")
    detail.save(output / "detail-sheet.png", optimize=True)
    (output / "manifest.json").write_text(json.dumps({"style": "hand-folded textured paper origami",
        "colorSpace": "sRGB", "alpha": "straight",
        "encoding": "PNG RGBA 8-bit; metadata stripped", "assets": manifest}, indent=2, ensure_ascii=False), encoding="utf-8")
    print(json.dumps({"assets": len(manifest), "largest512": max(m["bytes"] for m in manifest),
                      "largest128": max(m["previewBytes"] for m in manifest)}, indent=2))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--out", type=Path, default=ROOT / "deliverables",
                        help="Raw renders, review sheets and the manifest; raw renders are not committed")
    parser.add_argument("--assets", type=Path, default=ROOT.parents[1] / "frontend" / "public" / "badges",
                        help="Where the packaged 512/ and 128/ PNGs go; the app serves this folder")
    parser.add_argument("--samples", type=int, default=256)
    parser.add_argument("--keys", help="Comma-separated subset for design proofs")
    parser.add_argument("--save-blend", type=Path)
    parser.add_argument("--prepare-only", action="store_true")
    parser.add_argument("--silhouette-geometry", action="store_true",help="Blender: project meshes without rendering")
    parser.add_argument("--silhouette-preview", action="store_true",help="Python: draw the projected silhouette review sheet")
    parser.add_argument("--glb",type=Path,help="Export texture-free Draco GLBs to this folder; no render")
    parser.add_argument("--postprocess", action="store_true")
    parser.add_argument("--oxipng", default="oxipng")
    parser.add_argument("--font", type=Path, help="Archivo TTF for contact-sheet labels")
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
    args = parser.parse_args(argv)
    rows = catalog()
    if args.silhouette_preview:
        silhouette_preview(args)
    elif args.silhouette_geometry and bpy:
        silhouette_geometry(args,rows)
    elif args.glb and bpy:
        export_all_glbs(args,rows)
    elif args.postprocess:
        postprocess(args, rows)
    elif bpy:
        render(args, rows)
    else:
        parser.error("Rendering requires Blender; packaging requires --postprocess.")


if __name__ == "__main__":
    main()
