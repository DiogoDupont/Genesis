namespace Genesis.Hardware
{
    /// <summary>
    /// Uma IHM no projeto (linha MTP Unified Basic ou Comfort).
    /// </summary>
    public class Ihm
    {
        /// <summary>Nome da IHM (ex.: "HMI_FL1").</summary>
        public string Tag { get; set; } = "";

        /// <summary>Referência Siemens (ex.: "6AV2 123-3KB32-0AW0"). Chave de busca no catálogo.</summary>
        public string Referencia { get; set; } = "";

        /// <summary>Endereço IP na rede PROFINET.</summary>
        public string IpAddress { get; set; } = "";

        // Hardware/Ihm.cs — trocar a propriedade Protocolo por:
        public string Protocolo { get; set; } = "S7";   // hoje só S7; mantido como campo para futuro
    }
}