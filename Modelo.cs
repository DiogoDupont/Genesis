using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Serialization;

namespace Genesis
{
    // ===================================================================
    //  Modelo de dados
    // ===================================================================
    public class Io
    {
        /// <summary>Areas de endereco: entrada/saida digital, entrada/saida analogica, memoria e livre.</summary>
        public static readonly string[] Areas = { "DI", "DO", "AI", "AO", "MEM", "OUTRO" };

        public string Nome { get; set; } = "";
        public string Tipo { get; set; } = "Bool";
        public string Descricao { get; set; } = "";

        /// <summary>DI (%I x.y), DO (%Q x.y), AI (%IW n), AO (%QW n), MEM (%M x.y ou %MW n) ou OUTRO.</summary>
        public string Area { get; set; } = "DI";

        /// <summary>So o numero: "0.2" (bit) ou "100" (palavra). Em OUTRO, o endereco completo.</summary>
        public string Numero { get; set; } = "";

        /// <summary>Endereco completo (%I0.2, %QW100...), montado a partir de Area + Numero.</summary>
        [XmlIgnore]
        public string Endereco
        {
            get { return MontarEndereco(Area, Numero); }
            set { DefinirEndereco(value); }
        }

        /// <summary>So para ler arquivos antigos, que guardavam o endereco completo.</summary>
        [XmlElement("Endereco")]
        public string EnderecoLegado
        {
            get { return null; }
            set { if (!string.IsNullOrWhiteSpace(value)) DefinirEndereco(value); }
        }

        [XmlIgnore]
        public int Linha { get; set; }

        private static readonly Regex RxI = new Regex(@"^I(\d+\.[0-7])$");
        private static readonly Regex RxQ = new Regex(@"^Q(\d+\.[0-7])$");
        private static readonly Regex RxIW = new Regex(@"^IW(\d+)$");
        private static readonly Regex RxQW = new Regex(@"^QW(\d+)$");
        private static readonly Regex RxM = new Regex(@"^M(\d+\.[0-7])$");
        private static readonly Regex RxMW = new Regex(@"^MW(\d+)$");

        public static string MontarEndereco(string area, string numero)
        {
            string n = (numero ?? "").Trim().Replace(" ", "").Replace(',', '.').ToUpperInvariant();
            if (n.Length == 0) return "";

            switch (area)
            {
                case "DI": return "%I" + n;
                case "DO": return "%Q" + n;
                case "AI": return "%IW" + n;
                case "AO": return "%QW" + n;
                case "MEM": return n.Contains(".") ? "%M" + n : "%MW" + n;
                default: return n.StartsWith("%") ? n : "%" + n;
            }
        }

        /// <summary>Separa um endereco completo em Area + Numero (o que nao se encaixa vira OUTRO).</summary>
        public void DefinirEndereco(string endereco)
        {
            string t = (endereco ?? "").Trim().Replace(" ", "").ToUpperInvariant();
            if (t.StartsWith("%")) t = t.Substring(1);

            if (t.Length == 0) { Numero = ""; return; }

            Match m;
            if ((m = RxI.Match(t)).Success) { Area = "DI"; Numero = m.Groups[1].Value; }
            else if ((m = RxQ.Match(t)).Success) { Area = "DO"; Numero = m.Groups[1].Value; }
            else if ((m = RxIW.Match(t)).Success) { Area = "AI"; Numero = m.Groups[1].Value; }
            else if ((m = RxQW.Match(t)).Success) { Area = "AO"; Numero = m.Groups[1].Value; }
            else if ((m = RxM.Match(t)).Success) { Area = "MEM"; Numero = m.Groups[1].Value; }
            else if ((m = RxMW.Match(t)).Success) { Area = "MEM"; Numero = m.Groups[1].Value; }
            else { Area = "OUTRO"; Numero = "%" + t; }
        }

        /// <summary>Tipo mais provavel para a area (DI/DO = Bool, AI/AO = Int; memoria: bit = Bool, palavra = Int).</summary>
        public static string TipoPadrao(string area, string numero, string tipoAtual)
        {
            string n = (numero ?? "").Trim();
            switch (area)
            {
                case "DI":
                case "DO":
                    return "Bool";
                case "AI":
                case "AO":
                    return "Int";
                case "MEM":
                    if (n.Length == 0) return string.IsNullOrEmpty(tipoAtual) ? "Bool" : tipoAtual;
                    if (n.Contains(".") || n.Contains(",")) return "Bool";
                    return (string.IsNullOrEmpty(tipoAtual) || tipoAtual == "Bool") ? "Int" : tipoAtual;
                default:
                    return string.IsNullOrEmpty(tipoAtual) ? "Bool" : tipoAtual;
            }
        }

