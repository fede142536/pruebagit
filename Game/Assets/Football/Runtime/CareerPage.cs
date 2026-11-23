using System;
using Football.Core.Career;
using Football.Core.Data;
using Football.Core.Domain;
using UnityEngine;

namespace Football.Game
{
    /// <summary>Secciones de la partida de carrera (barra de abajo y subpantallas).</summary>
    public enum CareerSection
    {
        /// <summary>Central: próximo partido, calendario, tabla y buzón.</summary>
        Hub,
        /// <summary>Plantilla y táctica.</summary>
        Squad,
        /// <summary>Mercado y ojeo.</summary>
        Market,
        /// <summary>Oficina y objetivos.</summary>
        Office,
        /// <summary>Finanzas y entradas.</summary>
        Finance,
        /// <summary>Remodelación del estadio (se abre desde Finanzas).</summary>
        Stadium,
        /// <summary>Once inicial y banco (se abre desde Plantilla).</summary>
        Lineup,
        /// <summary>Calendario de todos los partidos del club, jugados y por jugar (se abre desde la Central).</summary>
        Calendar,
        /// <summary>Historial de las competiciones jugadas (se abre desde la Central).</summary>
        History,
        /// <summary>Contratos del plantel y renovaciones (se abre desde Plantilla).</summary>
        Contracts
    }

    /// <summary>Lo que una pantalla de carrera necesita del contenedor: la partida, la línea de estado y la navegación.</summary>
    public interface ICareerHost
    {
        /// <summary>Partida en curso (null antes de empezar).</summary>
        CareerSession Session { get; }

        /// <summary>Mensaje en la línea de estado.</summary>
        void SetStatus(string text);

        /// <summary>Pide una acción al controlador (jugar, empezar, cargar, salir).</summary>
        void Request(CareerAction action);

        /// <summary>Abre otra sección.</summary>
        void Open(CareerSection section);

        /// <summary>Texto de una noticia del buzón, con los nombres ya resueltos.</summary>
        string NewsText(NewsItem item);

        /// <summary>Entrada (punteros, teclado y texto escrito).</summary>
        PointerInput Input { get; }
    }

    /// <summary>
    /// Base de cada pantalla de la carrera: un grupo propio dentro del lienzo del contenedor, que se arma una vez y solo
    /// cambia textos al refrescar (nada se crea por fotograma). Sin lógica de juego: lee y pide todo a <see cref="CareerSession"/>.
    /// </summary>
    public abstract class CareerPage
    {
        /// <summary>Raíz de la pantalla.</summary>
        protected readonly Transform Panel;
        /// <summary>Contenedor.</summary>
        protected readonly ICareerHost Host;

        /// <summary>Crea el grupo (oculto).</summary>
        /// <param name="root">Área de diseño del contenedor.</param>
        /// <param name="name">Nombre del grupo.</param>
        /// <param name="host">Contenedor.</param>
        protected CareerPage(Transform root, string name, ICareerHost host)
        {
            Host = host;
            Panel = CreateGroup(root, name);
            Panel.gameObject.SetActive(false);
        }

        /// <summary>Partida.</summary>
        protected CareerSession Session => Host.Session;

        /// <summary>Datos de la partida.</summary>
        protected CareerData Data => Host.Session.Data;

        /// <summary>Está a la vista.</summary>
        public bool Visible => Panel.gameObject.activeSelf;

        /// <summary>Muestra u oculta la pantalla (al mostrarla, la refresca).</summary>
        public virtual void SetVisible(bool visible)
        {
            Panel.gameObject.SetActive(visible);
            if (visible)
            {
                Refresh();
            }
        }

        /// <summary>Vuelve a escribir los textos con el estado actual.</summary>
        public abstract void Refresh();

        /// <summary>Procesa un toque (coordenadas del área de diseño).</summary>
        public abstract void Tap(Vector2 p);

        /// <summary>Trabajo por fotograma (animaciones o teclado); por defecto nada.</summary>
        public virtual void Tick(float deltaTime)
        {
        }

        /// <summary>Crea un grupo vacío del tamaño del área de diseño.</summary>
        public static Transform CreateGroup(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = UiKit.Reference;
            return go.transform;
        }

        /// <summary>Definición del archivo de equipos para un club (escudo y camisetas), o una mínima si no está.</summary>
        public static TeamDefinition TeamOf(Club club)
        {
            TeamDefinition found = Array.Find(TeamCatalog.AllTeams, t => t.Name == club.Name);
            return found ?? new TeamDefinition { Name = club.Name, ShortName = club.ShortName };
        }

        /// <summary>Media del club en el catálogo (para las estrellas), o 0 si no está.</summary>
        public static float RatingOf(Club club)
        {
            int i = Array.FindIndex(TeamCatalog.AllTeams, t => t.Name == club.Name);
            return i >= 0 ? TeamCatalog.RatingAll(i) : 0f;
        }
    }
}
