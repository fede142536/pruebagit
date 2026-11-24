"""Retargeteo de las animaciones de Mixamo (65 huesos, T-pose, mano derecha) al esqueleto de 20 huesos del juego (pose A, mano izquierda de Unity).

Idea: cada hueso del juego copia la ORIENTACIÓN en el mundo de su hueso equivalente de Mixamo, medida respecto de la pose de referencia, y corrige la
diferencia entre las dos poses de referencia (brazos en T contra brazos abajo) con la rotación más corta entre la dirección del hueso en una y otra.
"""
import numpy as np
from fbx import Fbx

# Huesos del juego (mismo orden que BodyBone) y el hueso de Mixamo que los mueve, el hueso de Mixamo hacia el que apunta (su «hijo») y el padre.
GAME = ['Root', 'Pelvis', 'Spine', 'Chest', 'Neck', 'Head', 'ShoulderL', 'ElbowL', 'WristL', 'ShoulderR', 'ElbowR', 'WristR',
        'HipL', 'KneeL', 'AnkleL', 'ToeL', 'HipR', 'KneeR', 'AnkleR', 'ToeR']
PARENT = [-1, 0, 1, 2, 3, 4, 3, 6, 7, 3, 9, 10, 1, 12, 13, 14, 1, 16, 17, 18]
MIXAMO = {
    'Pelvis': ('Hips', 'Spine'), 'Spine': ('Spine1', 'Spine2'), 'Chest': ('Spine2', 'Neck'), 'Neck': ('Neck', 'Head'), 'Head': ('Head', 'HeadTop_End'),
    'ShoulderL': ('LeftArm', 'LeftForeArm'), 'ElbowL': ('LeftForeArm', 'LeftHand'), 'WristL': ('LeftHand', 'LeftHandMiddle1'),
    'ShoulderR': ('RightArm', 'RightForeArm'), 'ElbowR': ('RightForeArm', 'RightHand'), 'WristR': ('RightHand', 'RightHandMiddle1'),
    'HipL': ('LeftUpLeg', 'LeftLeg'), 'KneeL': ('LeftLeg', 'LeftFoot'), 'AnkleL': ('LeftFoot', 'LeftToeBase'), 'ToeL': ('LeftToeBase', 'LeftToe_End'),
    'HipR': ('RightUpLeg', 'RightLeg'), 'KneeR': ('RightLeg', 'RightFoot'), 'AnkleR': ('RightFoot', 'RightToeBase'), 'ToeR': ('RightToeBase', 'RightToe_End'),
}
# Hueso del que sale la dirección de reposo de cada hueso del juego (hijo) — el último hueso de cada cadena mira como su padre.
GAME_CHILD = {'Pelvis': 'Spine', 'Spine': 'Chest', 'Chest': 'Neck', 'Neck': 'Head', 'Head': None, 'ShoulderL': 'ElbowL', 'ElbowL': 'WristL', 'WristL': None,
              'ShoulderR': 'ElbowR', 'ElbowR': 'WristR', 'WristR': None, 'HipL': 'KneeL', 'KneeL': 'AnkleL', 'AnkleL': 'ToeL', 'ToeL': None,
              'HipR': 'KneeR', 'KneeR': 'AnkleR', 'AnkleR': 'ToeR', 'ToeR': None}
MIRROR = np.diag([-1.0, 1.0, 1.0])
SWAP = {'ShoulderL': 'ShoulderR', 'ElbowL': 'ElbowR', 'WristL': 'WristR', 'HipL': 'HipR', 'KneeL': 'KneeR', 'AnkleL': 'AnkleR', 'ToeL': 'ToeR'}
SWAP.update({v: k for k, v in list(SWAP.items())})


def read_game_bind(path):
    """Posiciones de reposo de los 20 huesos del juego (body.bytes de Tools/ImportHumanoid)."""
    import struct
    d = open(path, 'rb').read()
    assert d[:4] == b'HUM1'
    return np.array([struct.unpack('<3f', d[5 + 12 * i:17 + 12 * i]) for i in range(20)])


def unit(v):
    n = np.linalg.norm(v)
    return v / n if n > 1e-9 else v


def from_to(a, b):
    """Rotación de arco más corto que lleva el vector a sobre el vector b."""
    a, b = unit(a), unit(b)
    c = float(np.dot(a, b))
    if c > 0.999999:
        return np.eye(3)
    if c < -0.999999:
        axis = unit(np.cross(a, [1, 0, 0] if abs(a[0]) < 0.9 else [0, 1, 0]))
        return 2 * np.outer(axis, axis) - np.eye(3)
    v = np.cross(a, b)
    k = np.array([[0, -v[2], v[1]], [v[2], 0, -v[0]], [-v[1], v[0], 0]])
    return np.eye(3) + k + k @ k * (1.0 / (1.0 + c))