        /// <summary>Null se o numero serve para a area; senao, o que esta errado.</summary>
        public string ErroNumero()
        {
            string n = (Numero ?? "").Trim().Replace(',', '.');
            if (n.Length == 0) return "sem numero";

            switch (Area)
            {
                case "DI":
                case "DO":
                    return Regex.IsMatch(n, @"^\d+\.[0-7]$") ? null : "numero '" + n + "' invalido para " + Area + " (use byte.bit, ex.: 0.2)";
                case "AI":
                case "AO":
                    return Regex.IsMatch(n, @"^\d+$") ? null : "numero '" + n + "' invalido para " + Area + " (use so o endereco em bytes, ex.: 100)";
                case "MEM":
                    return Regex.IsMatch(n, @"^\d+(\.[0-7])?$") ? null : "numero '" + n + "' invalido para memoria (ex.: 10.0 ou 20)";
                default:
                    return Regex.IsMatch(n, @"^%?[IQM][XBWD]?\d+(\.[0-7])?$", RegexOptions.IgnoreCase) ? null
                        : "endereco '" + n + "' invalido (ex.: %QD100)";
            }
        }
    }

    public class Partida
    {
        public string Sub { get; set; } = "";
        public string Eq { get; set; } = "";
        public string Fn { get; set; } = "";
        public string Tipo { get; set; } = "Partida direta";
        public string Descricao { get; set; } = "";
        public string TagConf { get; set; } = "";
        public string TagRef { get; set; } = "";
        public string TagHab { get; set; } = "";   // saida "habilita" do drive/contator (bobina espelhando LEI_HAB)
        public string TagDef { get; set; } = "";   // espelho de LEI_DEF (vazio = nome automatico)

        public string TagHabC1 { get; set; } = "";     // bypass: saida de habilita do contator 1 (MOT_CMD_HA1)
        public string TagHabC2 { get; set; } = "";     // bypass: saida de habilita do contator 2 (MOT_CMD_HA2)
        public string TagDefDrive { get; set; } = "";  // inversor/soft: IO de defeito do drive (entra negada em MOT_LEI_TRP)
        public string TagConfFec { get; set; } = "";   // valvula 2 DI: confirmacao fechada (VAL_CNF_FEC)
        public string TagFec { get; set; } = "";       // valvula 2 DO: saida "fecha" (bobina de LEI_FEC)
        public string TagPosAi { get; set; } = "";     // valvula 1AI: entrada analogica de posicao
        public string TagSensor { get; set; } = "";    // sensor: entrada digital (nivel) ou analogica
        public string TagPulso { get; set; } = "";     // medidor de vazao: entrada digital de pulso do totalizador
        public string TagSensor2 { get; set; } = "";   // pas do raspador: sensor do lado esquerdo (o direito vai em TagSensor)

        /// <summary>Aba onde a linha esta ("Partidas" ou "Sensores"), para as mensagens (nao e salva).</summary>
        [XmlIgnore]
        public string Aba { get; set; }

        /// <summary>Caixinha "Gerar" da tela: usada pelo "Gerar marcadas" (nao e salva no arquivo).</summary>
        [XmlIgnore]
        public bool Marcada { get; set; }

        public bool Auto { get; set; }              // operacao automatica (Set/Reset de CMD_LIG)
        public bool Intertravamento { get; set; }   // bobina CMD_INT com contato de condicao em aberto
        public bool Alternancia { get; set; }       // alternancia com a outra bomba do par (mesmo equipamento)

        /// <summary>Intermitente (BL_INTERMITENTE): unidade do tempo LIGADO ("" = nao tem; "Segundos", "Minutos" ou "Horas").</summary>
        public string Intermitente { get; set; } = "";

        /// <summary>Intermitente: unidade do tempo DESLIGADO ("" = a mesma do tempo ligado).</summary>
        public string IntermitenteDes { get; set; } = "";

        /// <summary>
        /// Modulacao pela vazao: medidor (com modulacao de quimicos) que comanda a bomba, como "SEN.FL1.VAZ"
        /// (DB.grupo.sensor da aba Sensores). Vazio = nao modula. Liga SIS_CMD_AUT = CMD_AUT e SIS_LEI_VAZ = LEI_VQP.
        /// </summary>
        public string ModVazao { get; set; } = "";

        /// <summary>
        /// Modulacao por PID: PID (aba PIDs) que comanda a velocidade, como "FL1.ALC.PID" (DB.grupo.nome).
        /// Vazio = nao modula. Varios acionamentos podem apontar para o mesmo PID.
        /// </summary>
        public string ModPid { get; set; } = "";

        /// <summary>PID: inverte a ação do controle (PID_Compact Config.InvertControl). Ex.: bomba de ácido que baixa o pH.</summary>
        public bool PidInverso { get; set; }

        /// <summary>Protecao do equipamento (DI, contato NF): abrindo, seta a memoria de defeito (reset geral reseta).</summary>
        public string TagProtecao { get; set; } = "";

        /// <summary>Memoria de defeito da protecao ("" = SUB_EQ_FN_LEI_PRT).</summary>
        public string TagProtecaoMem { get; set; } = "";

        /// <summary>Exaustor da cabine (sopradores): BL_PARTIDA_HIBRIDA proprio, defeito desliga o soprador.</summary>
        public bool Exaustor { get; set; }

        /// <summary>Maquina a que pertence (id = DB da maquina, ex.: PP1); vazio = partida avulsa ou periférico.</summary>
        public string Maquina { get; set; } = "";

        /// <summary>Tela da IHM a que esta partida pertence (ex: TL1, TL2). Usada no Fim de Expediente.</summary>
        public string Tela { get; set; } = "TL1";

