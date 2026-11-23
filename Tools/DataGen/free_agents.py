"""
Carga los jugadores libres y la nacionalidad en Data/teams/argentina.json. Los datos de cada libre (nombre, puesto, edad, nacionalidad y valor de mercado) vienen de
las capturas que aportó el usuario el 23/11/2026; la MEDIA es una estimación propia (valor de mercado y edad), y la nacionalidad de los planteles de los clubes se
completó de memoria para los extranjeros más conocidos (el resto figura como argentino): corregir en el archivo si hace falta.
Uso: python3 Tools/DataGen/free_agents.py   (idempotente)
"""
import json, math, os, re

PATH = os.path.join(os.path.dirname(__file__), '..', '..', 'Data', 'teams', 'argentina.json')
POS = {'Portero': 'POR', 'Defensa central': 'DFC', 'Lateral derecho': 'LD', 'Lateral izquierdo': 'LI', 'Pivote': 'MCD', 'Mediocentro': 'MC',
       'Mediocentro ofensivo': 'MCO', 'Mediapunta': 'MCO', 'Extremo derecho': 'ED', 'Extremo izquierdo': 'EI', 'Delantero centro': 'DC'}

# (nombre, puesto, edad, valor en miles de euros, nacionalidad)
LIST = """
Federico Vázquez|Delantero centro|33|150|ARG
Israel Coll|Pivote|33|150|ARG
Lautaro Chaparro|Extremo derecho|25|125|ARG
Agustín Mulet|Pivote|26|125|ARG
Rubén Darío Ríos|Mediocentro|31|125|ARG
Carlos Tombolini|Delantero centro|29|100|ARG
Tobías Donsanti|Delantero centro|26|100|ARG
Juan Manuel Cruz|Delantero centro|27|100|ARG
Santiago Visentín|Defensa central|27|100|ARG
Lautaro Belleggia|Mediocentro|29|100|ARG
Franco Jara|Delantero centro|38|100|ARG
Luis Gonzalo Castineira|Delantero centro|24|75|ARG
Santiago Flores|Defensa central|25|75|ARG
Valentino Quintero|Portero|23|75|ARG
Ignacio Figueroa|Mediocentro|26|75|ARG
Mariano Bettini|Lateral derecho|30|75|ARG
Felipe Carballo|Mediocentro|30|2500|URU
Matías Vecino|Mediocentro|35|900|URU
Edinson Cavani|Delantero centro|39|500|URU
Erik De Los Santos|Mediocentro|27|450|URU
Gonzalo Carneiro|Delantero centro|31|350|URU
Sebastián Sosa|Delantero centro|32|300|URU
Valentín Rodríguez|Lateral izquierdo|25|200|URU
Randall Rodríguez|Portero|22|200|URU
Santiago Brunelli|Defensa central|28|200|URU
Emiliano Villar|Delantero centro|27|150|URU
Álvaro Villete|Portero|35|100|URU
Mateo Cantera|Lateral derecho|25|100|URU
Erick Cabaco|Defensa central|31|100|URU
Enrique Almeida|Mediapunta|21|75|URU
Gastón Bartora|Lateral izquierdo|24|75|URU
Ariel López|Defensa central|31|75|URU
Facundo Ackermann|Portero|33|75|URU
Matías Rojas|Mediocentro ofensivo|31|1200|PAR
Mario Otazú|Extremo izquierdo|30|300|PAR
Julio Rivas|Delantero centro|26|125|PAR
Pablo Meza|Defensa central|29|100|PAR
Francisco Bareiro|Delantero centro|27|100|PAR
Matías Alfonso|Pivote|26|100|PAR
Richard Sánchez|Mediocentro ofensivo|29|75|PAR
Renato Mencía|Extremo izquierdo|29|50|PAR
Santiago Puzzo|Mediocentro|20|50|PAR
Ronald Acuña|Delantero centro|33|25|PAR
Dánnovi Quiñónes|Pivote|25|700|COL
Jeison Lucumí|Extremo izquierdo|31|450|COL
Nelson Quiñónes|Extremo izquierdo|24|300|COL
Luis Orejuela|Lateral derecho|31|250|COL
Michell Ramos|Delantero centro|28|225|COL
Andrés Aedo|Pivote|22|200|COL
Jeison Mena|Mediocentro ofensivo|24|200|COL
Jhon Altamiranda|Lateral derecho|26|200|COL
Bleiner Agrón|Defensa central|25|200|COL
Camilo Vargas|Pivote|29|175|COL
Geovan Montes|Lateral derecho|30|175|COL
Duván Mosquera|Pivote|28|150|COL
Quelmer Hurtado|Lateral derecho|27|150|COL
Devan Tanton|Lateral derecho|22|150|COL
Benjamín Inostroza|Extremo izquierdo|29|175|CHI
Julio Castro|Delantero centro|30|175|CHI
Ricardo Escobar|Defensa central|28|150|CHI
Sebastián Silva|Defensa central|35|100|CHI
Raúl Osorio|Defensa central|31|100|CHI
Daniel Mansilla|Mediocentro ofensivo|31|100|CHI
Walter Ponce|Delantero centro|28|75|CHI
Mauricio Morales|Pivote|26|75|CHI
Sebastián Julio|Delantero centro|29|75|CHI
Korben Rojas|Lateral izquierdo|26|75|CHI
Matías Ormazábal|Defensa central|28|50|CHI
Darko Fiamengo|Defensa central|23|50|CHI
Marcos Morales|Mediocentro ofensivo|31|25|CHI
"""

