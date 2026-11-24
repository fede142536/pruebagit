"""Lector mínimo de FBX binario (7.x) y utilidades de esqueleto/animación para las animaciones de Mixamo. Solo lo que hace falta: nodos Model con sus
propiedades, conexiones y curvas de animación. No depende de nada fuera de numpy."""
import struct
import zlib
import numpy as np

KTIME = 46186158000.0  # unidades de tiempo de FBX por segundo


def _parse(path):
    d = open(path, 'rb').read()
    ver = struct.unpack('<I', d[23:27])[0]
    big = ver >= 7500

    def node(p):
        if big:
            end, nprops, _plen, nl = struct.unpack('<QQQB', d[p:p + 25]); h = 25
        else:
            end, nprops, _plen, nl = struct.unpack('<IIIB', d[p:p + 13]); h = 13
        if end == 0:
            return None, p + h
        name = d[p + h:p + h + nl].decode('latin1')
        q = p + h + nl
        props = []
        for _ in range(nprops):
            t = chr(d[q]); q += 1
            if t in 'YCFDIL':
                fmt = {'Y': '<h', 'C': '<B', 'F': '<f', 'D': '<d', 'I': '<i', 'L': '<q'}[t]
                size = struct.calcsize(fmt)
                props.append(struct.unpack(fmt, d[q:q + size])[0]); q += size
            elif t in 'fdilb':
                n, enc, cl = struct.unpack('<III', d[q:q + 12]); q += 12
                raw = d[q:q + cl]; q += cl
                if enc:
                    raw = zlib.decompress(raw)
                dt = {'f': '<f4', 'd': '<f8', 'i': '<i4', 'l': '<i8', 'b': 'u1'}[t]
                props.append(np.frombuffer(raw, dtype=dt, count=n))
            elif t in 'SR':
                n = struct.unpack('<I', d[q:q + 4])[0]; q += 4
                props.append(d[q:q + n]); q += n
        children = []
        while q < end:
            c, q = node(q)
            if c is None:
                break
            children.append(c)
        return (name, props, children), end

    p = 27
    top = []
    while p < len(d) - 100:
        n, p = node(p)
        if n is None:
            break
        top.append(n)
    return top


def _walk(n, out):
    out.append(n)
    for c in n[2]:
        _walk(c, out)


def _s(x):
    return x.decode('latin1') if isinstance(x, bytes) else x


def euler_to_matrix(deg):
    """Rotación de FBX con orden XYZ: primero X, luego Y, luego Z sobre ejes fijos (R = Rz · Ry · Rx)."""
    x, y, z = np.radians(deg)
    cx, sx, cy, sy, cz, sz = np.cos(x), np.sin(x), np.cos(y), np.sin(y), np.cos(z), np.sin(z)
    rx = np.array([[1, 0, 0], [0, cx, -sx], [0, sx, cx]])
    ry = np.array([[cy, 0, sy], [0, 1, 0], [-sy, 0, cy]])
    rz = np.array([[cz, -sz, 0], [sz, cz, 0], [0, 0, 1]])
    return rz @ ry @ rx


