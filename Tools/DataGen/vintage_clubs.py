"""
Agrega a Data/teams/argentina.json (a) el puesto de cada club en la tabla anual 2026 (campo Club.AnnualRank) y (b) los clubes que solo juegan
«Fútbol vintage» (Primera de 20 + Nacional de 29): 19 clubes con nombre real y planteles GENERADOS (nombres ficticios, medias estimadas).
Es determinista: volver a correrlo da el mismo archivo. Uso: python3 Tools/DataGen/vintage_clubs.py
"""
import json, random, os

PATH = os.path.join(os.path.dirname(__file__), '..', '..', 'Data', 'teams', 'argentina.json')

# Tabla anual 2026 de Primera (capturas aportadas por el usuario, 23/11/2026). Godoy Cruz y San Martín (San Juan) no están: juegan el Nacional.
ANNUAL = {
    'Boca Juniors': 1, 'Rosario Central': 2, 'Argentinos Juniors': 3, 'Independiente Rivadavia': 4, 'Gimnasia y Esgrima La Plata': 5,
    'Vélez Sarsfield': 6, 'River Plate': 7, 'Instituto (Córdoba)': 8, 'Lanús': 9, 'Belgrano': 10, 'Estudiantes de La Plata': 11,
    'Barracas Central': 12, 'Defensa y Justicia': 13, 'Independiente': 14, 'Huracán': 15, 'Unión (Santa Fe)': 16,
    'Gimnasia y Esgrima (Mendoza)': 17, 'San Lorenzo': 18, 'Sarmiento (Junín)': 19, 'Tigre': 20, 'Talleres': 21, "Newell's Old Boys": 22,
    'Racing Club': 23, 'Platense': 24, 'Banfield': 25, 'Atlético Tucumán': 26, 'Central Córdoba (Santiago del Estero)': 27,
    'Deportivo Riestra': 28, 'Aldosivi': 29, 'Estudiantes (Río Cuarto)': 30,
}

