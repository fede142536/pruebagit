using System;
using System.Collections.Generic;
using System.Globalization;
using Football.Core.Common;
using Football.Core.Domain;

namespace Football.Core.Management
{
    /// <summary>
    /// Buzón de noticias: escucha el bus (lesiones, sanciones, obras, juveniles, traspasos, promesas, récords y retiros) y
    /// guarda noticias con una clave de texto y sus datos. El texto lo arma la interfaz con los archivos de idioma
    /// (<c>Data/locale/news.es.json</c> y <c>news.en.json</c>); el núcleo no tiene frases incrustadas.
    /// </summary>
    public sealed class Inbox
    {
        private readonly List<NewsItem> _items = new List<NewsItem>();
        private readonly Func<DateTime> _today;
        private readonly int _clubId;
        private readonly Func<int, int?> _clubOfPlayer;

        /// <summary>Crea el buzón de un club y lo suscribe al bus.</summary>
        /// <param name="clubId">Club del usuario.</param>
        /// <param name="bus">Bus.</param>
        /// <param name="today">Fecha actual.</param>
        /// <param name="clubOfPlayer">Club de un jugador (para filtrar lo que le interesa).</param>
        public Inbox(int clubId, EventBus bus, Func<DateTime> today, Func<int, int?> clubOfPlayer)
        {
            _clubId = clubId;
            _today = today;
            _clubOfPlayer = clubOfPlayer;
            bus.Subscribe<PlayerInjured>(e => AddIfMine(e.PlayerId, "news.injury", e.PlayerId.ToString(CultureInfo.InvariantCulture), e.Days.ToString(CultureInfo.InvariantCulture)));
            bus.Subscribe<PlayerSuspended>(e => AddIfMine(e.PlayerId, "news.suspension", e.PlayerId.ToString(CultureInfo.InvariantCulture), e.Matches.ToString(CultureInfo.InvariantCulture)));
            bus.Subscribe<PromiseBroken>(e => AddIfClub(e.ClubId, "news.promise_broken", e.PlayerId.ToString(CultureInfo.InvariantCulture)));
            bus.Subscribe<FacilityUpgraded>(e => AddIfClub(e.ClubId, "news.facility_upgraded", e.Kind.ToString(), e.Level.ToString(CultureInfo.InvariantCulture)));
            bus.Subscribe<YouthIntakeArrived>(e => AddIfClub(e.ClubId, "news.youth_intake", e.Count.ToString(CultureInfo.InvariantCulture), e.BestPotential.ToString(CultureInfo.InvariantCulture)));
            bus.Subscribe<TransferCompleted>(e =>
            {
                if (e.BuyerClubId == _clubId || e.SellerClubId == _clubId)
                {
                    Add(e.BuyerClubId == _clubId ? "news.transfer_in" : "news.transfer_out", e.PlayerId.ToString(CultureInfo.InvariantCulture), e.Fee.ToString(CultureInfo.InvariantCulture));
                }
            });
            bus.Subscribe<RecordBroken>(e => Add("news.record", e.Key, e.HolderId.ToString(CultureInfo.InvariantCulture), e.Value.ToString(CultureInfo.InvariantCulture)));
            bus.Subscribe<PlayerRetired>(e => Add(e.StaffId.HasValue ? "news.retired_to_staff" : "news.retired", e.PlayerId.ToString(CultureInfo.InvariantCulture)));
        }

        /// <summary>Noticias, de la más nueva a la más vieja.</summary>
        public IReadOnlyList<NewsItem> Items
        {
            get
            {
                var copy = new List<NewsItem>(_items);
                copy.Reverse();
                return copy;
            }
        }

        /// <summary>Noticias sin leer.</summary>
        public int Unread => _items.FindAll(i => !i.Read).Count;

        /// <summary>Arma el texto de una noticia con una plantilla («{0} se lesionó: {1} días»).</summary>
        /// <param name="item">Noticia.</param>
        /// <param name="templates">Plantillas del idioma elegido (clave → texto).</param>
        public static string Format(NewsItem item, IReadOnlyDictionary<string, string> templates) =>
            templates.TryGetValue(item.Key, out string template) ? string.Format(CultureInfo.InvariantCulture, template, item.Args) : item.Key;

        /// <summary>Publica una noticia propia del orquestador (informe de ojeador, oferta recibida, fin de temporada...).</summary>
        /// <param name="key">Clave del texto en <c>Data/locale/news.*.json</c>.</param>
        /// <param name="args">Datos que se insertan en el texto.</param>
        public void Post(string key, params string[] args) => Add(key, args);

        /// <summary>Noticias en orden de llegada (la más vieja primero), para guardarlas.</summary>
        public IReadOnlyList<NewsItem> Chronological => _items;

        /// <summary>Reemplaza el buzón por noticias guardadas (en orden de llegada).</summary>
        /// <param name="items">Noticias.</param>
        public void Restore(IEnumerable<NewsItem> items)
        {
            _items.Clear();
            _items.AddRange(items);
        }

        /// <summary>Marca todas las noticias como leídas.</summary>
        public void MarkAllRead() => _items.ForEach(i => i.Read = true);

        private void AddIfMine(int playerId, string key, params string[] args)
        {
            if (_clubOfPlayer(playerId) == _clubId)
            {
                Add(key, args);
            }
        }

        private void AddIfClub(int clubId, string key, params string[] args)
        {
            if (clubId == _clubId)
            {
                Add(key, args);
            }
        }

        private void Add(string key, params string[] args) =>
            _items.Add(new NewsItem { Date = _today(), ClubId = _clubId, Key = key, Args = args });
    }
}
