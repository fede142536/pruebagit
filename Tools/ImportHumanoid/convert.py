"""
Convierte el modelo humanoide CC0 (Quaternius, «Universal Base Characters», glTF de 65 huesos) al formato propio del juego:
cuerpo del cuello para abajo, en pose A (brazos colgando), reducido, con pesos sobre los 20 huesos del esqueleto del juego y una región por vértice
(piel, camiseta, short, media, botín) para pintarla con los colores de cada club al cargar. La cabeza, el pelo y la cara los sigue armando el código.

Uso:  python3 convert.py ENTRADA.gltf SALIDA.bytes [--ply carpeta]   (necesita numpy, scipy y fast-simplification)
"""
import struct, sys, os
import numpy as np
from scipy.spatial import cKDTree
import fast_simplification
import gltf_load

# ---- esqueleto del juego (mismo orden que SkinnedBodyGeometry.BodyBone) ------------------------------------------------
MY_BONES = ['Root', 'Pelvis', 'Spine', 'Chest', 'Neck', 'Head', 'Shoulder_L', 'Elbow_L', 'Wrist_L', 'Shoulder_R', 'Elbow_R', 'Wrist_R',
            'Hip_L', 'Knee_L', 'Ankle_L', 'Toe_L', 'Hip_R', 'Knee_R', 'Ankle_R', 'Toe_R']
# hueso del modelo -> hueso del juego
def my_bone(name):
    n = name.lower()
    side = 'L' if n.endswith('_l') or '_l_' in n else ('R' if n.endswith('_r') or '_r_' in n else '')
    table = {'root': 'Root', 'pelvis': 'Pelvis', 'spine_01': 'Spine', 'spine_02': 'Spine', 'spine_03': 'Chest', 'neck_01': 'Neck', 'head': 'Head'}
    if n in table:
        return table[n]
    if n.startswith('clavicle'):
        return 'Chest'
    if n.startswith('upperarm'):
        return 'Shoulder_' + side
    if n.startswith('lowerarm'):
        return 'Elbow_' + side
    if n.startswith('hand') or any(n.startswith(f) for f in ('index', 'middle', 'pinky', 'ring', 'thumb')):
        return 'Wrist_' + side
    if n.startswith('thigh'):
        return 'Hip_' + side
    if n.startswith('calf'):
        return 'Knee_' + side
    if n.startswith('foot'):
        return 'Ankle_' + side
    if n.startswith('ball'):
        return 'Toe_' + side
    raise ValueError(n)

ARM_ANGLE = 78.0          # grados que bajan los brazos desde la T hasta la pose A
HEAD_CUT = 1.545          # el modelo se corta aquí: la cabeza la arma el juego
LOD0_TRIS, LOD1_TRIS = 1900, 420
REGION = {'skin': 0, 'shirt': 1, 'shorts': 2, 'sock': 3, 'stripe': 4, 'boot': 5}
HEM, SHORT_HEM, SOCK_TOP, STRIPE_LO, STRIPE_HI, SLEEVE_END, NECKLINE, BOOT_TOP = 0.99, 0.74, 0.42, 0.355, 0.385, 1.28, 1.505, 0.085


def rot_z(deg):
    a = np.radians(deg); c, s = np.cos(a), np.sin(a)
    return np.array([[c, -s, 0], [s, c, 0], [0, 0, 1]])


def repose(d, body):
    """Baja los brazos (T → A) con skinning lineal sobre los 65 huesos del modelo; devuelve posiciones nuevas y el reposo de los huesos."""
    names, rest = d['names'], d['rest'].copy()
    pos = body['pos'].copy()
    out = np.zeros_like(pos)
    mats = []   # por hueso: (R, pivote) o None
    for n in names:
        if n.startswith(('upperarm', 'lowerarm', 'hand', 'index', 'middle', 'pinky', 'ring', 'thumb')):
            left = n.lower().endswith('_l') or '_l_' in n.lower()
            pivot = rest[names.index('upperarm_l' if left else 'upperarm_r')]
            mats.append((rot_z(-ARM_ANGLE if left else ARM_ANGLE), pivot))
        else:
            mats.append(None)
    for k in range(4):
        w = body['weights'][:, k]
        j = body['joints'][:, k]
        moved = pos.copy()
        for b in np.unique(j):
            m = mats[b]
            if m is None:
                continue
            sel = j == b
            R, pv = m
            moved[sel] = (pos[sel] - pv) @ R.T + pv
        out += moved * w[:, None]
    for i, m in enumerate(mats):
        if m is not None:
            rest[i] = (rest[i] - m[1]) @ m[0].T + m[1]
    return out, rest


