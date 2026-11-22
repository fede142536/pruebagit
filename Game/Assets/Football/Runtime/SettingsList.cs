using UnityEngine;

namespace Football.Game
{
    /// <summary>
    /// Lista de ajustes tocables (una o dos columnas) que muestra un subconjunto del <see cref="SettingsCatalog"/>: una
    /// sección en el menú principal o los ajustes en vivo en la pausa. Tocar una fila pasa al siguiente valor.
    /// </summary>
    public sealed class SettingsList
    {
        private readonly UiButton[] _rows;
        private readonly int[] _items;
        private int _count;

        /// <summary>Crea las filas (ocultas hasta <see cref="Show"/>).</summary>
        /// <param name="parent">Contenedor (coordenadas del área de diseño).</param>
        /// <param name="firstCenter">Centro de la primera fila.</param>
        /// <param name="size">Tamaño de cada fila.</param>
        /// <param name="step">Distancia vertical entre filas.</param>
        /// <param name="rowsPerColumn">Filas por columna.</param>
        /// <param name="columnStep">Distancia horizontal entre columnas.</param>
        /// <param name="columns">Columnas.</param>
        public SettingsList(Transform parent, Vector2 firstCenter, Vector2 size, float step, int rowsPerColumn, float columnStep, int columns)
        {
            _rows = new UiButton[rowsPerColumn * columns];
            _items = new int[SettingsCatalog.Items.Length];
            for (int i = 0; i < _rows.Length; i++)
            {
                var center = new Vector2(firstCenter.x + (i / rowsPerColumn) * columnStep, firstCenter.y - (i % rowsPerColumn) * step);
                _rows[i] = UiKit.Button(parent, "Ajuste" + i, center, size, string.Empty, 26, UiTheme.Button);
                _rows[i].SetVisible(false);
            }
        }

        /// <summary>Muestra los ajustes de una sección (o, si <paramref name="liveOnly"/>, todos los que se pueden cambiar en vivo).</summary>
        public void Show(GameSettings settings, SettingCategory category, bool liveOnly)
        {
            _count = 0;
            for (int i = 0; i < SettingsCatalog.Items.Length && _count < _rows.Length; i++)
            {
                SettingItem item = SettingsCatalog.Items[i];
                if (liveOnly ? item.Live : item.Category == category)
                {
                    _items[_count++] = i;
                }
            }

            Refresh(settings);
        }

        /// <summary>Oculta todas las filas.</summary>
        public void Hide()
        {
            for (int i = 0; i < _rows.Length; i++)
            {
                _rows[i].SetVisible(false);
            }
        }

        /// <summary>Procesa un toque; devuelve verdadero si cambió algún ajuste (ya guardado).</summary>
        public bool Tap(Vector2 point, GameSettings settings)
        {
            for (int i = 0; i < _count; i++)
            {
                if (_rows[i].Contains(point))
                {
                    SettingsCatalog.Items[_items[i]].Next(settings);
                    settings.Save();
                    Refresh(settings);
                    return true;
                }
            }

            return false;
        }

        private void Refresh(GameSettings settings)
        {
            for (int i = 0; i < _rows.Length; i++)
            {
                bool used = i < _count;
                _rows[i].SetVisible(used);
                if (used)
                {
                    _rows[i].Label.text = SettingsCatalog.Items[_items[i]].Text(settings);
                }
            }
        }
    }
}
