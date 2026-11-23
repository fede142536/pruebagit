"""Lector mínimo de glTF 2.0 (malla con huesos) sin dependencias de Unity. Devuelve arrays de numpy."""
import json, os
import numpy as np

COMPONENT = {5120: np.int8, 5121: np.uint8, 5122: np.int16, 5123: np.uint16, 5125: np.uint32, 5126: np.float32}
WIDTH = {'SCALAR': 1, 'VEC2': 2, 'VEC3': 3, 'VEC4': 4, 'MAT4': 16}


def read_accessor(g, blob, index):
    a = g['accessors'][index]
    view = g['bufferViews'][a['bufferView']]
    dtype = np.dtype(COMPONENT[a['componentType']])
    n = WIDTH[a['type']]
    stride = view.get('byteStride', dtype.itemsize * n)
    start = view.get('byteOffset', 0) + a.get('byteOffset', 0)
    count = a['count']
    if stride == dtype.itemsize * n:
        arr = np.frombuffer(blob, dtype=dtype, count=count * n, offset=start).reshape(count, n)
    else:
        arr = np.stack([np.frombuffer(blob, dtype=dtype, count=n, offset=start + i * stride) for i in range(count)])
    return arr.copy()


def load(path):
    g = json.load(open(path))
    blob = open(os.path.join(os.path.dirname(path), g['buffers'][0]['uri']), 'rb').read()
    skin = g['skins'][0]
    joints = skin['joints']
    names = [g['nodes'][j]['name'] for j in joints]
    ibm = read_accessor(g, blob, skin['inverseBindMatrices']).reshape(-1, 4, 4)
    # Matrices glTF en columnas: la posición de reposo de cada hueso es la inversa de su matriz inversa de bind.
    rest = np.stack([np.linalg.inv(m.T)[:3, 3] for m in ibm])
    meshes = []
    for m in g['meshes']:
        for p in m['primitives']:
            at = p['attributes']
            meshes.append({
                'name': m['name'],
                'material': g['materials'][p['material']]['name'],
                'pos': read_accessor(g, blob, at['POSITION']).astype(np.float64),
                'nrm': read_accessor(g, blob, at['NORMAL']).astype(np.float64),
                'uv': read_accessor(g, blob, at['TEXCOORD_0']).astype(np.float64),
                'joints': read_accessor(g, blob, at['JOINTS_0']).astype(np.int64),
                'weights': read_accessor(g, blob, at['WEIGHTS_0']).astype(np.float64),
                'tris': read_accessor(g, blob, p['indices']).reshape(-1, 3).astype(np.int64),
            })
    return {'names': names, 'rest': rest, 'meshes': meshes, 'gltf': g}