# nombre, corto, (camiseta, secundario, diseño), (alterna, secundario), estadio, capacidad, ciudad, fundación, jerarquía, media base, estilo, historia
CLUBS = [
    ('Gimnasia y Esgrima (Mendoza)', 'GEM', ('#1F4FA3', '#FFFFFF', 'Stripes'), ('#FFFFFF', '#1F4FA3'), 'Víctor Antonio Legrotaglie', 12500, 'Mendoza', 1908, 'Development', 64, 'Balanced', 'El Lobo mendocino, azul y blanco, llegó a Primera en 2026.'),
    ('Estudiantes (Río Cuarto)', 'ERC', ('#7EC8F0', '#FFFFFF', 'Stripes'), ('#FFFFFF', '#7EC8F0'), 'Ciudad de Río Cuarto', 22000, 'Río Cuarto', 1912, 'Development', 62, 'Defensive', 'Club cordobés que ascendió a Primera en 2026.'),
    ('Colón', 'COL', ('#C8102E', '#111111', 'Stripes'), ('#FFFFFF', '#C8102E'), 'Brigadier General Estanislao López', 40000, 'Santa Fe', 1905, 'Mid', 63, 'Balanced', 'El Sabalero, rojinegro, uno de los grandes del interior.'),
    ('Quilmes', 'QUI', ('#FFFFFF', '#2E7BC6', 'Stripes'), ('#2E7BC6', '#FFFFFF'), 'Centenario', 30200, 'Quilmes', 1887, 'Mid', 62, 'Balanced', 'El Cervecero, el club más antiguo del Gran Buenos Aires.'),
    ('Chacarita Juniors', 'CHA', ('#C8102E', '#111111', 'Stripes'), ('#FFFFFF', '#C8102E'), 'Chacarita Juniors', 17000, 'San Martín, Buenos Aires', 1906, 'Mid', 61, 'Defensive', 'El Funebrero, de San Martín.'),
    ('Atlético Rafaela', 'RAF', ('#8FD0F5', '#FFFFFF', 'Stripes'), ('#FFFFFF', '#8FD0F5'), 'Nuevo Monumental', 15000, 'Rafaela', 1907, 'Development', 60, 'Counter Attack', 'La Crema, de Rafaela.'),
    ('Patronato', 'PAT', ('#B31B1B', '#111111', 'Stripes'), ('#FFFFFF', '#B31B1B'), 'Presbítero Bartolomé Grella', 22000, 'Paraná', 1914, 'Development', 61, 'Balanced', 'El Patrón, rojinegro, de Paraná.'),
    ('Temperley', 'TEM', ('#8FD0F5', '#FFFFFF', 'Stripes'), ('#FFFFFF', '#8FD0F5'), 'Alfredo Beranger', 15000, 'Turdera', 1912, 'Development', 60, 'Possession', 'El Gasolero, celeste y blanco, de Turdera.'),
    ('Almagro', 'ALM', ('#1F4FA3', '#FFFFFF', 'Stripes'), ('#FFFFFF', '#1F4FA3'), 'Tres de Febrero', 15000, 'José Ingenieros', 1911, 'Development', 59, 'Balanced', 'El Tricolor, de José Ingenieros.'),
    ('Ferro Carril Oeste', 'FER', ('#0A7D3A', '#FFFFFF', 'Stripes'), ('#FFFFFF', '#0A7D3A'), 'Ricardo Etcheverri', 24000, 'Caballito', 1904, 'Mid', 62, 'Possession', 'El Verdolaga, de Caballito.'),
    ('All Boys', 'ABO', ('#FFFFFF', '#111111', 'Stripes'), ('#111111', '#FFFFFF'), 'Islas Malvinas', 14000, 'Floresta', 1913, 'Development', 60, 'Balanced', 'El Albo, de Floresta.'),
    ('Atlanta', 'ATL', ('#F2C500', '#1F4FA3', 'Stripes'), ('#1F4FA3', '#F2C500'), 'Don León Kolbowski', 14000, 'Villa Crespo', 1904, 'Development', 60, 'Counter Attack', 'Los Bohemios, de Villa Crespo.'),
    ('Tristán Suárez', 'TSU', ('#1F3C88', '#FFFFFF', 'Stripes'), ('#FFFFFF', '#1F3C88'), 'Tristán Suárez', 12000, 'Ezeiza', 1929, 'Development', 61, 'Balanced', 'El Funebrero del sur, de Ezeiza.'),
    ('Deportivo Madryn', 'MAD', ('#111111', '#F2C500', 'Stripes'), ('#F2C500', '#111111'), 'Abel Sastre', 9000, 'Puerto Madryn', 1965, 'Development', 60, 'Counter Attack', 'El Aurinegro, de Puerto Madryn.'),
    ('Deportivo Morón', 'MOR', ('#FFFFFF', '#C8102E', 'Stripes'), ('#C8102E', '#FFFFFF'), 'Nuevo Francisco Urbano', 15000, 'Morón', 1947, 'Development', 61, 'Balanced', 'El Gallo, de Morón.'),
    ('Gimnasia y Esgrima (Jujuy)', 'GEJ', ('#FFFFFF', '#1F4FA3', 'Stripes'), ('#1F4FA3', '#FFFFFF'), '23 de Agosto', 23000, 'San Salvador de Jujuy', 1931, 'Development', 60, 'Balanced', 'El Lobo jujeño.'),
    ('Mitre (Santiago del Estero)', 'MIT', ('#F2C500', '#111111', 'Stripes'), ('#111111', '#F2C500'), 'Doctor Antonio Zorrilla', 14000, 'Santiago del Estero', 1907, 'Development', 59, 'Defensive', 'El Aurinegro santiagueño.'),
    ('Los Andes', 'LAD', ('#FFFFFF', '#C8102E', 'Stripes'), ('#C8102E', '#FFFFFF'), 'Eduardo Gallardón', 24000, 'Lomas de Zamora', 1917, 'Development', 59, 'Balanced', 'Las Milrayitas, de Lomas de Zamora.'),
    ('Almirante Brown', 'ABR', ('#F2C500', '#111111', 'Stripes'), ('#111111', '#F2C500'), 'Fragata Presidente Sarmiento', 22000, 'Isidro Casanova', 1922, 'Development', 58, 'Defensive', 'El Mirasol, de Isidro Casanova.'),
]

FIRST = ['Lucas', 'Matías', 'Nicolás', 'Facundo', 'Franco', 'Agustín', 'Tomás', 'Joaquín', 'Santiago', 'Ezequiel', 'Gonzalo', 'Federico', 'Maximiliano', 'Julián',
         'Emiliano', 'Bruno', 'Leandro', 'Rodrigo', 'Martín', 'Ignacio', 'Sebastián', 'Cristian', 'Diego', 'Pablo', 'Gastón', 'Ramiro', 'Axel', 'Brian', 'Kevin', 'Lautaro']
