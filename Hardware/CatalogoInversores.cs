using System.Collections.Generic;
using System.Linq;

namespace Genesis.Hardware
{
    /// <summary>
    /// Um modelo de inversor do catálogo (com o GSDML e o telegrama padrão que vocês usam).
    /// </summary>
    internal class InversorInfo
    {
        public string Modelo { get; private set; }
        public string Gsdml { get; private set; }
        public string TelegramaPadrao { get; private set; }

        public InversorInfo(string modelo, string gsdml, string telegramaPadrao)
        {
            Modelo = modelo;
            Gsdml = gsdml;
            TelegramaPadrao = telegramaPadrao;
        }

        public override string ToString() { return Modelo + "  (" + TelegramaPadrao + ")"; }
    }

    /// <summary>
    /// Catálogo de inversores suportados. Fixo no código.
    /// </summary>
    internal static class CatalogoInversores
    {
        public static readonly IReadOnlyList<InversorInfo> Todos = new[]
        {
            new InversorInfo("G120",  "gsdml-v2.31-siemens-sinamics_g120-20221115.xml",  "352"),
            new InversorInfo("G120C", "gsdml-v2.31-siemens-sinamics_g120c-20221115.xml", "352"),
            new InversorInfo("G120X", "gsdml-v2.34-siemens-sinamics_g120x-20190426.xml", "352"),
        };

        public static InversorInfo PorModelo(string modelo)
        {
            if (string.IsNullOrEmpty(modelo)) return null;
            return Todos.FirstOrDefault(i => string.Equals(i.Modelo, modelo, System.StringComparison.OrdinalIgnoreCase));
        }
    }
}