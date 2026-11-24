"""Dibuja el esqueleto del juego (20 huesos) en fotogramas de una animación retargeteada, de frente y de costado, para revisarla sin Unity."""
import sys
import numpy as np
from PIL import Image, ImageDraw
from retarget import Retargeter, GAME, PARENT

CELL = 170


def draw_skeleton(d, pos, ox, oy, view, color, scale=95.0):
    def pt(p):
        h = p[0] if view == 'front' else p[2]
        return (ox + h * scale, oy - p[1] * scale)
    for i, p in enumerate(PARENT):
        if p >= 0:
            d.line([pt(pos[p]), pt(pos[i])], fill=color, width=2)
    x, y = pt(pos[5])
    d.ellipse([x - 5, y - 8, x + 5, y + 2], outline=color)
    # Pelvis sobre las caderas
    d.line([pt(pos[12]), pt(pos[16])], fill=color, width=2)
    d.line([pt(pos[6]), pt(pos[9])], fill=color, width=2)


def sheet(r, fbx, times, out, title, pelvis_scale=None):
    img = Image.new('RGB', (CELL * len(times), CELL * 2 + 18), (20, 24, 30))
    d = ImageDraw.Draw(img)
    d.text((4, 2), title, fill=(220, 220, 220))
    for k, t in enumerate(times):
        local, hips = r.frame(fbx, t)
        pos = r.positions(local)
        ph = r.game_pos[1][1] + (hips[1] - r.hips_height / 100.0) * r.scale
        pos = [p + np.array([0.0, ph - r.game_pos[1][1], 0.0]) for p in pos]
        pos[0] = np.zeros(3)
        ox = CELL * k + CELL / 2
        for row, view in enumerate(('front', 'side')):
            oy = 18 + CELL * row + CELL - 30
            d.line([(CELL * k + 10, oy), (CELL * k + CELL - 10, oy)], fill=(70, 90, 70))
            draw_skeleton(d, pos, ox, oy, view, (120, 230, 150) if view == 'front' else (130, 190, 250))
        d.text((CELL * k + 4, 18), f"{t:.2f}s", fill=(160, 160, 160))
    img.save(out)


if __name__ == '__main__':
    from fbx import Fbx
    r = Retargeter('../../ThirdParty/Mixamo_Soccer/X Bot.fbx', '../../Game/Assets/Football/Resources/Humanoid/body.bytes')
    name = sys.argv[1]
    f = Fbx('../../ThirdParty/Mixamo_Soccer/' + name + '.fbx')
    n = int(sys.argv[3]) if len(sys.argv) > 3 else 7
    d = f.duration()
    sheet(r, f, list(np.linspace(0, d, n)), sys.argv[2], name)