        /// <summary>
        /// Subsistema a que pertence (id = DB do subsistema, ex.: FL1); vazio = avulsa ou standalone.
        /// Uma partida dentro de um subsistema pode ter Maquina (se veio de máquina incluída) ou não (periférico).
        /// </summary>
        public string Subsistema { get; set; } = "";

        /// <summary>Papel na maquina (SIS, ROS, AG1, AG2, EAG, VAZ, NBA, NAL, NAN, NPO).</summary>
        public string Papel { get; set; } = "";

                /// <summary>
        /// Fn manual do nível (ex: TB1, TAN). Vazio = automático (calculado pelo sistema).
        /// Quando preenchido, o normalizador respeita e não renomeia.
        /// </summary>
        public string FnNivel { get; set; } = "";

        /// <summary>Receita que criou a linha (Preparador de polimero, Flotador...); vazio em projetos antigos.</summary>
        public string Receita { get; set; } = "";

        /// <summary>Partida, Sensor ou PID (coluna da pagina da maquina).</summary>
        [XmlIgnore]
        public string Categoria
        {
            get { return Gerador.EhSensor(this) ? "Sensor" : Gerador.EhPid(this) ? "PID" : "Partida"; }
        }

        /// <summary>Condicoes do intertravamento, uma por linha ("SEN.EQU.TAN.LEI_BAI" ou "NAO FL1.ALM.BBA.LEI_HAB"), em OU.</summary>
        public string CondicoesInt { get; set; } = "";

        /// <summary>Defeitos externos (um por linha): desligam a partida (reset de CMD_LIG/AUT) e entram na severidade 1.</summary>
        public string DefeitosExtras { get; set; } = "";

        /// <summary>
        /// Condicoes: cada linha e um termo do OU; dentro da linha, " E " junta termos em serie.
        /// Ex.: "NÃO FL1.ALM.BB1.LEI_HAB E NÃO FL1.ALM.BB2.LEI_HAB".
        /// </summary>
        public static List<List<KeyValuePair<string, bool>>> LinhasE(string texto)
        {
            var r = new List<List<KeyValuePair<string, bool>>>();
            foreach (string linha in (texto ?? "").Split('\n', ';'))
            {
                var termos = new List<KeyValuePair<string, bool>>();
                foreach (string parte in System.Text.RegularExpressions.Regex.Split(linha, @"\s+E\s+"))
                    termos.AddRange(Linhas(parte));
                if (termos.Count > 0) r.Add(termos);
            }
            return r;
        }

