using System.Collections.Generic;
using System.Linq;

namespace Genesis.Hardware
{
    /// <summary>
    /// Um modelo de CLP do catálogo. Imutável — é dado fixo que descreve o hardware real.
    /// </summary>
    internal class ClpInfo
    {
        public string Nome { get; private set; }
        public string Referencia { get; private set; }
        public int DiOnboard { get; private set; }
        public int DqOnboard { get; private set; }
        public int AiOnboard { get; private set; }
        public int AqOnboard { get; private set; }
        public bool Profinet { get; private set; }

        public ClpInfo(string nome, string referencia, int di, int dq, int ai, int aq, bool profinet)
        {
            Nome = nome;
            Referencia = referencia;
            DiOnboard = di;
            DqOnboard = dq;
            AiOnboard = ai;
            AqOnboard = aq;
            Profinet = profinet;
        }

        public override string ToString() { return Nome + "  (" + Referencia + ")"; }
    }

    /// <summary>
    /// Catálogo de CLPs suportados. Fixo no código — se um dia entrar um CLP novo,
    /// é só adicionar uma linha aqui (não precisa mexer em mais nada).
    /// </summary>
    internal static class CatalogoClp
    {
        public static readonly IReadOnlyList<ClpInfo> Todos = new[]
        {
            new ClpInfo("S7-1200 G2 1212C", "6ES7 212-1AG50-0XB0", di: 8,  dq: 6,  ai: 2, aq: 0, profinet: true),
            new ClpInfo("S7-1200 G2 1214C", "6ES7 214-1AH50-0XB0", di: 14, dq: 10, ai: 2, aq: 0, profinet: true),
            new ClpInfo("S7-1500 1511-1 PN", "6ES7 511-1AL03-0AB0", di: 0,  dq: 0,  ai: 0, aq: 0, profinet: true),
        };

        public static ClpInfo PorReferencia(string referencia)
        {
            if (string.IsNullOrEmpty(referencia)) return null;
            return Todos.FirstOrDefault(c => string.Equals(c.Referencia, referencia, System.StringComparison.OrdinalIgnoreCase));
        }

        public static ClpInfo PorNome(string nome)
        {
            if (string.IsNullOrEmpty(nome)) return null;
            return Todos.FirstOrDefault(c => string.Equals(c.Nome, nome, System.StringComparison.OrdinalIgnoreCase));
        }
    }
}