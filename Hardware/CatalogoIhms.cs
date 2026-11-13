using System.Collections.Generic;
using System.Linq;

namespace Genesis.Hardware
{
    /// <summary>
    /// Um modelo de IHM do catálogo (linha MTP Unified, Basic ou Comfort).
    /// </summary>
    internal class IhmInfo
    {
        public string Modelo { get; private set; }
        public string Referencia { get; private set; }
        /// <summary>"Basic" ou "Comfort".</summary>
        public string Linha { get; private set; }
        public int Polegadas { get; private set; }

        public IhmInfo(string modelo, string referencia, string linha, int polegadas)
        {
            Modelo = modelo;
            Referencia = referencia;
            Linha = linha;
            Polegadas = polegadas;
        }

        public override string ToString() { return Modelo + "  (" + Polegadas + "\", " + Linha + ")"; }
    }

    /// <summary>
    /// Catálogo de IHMs suportadas (linha MTP Unified Basic e Comfort).
    /// </summary>
    internal static class CatalogoIhms
    {
        public static readonly IReadOnlyList<IhmInfo> Todos = new[]
        {
            // MTP Unified Basic
            new IhmInfo("MTP700 Unified Basic",  "6AV2 123-3GB32-0AW0", "Basic",   7),
            new IhmInfo("MTP1000 Unified Basic", "6AV2 123-3KB32-0AW0", "Basic",  10),
            new IhmInfo("MTP1200 Unified Basic", "6AV2 123-3MB32-0AW0", "Basic",  12),

            // MTP Unified Comfort
            new IhmInfo("MTP700 Unified Comfort",  "6AV2 128-3GB06-0AXx", "Comfort", 7),
            new IhmInfo("MTP1000 Unified Comfort", "6AV2 128-3KB06-0AXx", "Comfort", 10),
            new IhmInfo("MTP1200 Unified Comfort", "6AV2 128-3MB06-0AXx", "Comfort", 12),
        };

        public static IhmInfo PorReferencia(string referencia)
        {
            if (string.IsNullOrEmpty(referencia)) return null;
            return Todos.FirstOrDefault(i => string.Equals(i.Referencia, referencia, System.StringComparison.OrdinalIgnoreCase));
        }

        public static IhmInfo PorModelo(string modelo)
        {
            if (string.IsNullOrEmpty(modelo)) return null;
            return Todos.FirstOrDefault(i => string.Equals(i.Modelo, modelo, System.StringComparison.OrdinalIgnoreCase));
        }
    }
}