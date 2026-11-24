#!/usr/bin/env python3
"""
Recorta los escudos de las hojas que subió el usuario (Game/Assets/escudos completos*.jfif) y deja un PNG por club, con fondo transparente, en
Game/Assets/MisEscudos/Resources/Crests/<club>.png (los nombres son los de CrestLibrary.SlugOf). Son imágenes chicas (cada escudo mide unos 40-60 px en la hoja):
el resultado se ve borroso de cerca; conviene reemplazarlos por imágenes mejores cuando se pueda.
Uso: python3 Tools/slice_crests.py [--contact hoja.png]
"""
import os
import re
import sys
import unicodedata
from collections import deque
import numpy as np
from PIL import Image, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
ASSETS = os.path.join(ROOT, 'Game', 'Assets')
OUT = os.path.join(ASSETS, 'MisEscudos', 'Resources', 'Crests')
SIZE = 256
THRESHOLD = 34

# Hoja «Los nuevos 30 del 2026»: 5 columnas x 6 filas.
SHEET_PRIMERA = ('escudos completos 2.jfif', [
    ['Aldosivi', 'Argentinos Juniors', 'Atlético Tucumán', 'Banfield', 'Barracas Central'],
    ['Belgrano', 'Boca Juniors', 'Central Córdoba (Santiago del Estero)', 'Defensa y Justicia', 'Deportivo Riestra'],
    ['Estudiantes (Río Cuarto)', 'Estudiantes de La Plata', 'Gimnasia y Esgrima La Plata', 'Gimnasia y Esgrima (Mendoza)', 'Huracán'],
    ['Independiente Rivadavia', 'Independiente', 'Instituto (Córdoba)', 'Lanús', "Newell's Old Boys"],
    ['Platense', 'Racing Club', 'River Plate', 'Rosario Central', 'San Lorenzo'],
    ['Sarmiento (Junín)', 'Talleres', 'Tigre', 'Unión (Santa Fe)', 'Vélez Sarsfield'],
], dict(left=0.0, top=0.075, width=1.0, height=0.85, columns=5, rows=6))

# Hoja «Primera Nacional 2026»: 6 columnas x 6 filas, cada escudo sobre una baldosa de color. None = club que no está en el juego o no se pudo identificar.
SHEET_NACIONAL = ('escudos completos nacional b.jfif', [
    [None, 'All Boys', 'Almagro', 'Almirante Brown', 'Atlanta', None],
    ['Chacarita Juniors', None, None, None, 'Colón', None],
    [None, None, 'Deportivo Morón', None, 'Ferro Carril Oeste', 'Gimnasia y Esgrima (Jujuy)'],
    [None, 'Godoy Cruz Antonio Tomba', None, 'Los Andes', None, None],
    [None, 'Patronato', 'Quilmes', 'Atlético Rafaela', 'San Martín (San Juan)', None],
    [None, None, 'Temperley', 'Tristán Suárez', None, None],
], dict(left=0.065, top=0.168, width=0.87, height=0.742, columns=6, rows=6, tile=True))


def slug(name):
    d = unicodedata.normalize('NFD', name.lower())
    d = ''.join(c for c in d if unicodedata.category(c) != 'Mn')
    return re.sub(r'[^a-z0-9]+', '_', d).strip('_')


def remove_background(rgb):
    """Quita el fondo conectado con el borde (color parecido al de los píxeles del borde) y devuelve un RGBA."""
    h, w, _ = rgb.shape
    border = np.concatenate([rgb[0], rgb[-1], rgb[:, 0], rgb[:, -1]]).astype(np.float32)
    bg = np.median(border, axis=0)
    dist = np.linalg.norm(rgb.astype(np.float32) - bg, axis=2)
    similar = dist <= THRESHOLD
    outside = np.zeros((h, w), bool)
    queue = deque()
    for x in range(w):
        for y in (0, h - 1):
            if similar[y, x] and not outside[y, x]:
                outside[y, x] = True
                queue.append((y, x))
    for y in range(h):
        for x in (0, w - 1):
            if similar[y, x] and not outside[y, x]:
                outside[y, x] = True
                queue.append((y, x))
    while queue:
        y, x = queue.popleft()
        for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            ny, nx = y + dy, x + dx
            if 0 <= ny < h and 0 <= nx < w and similar[ny, nx] and not outside[ny, nx]:
                outside[ny, nx] = True
                queue.append((ny, nx))
    alpha = np.where(outside, 0, 255).astype(np.uint8)
    return alpha


def crest_from(cell):
    rgb = np.array(cell.convert('RGB'))
    alpha = remove_background(rgb)
    ys, xs = np.where(alpha > 0)
    if len(ys) == 0:
        return None
    box = (xs.min(), ys.min(), xs.max() + 1, ys.max() + 1)
    img = Image.fromarray(np.dstack([rgb, alpha]), 'RGBA').crop(box)
    side = max(img.size)
    square = Image.new('RGBA', (side, side), (0, 0, 0, 0))
    square.paste(img, ((side - img.width) // 2, (side - img.height) // 2))
    big = square.resize((SIZE, SIZE), Image.LANCZOS)
    a = big.getchannel('A').filter(ImageFilter.GaussianBlur(0.8))
    big.putalpha(a.point(lambda v: 0 if v < 40 else min(255, int((v - 40) * 255 / 160))))
    return big


def process(sheet, written):
    file, names, g = sheet
    im = Image.open(os.path.join(ASSETS, file)).convert('RGB')
    W, H = im.size
    x0, y0 = g['left'] * W, g['top'] * H
    cw, ch = g['width'] * W / g['columns'], g['height'] * H / g['rows']
    inset = 0.14 if g.get('tile') else 0.0
    for r, row in enumerate(names):
        for c, name in enumerate(row):
            if not name:
                continue
            box = (int(x0 + c * cw + cw * inset), int(y0 + r * ch + ch * inset), int(x0 + (c + 1) * cw - cw * inset), int(y0 + (r + 1) * ch - ch * inset))
            crest = crest_from(im.crop(box))
            if crest is None:
                print('sin escudo:', name)
                continue
            os.makedirs(OUT, exist_ok=True)
            crest.save(os.path.join(OUT, slug(name) + '.png'))
            written.append((name, crest))


def main():
    written = []
    process(SHEET_PRIMERA, written)
    process(SHEET_NACIONAL, written)
    print(len(written), 'escudos en', OUT)
    if '--contact' in sys.argv:
        path = sys.argv[sys.argv.index('--contact') + 1]
        cols = 8
        rows = (len(written) + cols - 1) // cols
        sheet = Image.new('RGB', (cols * 130, rows * 150), (60, 120, 60))
        from PIL import ImageDraw
        d = ImageDraw.Draw(sheet)
        for i, (name, img) in enumerate(written):
            t = img.resize((110, 110))
            x, y = (i % cols) * 130 + 10, (i // cols) * 150 + 5
            sheet.paste(t, (x, y), t)
            d.text((x, y + 114), name[:20], fill=(255, 255, 255))
        sheet.save(path)


if __name__ == '__main__':
    main()