FOREIGN = {
    'Boca Juniors': {'Álvaro Montero': 'COL', 'Sebastián Villa': 'COL', 'Ángel Romero': 'PAR', 'Miguel Merentiel': 'URU', 'Enner Valencia': 'ECU', 'Adam Bareiro': 'PAR'},
    'River Plate': {'Mauro Arambarri': 'URU', 'Giovanni González': 'URU', 'Joaquín Freitas': 'URU'},
    'Racing Club': {'Duván Vergara': 'COL'},
    'Estudiantes de La Plata': {'Fernando Muslera': 'URU', 'Gabriel Neves': 'URU', 'Santiago Arzamendia': 'PAR'},
    'San Lorenzo': {'Orlando Gill': 'PAR', 'Danilo Arboleda': 'COL', 'Diego Herazo': 'COL'},
    'Independiente': {'Santiago Arias': 'COL'},
    'Vélez Sarsfield': {'Diego Valdés': 'CHI'},
    'Talleres': {'Diego Valoyes': 'COL', 'Matías Galarza': 'PAR'},
    'Belgrano': {'Alcides Benítez': 'PAR', 'Federico Ricca': 'URU'},
    'Rosario Central': {'Jáminton Campaz': 'COL', 'Vicente Pizarro': 'CHI'},
    "Newell's Old Boys": {'Gabriel Arias': 'CHI', 'Matías Cóccaro': 'URU', 'Saúl Salcedo': 'PAR', 'Oscar Salomón': 'PAR'},
    'Huracán': {'Jordy Caicedo': 'ECU', 'Óscar Romero': 'PAR'},
    'Argentinos Juniors': {'Brayan Cortés': 'CHI'},
    'Unión (Santa Fe)': {'Marcelo Estigarribia': 'PAR'},
    'Instituto (Córdoba)': {'Jhon Córdoba': 'COL'},
    'Atlético Tucumán': {'Clever Ferreira': 'PAR'},
    'Central Córdoba (Santiago del Estero)': {'Michael Santos': 'URU'},
    'Barracas Central': {'Yeison Gordillo': 'COL', 'Rodrigo Bogarín': 'PAR', 'Jhonatan Candia': 'URU'},
    'Sarmiento (Junín)': {'Iván Arboleda': 'COL'},
    'Gimnasia y Esgrima La Plata': {'Nelson Insfrán': 'PAR', 'Enzo Martínez': 'URU'},
    'Banfield': {'Alexander Machado': 'URU'},
}


def rating(value_k, age):
    r = 56 + 2.75 * math.log2(max(value_k, 25) / 25)
    r += 3 if age >= 36 else 2 if age >= 33 else -1 if age <= 22 else 0
    return int(max(52, min(78, round(r))))


def main():
    d = json.load(open(PATH, encoding='utf-8'))
    old = {p['Name']: p for p in d.get('FreeAgents', []) if not p.get('Nationality') == 'x'}
    free = {p['Name']: p for p in d['FreeAgents'] if p['Name'] in old and p.get('Rating')}
    # primera lista (ya cargada, con su media): nacionalidad argentina
    for p in d['FreeAgents']:
        p.setdefault('Nationality', 'ARG')
    keep = {p['Name'] for p in d['FreeAgents']}
    for line in LIST.strip().splitlines():
        name, pos, age, val, nat = line.split('|')
        if name in keep:
            continue
        d['FreeAgents'].append({'Name': name, 'ShortName': '', 'Number': 0, 'Position': POS[pos], 'Rating': rating(int(val), int(age)), 'Age': int(age), 'Nationality': nat})
        keep.add(name)
    # quitar de los planteles a los que figuran como libres (la lista del usuario manda)
    removed = []
    for t in d['Teams']:
        before = len(t['Players'])
        t['Players'] = [p for p in t['Players'] if p['Name'] not in keep]
        if len(t['Players']) != before:
            removed.append(t['Name'])
        for p in t['Players']:
            p['Nationality'] = FOREIGN.get(t['Name'], {}).get(p['Name'], p.get('Nationality') or 'ARG')
    d['Notes'] = re.sub(r' Nacionalidad:.*$', '', d['Notes']) + (' Nacionalidad: la de los libres sale de las capturas del usuario; la de los planteles se completó de memoria para los extranjeros más conocidos (el resto figura como ARG) y puede tener errores.')
    json.dump(d, open(PATH, 'w', encoding='utf-8'), ensure_ascii=False, indent=2)
    print(len(d['FreeAgents']), 'libres; quitados de planteles:', removed)


if __name__ == '__main__':
    main()