class Fbx:
    """Un archivo de Mixamo: esqueleto y (si trae) animación."""

    def __init__(self, path):
        self.path = path
        top = _parse(path)
        nodes = []
        for n in top:
            _walk(n, nodes)
        self.bones = {}      # id -> dict(name, pre, rot, trans)
        self.by_name = {}
        for n in nodes:
            if n[0] == 'Model' and _s(n[1][2]) == 'LimbNode':
                props = {}
                for c in n[2]:
                    if c[0] == 'Properties70':
                        for p in c[2]:
                            props[_s(p[1][0])] = [float(v) for v in p[1][4:7]] if len(p[1]) >= 7 else p[1][4:]
                name = _s(n[1][1]).split('\x00')[0].replace('mixamorig:', '')
                b = {'id': n[1][0], 'name': name, 'pre': np.array(props.get('PreRotation', [0, 0, 0])),
                     'rot': np.array(props.get('Lcl Rotation', [0, 0, 0])), 'trans': np.array(props.get('Lcl Translation', [0, 0, 0])), 'parent': None}
                self.bones[b['id']] = b
                self.by_name[name] = b
        conns = [n for n in nodes if n[0] == 'C']
        curve_nodes = {}
        curves = {}
        for n in nodes:
            if n[0] == 'AnimationCurveNode':
                curve_nodes[n[1][0]] = _s(n[1][1]).split('\x00')[0]
            elif n[0] == 'AnimationCurve':
                t = v = None
                for c in n[2]:
                    if c[0] == 'KeyTime':
                        t = c[1][0] / KTIME
                    elif c[0] == 'KeyValueFloat':
                        v = c[1][0].astype(np.float64)
                curves[n[1][0]] = (t, v)
        node_target = {}
        self.channels = {}   # (bone, 'R'|'T') -> {axis: (t, v)}
        for c in conns:
            kind = _s(c[1][0])
            a, b = c[1][1], c[1][2]
            if kind == 'OO' and a in self.bones and b in self.bones:
                self.bones[a]['parent'] = b
            if kind == 'OP' and a in curve_nodes and b in self.bones:
                prop = _s(c[1][3])
                node_target[a] = (self.bones[b]['name'], 'R' if 'Rotation' in prop else 'T' if 'Translation' in prop else 'S')
        for c in conns:
            kind = _s(c[1][0])
            a, b = c[1][1], c[1][2]
            if kind == 'OP' and a in curves and b in node_target:
                axis = _s(c[1][3]).replace('d|', '')
                self.channels.setdefault(node_target[b], {})[axis] = curves[a]

    def order(self):
        """Huesos con el padre antes que el hijo."""
        done, out = set(), []
        def visit(i):
            if i in done:
                return
            p = self.bones[i]['parent']
            if p is not None:
                visit(p)
            done.add(i)
            out.append(i)
        for i in self.bones:
            visit(i)
        return out

    def duration(self):
        t = 0.0
        for ch in self.channels.values():
            for tt, _ in ch.values():
                if len(tt):
                    t = max(t, float(tt[-1]))
        return t

    def sample(self, name, kind, time):
        """Valor (3 ejes) de un canal de rotación (grados) o traslación (cm) en un instante; el valor por defecto si no hay curva."""
        b = self.by_name[name]
        default = b['rot'] if kind == 'R' else b['trans']
        ch = self.channels.get((name, kind))
        out = np.array(default, dtype=np.float64)
        if not ch:
            return out
        for axis, i in (('X', 0), ('Y', 1), ('Z', 2)):
            if axis in ch:
                t, v = ch[axis]
                out[i] = np.interp(time, t, v)
        return out

    def pose(self, time, with_translation=True):
        """Orientaciones y posiciones en el mundo de todos los huesos en un instante: {nombre: (matriz 3x3, posición cm)}."""
        world = {}
        for i in self.order():
            b = self.bones[i]
            local = euler_to_matrix(b['pre']) @ euler_to_matrix(self.sample(b['name'], 'R', time))
            t = self.sample(b['name'], 'T', time) if with_translation else b['trans']
            if b['parent'] is None:
                world[b['name']] = (local, np.array(t))
            else:
                pr, pp = world[self.bones[b['parent']]['name']]
                world[b['name']] = (pr @ local, pp + pr @ t)
        return world

    def bind(self):
        """Pose de referencia (la que trae el archivo del personaje sin animar)."""
        world = {}
        for i in self.order():
            b = self.bones[i]
            local = euler_to_matrix(b['pre']) @ euler_to_matrix(b['rot'])
            if b['parent'] is None:
                world[b['name']] = (local, np.array(b['trans']))
            else:
                pr, pp = world[self.bones[b['parent']]['name']]
                world[b['name']] = (pr @ local, pp + pr @ b['trans'])
        return world