def aggregate_weights(d, body):
    """Pesos del modelo -> dos huesos del juego por vértice (los dos de mayor peso, renormalizados)."""
    idx = {n: i for i, n in enumerate(MY_BONES)}
    mapping = np.array([idx[my_bone(n)] for n in d['names']])
    count = len(body['pos'])
    acc = np.zeros((count, len(MY_BONES)))
    for k in range(4):
        np.add.at(acc, (np.arange(count), mapping[body['joints'][:, k]]), body['weights'][:, k])
    order = np.argsort(-acc, axis=1)[:, :2]
    b0, b1 = order[:, 0], order[:, 1]
    w0 = acc[np.arange(count), b0]; w1 = acc[np.arange(count), b1]
    total = np.maximum(w0 + w1, 1e-9)
    return b0, b1, w0 / total


def my_rest(d, rest):
    """Posición de reposo (pose A) de cada uno de los 20 huesos del juego."""
    pick = {'Root': 'root', 'Pelvis': 'pelvis', 'Spine': 'spine_01', 'Chest': 'spine_03', 'Neck': 'neck_01', 'Head': 'Head',
            'Shoulder_L': 'upperarm_l', 'Elbow_L': 'lowerarm_l', 'Wrist_L': 'hand_l', 'Shoulder_R': 'upperarm_r', 'Elbow_R': 'lowerarm_r', 'Wrist_R': 'hand_r',
            'Hip_L': 'thigh_l', 'Knee_L': 'calf_l', 'Ankle_L': 'foot_l', 'Toe_L': 'ball_l', 'Hip_R': 'thigh_r', 'Knee_R': 'calf_r', 'Ankle_R': 'foot_r', 'Toe_R': 'ball_r'}
    return np.stack([rest[d['names'].index(pick[b])] for b in MY_BONES])


def cut_above(pos, tris, attrs, y):
    """Quita los triángulos cuyo centro está sobre y y los vértices que quedan sueltos."""
    keep = pos[tris][:, :, 1].mean(axis=1) < y
    return compact(pos, tris[keep], attrs)


def compact(pos, tris, attrs):
    used = np.unique(tris)
    remap = -np.ones(len(pos), dtype=np.int64); remap[used] = np.arange(len(used))
    return pos[used], remap[tris], {k: v[used] for k, v in attrs.items()}


def weld(pos, tris, attrs):
    """Une los vértices repetidos en las costuras de UV (misma posición): sin esto el reductor deja grietas."""
    key = np.round(pos / 1e-5).astype(np.int64)
    _, first, inverse = np.unique(key, axis=0, return_index=True, return_inverse=True)
    inverse = inverse.reshape(-1)
    t = inverse[tris]
    ok = (t[:, 0] != t[:, 1]) & (t[:, 1] != t[:, 2]) & (t[:, 0] != t[:, 2])
    return compact(pos[first], t[ok], {k: v[first] for k, v in attrs.items()})


def decimate(pos, tris, attrs, target):
    """Reduce la malla a `target` triángulos (colapso de aristas) y copia a cada vértice nuevo los atributos del vértice original más cercano."""
    p, f = fast_simplification.simplify(pos.astype(np.float32), tris.astype(np.int32), target_count=target, agg=7)
    _, nearest = cKDTree(pos).query(p)
    return p.astype(np.float64), f.astype(np.int64), {k: v[nearest] for k, v in attrs.items()}


