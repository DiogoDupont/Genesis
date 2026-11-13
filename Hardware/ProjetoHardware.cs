using System.Collections.Generic;

namespace Genesis.Hardware
{
    /// <summary>
    /// Hardware do projeto: CLP, módulos, inversores, IHMs e remotas ET200.
    /// Projetos antigos (.genesis antes da Fase B) abrem com este objeto null — as receitas
    /// tratam isso como "via terminais" (comportamento antigo).
    /// </summary>
    public class ProjetoHardware
    {
        /// <summary>Modelo do CLP (ex.: "S7-1200 G2 1214C").</summary>
        public string ModeloClp { get; set; } = "";

        /// <summary>Referência Siemens do CLP (ex.: "6ES7 214-1AH50-0XB0").</summary>
        public string ReferenciaClp { get; set; } = "";

        /// <summary>true = projeto via PROFINET; false = via terminais (fiação direta).</summary>
        public bool ViaRede { get; set; }

        public List<Modulo> Modulos { get; set; } = new List<Modulo>();
        public List<Inversor> Inversores { get; set; } = new List<Inversor>();
        public List<Ihm> Ihs { get; set; } = new List<Ihm>();
        public List<RemotaEt200> RemotasEt200 { get; set; } = new List<RemotaEt200>();

        /// <summary>Quantidade de canais disponíveis no CLP, somando onboard + módulos + remotas.</summary>
        public (int Di, int Dq, int Ai, int Aq) TotalCanais()
        {
            // onboard é preenchido pela PaginaHardware quando o CLP for escolhido;
            // aqui só somamos módulos e remotas (o onboard entra como um "modulo virtual" na UI)
            int di = 0, dq = 0, ai = 0, aq = 0;
            foreach (Modulo m in Modulos) { di += m.Di; dq += m.Dq; ai += m.Ai; aq += m.Aq; }
            foreach (RemotaEt200 r in RemotasEt200)
            {
                foreach (Modulo m in r.Modulos) { di += m.Di; dq += m.Dq; ai += m.Ai; aq += m.Aq; }
            }
            return (di, dq, ai, aq);
        }
    }
}