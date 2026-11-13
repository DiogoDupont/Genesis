namespace Genesis.Hardware
{
    /// <summary>
    /// Um módulo de IO (SM, SB, ou módulo de remota ET200).
    /// Não guarda "modelo" nem "referência" como string livre — a referência vem do catálogo
    /// (CatalogoModulos), e o módulo só aponta para qual entrada do catálogo ele é + quantidade.
    /// </summary>
    public class Modulo
    {
        /// <summary>Referência Siemens do catálogo (ex.: "6ES7 221-1BH50-0XB0"). Chave de busca no catálogo.</summary>
        public string Referencia { get; set; } = "";

        /// <summary>Quantidade de canais DI neste módulo (vem do catálogo, duplicado aqui para o TotalCanais ficar barato).</summary>
        public int Di { get; set; }

        public int Dq { get; set; }
        public int Ai { get; set; }
        public int Aq { get; set; }

        /// <summary>Quantas unidades deste módulo estão instaladas.</summary>
        public int Quantidade { get; set; } = 1;
    }
}