def mat_to_quat(m):
    """Cuaternión (x, y, z, w) de una matriz de rotación 3x3."""
    t = m[0, 0] + m[1, 1] + m[2, 2]
    if t > 0:
        s = np.sqrt(t + 1.0) * 2
        q = [(m[2, 1] - m[1, 2]) / s, (m[0, 2] - m[2, 0]) / s, (m[1, 0] - m[0, 1]) / s, 0.25 * s]
    elif m[0, 0] > m[1, 1] and m[0, 0] > m[2, 2]:
        s = np.sqrt(1.0 + m[0, 0] - m[1, 1] - m[2, 2]) * 2
        q = [0.25 * s, (m[0, 1] + m[1, 0]) / s, (m[0, 2] + m[2, 0]) / s, (m[2, 1] - m[1, 2]) / s]
    elif m[1, 1] > m[2, 2]:
        s = np.sqrt(1.0 + m[1, 1] - m[0, 0] - m[2, 2]) * 2
        q = [(m[0, 1] + m[1, 0]) / s, 0.25 * s, (m[1, 2] + m[2, 1]) / s, (m[0, 2] - m[2, 0]) / s]
    else:
        s = np.sqrt(1.0 + m[2, 2] - m[0, 0] - m[1, 1]) * 2
        q = [(m[0, 2] + m[2, 0]) / s, (m[1, 2] + m[2, 1]) / s, 0.25 * s, (m[1, 0] - m[0, 1]) / s]
    q = np.array(q)
    return q / np.linalg.norm(q)


class Retargeter:
    def __init__(self, xbot_path, body_path):
        self.xbot = Fbx(xbot_path)
        self.bind_m = self.xbot.bind()                    # pose de referencia de Mixamo (cm, mano derecha)
        self.game_pos = read_game_bind(body_path)         # pose de referencia del juego (m)
        self.hips_height = self.bind_m['Hips'][1][1]      # cm
        # Escala de movimiento: largo de la pierna del juego (cadera–rodilla–tobillo) sobre el de Mixamo, para que los pies toquen el suelo igual.
        gp = self.game_pos
        game_leg = np.linalg.norm(gp[13] - gp[12]) + np.linalg.norm(gp[14] - gp[13])
        mix_leg = (np.linalg.norm(self.bind_m['LeftLeg'][1] - self.bind_m['LeftUpLeg'][1]) + np.linalg.norm(self.bind_m['LeftFoot'][1] - self.bind_m['LeftLeg'][1])) / 100.0
        self.scale = game_leg / mix_leg
        self.F = {}
        for g, (m, child) in MIXAMO.items():
            wm = self.bind_m[m][0]
            e_m = MIRROR @ unit(self.bind_m[child][1] - self.bind_m[m][1])
            gi = GAME.index(g)
            gc = GAME_CHILD[g]
            if gc is None:
                ref = GAME.index(g)
                d_o = unit(self.game_pos[ref] - self.game_pos[PARENT[ref]])
            else:
                d_o = unit(self.game_pos[GAME.index(gc)] - self.game_pos[gi])
            self.F[g] = from_to(d_o, e_m)
        self.bindT = {g: MIRROR @ self.bind_m[m][0] @ MIRROR for g, (m, _) in MIXAMO.items()}

    def frame(self, fbx, time):
        """Rotaciones locales (matrices 3x3 de los 20 huesos del juego) y posición de la pelvis (m, en el espacio del jugador) en un instante."""
        w = fbx.pose(time)
        world = {}
        local = [np.eye(3)] * 20
        for gi, g in enumerate(GAME):
            if g == 'Root':
                world[g] = np.eye(3)
                continue
            m, _ = MIXAMO[g]
            cur = MIRROR @ w[m][0] @ MIRROR
            o = cur @ self.bindT[g].T @ self.F[g]
            world[g] = o
            local[gi] = world[GAME[PARENT[gi]]].T @ o
        hips = MIRROR @ w['Hips'][1] / 100.0
        return local, hips

    def positions(self, local, pelvis_y=None):
        """Posiciones en el espacio del jugador de los 20 huesos (para dibujar)."""
        pos = [None] * 20
        world = [None] * 20
        for i in range(20):
            p = PARENT[i]
            if p < 0:
                world[i] = local[i]
                pos[i] = np.zeros(3)
            else:
                world[i] = world[p] @ local[i]
                pos[i] = pos[p] + world[p] @ (self.game_pos[i] - self.game_pos[p])
        return pos
