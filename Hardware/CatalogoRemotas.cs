using System.Collections.Generic;
using System.Linq;

namespace Genesis.Hardware
{
    /// <summary>
    /// Um cabeçalho de interface de remota ET200 (o "rack" da remota).
    /// </summary>
    internal class RemotaInfo
    {
        public string Nome { get; private set; }
        public string Referencia { get; private set; }
        public int Slots { get; private set; }

        public RemotaInfo(string nome, string referencia, int slots)
        {
            Nome = nome;
            Referencia = referencia;
            Slots = slots;
        }

        public override string ToString() { return Nome + "  (" + Referencia + ")"; }
    }

    internal static class CatalogoRemotas
    {
        public static readonly IReadOnlyList<RemotaInfo> Todos = new[]
        {
            new RemotaInfo("IM 155-6 PN ST", "6ES7 155-6AU02-0BN0", slots: 32),
        };

        public static RemotaInfo PorReferencia(string referencia)
        {
            if (string.IsNullOrEmpty(referencia)) return null;
            return Todos.FirstOrDefault(r => string.Equals(r.Referencia, referencia, System.StringComparison.OrdinalIgnoreCase));
        }
    }
}