        /// <summary>Linhas nao vazias de um campo de lista; "NAO X", "NÃO X" ou "!X" = negado.</summary>
        public static List<KeyValuePair<string, bool>> Linhas(string texto)
        {
            var l = new List<KeyValuePair<string, bool>>();
            foreach (string bruta in (texto ?? "").Split('\n', ';'))
            {
                string s = bruta.Trim();
                if (s.Length == 0) continue;
                bool neg = false;
                var m = System.Text.RegularExpressions.Regex.Match(s, @"^(N[AÃ]O\s+|!)\s*(.+)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (m.Success) { neg = true; s = m.Groups[2].Value.Trim(); }
                s = s.Replace("\"", "");
                if (s.Length > 0) l.Add(new KeyValuePair<string, bool>(s, neg));
            }
            return l;
        }
        public string TagExaConf { get; set; } = "";   // exaustor: confirmacao (DI)
        public string TagExaHab { get; set; } = "";    // exaustor: habilita (DO)

        /// <summary>Memoria de defeito da protecao: a informada ou SUB_EQ_FN_LEI_PRT.</summary>
        [XmlIgnore]
        public string TagProtecaoMemEfetiva
        {
            get { return string.IsNullOrWhiteSpace(TagProtecaoMem) ? Sub + "_" + Eq + "_" + Fn + "_LEI_PRT" : TagProtecaoMem.Trim(); }
        }

        /// <summary>Exaustor: DB de instancia do BL_PARTIDA_HIBRIDA e do BL_SEVERIDADE.</summary>
        [XmlIgnore]
        public string InstanciaExa
        {
            get { return "iEXA_" + Cfg.Prefixo + Sub + "_" + Eq + "_" + Fn; }
        }

        [XmlIgnore]
        public string InstanciaSevExa
        {
            get { return "iSEVEXA_" + Cfg.Prefixo + Sub + "_" + Eq + "_" + Fn; }
        }

        [XmlIgnore]
        public int Linha { get; set; }

        [XmlIgnore]
        public string Instancia
        {
            get { return "i" + Cfg.Prefixo + Sub + "_" + Eq + "_" + Fn; }
        }

        [XmlIgnore]
        public string InstanciaSev
        {
            get { return "iSEV_" + Cfg.Prefixo + Sub + "_" + Eq + "_" + Fn; }
        }

        /// <summary>Segundo BL_SEVERIDADE das partidas com bypass (motor 2).</summary>
        [XmlIgnore]
        public string InstanciaSev2
        {
            get { return "iSEV2_" + Cfg.Prefixo + Sub + "_" + Eq + "_" + Fn; }
        }

        /// <summary>PID (aba PIDs): objeto tecnologico PID_Compact.</summary>
        [XmlIgnore]
        public string InstanciaPid
        {
            get { return "iPID_" + Cfg.Prefixo + Sub + "_" + Eq + "_" + Fn; }
        }

        /// <summary>Modulacao por PID: BL_ANALOGICA que converte a saida do PID (0..100%) em velocidade (0..SET_VEL).</summary>
        [XmlIgnore]
        public string InstanciaPidVel
        {
            get { return "iPIDVEL_" + Cfg.Prefixo + Sub + "_" + Eq + "_" + Fn; }
        }

        /// <summary>Modulacao por PID: memoria com a velocidade que vai para o MOT_SET_VEL (como FL1_ALC_BBA_SET_AUX).</summary>
        [XmlIgnore]
        public string TagSetVelAux
        {
            get { return Sub + "_" + Eq + "_" + Fn + "_SET_AUX"; }
        }

        /// <summary>Intermitente: DB de instancia do BL_INTERMITENTE.</summary>
        [XmlIgnore]
        public string InstanciaItm
        {
            get { return "iITM_" + Cfg.Prefixo + Sub + "_" + Eq + "_" + Fn; }
        }

        /// <summary>Sensor analogico: BL_ANALOGICA que escala a entrada.</summary>
        [XmlIgnore]
        public string InstanciaAna
        {
            get { return "iANA_" + Cfg.Prefixo + Sub + "_" + Eq + "_" + Fn; }
        }

        /// <summary>Medidor de vazao com modulacao de quimicos: 2o BL_ANALOGICA (LEI_VAZ -> % de SET_VZQ).</summary>
        [XmlIgnore]
        public string InstanciaMod
        {
            get { return "iMOD_" + Cfg.Prefixo + Sub + "_" + Eq + "_" + Fn; }
        }

        /// <summary>Sensor de nivel analogico: variavel Temp da FC com o valor lido (antes de forcar).</summary>
        [XmlIgnore]
        public string TempLeiAux
        {
            get { return Sub + "_" + Eq + "_" + Fn + "_LEI_AUX"; }
        }

        /// <summary>Valvula analogica: BL_ANALOGICA que le a posicao (AI -> LEI_POS).</summary>
        [XmlIgnore]
        public string InstanciaLeiPos
        {
            get { return "iLEIPOS_" + Cfg.Prefixo + Sub + "_" + Eq + "_" + Fn; }
        }

        /// <summary>Valvula analogica: BL_ANALOGICA que escreve a posicao (SET_POS_AUX -> AO).</summary>
        [XmlIgnore]
        public string InstanciaSetPos
        {
            get { return "iSETPOS_" + Cfg.Prefixo + Sub + "_" + Eq + "_" + Fn; }
        }

        /// <summary>Valvula analogica: memoria com o SET_POS quando aberta e 0 quando fechada.</summary>
        [XmlIgnore]
        public string TagSetPosAux
        {
            get { return Sub + "_" + Eq + "_" + Fn + "_SET_POS_AUX"; }
        }

        /// <summary>Valvula 1AI: memoria com SET_POS - LEI_POS (confirmacao de aberta).</summary>
        [XmlIgnore]
        public string TagLeiPosAux
        {
            get { return Sub + "_" + Eq + "_" + Fn + "_LEI_POS_AUX"; }
        }

        /// <summary>Pas do raspador: memoria global de defeito do lado direito (SUB_EQ_FN_LEI_DED).</summary>
        [XmlIgnore]
        public string TagDefDireito
        {
            get { return Sub + "_" + Eq + "_" + Fn + "_LEI_DED"; }
        }

        /// <summary>Pas do raspador: memoria global de defeito do lado esquerdo (SUB_EQ_FN_LEI_DEE).</summary>
        [XmlIgnore]
        public string TagDefEsquerdo
        {
            get { return Sub + "_" + Eq + "_" + Fn + "_LEI_DEE"; }
        }

        /// <summary>Tag do espelho de defeito: a informada ou SUB_EQ_FN_LEI_DEF.</summary>
        [XmlIgnore]
        public string TagDefEfetiva
        {
            get
            {
                return string.IsNullOrWhiteSpace(TagDef)
                    ? Sub + "_" + Eq + "_" + Fn + "_LEI_DEF"
                    : TagDef.Trim();
            }
        }
    }

    // ===================================================================
    //  Arquivo de projeto (.genesis; os .fastproj antigos têm o mesmo formato) - XML simples
    // ===================================================================
    public class Projeto
    {
        public List<Io> Ios = new List<Io>();
        public List<Partida> Partidas = new List<Partida>();

        public Hardware.ProjetoHardware Hardware { get; set; }

        public static void Salvar(string arquivo, Projeto projeto)
        {
            var ser = new XmlSerializer(typeof(Projeto));
            using (var fs = new FileStream(arquivo, FileMode.Create))
                ser.Serialize(fs, projeto);
        }

        public static Projeto Abrir(string arquivo)
        {
            var ser = new XmlSerializer(typeof(Projeto));
            using (var fs = new FileStream(arquivo, FileMode.Open))
                return (Projeto)ser.Deserialize(fs);
        }
    }

