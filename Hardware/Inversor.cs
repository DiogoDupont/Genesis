namespace Genesis.Hardware
{
    /// <summary>
    /// Um inversor de frequência no projeto. Inclui o que é necessário para a topologia PROFINET
    /// (GSDML, telegrama, posição) e para amarrar na partida correspondente.
    /// </summary>
    public class Inversor
    {
        /// <summary>Nome PROFINET do inversor (ex.: "inv_fl1_mic1"). Único no projeto.</summary>
        public string Tag { get; set; } = "";

        /// <summary>Modelo do catálogo (ex.: "G120", "G120C", "G120X").</summary>
        public string Modelo { get; set; } = "";

        /// <summary>Arquivo GSDML associado ao modelo (do catálogo). Ex.: "gsdml-v2.31-siemens-sinamics_g120-20221115.xml".</summary>
        public string Gsdml { get; set; } = "";

        /// <summary>Telegrama (ex.: "352" = PZD-6/6).</summary>
        public string Telegrama { get; set; } = "352";

        /// <summary>Endereço IP na rede PROFINET (ex.: "192.168.0.10").</summary>
        public string IpAddress { get; set; } = "";

        /// <summary>Posição na topologia PROFINET (ordem física no cabo). 0 = ainda não definido.</summary>
        public int PosicaoTopologia { get; set; }

        /// <summary>Nome PROFINET (device name) configurado no TIA. Normalmente igual a Tag.</summary>
        public string NomeProfinet { get; set; } = "";

        /// <summary>Sub/Eq/Fn da partida associada (ex.: "FL1|MIC|BB1"), ou vazio se é inversor avulso.</summary>
        public string PartidaAssociada { get; set; } = "";
    }
}