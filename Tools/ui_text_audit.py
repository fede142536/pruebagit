#!/usr/bin/env python3
"""
Auditoría de textos de interfaz que se salen de su recuadro.

Recorre las llamadas UiKit.Label / UiKit.Button de Game/Assets/Football/Runtime cuyo tamaño y letra son números literales y cuyo texto sale de
Locale.Get("clave") o de un literal, mide el texto con Liberation Sans (mismas medidas que la Arial integrada de Unity) y avisa si no entra.
Todos los textos de UiKit tienen ajuste automático (UiKit.FitToBox): se avisa solo si no entran ni achicando la letra al 55 % y partiendo en líneas.
Uso: python3 Tools/ui_text_audit.py [--lang es|en]   (necesita Pillow)
No cubre textos armados en tiempo de ejecución (nombres, números): esos se revisan a mano.
"""
import glob, json, os, re, sys
from PIL import ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
FONT = "/usr/share/fonts/truetype/liberation/LiberationSans-Regular.ttf"
MARGIN, FIT_MIN = 10.0, 0.55

def lines_needed(text, size, avail):
    """Líneas que ocupa el texto a ese tamaño si se parte por palabras en un ancho dado (como el modo Wrap de Unity)."""
    total = 0
    for paragraph in text.split("\n"):
        current = ""
        count = 1
        for word in paragraph.split(" "):
            trial = (current + " " + word).strip()
            if current and width(trial, size) > avail:
                count += 1
                current = word
            else:
                current = trial
        total += count
    return total

def fits(text, size, w, h):
    """Verdadero si el texto entra en el recuadro con el ajuste automático (letra entre el 55 % y el 100 %, 1,15 de interlineado)."""
    for s in range(size, max(10, int(size * FIT_MIN)) - 1, -1):
        if lines_needed(text, s, w) * s * 1.15 <= h + 1 and width(max(text.replace("\n", " ").split(" "), key=len), s) <= w:
            return True
    return False

def width(text, size, cache={}):
    font = cache.setdefault(size, ImageFont.truetype(FONT, size))
    return max((font.getlength(line) for line in text.split("\n")), default=0)

def main():
    lang = sys.argv[sys.argv.index("--lang") + 1] if "--lang" in sys.argv else "es"
    strings = json.load(open(os.path.join(ROOT, f"Data/locale/ui.{lang}.json"), encoding="utf-8"))
    call = re.compile(
        r'UiKit\.(Label|Button)\(\s*[\w.]+\s*,\s*"[^"]*"(?:\s*\+[^,]+)?\s*,\s*new Vector2\(([^()]*)\)\s*,\s*new Vector2\(\s*([\d.]+)f?\s*,\s*([\d.]+)f?\s*\)\s*,\s*'
        r'(Locale\.Get\("([^"]+)"\)(?:\.ToUpperInvariant\(\))?|"([^"]*)")\s*,\s*(\d+)', re.S)
    # Elementos con tamaño literal cuyo texto se asigna después: «_campo = UiKit.Button(...)» y luego «_campo.Label.text = ... Locale.Get("clave") ...».
    field = re.compile(
        r'(\w+)\s*=\s*UiKit\.(Label|Button)\(\s*[\w.]+\s*,\s*"[^"]*"(?:\s*\+[^,]+)?\s*,\s*new Vector2\(([^()]*)\)\s*,\s*new Vector2\(\s*([\d.]+)f?\s*,\s*([\d.]+)f?\s*\)\s*,'
        r'\s*[^,]+,\s*(\d+)', re.S)
    bad = 0

    def report(path, src, pos, kind, w, h, size, text):
        nonlocal bad
        text = re.sub(r"\{\d+(?::[^}]*)?\}", "99", text)
        avail = w - (2 * MARGIN if kind == "Button" else 0)
        if not fits(text, size, avail, h):
            line = src[:pos].count("\n") + 1
            print(f"{os.path.basename(path)}:{line}  {kind} {w:.0f}x{h:.0f} letra {size}  no entra ni achicando  «{text}»")
            bad += 1

    for path in sorted(glob.glob(os.path.join(ROOT, "Game/Assets/Football/Runtime/*.cs"))):
        src = open(path, encoding="utf-8").read()
        seen = 0
        for fm in field.finditer(src):
            name, kind, w, h, size = fm.group(1), fm.group(2), float(fm.group(4)), float(fm.group(5)), int(fm.group(6))
            seen += 1
            assign = re.compile(re.escape(name) + r'(?:\.Label)?\.text\s*=([^;]*);', re.S)
            for am in assign.finditer(src):
                for key in re.findall(r'Locale\.Get\(\s*"([^"]+)"', am.group(1)):
                    if key in strings:
                        report(path, src, am.start(), kind, w, h, size, strings[key])
        for m in call.finditer(src):
            kind, w, h, expr, key, lit, size = m.group(1), float(m.group(3)), float(m.group(4)), m.group(5), m.group(6), m.group(7), int(m.group(8))
            text = strings.get(key) if key else lit
            if text is None:
                continue
            if "ToUpperInvariant" in expr:
                text = text.upper()
            report(path, src, m.start(), kind, w, h, size, text)
    print(f"{bad} textos no entran ({lang})")
    return 1 if bad else 0

if __name__ == "__main__":
    sys.exit(main())