    // ===================================================================
    //  Configuracao (salva em %AppData%\Genesis\config.txt; migra da pasta antiga FASTGenerator)
    // ===================================================================
    public static class Cfg
    {
        public static string Prefixo = "G_";
        public static string TagReset = "RESET_PAINEL";
        public static string TagEmergencia = "SIS_EME_ACI_CNF";
        public static string PastaExport = "";
        public static string ContatoRemLoc = "GER.REM_LOC_CMD_SEL";
        public static bool ManterXml = false;   // mantem os XMLs gerados (diagnostico)
        public static bool PidNoOb = true;      // PIDs gerados direto nas redes do OB ciclico (em vez de uma FC G_PID_<DB>)
        public static string NumeroObPid = "30"; // numero do OB de interrupcao ciclica dos PIDs
        public static string VersaoPid = "";    // versao do PID_Compact (vazio = automatica)
        public static bool AlocarDefeitos = true;          // da endereco %M as memorias de defeito sozinho
        public static string InicioDefeitos = "502.1";     // 1o bit dos defeitos (502.0 = emergencia geral)
        public static string NomeWordDefeitos = "DEFEITOS_";   // words DEFEITOS_1 (MW502), DEFEITOS_2 (MW504)...
        public static bool ComAtivo = true;                // gera o mapa Modbus (DB COM + FC de mapeamento)
        public static string ComDb = "COM";                // nome do DB do mapa
        public static string ComNumero = "6";              // numero fixo do DB do mapa
        public static bool ComRecriar = true;              // DB COM/FC ja existentes (qualquer formato/pasta): copia de seguranca, apaga e gera de novo
        public static string NomeStatusWord = "STATUS_WORD_";  // words de alarme dos sensores
        public static string InicioStatusWord = "600";     // STATUS_WORD_1 = MW600, _2 = MW602...
        public static string TagPrimeiroCiclo = "FirstScan";   // bit de primeiro ciclo (system memory)
        public static string InicioAux = "100";            // 1a word das memorias auxiliares (SET_AUX, SET_POS_AUX...): MW100
        public static string MbServerDb = "MB_SERVER_DB";  // DB de instancia do MB_SERVER (criado se nao existir)
        public static string ConexaoModbus = "CNT.CONNECT"; // TCON_IP_v4 do MB_SERVER (DB criado se nao existir)
        public static string InterfaceModbus = "64";        // HW ID da interface PROFINET (InterfaceId do TCON_IP_v4)
        public static string PortaModbus = "502";           // porta local do servidor Modbus TCP
        public static string TagClock1Hz = "Clock_1Hz";     // clock de 1 Hz (clock memory): pulso do totalizador do preparador
        // Padrões de nomenclatura dos sinais de campo. Placeholders: {DESC}, {SUB}, {EQ}, {FN}.
        public static string PadraoConf = "CONF_{DESC}";
        public static string PadraoHab = "HAB_{DESC}";
        public static string PadraoHabInv = "HAB_INV_{DESC}";
        public static string PadraoHabSoft = "HAB_SOFT_{DESC}";
        public static string PadraoHabBomba1 = "HAB_BOMBA_1_{SUB}_{EQ}_{FN}";
        public static string PadraoHabBomba2 = "HAB_BOMBA_2_{SUB}_{EQ}_{FN}";
        public static string PadraoDef = "DEF_{DESC}";
        // Sensores: nome completo, com a descricao do item (como nas partidas). Os padroes antigos (abreviados, com DB_GRUPO_NOME)
        // ficam em Legado*: servem para trocar a configuracao salva e as tags automaticas de projetos antigos.
        public static string PadraoMedidor = "MEDIDOR_{DESC}";
        public static string PadraoTotalizador = "TOTALIZADOR_{DESC}";
        public static string PadraoChave = "CHAVE_{DESC}";
        public static string PadraoChaveMovimento = "CHAVE_MOVIMENTO_{DESC}";
        public const string LegadoMedidor = "MEDIDOR_{SUB}_{EQ}_{FN}";
        public const string LegadoTotalizador = "TOTALIZADOR_VAZAO_{SUB}_{EQ}_{FN}";
        public const string LegadoChave = "CHAVE_{SUB}_{EQ}_{FN}";
        public const string LegadoChaveMovimento = "CHAVE_MOVIMENTO_{SUB}_{EQ}_{FN}";

        /// <summary>Último arquivo de projeto usado (para a sessão restaurada continuar salvando nele).</summary>
        public static string UltimoArquivo = "";

        private static bool _migrou;

        public static string PastaApp
        {
            get
            {
                string raiz = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string p = Path.Combine(raiz, "Genesis");
                Directory.CreateDirectory(p);
                if (!_migrou)
                {
                    _migrou = true;
                    MigrarPastaAntiga(Path.Combine(raiz, "FASTGenerator"), p);
                }
                return p;
            }
        }

        /// <summary>Primeira vez com o nome novo: traz a configuração e a última sessão do FAST Logic Generator.</summary>
        private static void MigrarPastaAntiga(string antiga, string nova)
        {
            try
            {
                if (!Directory.Exists(antiga)) return;
                foreach (string nome in new[] { "config.txt", "ultimo.fastproj" })
                {
                    string de = Path.Combine(antiga, nome), para = Path.Combine(nova, nome);
                    if (File.Exists(de) && !File.Exists(para) && !File.Exists(Path.Combine(nova, "ultimo.genesis"))) File.Copy(de, para);
                }
            }
            catch
            {
                // sem acesso: começa com os padrões
            }
        }

