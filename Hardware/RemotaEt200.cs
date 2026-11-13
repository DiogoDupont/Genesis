using System.Collections.Generic;

namespace Genesis.Hardware
{
    /// <summary>
    /// Uma remota ET200 (ex.: IM 155-6 PN ST + módulos de IO).
    /// </summary>
    public class RemotaEt200
    {
        /// <summary>Nome da remota PROFINET (ex.: "et200_poço").</summary>
        public string Tag { get; set; } = "";

        /// <summary>Referência do cabeçalho de interface (ex.: "6ES7 155-6AU02-0BN0" = IM 155-6 PN ST).</summary>
        public string ReferenciaInterface { get; set; } = "";

        /// <summary>Endereço IP na rede PROFINET.</summary>
        public string IpAddress { get; set; } = "";

        /// <summary>Posição na topologia PROFINET.</summary>
        public int PosicaoTopologia { get; set; }

        public List<Modulo> Modulos { get; set; } = new List<Modulo>();
    }
}