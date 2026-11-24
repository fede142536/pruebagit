#!/usr/bin/env python3
"""
Convierte las animaciones de fútbol de Mixamo (ThirdParty/Mixamo_Soccer/*.fbx) en un solo archivo para el juego:
Game/Assets/Football/Resources/Anim/clips.bytes. Cada animación queda retargeteada a los 20 huesos del juego (rotaciones locales por fotograma a 30 fps),
sin el desplazamiento del cuerpo (lo mueve el motor); solo se conserva la altura de la pelvis y su vaivén.

Uso:  python3 Tools/mixamo/import_animations.py            (genera clips.bytes)
      python3 Tools/mixamo/import_animations.py --preview  (además dibuja hojas de esqueletos en Tools/mixamo/preview/)
Formato de clips.bytes (little endian): "ANM1", int32 clips; por clip: string (7 bits de largo), byte loop, byte hold, float velocidad de la raíz (m/s),
int32 fotogramas; por fotograma: 3 float (pelvis x, y, z: desplazamiento en metros respecto de su posición de reposo) + 19 huesos x 4 int16 (cuaternión x,y,z,w / 32767).
Hace falta numpy (y Pillow para --preview).
"""
import json
import os
import struct
import sys
import numpy as np
from fbx import Fbx
from retarget import Retargeter, GAME, PARENT, SWAP, MIRROR, mat_to_quat

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
SRC = os.path.join(ROOT, 'ThirdParty', 'Mixamo_Soccer')
OUT = os.path.join(ROOT, 'Game', 'Assets', 'Football', 'Resources', 'Anim', 'clips.bytes')
BODY = os.path.join(ROOT, 'Game', 'Assets', 'Football', 'Resources', 'Humanoid', 'body.bytes')


def foot_contact_time(fbx, lo=None, hi=None):
    """Instante en que el pie (el que más rápido va) alcanza su mayor velocidad: el golpe al balón."""
    fps = 60.0
    d = fbx.duration()
    ts = np.arange(0.0, d, 1.0 / fps)
    best_t, best_v = 0.0, -1.0
    for foot in ('RightFoot', 'LeftFoot'):
        p = np.array([fbx.pose(t)[foot][1] for t in ts])
        v = np.linalg.norm(np.diff(p, axis=0), axis=1) * fps / 100.0
        v = np.convolve(v, np.ones(3) / 3, mode='same')
        i = int(np.argmax(v))
        if v[i] > best_v:
            best_v, best_t = v[i], ts[i]
    return best_t


def quat_continuity(qs):
    for i in range(1, len(qs)):
        for b in range(qs.shape[1]):
            if np.dot(qs[i, b], qs[i - 1, b]) < 0:
                qs[i, b] = -qs[i, b]
    return qs


def bake(r, entry, fps):
    f = Fbx(os.path.join(SRC, entry['file']))
    start = entry.get('start', 0.0)
    end = entry.get('end', f.duration())
    if entry.get('align') == 'foot':
        c = foot_contact_time(f)
        start, end = max(0.0, c - entry['pre']), min(f.duration(), c + entry['post'])
    loop = bool(entry.get('loop'))
    n = max(2, int(round((end - start) * fps)) + (0 if loop else 1))
    times = start + np.arange(n) / fps
    quats = np.zeros((n, 19, 4))
    hips = np.zeros((n, 3))
    for k, t in enumerate(times):
        local, h = r.frame(f, t)
        for b in range(1, 20):
            quats[k, b - 1] = mat_to_quat(local[b])
        hips[k] = h
    quats = quat_continuity(quats)
    # Sin desplazamiento: se quita la tendencia lineal horizontal de la cadera (lo que avanza el cuerpo); queda el vaivén. La altura se conserva.
    last = max(n - 1, 1)
    travel = hips[-1] - hips[0]
    root_speed = float(np.hypot(travel[0], travel[2]) / (last / fps))
    detr = hips.copy()
    detr[:, 0] -= hips[:, 0].copy() * 0 + np.arange(n) / last * travel[0] + hips[0, 0]
    detr[:, 2] -= np.arange(n) / last * travel[2] + hips[0, 2]
    # Desplazamiento de la pelvis en metros (a la escala del jugador): sube y baja como la cadera de Mixamo respecto de su pose de referencia.
    scale = r.scale
    ratio = detr * scale
    ratio[:, 1] = (hips[:, 1] - r.hips_height / 100.0) * scale
    if entry.get('mirror'):
        quats, ratio = mirror_clip(quats, ratio)
    return {'name': entry['name'], 'loop': loop, 'hold': bool(entry.get('hold')), 'speed': root_speed * scale, 'quats': quats, 'pelvis': ratio,
            'rate': entry.get('rate', 1.0)}


def mirror_clip(quats, pelvis):
    """Espeja una animación respecto del plano YZ: intercambia izquierda y derecha y refleja cada rotación."""
    out = np.zeros_like(quats)
    for b in range(1, 20):
        name = GAME[b]
        src = GAME.index(SWAP.get(name, name))
        q = quats[:, src - 1].copy()
        q[:, 1] = -q[:, 1]
        q[:, 2] = -q[:, 2]
        out[:, b - 1] = q
    p = pelvis.copy()
    p[:, 0] = -p[:, 0]
    return out, p


def write_string(w, s):
    b = s.encode('utf-8')
    n = len(b)
    out = bytearray()
    while True:
        byte = n & 0x7F
        n >>= 7
        out.append(byte | (0x80 if n else 0))
        if not n:
            break
    w.write(bytes(out) + b)


def main():
    preview = '--preview' in sys.argv
    manifest = json.load(open(os.path.join(HERE, 'clips.json'), encoding='utf-8'))
    fps = manifest['fps']
    r = Retargeter(os.path.join(SRC, 'X Bot.fbx'), BODY)
    clips = []
    for entry in manifest['clips']:
        c = bake(r, entry, fps)
        clips.append(c)
        print(f"{c['name']:12} {len(c['quats']):4} fotogramas  {len(c['quats']) / fps:5.2f}s  raíz {c['speed']:4.1f} m/s  {'lazo' if c['loop'] else ''}{' fija' if c['hold'] else ''}")
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, 'wb') as w:
        w.write(b'ANM1')
        w.write(struct.pack('<i', len(clips)))
        for c in clips:
            write_string(w, c['name'])
            w.write(struct.pack('<BBf', 1 if c['loop'] else 0, 1 if c['hold'] else 0, c['speed']))
            w.write(struct.pack('<f', c['rate']))
            w.write(struct.pack('<i', len(c['quats'])))
            for k in range(len(c['quats'])):
                w.write(struct.pack('<3f', *c['pelvis'][k]))
                q = np.clip(np.round(c['quats'][k] * 32767), -32767, 32767).astype('<i2')
                w.write(q.tobytes())
    print(f"{OUT}: {os.path.getsize(OUT) // 1024} KB")
    if preview:
        from preview import sheet
        os.makedirs(os.path.join(HERE, 'preview'), exist_ok=True)
        for entry in manifest['clips']:
            f = Fbx(os.path.join(SRC, entry['file']))
            start = entry.get('start', 0.0)
            end = entry.get('end', f.duration())
            sheet(r, f, list(np.linspace(start, end, 8)), os.path.join(HERE, 'preview', entry['name'] + '.png'), entry['name'])


if __name__ == '__main__':
    main()