        private static string Arquivo
        {
            get { return Path.Combine(PastaApp, "config.txt"); }
        }

        public static void Carregar()
        {
            try
            {
                if (!File.Exists(Arquivo)) return;
                foreach (string linha in File.ReadAllLines(Arquivo, Encoding.UTF8))
                {
                    int i = linha.IndexOf('=');
                    if (i <= 0) continue;
                    string chave = linha.Substring(0, i).Trim();
                    string valor = linha.Substring(i + 1).Trim();

                    if (chave == "Prefixo") Prefixo = valor;
                    else if (chave == "TagReset") TagReset = valor;
                    else if (chave == "TagEmergencia") TagEmergencia = valor;
                    else if (chave == "PastaExport") PastaExport = valor;
                    else if (chave == "ContatoRemLoc") ContatoRemLoc = valor;
                    else if (chave == "ManterXml") ManterXml = valor == "1";
                    else if (chave == "VersaoPid") VersaoPid = valor;
                    else if (chave == "PidNoOb") PidNoOb = valor != "0";
                    else if (chave == "NumeroObPid") NumeroObPid = valor;
                    else if (chave == "AlocarDefeitos") AlocarDefeitos = valor != "0";
                    else if (chave == "InicioDefeitos") InicioDefeitos = valor;
                    else if (chave == "NomeWordDefeitos") NomeWordDefeitos = valor;
                    else if (chave == "ComAtivo") ComAtivo = valor != "0";
                    else if (chave == "ComDb") ComDb = valor;
                    else if (chave == "ComNumero") ComNumero = valor;
                    else if (chave == "ComRecriar") ComRecriar = valor != "0";
                    else if (chave == "NomeStatusWord") NomeStatusWord = valor;
                    else if (chave == "InicioStatusWord") InicioStatusWord = valor;
                    else if (chave == "TagPrimeiroCiclo") TagPrimeiroCiclo = valor;
                    else if (chave == "InicioAux") InicioAux = valor;
                    else if (chave == "MbServerDb") MbServerDb = valor;
                    else if (chave == "ConexaoModbus") ConexaoModbus = valor;
                    else if (chave == "InterfaceModbus") InterfaceModbus = valor;
                    else if (chave == "PortaModbus") PortaModbus = valor;
                    else if (chave == "TagClock1Hz") TagClock1Hz = valor;
                    else if (chave == "PadraoConf") PadraoConf = valor;
                    else if (chave == "PadraoHab") PadraoHab = valor;
                    else if (chave == "PadraoHabInv") PadraoHabInv = valor;
                    else if (chave == "PadraoHabSoft") PadraoHabSoft = valor;
                    else if (chave == "PadraoHabBomba1") PadraoHabBomba1 = valor;
                    else if (chave == "PadraoHabBomba2") PadraoHabBomba2 = valor;
                    else if (chave == "PadraoDef") PadraoDef = valor;
                    // configuracao salva com o padrao antigo (abreviado) passa para o novo; padrao que voce mudou fica como esta
                    else if (chave == "PadraoMedidor") PadraoMedidor = valor == LegadoMedidor ? "MEDIDOR_{DESC}" : valor;
                    else if (chave == "PadraoTotalizador") PadraoTotalizador = valor == LegadoTotalizador ? "TOTALIZADOR_{DESC}" : valor;
                    else if (chave == "PadraoChave") PadraoChave = valor == LegadoChave ? "CHAVE_{DESC}" : valor;
                    else if (chave == "PadraoChaveMovimento") PadraoChaveMovimento = valor == LegadoChaveMovimento ? "CHAVE_MOVIMENTO_{DESC}" : valor;
                    else if (chave == "UltimoArquivo") UltimoArquivo = valor;
                }
            }
            catch
            {
                // usa os valores padrao
            }
        }

        public static void Salvar()
        {
            try
            {
                File.WriteAllLines(Arquivo, new[]
                {
                    "Prefixo=" + Prefixo,
                    "TagReset=" + TagReset,
                    "TagEmergencia=" + TagEmergencia,
                    "PastaExport=" + PastaExport,
                    "ContatoRemLoc=" + ContatoRemLoc,
                    "ManterXml=" + (ManterXml ? "1" : "0"),
                    "VersaoPid=" + VersaoPid,
                    "PidNoOb=" + (PidNoOb ? "1" : "0"),
                    "NumeroObPid=" + NumeroObPid,
                    "AlocarDefeitos=" + (AlocarDefeitos ? "1" : "0"),
                    "InicioDefeitos=" + InicioDefeitos,
                    "NomeWordDefeitos=" + NomeWordDefeitos,
                    "ComAtivo=" + (ComAtivo ? "1" : "0"),
                    "ComDb=" + ComDb,
                    "ComNumero=" + ComNumero,
                    "ComRecriar=" + (ComRecriar ? "1" : "0"),
                    "NomeStatusWord=" + NomeStatusWord,
                    "InicioStatusWord=" + InicioStatusWord,
                    "TagPrimeiroCiclo=" + TagPrimeiroCiclo,
                    "InicioAux=" + InicioAux,
                    "MbServerDb=" + MbServerDb,
                    "ConexaoModbus=" + ConexaoModbus,
                    "InterfaceModbus=" + InterfaceModbus,
                    "PortaModbus=" + PortaModbus,
                    "TagClock1Hz=" + TagClock1Hz,
                    "PadraoConf=" + PadraoConf,
                    "PadraoHab=" + PadraoHab,
                    "PadraoHabInv=" + PadraoHabInv,
                    "PadraoHabSoft=" + PadraoHabSoft,
                    "PadraoHabBomba1=" + PadraoHabBomba1,
                    "PadraoHabBomba2=" + PadraoHabBomba2,
                    "PadraoDef=" + PadraoDef,
                    "PadraoMedidor=" + PadraoMedidor,
                    "PadraoTotalizador=" + PadraoTotalizador,
                    "PadraoChave=" + PadraoChave,
                    "PadraoChaveMovimento=" + PadraoChaveMovimento,
                    "UltimoArquivo=" + UltimoArquivo
                }, new UTF8Encoding(false));
            }
            catch
            {
                // sem permissao: ignora
            }
        }
    }