LAST = ['González', 'Rodríguez', 'Fernández', 'López', 'Martínez', 'Pérez', 'Gómez', 'Sánchez', 'Díaz', 'Romero', 'Álvarez', 'Torres', 'Ruiz', 'Ramírez', 'Flores',
        'Benítez', 'Acosta', 'Medina', 'Herrera', 'Suárez', 'Aguirre', 'Giménez', 'Gutiérrez', 'Pereyra', 'Rojas', 'Molina', 'Castro', 'Ortiz', 'Silva', 'Núñez',
        'Luna', 'Juárez', 'Cabrera', 'Ríos', 'Morales', 'Domínguez', 'Peralta', 'Vega', 'Cardozo', 'Ledesma', 'Sosa', 'Correa', 'Paz', 'Villalba', 'Ibáñez', 'Quiroga']
SQUAD = ['POR'] * 3 + ['DFC'] * 5 + ['LI'] * 2 + ['LD'] * 2 + ['MCD'] * 2 + ['MC'] * 3 + ['MCO'] * 1 + ['EI', 'ED'] + ['DC'] * 2


def kit(shirt, secondary, pattern):
    return {'Shirt': shirt, 'Secondary': secondary, 'Pattern': pattern, 'Shorts': secondary if pattern != 'Solid' else shirt, 'Socks': shirt}


def players(name, base):
    rng = random.Random(sum(ord(c) * (i + 1) for i, c in enumerate(name)))
    used, lasts, out = set(), set(), []
    numbers = [1, 12, 22] + [n for n in range(2, 40) if n not in (12, 22)]
    for pos in SQUAD:
        while True:
            full = rng.choice(FIRST) + ' ' + rng.choice(LAST)
            if full not in used and full.split(' ', 1)[1] not in lasts:  # apellidos distintos: en pantalla se ve solo el apellido
                used.add(full)
                lasts.add(full.split(' ', 1)[1])
                break
        out.append({'Name': full, 'ShortName': '', 'Number': numbers[len(out)], 'Position': pos, 'Rating': 0, 'Age': rng.randint(18, 35)})
    ratings = sorted((max(55, min(74, int(round(base + rng.gauss(0, 3.2))))) for _ in out), reverse=True)
    # el mejor once es el que más nivel tiene; los suplentes, de 3 a 8 puntos menos
    order = list(range(len(out)))
    rng.shuffle(order)
    for r, idx in zip(ratings, order):
        out[idx]['Rating'] = r
    return out


def main():
    d = json.load(open(PATH, encoding='utf-8'))
    d['Teams'] = [t for t in d['Teams'] if not t.get('VintageOnly')]  # idempotente
    for t in d['Teams']:
        t.setdefault('Club', {})['AnnualRank'] = ANNUAL.get(t['Name'], 0)
        t.pop('VintageOnly', None)
    for (name, short, home, away, stadium, capacity, city, founded, tier, base, style, history) in CLUBS:
        d['Teams'].append({
            'Name': name, 'ShortName': short, 'Style': style, 'Formation': '4-4-2', 'VintageOnly': True,
            'Home': kit(*home), 'Away': kit(away[0], away[1], 'Solid'),
            'Players': players(name, base),
            'Club': {
                'Founded': founded, 'Stadium': stadium, 'Capacity': capacity, 'TransferBudget': 1200000 + (base - 58) * 120000,
                'AnnualWageBudget': 3600000 + (base - 58) * 450000, 'ClubValue': 12000000 + (base - 58) * 2200000, 'Tier': tier, 'History': history,
                'City': city, 'ParkingSpaces': 200, 'Pitch': 'Regular', 'AnnualRank': ANNUAL.get(name, 0),
            },
        })
    d['Notes'] = (d.get('Notes', '') + ' ' if 'vintage' not in d.get('Notes', '').lower() else d.get('Notes', '') + ' ')
    if 'Fútbol vintage' not in d['Notes']:
        d['Notes'] += ('Clubes con "VintageOnly" (solo juegan Fútbol vintage, 23/11/2026): nombre real; planteles GENERADOS por Tools/DataGen/vintage_clubs.py '
                       '(jugadores ficticios, medias estimadas); estadio, capacidad y fundación aproximados. El puesto en la tabla anual 2026 (Club.AnnualRank) viene de las capturas del usuario.')
    json.dump(d, open(PATH, 'w', encoding='utf-8'), ensure_ascii=False, indent=2)
    print(len(d['Teams']), 'equipos')


if __name__ == '__main__':
    main()