def slice_plane(pos, tris, attrs, axis_y, only=None):
    """
    Corta con un plano horizontal y = axis_y. Los triángulos que lo cruzan se parten y los vértices del corte se duplican, uno por lado
    (separados 1e-5 m), para que el límite entre regiones (camiseta/short/media…) quede nítido en vez de degradarse.
    """
    eps = 1e-5
    pos = [tuple(p) for p in pos]
    A = {k: list(v) for k, v in attrs.items()}
    newt = []
    cache = {}

    def mid(i, j, side):
        key = (min(i, j), max(i, j), side)
        if key in cache:
            return cache[key]
        pi, pj = np.array(pos[i]), np.array(pos[j])
        t = (axis_y - pi[1]) / (pj[1] - pi[1])
        p = pi + (pj - pi) * t
        p[1] = axis_y + side * eps
        pos.append(tuple(p))
        near = i if t < 0.5 else j
        for k in A:
            A[k].append(A[k][i] * (1 - t) + A[k][j] * t if k == 'w0' else A[k][near])
        cache[key] = len(pos) - 1
        return cache[key]

    for t in tris:
        s = [pos[v][1] - axis_y for v in t]
        if all(x >= 0 for x in s) or all(x <= 0 for x in s) or (only is not None and not only(t)):
            newt.append(tuple(t))
            continue
        above = [i for i in range(3) if s[i] > 0]
        lone = above[0] if len(above) == 1 else [i for i in range(3) if s[i] <= 0][0]
        a, b, c = t[lone], t[(lone + 1) % 3], t[(lone + 2) % 3]   # conserva el sentido de giro
        side_lone = 1 if len(above) == 1 else -1
        ab_l, ac_l = mid(a, b, side_lone), mid(a, c, side_lone)   # copias del lado del vértice solitario
        ab_o, ac_o = mid(a, b, -side_lone), mid(a, c, -side_lone)  # copias del otro lado
        newt += [(a, ab_l, ac_l), (ab_o, b, c), (ab_o, c, ac_o)]
    return np.array(pos), np.array(newt, dtype=np.int64), {k: np.array(v) for k, v in A.items()}


def classify(pos, b0, b1):
    """Región de cada vértice: piel, camiseta, short, media, banda de media o botín (según altura y hueso dominante)."""
    y = pos[:, 1]
    arm = np.isin(b0, [MY_BONES.index(n) for n in ('Shoulder_L', 'Elbow_L', 'Wrist_L', 'Shoulder_R', 'Elbow_R', 'Wrist_R')])
    region = np.full(len(pos), REGION['skin'])
    shirt_torso = (~arm) & (y > HEM) & (y < NECKLINE)
    shirt_sleeve = arm & (y > SLEEVE_END)
    region[shirt_torso | shirt_sleeve] = REGION['shirt']
    leg = np.isin(b0, [MY_BONES.index(n) for n in ('Hip_L', 'Hip_R', 'Knee_L', 'Knee_R', 'Ankle_L', 'Ankle_R', 'Toe_L', 'Toe_R', 'Pelvis')]) | (y < HEM)
    region[(~arm) & (y <= HEM) & (y > SHORT_HEM)] = REGION['shorts']
    region[(~arm) & (y <= SOCK_TOP) & (y > BOOT_TOP)] = REGION['sock']
    region[(~arm) & (y <= STRIPE_HI) & (y >= STRIPE_LO)] = REGION['stripe']
    region[(~arm) & (y <= BOOT_TOP)] = REGION['boot']
    return region


def build_lod(pos, tris, attrs, target, detailed=True):
    p, f, a = decimate(pos, tris, attrs, target)
    arm_ids = [MY_BONES.index(n) for n in ('Shoulder_L', 'Elbow_L', 'Wrist_L', 'Shoulder_R', 'Elbow_R', 'Wrist_R')]
    planes = [HEM, SHORT_HEM, SOCK_TOP, BOOT_TOP, NECKLINE] + ([STRIPE_HI, STRIPE_LO] if detailed else [])
    for plane in planes:
        p, f, a = slice_plane(p, f, a, plane)
    # La manga termina en un plano horizontal, pero solo se corta en el brazo (no atraviesa el pecho).
    p, f, a = slice_plane(p, f, a, SLEEVE_END, only=lambda t: all(a['b0'][v] in arm_ids for v in t))
    b0, b1, w0 = a['b0'].astype(int), a['b1'].astype(int), a['w0']
    region = classify(p, b0, b1)
    if detailed:
        p, f, b0, b1, w0, region = refine_shirt(p, f, b0, b1, w0, region)
    return p, f, b0, b1, w0, region