    // ===================================================================
    //  Importacao de CSV (compatibilidade com a versao anterior)
    // ===================================================================
    public static class Csv
    {
        private static List<string[]> Ler(string arquivo)
        {
            var linhas = new List<string[]>();
            string[] todas = File.ReadAllLines(arquivo, Encoding.UTF8);
            if (todas.Length == 0) return linhas;

            char sep = todas[0].Contains(";") ? ';' : ',';

            for (int i = 1; i < todas.Length; i++)   // pula o cabecalho
            {
                string l = todas[i];
                if (string.IsNullOrWhiteSpace(l) || l.TrimStart().StartsWith("#")) continue;
                linhas.Add(l.Split(sep).Select(x => x.Trim()).ToArray());
            }
            return linhas;
        }

        private static string Col(string[] c, int i)
        {
            return i < c.Length ? c[i] : "";
        }

        private static bool Marcado(string v)
        {
            v = (v ?? "").Trim().ToLowerInvariant();
            return v == "1" || v == "x" || v == "s" || v == "sim" || v == "true";
        }

        public static List<Io> LerIos(string arquivo)
        {
            var lista = new List<Io>();
            foreach (string[] c in Ler(arquivo))
            {
                lista.Add(new Io
                {
                    Nome = Col(c, 0),
                    Endereco = Col(c, 1),
                    Tipo = Col(c, 2),
                    Descricao = Col(c, 3)
                });
            }
            return lista;
        }

        public static List<Partida> LerPartidas(string arquivo)
        {
            var lista = new List<Partida>();
            foreach (string[] c in Ler(arquivo))
            {
                lista.Add(new Partida
                {
                    Sub = Col(c, 0),
                    Eq = Col(c, 1),
                    Fn = Col(c, 2),
                    Tipo = Col(c, 3),
                    Descricao = Col(c, 4),
                    TagConf = Col(c, 5),
                    TagRef = Col(c, 6),
                    TagHab = Col(c, 7),
                    TagDef = Col(c, 8),
                    Auto = Marcado(Col(c, 9)),
                    Intertravamento = Marcado(Col(c, 10)),
                    Alternancia = Marcado(Col(c, 11)),
                    TagHabC2 = Col(c, 13),
                    TagDefDrive = Col(c, 14),
                    TagHabC1 = Col(c, 15),
                    TagConfFec = Col(c, 16),
                    TagFec = Col(c, 17),
                    TagPosAi = Col(c, 18),
                    TagSensor = Col(c, 19),
                    TagPulso = Col(c, 20),
                    TagSensor2 = Col(c, 21),
                    Intermitente = Col(c, 22),
                    IntermitenteDes = Col(c, 23),
                    ModVazao = Col(c, 24),
                    ModPid = Col(c, 25),
                    TagProtecao = Col(c, 26),
                    TagProtecaoMem = Col(c, 27),
                    Exaustor = Marcado(Col(c, 28)),
                    TagExaConf = Col(c, 29),
                    TagExaHab = Col(c, 30)
                });
            }
            return lista;
        }
    }

    // ===================================================================
    //  Leitor de tags coladas (TIA Portal, Google Sheets, Excel, CSV)
    //  Reconhece as colunas pelo conteudo, em qualquer ordem:
    //    endereco  -> %I0.0, %QW100, %MD20 (o % e opcional se nao for a 1a coluna)
    //    tipo      -> Bool, Byte, Word, DWord, Int, UInt, DInt, UDInt, Real
    //    nome      -> primeira celula que sobrar
    //    descricao -> ultima celula que sobrar (ignorando True/False)
    // ===================================================================
    public static class LeitorTags
    {
        public static readonly string[] Tipos =
            { "Bool", "Byte", "Word", "DWord", "Int", "UInt", "DInt", "UDInt", "Real" };

        private static readonly Regex RxEndCompleto =
            new Regex(@"^%[IQM][XBWD]?\d+(\.[0-7])?$", RegexOptions.IgnoreCase);
        private static readonly Regex RxEndSemPct =
            new Regex(@"^[IQM][XBWD]?\d+(\.[0-7])?$", RegexOptions.IgnoreCase);

