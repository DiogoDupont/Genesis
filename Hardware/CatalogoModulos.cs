using System.Collections.Generic;
using System.Linq;

namespace Genesis.Hardware
{
    /// <summary>
    /// Um módulo de IO do catálogo. Imutável — descreve o hardware real.
    /// </summary>
    internal class ModuloInfo
    {
        public string Nome { get; private set; }
        public string Referencia { get; private set; }
        /// <summary>"SB" (Signal Board — dentro do CLP), "SM" (Signal Module — rack do CLP), "ET200" (remota).</summary>
        public string Familia { get; private set; }
        public int Di { get; private set; }
        public int Dq { get; private set; }
        public int Ai { get; private set; }
        public int Aq { get; private set; }

        public ModuloInfo(string nome, string referencia, string familia, int di, int dq, int ai, int aq)
        {
            Nome = nome;
            Referencia = referencia;
            Familia = familia;
            Di = di;
            Dq = dq;
            Ai = ai;
            Aq = aq;
        }

        public int TotalCanais { get { return Di + Dq + Ai + Aq; } }

        public override string ToString()
        {
            return Nome + "  (" + Referencia + ")  — DI" + Di + "/DQ" + Dq + "/AI" + Ai + "/AQ" + Aq;
        }
    }

    /// <summary>
    /// Catálogo de módulos suportados. Fixo no código.
    /// </summary>
    internal static class CatalogoModulos
    {
        /// <summary>Módulos para S7-1200 G2 (SB e SM).</summary>
        public static readonly IReadOnlyList<ModuloInfo> G2 = new[]
        {
            // Signal Boards (dentro do CLP, não ocupam slot)
            new ModuloInfo("SB DI8 x 24VDC",     "6ES7 221-3BF50-0XB0", "SB", 8, 0, 0, 0),
            new ModuloInfo("SB DQ8 x 24VDC",     "6ES7 222-5BF50-0XB0", "SB", 0, 8, 0, 0),
            new ModuloInfo("SB DI4/DQ4 x 24VDC", "6ES7 223-7BF50-0XB0", "SB", 4, 4, 0, 0),
            new ModuloInfo("SB AI4 x 14bit",     "6ES7 231-4HD50-0XB0", "SB", 0, 0, 4, 0),
            new ModuloInfo("SB AQ4 x 14bit",     "6ES7 232-4HD50-0XB0", "SB", 0, 0, 0, 4),
            new ModuloInfo("SB AI2/AQ2 x 14bit", "6ES7 233-4HD50-0XB0", "SB", 0, 0, 2, 2),

            // Signal Modules (rack do CLP, ocupam slot)
            new ModuloInfo("SM 1221 DI 16x24VDC",     "6ES7 221-1BH50-0XB0", "SM", 16, 0, 0, 0),
            new ModuloInfo("SM 1222 DQ 16x24VDC",     "6ES7 222-5BH50-0XB0", "SM", 0, 16, 0, 0),
            new ModuloInfo("SM 1223 DI8/DQ8 x 24VDC", "6ES7 223-5BH50-0XB0", "SM", 8, 8, 0, 0),
            new ModuloInfo("SM 1231 AI 8x14bit",      "6ES7 231-4HF50-0XB0", "SM", 0, 0, 8, 0),
            new ModuloInfo("SM 1232 AQ 8x14bit",      "6ES7 232-4HF50-0XB0", "SM", 0, 0, 0, 8),
            new ModuloInfo("SM 1233 AI4/AQ4 x 14bit", "6ES7 233-4HF50-0XB0", "SM", 0, 0, 4, 4),
        };

        /// <summary>Módulos para remota ET200 (IM 155-6 PN ST).</summary>
        public static readonly IReadOnlyList<ModuloInfo> Et200 = new[]
        {
            new ModuloInfo("ET200 DI 16x24VDC ST",      "6ES7 131-6BH01-0BA0", "ET200", 16, 0, 0, 0),
            new ModuloInfo("ET200 DQ 16x24VDC/0.5A BA", "6ES7 132-6BH00-0AA0", "ET200", 0, 16, 0, 0),
            new ModuloInfo("ET200 AI 8xI 2-/4-wire BA", "6ES7 134-6GF00-0AA1", "ET200", 0, 0, 8, 0),
            new ModuloInfo("ET200 AQ 4xU/I ST",         "6ES7 135-6HD00-0BA1", "ET200", 0, 0, 0, 4),
        };

        /// <summary>Todos os módulos (G2 + ET200), útil para busca por referência.</summary>
        public static IEnumerable<ModuloInfo> Todos
        {
            get { return G2.Concat(Et200); }
        }

        public static ModuloInfo PorReferencia(string referencia)
        {
            if (string.IsNullOrEmpty(referencia)) return null;
            return Todos.FirstOrDefault(m => string.Equals(m.Referencia, referencia, System.StringComparison.OrdinalIgnoreCase));
        }
    }
}