def refine_shirt(p, f, b0, b1, w0, region, longest=0.07):
    """Parte en cuatro los triángulos de la camiseta con aristas largas: así las rayas y franjas del diseño se pintan con los colores de los vértices sin desdibujarse."""
    pos = [tuple(x) for x in p]; B0 = list(b0); B1 = list(b1); W0 = list(w0); R = list(region)
    cache = {}
    def mid(i, j):
        key = (min(i, j), max(i, j))
        if key not in cache:
            pos.append(tuple((np.array(pos[i]) + np.array(pos[j])) / 2))
            B0.append(B0[i]); B1.append(B1[i]); W0.append(W0[i] if B0[i] == B0[j] and B1[i] == B1[j] else 0.5 * (W0[i] + W0[j]) if B0[i] == B0[j] else W0[i])
            R.append(R[i])
            cache[key] = len(pos) - 1
        return cache[key]
    out = []
    for t in f:
        a, b, c = t
        if R[a] == REGION['shirt'] and R[b] == R[a] and R[c] == R[a]:
            e = max(np.linalg.norm(np.array(pos[a]) - np.array(pos[b])), np.linalg.norm(np.array(pos[b]) - np.array(pos[c])), np.linalg.norm(np.array(pos[c]) - np.array(pos[a])))
            if e > longest:
                ab, bc, ca = mid(a, b), mid(b, c), mid(c, a)
                out += [(a, ab, ca), (ab, b, bc), (ca, bc, c), (ab, bc, ca)]
                continue
        out.append((a, b, c))
    return np.array(pos), np.array(out, dtype=np.int64), np.array(B0), np.array(B1), np.array(W0), np.array(R)


def to_unity(bones, lods):
    """glTF es de mano derecha (+X a la izquierda del personaje); Unity, de mano izquierda (−X): se espeja X y se invierte el sentido de los triángulos."""
    flip = np.array([-1.0, 1.0, 1.0])
    bones = np.asarray(bones) * flip
    out = []
    for p, f, b0, b1, w0, region in lods:
        out.append((np.asarray(p) * flip, np.asarray(f)[:, [0, 2, 1]], b0, b1, w0, region))
    return bones, out


def write(path, bones, lods):
    with open(path, 'wb') as fh:
        fh.write(b'HUM1')
        fh.write(struct.pack('<B', len(lods)))
        fh.write(np.asarray(bones, dtype='<f4').tobytes())
        for p, f, b0, b1, w0, region in lods:
            fh.write(struct.pack('<II', len(p), len(f)))
            fh.write(np.asarray(p, dtype='<f4').tobytes())
            fh.write(np.asarray(b0, dtype=np.uint8).tobytes())
            fh.write(np.asarray(b1, dtype=np.uint8).tobytes())
            fh.write(np.clip(np.asarray(w0) * 255 + 0.5, 0, 255).astype(np.uint8).tobytes())
            fh.write(np.asarray(region, dtype=np.uint8).tobytes())
            fh.write(np.asarray(f, dtype='<u2').tobytes())


def main():
    src, dst = sys.argv[1], sys.argv[2]
    d = gltf_load.load(src)
    body = [m for m in d['meshes'] if m['material'].startswith('MI_Superhero')][0]
    pos, rest = repose(d, body)
    b0, b1, w0 = aggregate_weights(d, body)
    attrs = {'b0': b0, 'b1': b1, 'w0': w0}
    pos, tris, attrs = cut_above(pos, body['tris'], attrs, HEAD_CUT)
    pos, tris, attrs = weld(pos, tris, attrs)
    lods = [build_lod(pos, tris, attrs, LOD0_TRIS, True), build_lod(pos, tris, attrs, LOD1_TRIS, False)]
    bones = my_rest(d, rest)
    bones, lods = to_unity(bones, lods)
    write(dst, bones, lods)
    for i, l in enumerate(lods):
        print(f'LOD{i}: {len(l[0])} vértices, {len(l[1])} triángulos')
    print('huesos:', {b: bones[i].round(3).tolist() for i, b in enumerate(MY_BONES)})
    if '--ply' in sys.argv:
        out = sys.argv[sys.argv.index('--ply') + 1]
        os.makedirs(out, exist_ok=True)
        palette = np.array([[0.8, 0.6, 0.5], [0.2, 0.3, 0.8], [1, 1, 1], [0.2, 0.3, 0.8], [1, 1, 1], [0.1, 0.1, 0.1]])
        for i, (p, f, b0_, b1_, w0_, region) in enumerate(lods):
            with open(os.path.join(out, f'humanoid_lod{i}.ply'), 'w') as fh:
                for v, r in zip(p, region):
                    fh.write('v %f %f %f %f %f %f\n' % (*v, *palette[r]))
                for t in f:
                    fh.write('f %d %d %d\n' % tuple(t))


if __name__ == '__main__':
    main()