        private static readonly string[] Cabecalhos =
            { "name", "nome", "tag", "tags", "tag name", "nome da tag", "variavel", "variável" };

        /// <summary>
        /// Interpreta o texto colado. Tipo vem vazio quando nao aparece na linha
        /// (use InferirTipo para tags novas). Linhas sem nome vao para 'ignoradas'.
        /// </summary>
        public static List<Io> Interpretar(string texto, List<string> ignoradas, string areaPadrao = null)
        {
            var lista = new List<Io>();
            if (string.IsNullOrWhiteSpace(texto)) return lista;

            string[] linhas = texto.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

            char sep = '\0';
            if (linhas.Any(l => l.Contains("\t"))) sep = '\t';
            else if (linhas.Any(l => l.Contains(";"))) sep = ';';
            else if (linhas.Any(l => l.Contains(","))) sep = ',';

            foreach (string linha in linhas)
            {
                if (string.IsNullOrWhiteSpace(linha)) continue;

                string[] celulas = sep == '\0' ? new[] { linha } : linha.Split(sep);
                celulas = celulas.Select(Limpar).ToArray();

                string end = null, tipo = null, numero = null;
                var resto = new List<string>();

                foreach (string c in celulas)
                {
                    if (c.Length == 0) continue;

                    if (end == null && RxEndCompleto.IsMatch(c))
                    {
                        end = NormalizarEndereco(c);
                        continue;
                    }

                    string t = Tipos.FirstOrDefault(
                        x => string.Equals(x, c, StringComparison.OrdinalIgnoreCase));
                    if (t != null && tipo == null)
                    {
                        tipo = t;
                        continue;
                    }

                    // area escolhida na tela: uma celula so com numero vira o endereco (ex.: 0.2 ou 100)
                    if (areaPadrao != null && end == null && numero == null && NumeroSolto(areaPadrao, c))
                    {
                        numero = c.Replace(',', '.');
                        continue;
                    }

                    resto.Add(c);
                }

                if (resto.Count == 0)
                {
                    if (ignoradas != null) ignoradas.Add(Resumo(linha));
                    continue;
                }

                string nome = resto[0];
                resto.RemoveAt(0);

                // linha de cabecalho (Name / Data type / Address ...)
                if (Cabecalhos.Contains(nome.ToLowerInvariant())) continue;

                // endereco sem % (planilha): so aceita depois do nome
                if (end == null)
                {
                    string cand = resto.FirstOrDefault(x => RxEndSemPct.IsMatch(x));
                    if (cand != null)
                    {
                        end = NormalizarEndereco(cand);
                        resto.Remove(cand);
                    }
                }

                string desc = resto.LastOrDefault(x => !EhBooleano(x)) ?? "";

                var novo = new Io { Nome = nome, Tipo = tipo ?? "", Descricao = desc };
                if (end != null) novo.Endereco = end;
                else if (numero != null) { novo.Area = areaPadrao; novo.Numero = numero; }
                lista.Add(novo);
            }

            return lista;
        }

        private static bool NumeroSolto(string area, string c)
        {
            switch (area)
            {
                case "DI":
                case "DO":
                    return Regex.IsMatch(c, @"^\d+[.,][0-7]$");
                case "AI":
                case "AO":
                    return Regex.IsMatch(c, @"^\d+$");
                case "MEM":
                    return Regex.IsMatch(c, @"^\d+([.,][0-7])?$");
                default:
                    return false;
            }
        }

        /// <summary>Tipo provavel quando a linha colada nao trouxe a coluna de tipo.</summary>
        public static string InferirTipo(string endereco)
        {
            if (string.IsNullOrEmpty(endereco) || endereco.Contains(".")) return "Bool";
            string area = endereco.TrimStart('%');
            if (area.Length < 2) return "Bool";
            switch (char.ToUpperInvariant(area[1]))
            {
                case 'B': return "Byte";
                case 'W': return "Int";
                case 'D': return "DWord";
                default: return "Bool";
            }
        }

        private static string NormalizarEndereco(string c)
        {
            return "%" + c.Replace(" ", "").TrimStart('%').ToUpperInvariant();
        }

        private static bool EhBooleano(string c)
        {
            string v = c.Trim().ToLowerInvariant();
            return v == "true" || v == "false" || v == "x" || v == "verdadeiro" || v == "falso";
        }

        private static string Limpar(string c)
        {
            c = (c ?? "").Trim();
            if (c.Length >= 2 && c[0] == '"' && c[c.Length - 1] == '"')
                c = c.Substring(1, c.Length - 2).Replace("\"\"", "\"");
            return c.Trim();
        }

        private static string Resumo(string linha)
        {
            linha = linha.Trim();
            return linha.Length > 60 ? linha.Substring(0, 60) + "..." : linha;
        }
    }
}
namespace Genesis
{
    /// <summary>Padrao dos textos que vao para o TIA (comentarios de DB, titulos de rede, descricoes de tags).</summary>
    internal static class Textos
    {
        /// <summary>Maiusculas, sem espacos sobrando (os acentos ficam como digitados).</summary>
        public static string Padrao(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return "";
            return System.Text.RegularExpressions.Regex.Replace(texto.Trim(), @"\s+", " ").ToUpperInvariant();
        }
    }
}