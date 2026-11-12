using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using Siemens.Engineering;
using Siemens.Engineering.Compiler;
using Siemens.Engineering.SW;
using Siemens.Engineering.SW.Blocks;
using Siemens.Engineering.SW.Tags;
using Siemens.Engineering.SW.TechnologicalObjects;
using Siemens.Engineering.HW;
using Siemens.Engineering.HW.Features;

namespace Genesis
{
    // ===================================================================
    //  Motor de geracao.
    //  Por subsistema gera:
    //    - DB global  <Prefixo><SUB>            (structs Equipamento > UDT)
    //    - DBs de instancia  i<Prefixo><SUB>_<EQ>_<FN>
    //    - FC  <Prefixo>PARTIDAS_<SUB>          (uma rede por partida)
    //  e cria as tags de PLC das IOs (tabela "FAST_GERADO").
    //  Tudo vai para o grupo de blocos "FAST_GERADO".
    //  Blocos com o mesmo nome fora desse grupo nunca sao tocados.
    // ===================================================================
    internal partial class Gerador
    {
        public const string NomeGrupo = "FAST_GERADO";
        private const string NomeUdt = "UDT_PARTIDAS";
        private const string NomeUdtValvula = "UDT_VALVULA";

        private static readonly string NsFlg =
            "http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v5";
        private static readonly string NsIf =
            "http://www.siemens.com/automation/Openness/SW/Interface/v5";
        private static readonly string[] Culturas = { "en-US", "es-ES", "pt-BR" };

        // --- Tipos de partida (todos usam o BL_PARTIDA_HIBRIDA) ------------------
        //   Drive      : inversor / soft-starter. Nao ha retorno de contator: MOT_LEI_CN1 recebe
        //                a propria habilitacao e o defeito do drive entra NEGADO em MOT_LEI_TRP.
        //   Velocidade : MOT_SET_VEL (DB) e MOT_LEI_VEL (saida analogica).
        //   Bypass     : dois contatores (motor titular / reserva) no mesmo drive.
        internal class TipoPartida
        {
            public string Nome;        // texto mostrado na tela e gravado na partida
            public string Chave;       // forma curta, aceita ao ler arquivos
            public string Fb;          // nome do FB
            public string[][] Pinos;   // nome, secao, tipo
            public bool Drive, Velocidade, Bypass;
            public bool Rede;          // inversor por rede (BL_INVERSOR_6)
            public bool Valvula;       // valvula (BL_VALVULA_5 / UDT_VALVULA)
            public bool Di2;           // valvula com 2 DI (confirmacao aberta e fechada)
            public bool Do2;           // valvula com 2 DO (saida abre e saida fecha)
            public bool Analogica;     // valvula com saida analogica de posicao (AO)
            public bool Ai;            // valvula com leitura analogica de posicao (AI)
            public bool Sensor;        // sensor (DB SEN, sem automatico/intertravamento/visualizacao)
            public bool SenAlto;       // sensor de nivel digital: alto (senao baixo)
            public bool SenAna;        // sensor analogico (BL_ANALOGICA + BL_SENSOR_NIVEL_ANALOGICA)
            public bool SenAnalitico;  // analitico: sem forcar valor, com BL_SEVERIDADE
            public bool SenVazao;      // medidor de vazao (BL_ANALOGICA + BL_SENSOR_MEDIDOR_VAZAO + BL_SEVERIDADE)
            public bool SenVazaoMod;   // medidor de vazao com modulacao de quimicos (2o BL_ANALOGICA + MOVEs)
            public bool SenRaspador;   // pas do raspador do flotador (BL_SENSOR_RASPADOR)
            public bool Pid;           // PID (aba PIDs): PID_Compact no OB ciclico, struct UDT_PID no DB
            public bool MaquinaSis;    // nucleo de maquina (bloco da maquina + automatico), ex.: preparador
            public bool MaquinaAgua;   // valvula de agua do preparador (BL_PARTIDA_HIBRIDA com UDT de valvula)
            public string Udt;         // UDT do membro no DB (null = padrao da partida/valvula)
        }

        private static readonly string[][] PinosPartida =
        {
            new[] {"SIS_CMD_RST","Input","Bool"}, new[] {"SIS_LEI_EME","Input","Bool"},
            new[] {"SIS_CMD_AUT","Input","Bool"}, new[] {"SIS_LEI_VAZ","Input","Int"},
            new[] {"MOT_CMD_LIG","Input","Bool"}, new[] {"MOT_CMD_INT","Input","Bool"},
            new[] {"MOT_CMD_SEL","Input","Bool"}, new[] {"MOT_LEI_CN1","Input","Bool"},
            new[] {"MOT_LEI_CN2","Input","Bool"}, new[] {"MOT_CMD_MN1","Input","Bool"},
            new[] {"MOT_CMD_MN2","Input","Bool"}, new[] {"MOT_CMD_RH1","Input","Bool"},
            new[] {"MOT_CMD_RH2","Input","Bool"}, new[] {"MOT_LEI_TRP","Input","Bool"},
            new[] {"MOT_SET_TPD","Input","Int"},  new[] {"MOT_SET_VEL","Input","Int"},
            new[] {"MOT_SET_VMI","Input","Int"},
            new[] {"MOT_LEI_HAB","Output","Bool"}, new[] {"MOT_CMD_HA1","Output","Bool"},
            new[] {"MOT_CMD_HA2","Output","Bool"}, new[] {"MOT_LEI_DEF","Output","Bool"},
            new[] {"MOT_LEI_ST1","Output","Int"},  new[] {"MOT_LEI_ST2","Output","Int"},
            new[] {"MOT_LEI_HO1","Output","DInt"}, new[] {"MOT_LEI_HO2","Output","DInt"},
            new[] {"MOT_LEI_VEL","Output","Real"}
        };

        private const string FbPartida = "BL_PARTIDA_HIBRIDA";
        private const string FbInversorRede = "BL_INVERSOR_6";
        private const string FbValvula = "BL_VALVULA_5";
        private const string FbAnalogica = "BL_ANALOGICA";
        private const string FbSensorDig = "BL_SENSOR_NIVEL_DIG_7";
        private const string FbSensorAna = "BL_SENSOR_NÍVEL_ANALOGICA_2";   // com acento, como no projeto
        private const string FbSensorVazao = "BL_SENSOR_MEDIDOR_VAZAO_2";
        private const string FbSensorRaspador = "BL_SENSOR_RASPADOR";

        private static readonly string[][] PinosSensorDig =
        {
            new[] {"SEN_SET_BAI","Input","Bool"}, new[] {"SET_TPO_BAI","Input","Int"},
            new[] {"SEN_SET_ALT","Input","Bool"}, new[] {"SET_TPO_ALT","Input","Int"},
            new[] {"SEN_CMD_MAN","Input","Bool"}, new[] {"SEL_SEG_MIN","Input","Bool"},
            new[] {"CMD_SKL_BAI","Input","Bool"}, new[] {"CMD_SKL_ALT","Input","Bool"},
            new[] {"SEN_LEI_BAI","Output","Bool"}, new[] {"SEN_LEI_SNB","Output","Int"},
            new[] {"SEN_LEI_TPB","Output","Int"}, new[] {"SEN_LEI_ALT","Output","Bool"},
            new[] {"SEN_LEI_SNA","Output","Int"}, new[] {"SEN_LEI_TPA","Output","Int"},
            new[] {"SEN_LEI_STB","Output","Int"}, new[] {"SEN_LEI_STA","Output","Int"}
        };

        private static readonly string[][] PinosSensorAna =
        {
            new[] {"SEN_LEI_ENT","Input","Real"}, new[] {"SEN_SET_SBA","Input","Real"},
            new[] {"SEN_SET_RBA","Input","Real"}, new[] {"SEN_SET_SAL","Input","Real"},
            new[] {"SEN_SET_RAL","Input","Real"}, new[] {"SEN_CMD_MAN","Input","Bool"},
            new[] {"SEN_LEI_BAI","Output","Bool"}, new[] {"SEN_LEI_ALT","Output","Bool"},
            new[] {"SEN_LEI_STB","Output","Int"}, new[] {"SEN_LEI_STA","Output","Int"}
        };

        // pinos do BL_SENSOR_MEDIDOR_VAZAO_2
        private static readonly string[][] PinosSensorVazao =
        {
            new[] {"SEN_LEI_VAZ","Input","Real"}, new[] {"SEN_SET_SBA","Input","Real"},
            new[] {"SEN_SET_RBA","Input","Real"}, new[] {"SEN_SET_SAL","Input","Real"},
            new[] {"SEN_SET_RAL","Input","Real"}, new[] {"SEN_CMD_MAN","Input","Bool"},
            new[] {"SEN_LEI_PUL","Input","Bool"}, new[] {"SEN_SET_TOT","Input","DInt"},
            new[] {"SEN_SET_CTO","Input","DInt"}, new[] {"SEN_CMD_CTO","Input","Bool"},
            new[] {"SEN_CMD_RST","Input","Bool"},
            new[] {"SEN_LEI_BAI","Output","Bool"}, new[] {"SEN_LEI_ALT","Output","Bool"},
            new[] {"SEN_LEI_TOT","Output","DInt"}
        };

        // pinos do BL_SENSOR_RASPADOR
        private static readonly string[][] PinosSensorRaspador =
        {
            new[] {"SEN_LEI_DIR","Input","Bool"}, new[] {"SEN_LEI_LES","Input","Bool"},
            new[] {"SIS_CMD_MAN","Input","Bool"}, new[] {"SIS_CMD_RST","Input","Bool"},
            new[] {"MOT_LEI_CNF","Input","Bool"},
            new[] {"SEN_SET_TPD","Input","Int"},  new[] {"SEN_SET_TPE","Input","Int"},
            new[] {"LDI_LEI_TPO","Output","Int"}, new[] {"LES_LEI_TPO","Output","Int"},
            new[] {"LDI_LEI_DEF","Output","Bool"}, new[] {"LES_LEI_DEF","Output","Bool"}
        };

        // pinos do BL_ANALOGICA (os nomes tem espaco, como no bloco)
        private static readonly string[][] PinosAnalogica =
        {
            new[] {"IN MIN","Input","Real"}, new[] {"IN MAX","Input","Real"},
            new[] {"IN ENTRADA","Input","Real"},
            new[] {"OUT MIN","Input","Real"}, new[] {"OUT MAX","Input","Real"},
            new[] {"OUT VALOR","Output","Real"}, new[] {"OUT_POR","Output","Real"},
            new[] {"OUT_ANA","Output","Real"}
        };

        // pinos do BL_VALVULA_5 (mesmos do BL_VALVULA_4 das redes reais; mudou so a logica interna)
        private static readonly string[][] PinosValvula =
        {
            new[] {"SIS_CMD_RST","Input","Bool"}, new[] {"SIS_LEI_EME","Input","Bool"},
            new[] {"SIS_CMD_CLP","Input","Bool"},
            new[] {"VAL_CMD_ABR","Input","Bool"}, new[] {"VAL_CMD_INT","Input","Bool"},
            new[] {"VAL_CMD_MAN","Input","Bool"},
            new[] {"VAL_CNF_ABR","Input","Bool"}, new[] {"VAL_CNF_FEC","Input","Bool"},
            new[] {"VAL_TPO_FAL","Input","Int"},
            new[] {"VAL_HAB_ABR","Output","Bool"}, new[] {"VAL_HAB_FEC","Output","Bool"},
            new[] {"VAL_LEI_DEF","Output","Bool"}, new[] {"VAL_LEI_STS","Output","Int"}
        };

        // pinos do BL_INVERSOR_6 (como nas chamadas reais do projeto)
        private static readonly string[][] PinosInversorRede =
        {
            new[] {"INV_SET_END","Input","HW_SUBMODULE"},
            new[] {"SIS_LEI_EME","Input","Bool"}, new[] {"SIS_CMD_AUT","Input","Bool"},
            new[] {"SIS_CMD_RST","Input","Bool"}, new[] {"SIS_LEI_VAZ","Input","Int"},
            new[] {"SIS_CMD_CLP","Input","Bool"},
            new[] {"MOT_CMD_LIG","Input","Bool"}, new[] {"MOT_CMD_INT","Input","Bool"},
            new[] {"MOT_CMD_MA1","Input","Bool"}, new[] {"MOT_CMD_MA2","Input","Bool"},
            new[] {"MOT_CMD_RH1","Input","Bool"}, new[] {"MOT_CMD_RH2","Input","Bool"},
            new[] {"MOT_CMD_SEL","Input","Bool"}, new[] {"MOT_CMD_REV","Input","Bool"},
            new[] {"MOT_SET_VEL","Input","Real"}, new[] {"MOT_SET_VMI","Input","Real"},
            new[] {"MOT_SET_ANA","Input","Int"},  new[] {"MOT_SET_FES","Input","Real"},
            new[] {"MOT_LEI_CN1","Input","Bool"}, new[] {"MOT_LEI_CN2","Input","Bool"},
            new[] {"MOT_LEI_RUN","Output","Bool"}, new[] {"MOT_LEI_FRP","Output","Bool"},
            new[] {"MOT_LEI_DEF","Output","Bool"},
            new[] {"MOT_LEI_HA1","Output","Bool"}, new[] {"MOT_LEI_HA2","Output","Bool"},
            new[] {"MOT_LEI_ST1","Output","Int"},  new[] {"MOT_LEI_ST2","Output","Int"},
            new[] {"MOT_LEI_VEL","Output","Int"},  new[] {"MOT_LEI_COR","Output","Real"},
            new[] {"MOT_LEI_CSM","Output","Int"},
            new[] {"MOT_LEI_ALA","Output","DInt"}, new[] {"MOT_LEI_FAL","Output","DInt"},
            new[] {"MOT_LEI_HO1","Output","DInt"}, new[] {"MOT_LEI_HO2","Output","DInt"}
        };

        private static readonly TipoPartida[] TiposPartida =
        {
            new TipoPartida { Nome = "Partida direta", Chave = "direta", Fb = FbPartida, Pinos = PinosPartida },
            new TipoPartida { Nome = "Partida inversor", Chave = "inversor", Fb = FbPartida, Pinos = PinosPartida,
                Drive = true, Velocidade = true },
            new TipoPartida { Nome = "Partida inversor com bypass", Chave = "inversor_bypass", Fb = FbPartida, Pinos = PinosPartida,
                Drive = true, Velocidade = true, Bypass = true },
            new TipoPartida { Nome = "Partida soft-starter", Chave = "soft", Fb = FbPartida, Pinos = PinosPartida,
                Drive = true },
            new TipoPartida { Nome = "Partida soft-starter com bypass", Chave = "soft_bypass", Fb = FbPartida, Pinos = PinosPartida,
                Drive = true, Bypass = true },
            new TipoPartida { Nome = "Partida diafragma", Chave = "diafragma", Fb = FbPartida, Pinos = PinosPartida,
                Velocidade = true },
            new TipoPartida { Nome = "Partida inversor por rede", Chave = "inversor_rede",
                Fb = FbInversorRede, Pinos = PinosInversorRede, Rede = true },
            new TipoPartida { Nome = "Partida inversor por rede com bypass", Chave = "inversor_rede_bypass",
                Fb = FbInversorRede, Pinos = PinosInversorRede, Rede = true, Bypass = true },
            new TipoPartida { Nome = "Válvula 0DI e 1DO", Chave = "valvula_0di_1do",
                Fb = FbValvula, Pinos = PinosValvula, Valvula = true },
            new TipoPartida { Nome = "Válvula 2DI e 1DO", Chave = "valvula_2di_1do",
                Fb = FbValvula, Pinos = PinosValvula, Valvula = true, Di2 = true },
            new TipoPartida { Nome = "Válvula 2DI e 2DO", Chave = "valvula_2di_2do",
                Fb = FbValvula, Pinos = PinosValvula, Valvula = true, Di2 = true, Do2 = true },
            new TipoPartida { Nome = "Válvula 1AI e 1AO", Chave = "valvula_1ai_1ao",
                Fb = FbValvula, Pinos = PinosValvula, Valvula = true, Analogica = true, Ai = true },
            new TipoPartida { Nome = "Válvula 1AO", Chave = "valvula_1ao",
                Fb = FbValvula, Pinos = PinosValvula, Valvula = true, Analogica = true },

            // --- sensores (aba Sensores; DB SEN) ---
            new TipoPartida { Nome = "Sensor nível alto digital", Chave = "sensor_na_dig",
                Fb = FbSensorDig, Pinos = PinosSensorDig, Sensor = true, SenAlto = true, Udt = "UDT_SENS_NA_DIGITAL" },
            new TipoPartida { Nome = "Sensor nível baixo digital", Chave = "sensor_nb_dig",
                Fb = FbSensorDig, Pinos = PinosSensorDig, Sensor = true, Udt = "UDT_SENS_NB_DIGITAL" },
            new TipoPartida { Nome = "Sensor nível analógico", Chave = "sensor_nivel_ana",
                Fb = FbSensorAna, Pinos = PinosSensorAna, Sensor = true, SenAna = true, Udt = "UDT_SENS_ANALOG" },
            new TipoPartida { Nome = "Sensor analítico", Chave = "sensor_analitico",
                Fb = FbSensorAna, Pinos = PinosSensorAna, Sensor = true, SenAna = true, SenAnalitico = true,
                Udt = "UDT_SENS_ANALOG" },
            new TipoPartida { Nome = "Medidor de vazão", Chave = "sensor_vazao",
                Fb = FbSensorVazao, Pinos = PinosSensorVazao, Sensor = true, SenVazao = true, Udt = "UDT_MED_VAZAO" },
            new TipoPartida { Nome = "Medidor de vazão com modulação de químicos", Chave = "sensor_vazao_mod",
                Fb = FbSensorVazao, Pinos = PinosSensorVazao, Sensor = true, SenVazao = true, SenVazaoMod = true,
                Udt = "UDT_MED_VAZAO" },
            new TipoPartida { Nome = "Sensor pás raspador", Chave = "sensor_raspador",
                Fb = FbSensorRaspador, Pinos = PinosSensorRaspador, Sensor = true, SenRaspador = true,
                Udt = "UDT_PAS_RASPADOR" },

            // --- Maquinas (criadas pela pagina Maquinas) ---
            new TipoPartida { Nome = Maquinas.TipoSisPreparador, Chave = "maq_pp_sis", Fb = "BL_POLIMERO_PREPARO_FLOCULANTE_1",
                Pinos = new string[0][], MaquinaSis = true, Udt = "UDT_POLIMERO_PREPARO_FLOCULANTE" },
            new TipoPartida { Nome = Maquinas.TipoAguaPreparador, Chave = "maq_pp_eag", Fb = FbPartida, Pinos = PinosPartida,
                MaquinaAgua = true, Udt = NomeUdtValvula },

            // --- PID (aba PIDs) ---
            new TipoPartida { Nome = "PID", Chave = "pid", Fb = TipoPid, Pinos = PinosPid, Pid = true, Udt = NomeUdtPid }
        };

        /// <summary>Tipos da aba Partidas (sem os sensores).</summary>
        public static string[] NomesTiposPartida
        {
            get { return TiposPartida.Where(t => !t.Sensor && !t.Pid && !t.MaquinaSis && !t.MaquinaAgua).Select(t => t.Nome).ToArray(); }
        }

        /// <summary>Tipos da aba Sensores.</summary>
        public static string[] NomesTiposSensor
        {
            get { return TiposPartida.Where(t => t.Sensor).Select(t => t.Nome).ToArray(); }
        }

        public static bool EhSensor(Partida p)
        {
            TipoPartida t = BuscarTipo(p.Tipo);
            return t != null && t.Sensor;
        }

        public static bool EhPid(Partida p)
        {
            TipoPartida t = BuscarTipo(p.Tipo);
            return t != null && t.Pid;
        }

        // ===================================================================
        //  PID: PID_Compact (objeto tecnologico) numa FC propria, para chamar no OB ciclico
        // ===================================================================
        private const string TipoPid = "PID_Compact";
        private const string NomeUdtPid = "UDT_PID";

        // pinos do PID_Compact (Mode e InOut; para os fios conta como entrada)
        private static readonly string[][] PinosPid =
        {
            new[] {"Setpoint","Input","Real"}, new[] {"Input","Input","Real"},
            new[] {"Input_PER","Input","Int"}, new[] {"Disturbance","Input","Real"},
            new[] {"ManualEnable","Input","Bool"}, new[] {"ManualValue","Input","Real"},
            new[] {"ErrorAck","Input","Bool"}, new[] {"Reset","Input","Bool"},
            new[] {"ModeActivate","Input","Bool"}, new[] {"Mode","Input","Int"},
            new[] {"ScaledInput","Output","Real"}, new[] {"Output","Output","Real"},
            new[] {"Output_PER","Output","Int"}, new[] {"Output_PWM","Output","Bool"},
            new[] {"SetpointLimit_H","Output","Bool"}, new[] {"SetpointLimit_L","Output","Bool"},
            new[] {"InputWarning_H","Output","Bool"}, new[] {"InputWarning_L","Output","Bool"},
            new[] {"State","Output","Int"}, new[] {"Error","Output","Bool"},
            new[] {"ErrorBits","Output","DWord"}
        };

        // versoes tentadas ao criar o PID_Compact quando nao da para descobrir a do projeto
        private static readonly string[] VersoesPid = { "2.4", "3.0", "3.1", "2.3", "2.2", "2.1", "2.0" };

        private string _versaoPid;   // versao do PID_Compact desta geracao (a mesma da chamada na FC)

        /// <summary>FC dos PIDs do subsistema (chamar no OB de interrupcao ciclica).</summary>
        internal static string NomeFcPidDe(string sub)
        {
            return Cfg.Prefixo + "PID_" + sub;
        }

        /// <summary>Acionamento modulado por um PID (so partidas com velocidade e inversores por rede).</summary>
        internal static bool TemPidMod(Partida p)
        {
            return p != null && !string.IsNullOrWhiteSpace(p.ModPid) && !EhSensor(p) && !EhPid(p);
        }

        /// <summary>"FL1.ALC.PID" -> "&lt;Prefixo&gt;FL1.ALC.PID." (DB do PID com o prefixo, como os gerados).</summary>
        private static string BasePidRef(Partida p)
        {
            return Cfg.Prefixo + p.ModPid.Trim() + ".";
        }

        /// <summary>Sensor analogico do PID (aba Sensores): "SEN.RAE.ODI" -> "&lt;Prefixo&gt;SEN.RAE.ODI.".</summary>
        private static string BaseSensorPid(Partida pid)
        {
            return string.IsNullOrWhiteSpace(pid.TagSensor) ? null : Cfg.Prefixo + pid.TagSensor.Trim() + ".";
        }

        // ===================================================================
        //  Nivel digital alto + baixo do mesmo tanque: um BL_SENSOR_NIVEL_DIG so
        // ===================================================================

        // sensor digital -> o parceiro (baixo <-> alto) do mesmo DB e grupo
        private readonly Dictionary<Partida, Partida> _pares = new Dictionary<Partida, Partida>();

        private static bool EhNivelDigital(Partida p)
        {
            TipoPartida t = BuscarTipo(p.Tipo);
            return t != null && t.Sensor && t.Fb == FbSensorDig;
        }

        /// <summary>
        /// No mesmo DB e grupo, o 1o nivel baixo digital forma par com o 1o nivel alto digital,
        /// o 2o com o 2o, e assim por diante (ordem da lista). O que sobra fica sozinho.
        /// </summary>
        private void MontarPares(List<Partida> partidas)
        {
            _pares.Clear();
            foreach (var g in partidas.Where(EhNivelDigital).GroupBy(p => p.Sub + "." + p.Eq))
            {
                List<Partida> baixos = g.Where(x => !BuscarTipo(x.Tipo).SenAlto).ToList();
                List<Partida> altos = g.Where(x => BuscarTipo(x.Tipo).SenAlto).ToList();
                for (int i = 0; i < Math.Min(baixos.Count, altos.Count); i++)
                {
                    _pares[baixos[i]] = altos[i];
                    _pares[altos[i]] = baixos[i];
                    Log("INFO: " + g.Key + ": " + baixos[i].Fn + " (baixo) e " + altos[i].Fn
                        + " (alto) no mesmo BL_SENSOR_NIVEL_DIG (" + InstanciaSensor(baixos[i]) + ").");
                }
            }
        }

        /// <summary>Instancia do sensor; no par alto/baixo: i&lt;SUB&gt;_&lt;GRUPO&gt;_&lt;BAIXO&gt;_&lt;ALTO&gt;.</summary>
        private string InstanciaSensor(Partida p)
        {
            Partida par;
            if (!_pares.TryGetValue(p, out par)) return p.Instancia;
            Partida baixo = BuscarTipo(p.Tipo).SenAlto ? par : p;
            Partida alto = BuscarTipo(p.Tipo).SenAlto ? p : par;
            return baixo.Instancia + "_" + alto.Fn;
        }

        /// <summary>FC do subsistema: PARTIDAS_SUB, ou SENSORES (SENSORES_SUB) quando o grupo so tem sensores.</summary>
        internal static string NomeFcDe(string sub, IEnumerable<Partida> doGrupo)
        {
            List<Partida> semPid = doGrupo.Where(x => !EhPid(x)).ToList();
            if (semPid.Count > 0 && semPid.All(EhSensor))
                return Cfg.Prefixo + (sub == "SEN" ? "SENSORES" : "SENSORES_" + sub);
            return Cfg.Prefixo + "PARTIDAS_" + sub;
        }

        /// <summary>DBs de instancia da partida: (nome, FB padrao).</summary>
        internal List<KeyValuePair<string, string>> InstanciasDaPartida(Partida p)
        {
            TipoPartida t = BuscarTipo(p.Tipo);
            if (t.Pid) return new List<KeyValuePair<string, string>>();   // PID_Compact e objeto tecnologico (criado a parte)
            if (t.Sensor)
            {
                var sen = new List<KeyValuePair<string, string>>();
                if (t.SenAna || t.SenVazao) sen.Add(new KeyValuePair<string, string>(p.InstanciaAna, FbAnalogica));
                sen.Add(new KeyValuePair<string, string>(InstanciaSensor(p), t.Fb));
                if (t.SenVazaoMod) sen.Add(new KeyValuePair<string, string>(p.InstanciaMod, FbAnalogica));
                if (t.SenAnalitico || t.SenVazao) sen.Add(new KeyValuePair<string, string>(p.InstanciaSev, FbSeveridade));
                return sen;
            }

            if (t.MaquinaSis)   // nucleo da maquina: so o bloco da maquina
                return new List<KeyValuePair<string, string>> { new KeyValuePair<string, string>(p.Instancia, t.Fb) };

            var lista = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>(p.Instancia, t.Fb),
                new KeyValuePair<string, string>(p.InstanciaSev, FbSeveridade)
            };
            if (t.Bypass)
                lista.Add(new KeyValuePair<string, string>(p.InstanciaSev2, FbSeveridade));
            if (t.Ai)
                lista.Add(new KeyValuePair<string, string>(p.InstanciaLeiPos, FbAnalogica));
            if (t.Analogica)
                lista.Add(new KeyValuePair<string, string>(p.InstanciaSetPos, FbAnalogica));
            if (TemIntermitente(p))
                lista.Add(new KeyValuePair<string, string>(p.InstanciaItm, FbIntermitente));
            if (TemPidMod(p))
                lista.Add(new KeyValuePair<string, string>(p.InstanciaPidVel, FbAnalogica));
            if (TemExaustor(p))
            {
                lista.Add(new KeyValuePair<string, string>(p.InstanciaExa, FbPartida));
                lista.Add(new KeyValuePair<string, string>(p.InstanciaSevExa, FbSeveridade));
            }
            return lista;
        }

        /// <summary>UDT do membro da partida no DB do subsistema.</summary>
        internal static string UdtDe(Partida p)
        {
            TipoPartida t = BuscarTipo(p.Tipo);
            if (t != null && t.Udt != null) return t.Udt;
            return t != null && t.Valvula ? NomeUdtValvula : NomeUdt;
        }

        /// <summary>Tipos de partida que a interface oferece.</summary>
        public static readonly string[] TiposSuportados = TiposPartida.Select(t => t.Nome).ToArray();

        private const string FbSeveridade = "BL_SEVERIDADE";

        // --- Intermitente (BL_INTERMITENTE_3 / UDT_INTERMITENTE) ------------------
        private const string FbIntermitente = "BL_INTERMITENTE_3";
        private const string NomeUdtIntermitente = "UDT_INTERMITENTE";

        /// <summary>Opcoes da coluna Intermitente ("" = sem intermitente).</summary>
        public static readonly string[] UnidadesIntermitente = { "", "Segundos", "Minutos", "Horas" };

        private static readonly string[][] PinosIntermitente =
        {
            new[] {"ITM_CMD_LIG","Input","Bool"}, new[] {"MOT_LEI_CNF","Input","Bool"},
            new[] {"TPO_SET_LIG_MIN","Input","DInt"}, new[] {"TPO_SET_DES_MIN","Input","DInt"},
            new[] {"TPO_SET_LIG_SEG","Input","DInt"}, new[] {"TPO_SET_DES_SEG","Input","DInt"},
            new[] {"TPO_SET_LIG_HOR","Input","DInt"}, new[] {"TPO_SET_DES_HOR","Input","DInt"},
            new[] {"TPO_LEI_LIG","Output","DInt"}, new[] {"TPO_LEI_DES","Output","DInt"},
            new[] {"MOT_CMD_HAB","Output","Bool"}
        };

        // --- Modulacao pela vazao (LEI_VQP do medidor com modulacao de quimicos) ------
        internal static bool TemModulacao(Partida p)
        {
            return p != null && !string.IsNullOrWhiteSpace(p.ModVazao);
        }

        /// <summary>"SEN.FL1.VAZ" -> "&lt;Prefixo&gt;SEN.FL1.VAZ.LEI_VQP" (DB do sensor com o prefixo, como os gerados).</summary>
        private static string TagVqp(Partida p)
        {
            return Cfg.Prefixo + p.ModVazao.Trim() + ".LEI_VQP";
        }

        /// <summary>
        /// Bomba que modula pela vazao (como as bombas de floculante e coagulante do flotador):
        /// SIS_CMD_AUT = CMD_AUT da propria bomba e SIS_LEI_VAZ = LEI_VQP do medidor.
        /// </summary>
        private static void LigarModulacao(Rede r, int call, Partida p, string b)
        {
            if (!TemModulacao(p)) return;
            r.Entrada(call, "SIS_CMD_AUT", Rede.Ident(r.Acesso("SYM|" + b + "CMD_AUT")));
            r.Entrada(call, "SIS_LEI_VAZ", Rede.Ident(r.Acesso("SYM|" + TagVqp(p))));
        }

        // --- Protecao (DI NF -> memoria de defeito) e exaustor da cabine --------------
        internal static bool TemProtecao(Partida p)
        {
            return p != null && !EhSensor(p) && !EhPid(p) && !string.IsNullOrWhiteSpace(p.TagProtecao);
        }

        internal static bool TemExaustor(Partida p)
        {
            if (p == null || !p.Exaustor || EhSensor(p) || EhPid(p)) return false;
            TipoPartida t = BuscarTipo(p.Tipo);
            return t != null && !t.Valvula;
        }

        /// <summary>Struct do exaustor no equipamento: EXA (um so) ou EX1, EX2... (varios no mesmo equipamento).</summary>
        internal static string NomeExa(IEnumerable<Partida> todas, Partida p)
        {
            List<Partida> doEq = todas.Where(x => x.Sub == p.Sub && x.Eq == p.Eq && TemExaustor(x)).ToList();
            if (doEq.Count <= 1) return "EXA";
            return "EX" + (doEq.IndexOf(p) + 1);
        }

        /// <summary>Memoria espelho do defeito do exaustor (como MBB_SA1_EXA_LEI_DEF).</summary>
        internal static string TagDefExa(IEnumerable<Partida> todas, Partida p)
        {
            return p.Sub + "_" + p.Eq + "_" + NomeExa(todas, p) + "_LEI_DEF";
        }

        internal static bool TemIntermitente(Partida p)
        {
            return p != null && !EhSensor(p)
                && (!string.IsNullOrWhiteSpace(p.Intermitente) || !string.IsNullOrWhiteSpace(p.IntermitenteDes));
        }

        /// <summary>Unidade do tempo ligado (se so a do desligado foi escolhida, usa ela).</summary>
        private static string UnidadeLigado(Partida p)
        {
            return !string.IsNullOrWhiteSpace(p.Intermitente) ? p.Intermitente : p.IntermitenteDes;
        }

        /// <summary>Unidade do tempo desligado (se vazia, a mesma do tempo ligado).</summary>
        private static string UnidadeDesligado(Partida p)
        {
            return !string.IsNullOrWhiteSpace(p.IntermitenteDes) ? p.IntermitenteDes : p.Intermitente;
        }

        /// <summary>Sufixo dos pinos de tempo do BL_INTERMITENTE para a unidade escolhida.</summary>
        private static string SufixoUnidade(string unidade)
        {
            string u = (unidade ?? "").Trim().ToLowerInvariant();
            if (u.StartsWith("seg")) return "SEG";
            if (u.StartsWith("hor")) return "HOR";
            return "MIN";
        }

        /// <summary>
        /// Nome do struct do intermitente no equipamento: ITM quando so uma partida do equipamento
        /// tem intermitente; com varias, IT1, IT2... na ordem da lista (como "FL1".RAS.ITM e "FL1".FME.IT1).
        /// </summary>
        internal static string NomeItm(IEnumerable<Partida> todas, Partida p)
        {
            List<Partida> doEq = todas.Where(x => x.Sub == p.Sub && x.Eq == p.Eq && TemIntermitente(x)).ToList();
            if (doEq.Count <= 1) return "ITM";
            return "IT" + (doEq.IndexOf(p) + 1);
        }

        private static readonly string[][] PinosSeveridade =
        {
            new[] {"MOT_LEI_SV1","Input","Bool"}, new[] {"MOT_LEI_SV2","Input","Bool"},
            new[] {"MOT_LEI_SV3","Input","Bool"}, new[] {"MOT_LEI_SV4","Input","Bool"},
            new[] {"MOT_LEI_MAN","Input","Bool"}, new[] {"MOT_LEI_SVD","Output","Int"}
        };

        /// <summary>Acha o tipo pelo nome, pela chave curta ou pelo antigo "hibrida" (= partida direta).</summary>
        internal static TipoPartida BuscarTipo(string texto)
        {
            string t = (texto ?? "").Trim();
            foreach (TipoPartida x in TiposPartida)
            {
                if (string.Equals(x.Nome, t, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(x.Chave, t, StringComparison.OrdinalIgnoreCase))
                    return x;
            }
            if (string.Equals(t, "hibrida", StringComparison.OrdinalIgnoreCase)) return TiposPartida[0];
            // nome antigo da valvula do preparador (arquivos salvos antes da troca de nome)
            if (string.Equals(t, Maquinas.TipoAguaPreparadorAntigo, StringComparison.OrdinalIgnoreCase))
                return TiposPartida.First(x => x.MaquinaAgua);
            return null;
        }

        /// <summary>Nome oficial do tipo (converte arquivos antigos); texto desconhecido volta como veio.</summary>
        internal static string NormalizarTipo(string texto)
        {
            TipoPartida t = BuscarTipo(texto);
            return t != null ? t.Nome : texto;
        }

        private class SubsistemaGerado
        {
            public string Sub, NomeDb, NomeFc, ArqDb, ArqFc;
            public string NomeFcPid, ArqFcPid;   // FC dos PIDs (null = subsistema sem PID)
            public List<Partida> Partidas;
            public List<string> ArqsIdb = new List<string>();

            // "EQ.FN" -> n do SI<n>, lido do DB existente (null = pela ordem da lista)
            public Dictionary<string, int> SiMap;
        }

        // --- Estado --------------------------------------------------------
        private readonly Action<string> _log;
        private int _id;   // contador de IDs de objeto (hex)

        // Contato remoto/local usado na bobina de visualizacao (null = omitido)
        private string _remLoc;

        public Gerador(Action<string> log)
        {
            _log = log;
        }

        private void Log(string texto)
        {
            _log(texto);
        }

        // ===================================================================
        //  Validacao (pode ser chamada pela interface antes de gerar)
        // ===================================================================
        public static List<string> Validar(List<Io> ios, List<Partida> partidas)
        {
            var erros = new List<string>();
            var ident = new Regex("^[A-Za-z][A-Za-z0-9_]*$");

            foreach (Io io in ios)
            {
                string onde = "Aba IOs, linha " + io.Linha + ": ";
                if (!ident.IsMatch(io.Nome ?? ""))
                    erros.Add(onde + "nome de tag invalido '" + io.Nome + "'");
                string erroEnd = io.ErroNumero();
                // Endereco em branco e permitido: a tag simbolica pode ser gerada/listada
                // agora e receber o endereco fisico em uma etapa posterior.
                if (erroEnd != null && erroEnd != "sem numero")
                    erros.Add(onde + "tag '" + io.Nome + "': " + erroEnd);
                if (string.IsNullOrWhiteSpace(io.Tipo))
                    erros.Add(onde + "tag '" + io.Nome + "' sem tipo");
            }

            foreach (var g in ios.GroupBy(i => i.Nome).Where(g => g.Count() > 1))
                erros.Add("Aba IOs: tag duplicada '" + g.Key + "'");

            var vistos = new HashSet<string>();
            foreach (Partida p in partidas)
            {
                string aba = !string.IsNullOrEmpty(p.Aba) ? p.Aba : (EhSensor(p) ? "Sensores" : "Partidas");
                string onde = "Aba " + aba + ", linha " + p.Linha + ": ";

                // o tipo gravado tem que combinar com a aba
                TipoPartida tpAba = BuscarTipo(p.Tipo);
                if (tpAba != null && p.Aba == "Sensores" && !tpAba.Sensor)
                    erros.Add(onde + p.Eq + "." + p.Fn + " esta gravado como '" + p.Tipo
                        + "', que nao e sensor. Apague a linha e crie de novo (ou escolha outro tipo e volte).");
                if (tpAba != null && p.Aba == "Partidas" && tpAba.Sensor)
                    erros.Add(onde + "'" + p.Tipo + "' e sensor: cadastre na aba Sensores.");
                if (tpAba != null && p.Aba == "PIDs" && !tpAba.Pid)
                    erros.Add(onde + p.Eq + "." + p.Fn + " esta gravado como '" + p.Tipo + "', que nao e PID.");
                if (tpAba != null && p.Aba != "PIDs" && tpAba.Pid)
                    erros.Add(onde + "PID se cadastra na aba PIDs.");
                if (!ident.IsMatch(p.Sub ?? "")) erros.Add(onde + "subsistema invalido '" + p.Sub + "'");
                if (!ident.IsMatch(p.Eq ?? "")) erros.Add(onde + "equipamento invalido '" + p.Eq + "'");
                if (!ident.IsMatch(p.Fn ?? "")) erros.Add(onde + "funcao invalida '" + p.Fn + "'");
                if (BuscarTipo(p.Tipo) == null)
                    erros.Add(onde + "tipo de partida nao suportado '" + p.Tipo + "'");
                if (!vistos.Add(p.Sub + "." + p.Eq + "." + p.Fn))
                    erros.Add(onde + "partida duplicada " + p.Sub + "." + p.Eq + "." + p.Fn);
                if (p.Alternancia && !p.Auto)
                    erros.Add(onde + "alternancia exige operacao automatica marcada");
                if (!string.IsNullOrWhiteSpace(p.Intermitente) || !string.IsNullOrWhiteSpace(p.IntermitenteDes))
                {
                    if (!UnidadesIntermitente.Contains((p.Intermitente ?? "").Trim()))
                        erros.Add(onde + "intermitente (ligado) '" + p.Intermitente + "' invalido (use Segundos, Minutos ou Horas)");
                    else if (!UnidadesIntermitente.Contains((p.IntermitenteDes ?? "").Trim()))
                        erros.Add(onde + "intermitente (desligado) '" + p.IntermitenteDes + "' invalido (use Segundos, Minutos ou Horas)");
                    else if (EhSensor(p))
                        erros.Add(onde + "sensor nao tem intermitente");
                    else if (!p.Auto)
                        erros.Add(onde + "intermitente exige operacao automatica marcada");
                }
            }

            // PID: sensor analogico no formato DB.GRUPO.SENSOR; acionamento modulado: so com velocidade
            var rxRef = new Regex("^[A-Za-z][A-Za-z0-9_]*\\.[A-Za-z][A-Za-z0-9_]*\\.[A-Za-z][A-Za-z0-9_]*$");
            foreach (Partida p in partidas.Where(EhPid))
            {
                string onde = "Aba PIDs, linha " + p.Linha + ": ";
                if (string.IsNullOrWhiteSpace(p.TagSensor))
                    erros.Add(onde + "informe o sensor analogico do PID (DB.GRUPO.SENSOR, ex.: SEN.RAE.ODI)");
                else if (!rxRef.IsMatch(p.TagSensor.Trim()))
                    erros.Add(onde + "sensor '" + p.TagSensor + "' invalido (use DB.GRUPO.SENSOR, ex.: SEN.RAE.ODI)");
            }
            foreach (Partida p in partidas.Where(TemPidMod))
            {
                TipoPartida t = BuscarTipo(p.Tipo);
                string onde = "Aba Partidas, linha " + p.Linha + ": ";
                if (t != null && !(t.Velocidade || t.Rede))
                    erros.Add(onde + "'" + p.Tipo + "' nao tem velocidade para o PID modular "
                        + "(so inversor, diafragma e inversor por rede)");
                else if (!rxRef.IsMatch(p.ModPid.Trim()))
                    erros.Add(onde + "PID '" + p.ModPid + "' invalido (use DB.GRUPO.NOME, ex.: FL1.ALC.PID)");
            }

            // modulacao pela vazao: so partida (BL_PARTIDA_HIBRIDA / BL_INVERSOR), medidor no formato DB.GRUPO.SENSOR
            var rxMed = new Regex("^[A-Za-z][A-Za-z0-9_]*\\.[A-Za-z][A-Za-z0-9_]*\\.[A-Za-z][A-Za-z0-9_]*$");
            foreach (Partida p in partidas.Where(TemModulacao))
            {
                TipoPartida t = BuscarTipo(p.Tipo);
                string onde = "Aba Partidas, linha " + p.Linha + ": ";
                if (t != null && (t.Valvula || t.Sensor))
                    erros.Add(onde + "'" + p.Tipo + "' nao modula pela vazao (so partidas e inversores)");
                else if (!rxMed.IsMatch(p.ModVazao.Trim()))
                    erros.Add(onde + "medidor da modulacao '" + p.ModVazao + "' invalido (use DB.GRUPO.SENSOR, ex.: SEN.FL1.VAZ)");
            }

            // exaustor: so partidas (nao valvula); o struct EXA / EX<n> nao pode ter o nome de uma partida do equipamento
            foreach (Partida p in partidas.Where(x => x.Exaustor && !EhSensor(x) && !EhPid(x)))
            {
                TipoPartida t = BuscarTipo(p.Tipo);
                if (t != null && t.Valvula)
                {
                    erros.Add("Aba Partidas, linha " + p.Linha + ": valvula nao tem exaustor (so motores, ex.: sopradores).");
                    continue;
                }
                string exa = NomeExa(partidas, p);
                Partida igual = partidas.FirstOrDefault(x => x.Sub == p.Sub && x.Eq == p.Eq && x.Fn == exa);
                if (igual != null)
                    erros.Add("Exaustor de " + p.Sub + "." + p.Eq + "." + p.Fn + ": o struct seria '" + exa
                        + "', mas ja ha uma partida/sensor com esse nome no equipamento (linha " + igual.Linha + ").");
            }

            // o struct do intermitente (ITM / IT<n>) nao pode ter o nome de uma partida do mesmo equipamento
            foreach (Partida p in partidas.Where(x => BuscarTipo(x.Tipo) != null && TemIntermitente(x)))
            {
                string itm = NomeItm(partidas, p);
                Partida igual = partidas.FirstOrDefault(x => x.Sub == p.Sub && x.Eq == p.Eq && x.Fn == itm);
                if (igual != null)
                    erros.Add("Intermitente de " + p.Sub + "." + p.Eq + "." + p.Fn + ": o struct seria '" + itm
                        + "', mas ja ha uma partida/sensor com esse nome no equipamento (linha " + igual.Linha + ").");
            }

            // um DB tem so sensores (aba Sensores) ou so partidas (aba Partidas)
            foreach (var g in partidas.Where(x => BuscarTipo(x.Tipo) != null).GroupBy(x => x.Sub))
            {
                List<Partida> sens = g.Where(EhSensor).ToList();
                // (linha da aba Sensores com tipo errado ja foi acusada acima)
                List<Partida> parts = g.Where(x => !EhSensor(x) && !EhPid(x) && x.Aba != "Sensores" && x.Aba != "PIDs").ToList();
                if (sens.Count > 0 && parts.Count > 0)
                    erros.Add("O DB '" + g.Key + "' tem sensores (aba Sensores) e partidas (aba Partidas, linha(s) "
                        + string.Join(", ", parts.Select(x => x.Linha.ToString()).ToArray())
                        + ": " + string.Join(", ", parts.Select(x => x.Eq + "." + x.Fn).ToArray())
                        + "). Sensor vai na aba Sensores; partida em outro DB.");
            }

            // Alternancia: as partidas marcadas no mesmo equipamento formam pares (1a com 2a, 3a com 4a...)
            foreach (var g in partidas.Where(x => x.Alternancia).GroupBy(x => x.Sub + "." + x.Eq))
            {
                if (g.Count() % 2 != 0)
                    erros.Add("Alternancia em " + g.Key + ": marque um numero par de partidas "
                        + "(ha " + g.Count() + "); elas formam pares na ordem da lista.");
            }
            return erros;
        }

        // ===================================================================
        //  Execucao
        // ===================================================================
        /// <summary>
        /// selecionadas == null: gera tudo (recria os blocos do subsistema).
        /// selecionadas != null: atualiza so essas partidas nos blocos que ja existem.
        /// </summary>
        public bool Executar(List<Io> ios, List<Partida> partidas, List<Partida> selecionadas = null)
        {
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

            Log("Genesis · motor de geração v8.0 (sensores do preparador: medidor por tempo de agua, niveis sem TPB/TPA)");
            Log("");

            // 1) Pasta de export: reserva de modelos de DB de instancia (o principal vem do projeto)
            string pastaExport = null;
            bool automatica = false;

            if (!string.IsNullOrWhiteSpace(Cfg.PastaExport))
            {
                if (Directory.Exists(Cfg.PastaExport))
                    pastaExport = Cfg.PastaExport;
                else
                    Log("AVISO: a pasta do export informada nao existe (" + Cfg.PastaExport
                        + "); procurando automaticamente.");
            }

            if (pastaExport == null)
            {
                pastaExport = AcharPastaExport(desktop);
                automatica = pastaExport != null;
            }

            if (pastaExport != null)
                Log("Pasta de export (reserva de modelos): " + pastaExport + (automatica ? " (automatico)" : ""));

            MontarPares(partidas);

            List<string> fbsNecessarios = partidas
                .SelectMany(p => InstanciasDaPartida(p).Select(x => x.Value))
                .Distinct().ToList();
            Log("");

            // 2) Conecta no TIA
            var processos = TiaPortal.GetProcesses().ToArray();
            if (processos.Length == 0)
            {
                Log("Nenhuma instancia do TIA Portal encontrada.");
                return false;
            }

            using (TiaPortal tia = processos[0].Attach())
            {
                if (tia.Projects.Count == 0)
                {
                    Log("Nenhum projeto aberto no TIA.");
                    return false;
                }

                Project projeto = tia.Projects[0];
                Log("Projeto: " + projeto.Name);

                PlcSoftware plc = EncontrarPlc(projeto);
                if (plc == null)
                {
                    Log("Software PLC nao encontrado.");
                    return false;
                }

                // versao de cada FB que existe no projeto (ex.: BL_VALVULA_6 no lugar de BL_VALVULA_5)
                if (!ResolverFbs(plc, fbsNecessarios)) return false;
                fbsNecessarios = fbsNecessarios.Select(FbReal).Distinct().ToList();

                // modelos de DB de instancia: do proprio projeto (mesma versao do FB) ou da pasta de export
                Dictionary<string, string> templates = CarregarTemplates(plc, fbsNecessarios, pastaExport);
                if (templates == null) return false;
                _templatesFb = templates;
                _todas = partidas;
                VerificarUdts(plc, selecionadas ?? partidas);
                Log("");

                // contato remoto/local da bobina de visualizacao: so se o DB existir
                _remLoc = null;
                if (!string.IsNullOrWhiteSpace(Cfg.ContatoRemLoc))
                {
                    string dbRemLoc = Cfg.ContatoRemLoc.Trim().Split('.')[0];
                    if (ExisteBloco(plc.BlockGroup, dbRemLoc))
                    {
                        _remLoc = Cfg.ContatoRemLoc.Trim();
                        Log("Contato remoto/local: " + _remLoc + " (em paralelo com CMD_AUT na bobina VIS.OFF).");
                    }
                    else
                    {
                        Log("AVISO: o bloco '" + dbRemLoc + "' (contato remoto/local) nao existe no projeto;");
                        Log("       a bobina de visualizacao usara somente CMD_AUT (a comunicacao usa o contato mesmo assim).");
                    }
                }

                // 3) Tags de PLC (antes: endereco %M para as memorias de defeito que ainda nao tem)
                AlocarDefeitos(plc, partidas, selecionadas ?? partidas, ios);
                AlocarAuxiliares(plc, selecionadas ?? partidas, ios);
                PrepararTagsComunicacao(plc, partidas, ios);
                HashSet<string> tags = CriarTags(plc, ios);

                Func<string, bool> existe = delegate (string nome)
                {
                    return !string.IsNullOrEmpty(nome) && tags.Contains(nome);
                };

                AvisarTagsAusentes(selecionadas ?? partidas, existe, ios);
                AvisarModulacao(partidas, selecionadas ?? partidas);

                // nas redes, toda tag com nome vai pelo nome, mesmo que ainda nao exista no PLC:
                // fica pronta, bastando declarar a tag (a lista do que falta saiu acima)
                Func<string, bool> usar = delegate (string nome) { return !string.IsNullOrWhiteSpace(nome); };

                // so a comunicacao (botao da pagina Modbus)
                if (SoComunicacao)
                {
                    string saidaCom = Path.Combine(Path.GetTempPath(), "Genesis", "Com_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
                    Directory.CreateDirectory(saidaCom);
                    bool okCom = false;
                    try
                    {
                        PlcBlockUserGroup grupoCom = plc.BlockGroup.Groups.Find(NomeGrupo) ?? plc.BlockGroup.Groups.Create(NomeGrupo);
                        okCom = GerarComunicacao(plc, grupoCom, partidas, existe, pastaExport, saidaCom);
                        Log(okCom ? "CONCLUIDO sem erros." : "CONCLUIDO com pendencias (veja acima).");
                        return okCom;
                    }
                    finally
                    {
                        LimparSaida(saidaCom, true);
                    }
                }

                // PIDs: objeto tecnologico PID_Compact (antes das FCs, que chamam ele com a versao certa)
                _versaoPid = null;
                List<Partida> pidsAlvo = (selecionadas ?? partidas).Where(EhPid).ToList();
                if (pidsAlvo.Count > 0)
                {
                    GarantirPids(plc, pidsAlvo);
                    Log("");
                }

                if (selecionadas != null)
                {
                    return ExecutarSelecionadas(plc, partidas, selecionadas, templates, usar);
                }

                // 4) Gera XMLs
                LimparXmlAntigos();
                string saida = Path.Combine(Path.GetTempPath(), "Genesis",
                    "Gerado_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
                Directory.CreateDirectory(saida);
                var utf8 = new UTF8Encoding(false);

                var arquivos = new List<SubsistemaGerado>();

                foreach (var g in partidas.GroupBy(p => p.Sub))
                {
                    var sg = new SubsistemaGerado();
                    sg.Sub = g.Key;
                    sg.NomeDb = Cfg.Prefixo + g.Key;
                    sg.NomeFc = NomeFcDe(g.Key, g);
                    sg.Partidas = g.ToList();

                    sg.ArqDb = Path.Combine(saida, sg.NomeDb + ".xml");
                    File.WriteAllText(sg.ArqDb, XmlDb(sg), utf8);

                    var jaFeitas = new HashSet<string>();
                    foreach (Partida p in sg.Partidas)
                    {
                        foreach (var inst in InstanciasDaPartida(p))
                        {
                            if (!jaFeitas.Add(inst.Key)) continue;   // par alto/baixo: instancia unica
                            string arq = Path.Combine(saida, inst.Key + ".xml");
                            File.WriteAllText(arq, XmlInstancia(templates[FbReal(inst.Value)], inst.Key), utf8);
                            sg.ArqsIdb.Add(arq);
                        }
                    }

                    if (sg.Partidas.Any(x => !EhPid(x)))
                    {
                        sg.ArqFc = Path.Combine(saida, sg.NomeFc + ".xml");
                        File.WriteAllText(sg.ArqFc, XmlFc(sg, usar), utf8);
                    }

                    List<Partida> pids = sg.Partidas.Where(EhPid).ToList();
                    if (pids.Count > 0 && !Cfg.PidNoOb)
                    {
                        sg.NomeFcPid = NomeFcPidDe(g.Key);
                        sg.ArqFcPid = Path.Combine(saida, sg.NomeFcPid + ".xml");
                        File.WriteAllText(sg.ArqFcPid, XmlFcPid(sg, sg.NomeFcPid, pids), utf8);
                    }

                    arquivos.Add(sg);
                }

                Log("");

                // 5) Importa e compila
                //    Os XMLs so servem para a importacao: a pasta temporaria e apagada
                //    ao terminar. Se a importacao falhar (excecao), ela e mantida para diagnostico.
                bool importou = false;
                try
                {
                    PlcBlockUserGroup grupo =
                        plc.BlockGroup.Groups.Find(NomeGrupo)
                        ?? plc.BlockGroup.Groups.Create(NomeGrupo);

                    bool tudoOk = true;
                    foreach (SubsistemaGerado sg in arquivos)
                    {
                        if (!LimparBlocosAnteriores(plc, grupo, sg))
                        {
                            tudoOk = false;
                            continue;
                        }
                        try
                        {
                            if (!ImportarECompilar(grupo, sg)) tudoOk = false;
                        }
                        catch (Exception ex)
                        {
                            // um subsistema com erro de importacao nao impede os outros
                            tudoOk = false;
                            Log("ERRO na importacao do subsistema " + sg.Sub + ": " + DetalheErro(ex));
                            Log("      Os outros subsistemas continuam. Mande esta mensagem se nao souber o que e.");
                            // compila o que chegou a entrar (DB e instancias), para nao travar quem le esse DB
                            try
                            {
                                var jaImportados = new List<string> { sg.NomeDb };
                                jaImportados.AddRange(sg.Partidas.SelectMany(x => InstanciasDaPartida(x).Select(i => i.Key)).Distinct());
                                CompilarPorNome(plc, jaImportados.Where(n => ExisteBloco(plc.BlockGroup, n)).ToList());
                            }
                            catch { /* so tentativa */ }
                            Log("");
                        }
                    }

                    // mapa Modbus (DB COM + FC de mapeamento), com todas as partidas
                    if (!GerarComunicacao(plc, grupo, partidas, existe, pastaExport, saida)) tudoOk = false;

                    // PIDs direto no OB ciclico (OB30)
                    if (Cfg.PidNoOb && partidas.Any(EhPid))
                    {
                        try
                        {
                            if (!GerarPidsNoOb(plc, grupo, partidas, saida, null)) tudoOk = false;
                        }
                        catch (Exception ex)
                        {
                            tudoOk = false;
                            Log("ERRO nos PIDs do OB" + NumeroObPid + ": " + DetalheErro(ex));
                        }
                    }

                    importou = true;
                    Log(tudoOk ? "CONCLUIDO sem erros." : "CONCLUIDO com pendencias (veja acima).");
                    return tudoOk;
                }
                finally
                {
                    LimparSaida(saida, importou);
                }
            }
        }

        // ===================================================================
        //  Tags de PLC
        // ===================================================================
        private HashSet<string> CriarTags(PlcSoftware plc, List<Io> ios)
        {
            var existentes = new HashSet<string>();
            ColetarTags(plc.TagTableGroup, existentes);

            PlcTagTable tabela =
                plc.TagTableGroup.TagTables.Find(NomeGrupo)
                ?? plc.TagTableGroup.TagTables.Create(NomeGrupo);

            int criadas = 0, reaproveitadas = 0, falhas = 0, semEndereco = 0;
            foreach (Io io in ios)
            {
                if (existentes.Contains(io.Nome))
                {
                    reaproveitadas++;
                    continue;
                }

                // sem endereco o TIA nao cria a tag: fica para declarar (entra na lista dos avisos)
                if (string.IsNullOrWhiteSpace(io.Numero))
                {
                    semEndereco++;
                    continue;
                }

                try
                {
                    tabela.Tags.Create(io.Nome, io.Tipo, io.Endereco);
                    existentes.Add(io.Nome);
                    criadas++;
                }
                catch (Exception ex)
                {
                    falhas++;
                    Log("  FALHA ao criar tag " + io.Nome + ": " + PrimeiraLinha(ex));
                }
            }

            Log("Tags: " + criadas + " criadas, " + reaproveitadas + " ja existiam, "
                + (semEndereco > 0 ? semEndereco + " sem endereco (nao criadas), " : "")
                + falhas + " falhas");
            Log("");
            return existentes;
        }

        // ===================================================================
        //  Defeitos: um bit por defeito nas words DEFEITOS_1 (MW502), DEFEITOS_2 (MW504)...
        // ===================================================================

        /// <summary>Avisa a tela das tags criadas aqui (para entrarem na aba Tags e ficarem no projeto).</summary>
        public Action<List<Io>> AoCriarTags;

        /// <summary>Memorias de defeito de cada item, na ordem: nome -> descricao.</summary>
        private static List<KeyValuePair<string, string>> MemoriasDeDefeito(List<Partida> todas, IEnumerable<Partida> alvo)
        {
            var lista = new List<KeyValuePair<string, string>>();
            Action<string, string> add = delegate (string nome, string desc)
            {
                if (string.IsNullOrWhiteSpace(nome)) return;
                nome = nome.Trim();
                if (!lista.Any(x => x.Key == nome)) lista.Add(new KeyValuePair<string, string>(nome, desc.Trim()));
            };
            foreach (Partida p in alvo)
            {
                TipoPartida t = BuscarTipo(p.Tipo);
                if (t == null || t.Pid) continue;
                string d = (p.Descricao ?? "").Trim();
                if (t.SenRaspador)
                {
                    add(p.TagDefDireito, "DEFEITO LADO DIREITO " + d);
                    add(p.TagDefEsquerdo, "DEFEITO LADO ESQUERDO " + d);
                    continue;
                }
                if (t.Sensor || t.MaquinaSis) continue;
                add(p.TagDefEfetiva, "DEFEITO " + d);
                if (TemProtecao(p)) add(p.TagProtecaoMemEfetiva, "DEFEITO PROTEÇÃO " + d);
                if (TemExaustor(p)) add(TagDefExa(todas, p), "DEFEITO EXAUSTOR " + d);
            }
            return lista;
        }

        private static readonly Regex RxBitM = new Regex(@"^%?M(\d+)\.([0-7])$", RegexOptions.IgnoreCase);
        private static readonly Regex RxPalM = new Regex(@"^%?M([BWD])(\d+)$", RegexOptions.IgnoreCase);

        /// <summary>Marca os bits de %M ocupados por um endereco (bit, byte, word ou dword).</summary>
        private static void OcuparBits(string endereco, HashSet<int> usados)
        {
            string e = (endereco ?? "").Trim().Replace(" ", "");
            Match m = RxBitM.Match(e);
            if (m.Success) { usados.Add(int.Parse(m.Groups[1].Value) * 8 + int.Parse(m.Groups[2].Value)); return; }
            m = RxPalM.Match(e);
            if (!m.Success) return;
            int bytes = m.Groups[1].Value.ToUpperInvariant() == "B" ? 1 : m.Groups[1].Value.ToUpperInvariant() == "W" ? 2 : 4;
            int ini = int.Parse(m.Groups[2].Value);
            for (int b = ini; b < ini + bytes; b++)
                for (int k = 0; k < 8; k++) usados.Add(b * 8 + k);
        }

        /// <summary>
        /// Da endereco as memorias de defeito que ainda nao tem (nem no PLC, nem na aba Tags):
        /// proximo bit livre a partir de Cfg.InicioDefeitos (502.1; o 502.0 fica para a emergencia geral).
        /// Cria tambem as words DEFEITOS_n (MW502, MW504...) que cobrem esses bits.
        /// </summary>
        private void AlocarDefeitos(PlcSoftware plc, List<Partida> todas, List<Partida> alvo, List<Io> ios)
        {
            if (!Cfg.AlocarDefeitos) return;

            Match mi = RxBitM.Match("M" + (Cfg.InicioDefeitos ?? "").Trim());
            if (!mi.Success)
            {
                Log("AVISO: inicio dos defeitos '" + Cfg.InicioDefeitos + "' invalido (use byte.bit, ex.: 502.1); defeitos sem endereco automatico.");
                return;
            }
            int inicio = int.Parse(mi.Groups[1].Value) * 8 + int.Parse(mi.Groups[2].Value);
            int byteWord0 = int.Parse(mi.Groups[1].Value) & ~1;   // 1a word: MW502 (word comeca em byte par)
            string prefWord = string.IsNullOrWhiteSpace(Cfg.NomeWordDefeitos) ? "DEFEITOS_" : Cfg.NomeWordDefeitos.Trim();

            // tags do PLC: nome -> endereco (as words DEFEITOS_n se sobrepoem aos bits de proposito: nao contam)
            var noPlc = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            ColetarEnderecos(plc.TagTableGroup, noPlc);

            var usados = new HashSet<int>();
            foreach (var kv in noPlc)
                if (!kv.Key.StartsWith(prefWord, StringComparison.OrdinalIgnoreCase)) OcuparBits(kv.Value, usados);
            foreach (Io io in ios)
                if (!string.IsNullOrWhiteSpace(io.Numero) && !(io.Nome ?? "").StartsWith(prefWord, StringComparison.OrdinalIgnoreCase))
                    OcuparBits(io.Endereco, usados);

            var novas = new List<Io>();
            var linhas = new List<string>();
            int proximo = inicio;
            int maiorByte = -1;
            foreach (var def in MemoriasDeDefeito(todas, alvo))
            {
                if (noPlc.ContainsKey(def.Key)) continue;                     // ja existe no PLC: fica onde esta
                Io io = ios.FirstOrDefault(x => string.Equals((x.Nome ?? "").Trim(), def.Key, StringComparison.OrdinalIgnoreCase));
                if (io != null && !string.IsNullOrWhiteSpace(io.Numero)) continue;   // ja tem endereco na aba Tags

                while (usados.Contains(proximo)) proximo++;
                string numero = (proximo / 8) + "." + (proximo % 8);
                usados.Add(proximo);
                maiorByte = Math.Max(maiorByte, proximo / 8);

                if (io == null)
                {
                    io = new Io { Nome = def.Key, Area = "MEM", Numero = numero, Tipo = "Bool", Descricao = Textos.Padrao(def.Value) };
                    ios.Add(io);
                    novas.Add(io);
                }
                else
                {
                    io.Area = "MEM";
                    io.Numero = numero;
                    io.Tipo = "Bool";
                    if (string.IsNullOrWhiteSpace(io.Descricao)) io.Descricao = def.Value;
                }
                linhas.Add("   %M" + numero + "  " + def.Key);
            }

            // words DEFEITOS_n que cobrem os bits usados
            if (maiorByte >= 0)
            {
                for (int w = byteWord0; w <= maiorByte; w += 2)
                {
                    string nome = prefWord + ((w - byteWord0) / 2 + 1);
                    bool existeNome = noPlc.ContainsKey(nome) || ios.Any(x => string.Equals(x.Nome, nome, StringComparison.OrdinalIgnoreCase));
                    if (existeNome) continue;

                    // outra tag (que nao e de defeitos) ocupando essa word: nao cria a DEFEITOS_n por cima
                    var bitsWord = new HashSet<int>();
                    OcuparBits("%MW" + w, bitsWord);
                    string alheia = noPlc.Where(kv => !kv.Key.StartsWith(prefWord, StringComparison.OrdinalIgnoreCase))
                        .Select(kv => new { kv.Key, kv.Value })
                        .Concat(ios.Where(x => !string.IsNullOrWhiteSpace(x.Numero) && !(x.Nome ?? "").StartsWith(prefWord, StringComparison.OrdinalIgnoreCase))
                            .Select(x => new { Key = x.Nome, Value = x.Endereco }))
                        .Where(t => { var b = new HashSet<int>(); OcuparBits(t.Value, b); return b.Count > 8 && b.Overlaps(bitsWord); })
                        .Select(t => t.Key + " (" + t.Value + ")").FirstOrDefault();
                    if (alheia != null)
                    {
                        Log("AVISO: %MW" + w + " (seria " + nome + ") ja esta ocupada por " + alheia + "; os defeitos pularam essa word.");
                        continue;
                    }
                    var word = new Io { Nome = nome, Area = "MEM", Numero = w.ToString(), Tipo = "Word", Descricao = "DEFEITOS " + ((w - byteWord0) / 2 + 1) };
                    ios.Add(word);
                    novas.Add(word);
                    linhas.Add("   %MW" + w + "  " + nome);
                }
            }

            if (linhas.Count > 0)
            {
                Log("Defeitos: endereco automatico a partir do %M" + Cfg.InicioDefeitos + " (" + linhas.Count + " tag(s)):");
                foreach (string l in linhas) Log(l);
                Log("");
            }
            if (AoCriarTags != null && linhas.Count > 0) AoCriarTags(novas);
        }

        /// <summary>
        /// Memorias auxiliares (Int) das redes: SET_AUX dos acionamentos modulados por PID e SET_POS_AUX / LEI_POS_AUX
        /// das valvulas analogicas. As que ainda nao tem endereco ganham a proxima %MW livre a partir de Cfg.InicioAux.
        /// </summary>
        private void AlocarAuxiliares(PlcSoftware plc, List<Partida> alvo, List<Io> ios)
        {
            var pedidos = new List<KeyValuePair<string, string>>();
            Action<string, string> add = delegate (string nome, string desc)
            {
                if (!string.IsNullOrWhiteSpace(nome) && !pedidos.Any(x => x.Key == nome))
                    pedidos.Add(new KeyValuePair<string, string>(nome, desc));
            };
            foreach (Partida p in alvo)
            {
                TipoPartida t = BuscarTipo(p.Tipo);
                if (t == null) continue;
                string d = (p.Descricao ?? "").Trim();
                if (TemPidMod(p)) add(p.TagSetVelAux, "AUX VELOCIDADE PID " + d);
                if (t.Valvula && t.Analogica)
                {
                    add(p.TagSetPosAux, "AUX SET POSIÇÃO " + d);
                    if (t.Ai) add(p.TagLeiPosAux, "AUX LEITURA POSIÇÃO " + d);
                }
            }
            if (pedidos.Count == 0) return;

            int ini;
            if (!int.TryParse((Cfg.InicioAux ?? "").Trim(), out ini) || ini < 0) ini = 100;
            ini &= ~1;   // word em byte par

            var noPlc = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            ColetarEnderecos(plc.TagTableGroup, noPlc);
            var usados = new HashSet<int>();
            foreach (var kv in noPlc) OcuparBits(kv.Value, usados);
            foreach (Io io in ios)
                if (!string.IsNullOrWhiteSpace(io.Numero)) OcuparBits(io.Endereco, usados);

            var novas = new List<Io>();
            var linhas = new List<string>();
            int w = ini;
            foreach (var ped in pedidos)
            {
                if (noPlc.ContainsKey(ped.Key)) continue;
                Io io = ios.FirstOrDefault(x => string.Equals((x.Nome ?? "").Trim(), ped.Key, StringComparison.OrdinalIgnoreCase));
                if (io != null && !string.IsNullOrWhiteSpace(io.Numero)) continue;

                // proxima word (2 bytes) inteiramente livre
                while (Enumerable.Range(w * 8, 16).Any(usados.Contains)) w += 2;
                for (int k = w * 8; k < w * 8 + 16; k++) usados.Add(k);

                if (io == null)
                {
                    io = new Io { Nome = ped.Key, Area = "MEM", Numero = w.ToString(), Tipo = "Int", Descricao = Textos.Padrao(ped.Value) };
                    ios.Add(io);
                    novas.Add(io);
                }
                else
                {
                    io.Area = "MEM";
                    io.Numero = w.ToString();
                    io.Tipo = "Int";
                    if (string.IsNullOrWhiteSpace(io.Descricao)) io.Descricao = ped.Value;
                }
                linhas.Add("   %MW" + w + "  " + ped.Key);
                w += 2;
            }

            if (linhas.Count > 0)
            {
                Log("Memorias auxiliares: endereco automatico a partir do %MW" + ini + " (" + linhas.Count + " tag(s)):");
                foreach (string l in linhas) Log(l);
                Log("");
            }
            if (AoCriarTags != null && linhas.Count > 0) AoCriarTags(novas);
        }

        /// <summary>OU de contatos (do trilho) para uma lista de condicoes; null se a lista e vazia.</summary>
        private string OuDeContatos(Rede r, List<KeyValuePair<string, bool>> conds)
        {
            if (conds == null) return null;
            return OuDeContatos(r, conds.Select(c => new List<KeyValuePair<string, bool>> { c }).ToList());
        }

        /// <summary>OU de ramos; cada ramo e uma serie (E) de contatos.</summary>
        private string OuDeContatos(Rede r, List<List<KeyValuePair<string, bool>>> ramos)
        {
            if (ramos == null || ramos.Count == 0) return null;
            var saidas = new List<string>();
            foreach (var ramo in ramos)
            {
                string ant = null;
                foreach (var c in ramo)
                {
                    int ct = r.Contato(SimboloUsuario(c.Key), c.Value);
                    if (ant == null) r.ParaTrilho(ct, "in"); else r.Fio(ant, Rede.Pino(ct, "in"));
                    ant = Rede.Pino(ct, "out");
                }
                if (ant != null) saidas.Add(ant);
            }
            if (saidas.Count == 1) return saidas[0];
            int ou = r.Ou(saidas.Count);
            for (int k = 0; k < saidas.Count; k++) r.Fio(saidas[k], Rede.Pino(ou, "in" + (k + 1)));
            return Rede.Pino(ou, "out");
        }

        /// <summary>Severidade 1: memoria da protecao e defeitos externos (em OU); null se nao houver.</summary>
        private string FonteSv1(Rede r, Partida p)
        {
            var l = new List<KeyValuePair<string, bool>>();
            if (TemProtecao(p)) l.Add(new KeyValuePair<string, bool>(p.TagProtecaoMemEfetiva, false));
            l.AddRange(Partida.Linhas(p.DefeitosExtras));
            if (l.Count == 0) return null;
            if (l.Count == 1 && !l[0].Value) return Rede.Ident(r.Acesso("SYM|" + SimboloUsuario(l[0].Key)));
            return OuDeContatos(r, l);
        }

        /// <summary>
        /// Simbolo digitado na tela: "SEN.EQU.TAN.LEI_BAI" ganha o prefixo dos DBs gerados (G_SEN...) quando o
        /// primeiro nome e um DB do gerador; tags do PLC (sem ponto) ficam como estao.
        /// </summary>
        private string SimboloUsuario(string s)
        {
            s = (s ?? "").Trim();
            if (string.IsNullOrEmpty(Cfg.Prefixo) || !s.Contains(".")) return s;
            string db = s.Substring(0, s.IndexOf('.'));
            if (db.StartsWith(Cfg.Prefixo, StringComparison.OrdinalIgnoreCase)) return s;
            bool doGerador = _todas.Any(x => string.Equals(x.Sub, db, StringComparison.OrdinalIgnoreCase));
            return doGerador ? Cfg.Prefixo + s : s;
        }

        /// <summary>Membros extras do DB para as maquinas (no "Gerar marcadas", quando o DB ja existe).</summary>
        private static IEnumerable<System.Xml.Linq.XElement> ExtrasMaquinaDb(Partida p, System.Xml.Linq.XNamespace ns)
        {
            if (!Maquinas.EhPreparador(p) || EhSensor(p)) yield break;
            TipoPartida t = BuscarTipo(p.Tipo);
            if (t != null && t.MaquinaSis)
            {
                yield return new System.Xml.Linq.XElement(ns + "Member",
                    new System.Xml.Linq.XAttribute("Name", NomeTimerPreparador),
                    new System.Xml.Linq.XAttribute("Datatype", "IEC_TIMER"),
                    new System.Xml.Linq.XAttribute("Version", "1.0"));
            }
            else
            {
                yield return new System.Xml.Linq.XElement(ns + "Member",
                    new System.Xml.Linq.XAttribute("Name", "SIS"),
                    new System.Xml.Linq.XAttribute("Datatype", "Struct"),
                    new System.Xml.Linq.XElement(ns + "Member",
                        new System.Xml.Linq.XAttribute("Name", "VIS"),
                        new System.Xml.Linq.XAttribute("Datatype", "Struct"),
                        new System.Xml.Linq.XElement(ns + "Member",
                            new System.Xml.Linq.XAttribute("Name", "OFF"),
                            new System.Xml.Linq.XAttribute("Datatype", "Bool"))));
            }
        }

        /// <summary>Confere se as UDTs que os DBs vao usar existem no projeto (senao o DB importa quebrado).</summary>
        private void VerificarUdts(PlcSoftware plc, List<Partida> alvo)
        {
            var nomes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try { ColetarUdts(plc.TypeGroup, nomes); }
            catch (Exception ex)
            {
                Log("AVISO: nao consegui listar as UDTs do projeto (" + DetalheErro(ex) + ").");
                return;
            }

            var falta = new Dictionary<string, List<string>>();
            foreach (Partida p in alvo)
            {
                TipoPartida t = BuscarTipo(p.Tipo);
                if (t == null) continue;
                var usadas = new List<string> { UdtDe(p) };
                if (TemIntermitente(p)) usadas.Add(NomeUdtIntermitente);
                if (TemExaustor(p)) usadas.Add(NomeUdt);
                foreach (string u in usadas)
                {
                    if (string.IsNullOrEmpty(u) || nomes.Contains(u)) continue;
                    if (!falta.ContainsKey(u)) falta[u] = new List<string>();
                    falta[u].Add(p.Sub + "." + p.Eq + "." + p.Fn);
                }
            }
            foreach (var kv in falta)
            {
                Log("ERRO: a UDT " + kv.Key + " nao existe no projeto (usada por " + string.Join(", ", kv.Value.Take(5).ToArray())
                    + (kv.Value.Count > 5 ? "..." : "") + ").");
                Log("      Copie a " + kv.Key + " da biblioteca para PLC data types; sem ela o DB fica quebrado.");
            }
        }

        private static void ColetarUdts(object grupo, HashSet<string> destino)
        {
            if (grupo == null) return;
            var tipos = grupo.GetType().GetProperty("Types");
            var col = tipos != null ? tipos.GetValue(grupo, null) as System.Collections.IEnumerable : null;
            if (col != null)
                foreach (object t in col)
                {
                    var pn = t.GetType().GetProperty("Name");
                    if (pn != null) destino.Add(Convert.ToString(pn.GetValue(t, null)));
                }
            var grupos = grupo.GetType().GetProperty("Groups");
            var gs = grupos != null ? grupos.GetValue(grupo, null) as System.Collections.IEnumerable : null;
            if (gs != null)
                foreach (object g in gs) ColetarUdts(g, destino);
        }

        private static void ColetarEnderecos(PlcTagTableGroup grupo, Dictionary<string, string> destino)
        {
            foreach (PlcTagTable tabela in grupo.TagTables)
            {
                foreach (PlcTag tag in tabela.Tags)
                {
                    string end;
                    try { end = tag.LogicalAddress; }
                    catch { end = ""; }
                    destino[tag.Name] = end ?? "";
                }
            }
            foreach (PlcTagTableGroup sub in grupo.Groups)
                ColetarEnderecos(sub, destino);
        }

        private static void ColetarTags(PlcTagTableGroup grupo, HashSet<string> destino)
        {
            foreach (PlcTagTable tabela in grupo.TagTables)
            {
                foreach (PlcTag tag in tabela.Tags)
                    destino.Add(tag.Name);
            }
            foreach (PlcTagTableGroup sub in grupo.Groups)
                ColetarTags(sub, destino);
        }

        // tags usadas nas redes que ainda nao existem no PLC: nome -> tipo sugerido
        private readonly Dictionary<string, string> _paraDeclarar =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private void AvisarTagsAusentes(List<Partida> partidas, Func<string, bool> existe, List<Io> ios)
        {
            _paraDeclarar.Clear();
            Declarar(Cfg.TagReset, "Bool", existe);
            Declarar(Cfg.TagEmergencia, "Bool", existe);

            foreach (Partida p in partidas)
            {
                string id = p.Sub + "." + p.Eq + "." + p.Fn;
                TipoPartida tipo = BuscarTipo(p.Tipo);

                if (tipo.Pid || tipo.MaquinaSis) continue;   // so acessam DBs (sensor / maquina)
                if (tipo.MaquinaAgua)
                {
                    Conferir(id, "saida da valvula de alimentacao (DO)", p.TagHab, "Bool", existe);
                    Declarar(p.TagDefEfetiva, "Bool", existe);
                    continue;
                }

                if (tipo.SenVazao)
                {
                    Conferir(id, "entrada analogica da vazao instantanea (IN ENTRADA do BL_ANALOGICA)", p.TagSensor, "Int", existe);
                    if (!Maquinas.EhPreparador(p))
                        Conferir(id, "entrada digital de pulso do totalizador (SEN_LEI_PUL)", p.TagPulso, "Bool", existe);
                    else if (!existe(Cfg.TagClock1Hz))
                        Log("AVISO: " + id + ": o clock de 1 Hz '" + Cfg.TagClock1Hz + "' nao existe; o totalizador conta so com a agua habilitada (ative a clock memory do CLP).");
                    continue;
                }

                if (tipo.SenRaspador)
                {
                    Conferir(id, "sensor da pa do lado direito (SEN_LEI_DIR)", p.TagSensor, "Bool", existe);
                    Conferir(id, "sensor da pa do lado esquerdo (SEN_LEI_LES)", p.TagSensor2, "Bool", existe);
                    Conferir(id, "confirmacao da partida do raspador (MOT_LEI_CNF)", p.TagConf, "Bool", existe);
                    Declarar(p.TagDefDireito, "Bool", existe);
                    Declarar(p.TagDefEsquerdo, "Bool", existe);
                    continue;
                }

                if (tipo.Sensor)
                {
                    Conferir(id, tipo.SenAna ? "entrada analogica do sensor (IN ENTRADA)" : "entrada digital do sensor",
                        p.TagSensor, tipo.SenAna ? "Int" : "Bool", existe);
                    continue;   // sensor nao tem espelho de defeito
                }

                if (tipo.Valvula)
                {
                    if (tipo.Di2)
                    {
                        Conferir(id, "confirmacao aberta (VAL_CNF_ABR)", p.TagConf, "Bool", existe);
                        Conferir(id, "confirmacao fechada (VAL_CNF_FEC)", p.TagConfFec, "Bool", existe);
                    }
                    if (tipo.Analogica)
                    {
                        Conferir(id, "saida analogica de posicao (OUT VALOR do BL_ANALOGICA)", p.TagRef, "Int", existe);
                        if (tipo.Ai)
                            Conferir(id, "entrada analogica de posicao (IN ENTRADA do BL_ANALOGICA)", p.TagPosAi, "Int", existe);
                        Declarar(p.TagSetPosAux, "Int", existe);
                        if (tipo.Ai) Declarar(p.TagLeiPosAux, "Int", existe);
                    }
                    else
                    {
                        Conferir(id, "saida 'abre' (bobina de LEI_ABR)", p.TagHab, "Bool", existe);
                        if (tipo.Do2)
                            Conferir(id, "saida 'fecha' (bobina de LEI_FEC)", p.TagFec, "Bool", existe);
                    }
                }
                else
                {
                    if (tipo.Bypass)
                    {
                        Conferir(id, "habilita do contator 1 (MOT_CMD_HA1)", p.TagHabC1, "Bool", existe);
                        Conferir(id, "habilita do contator 2 (MOT_CMD_HA2)", p.TagHabC2, "Bool", existe);
                    }
                    else if (!tipo.Drive && !tipo.Rede)
                    {
                        Conferir(id, "confirmacao (MOT_LEI_CN1)", p.TagConf, "Bool", existe);
                    }

                    // habilita do drive/contator: bobina espelhando LEI_HAB (nao existe no inversor por rede)
                    if (!tipo.Rede)
                        Conferir(id, "habilita (bobina de LEI_HAB)", p.TagHab, "Bool", existe);
                    if (tipo.Drive)
                        Conferir(id, "defeito do drive (MOT_LEI_TRP)", p.TagDefDrive, "Bool", existe);
                    if (tipo.Velocidade)
                        Conferir(id, "referencia de velocidade (MOT_LEI_VEL)", p.TagRef, "Int", existe);
                }

                // modulacao por PID: memoria com a velocidade (vai no MOT_SET_VEL)
                if (TemPidMod(p)) Declarar(p.TagSetVelAux, "Int", existe);

                // protecao: DI (contato NF) e memoria de defeito, resetada pelo reset geral
                if (TemProtecao(p))
                {
                    Conferir(id, "protecao do equipamento (DI, contato NF)", p.TagProtecao, "Bool", existe);
                    Declarar(p.TagProtecaoMemEfetiva, "Bool", existe);
                    if (!existe(Cfg.TagReset))
                        Log("AVISO: " + id + ": sem tag de reset geral na configuracao, a memoria da protecao "
                            + p.TagProtecaoMemEfetiva + " nunca sera resetada.");
                }

                // exaustor da cabine: confirmacao (DI), habilita (DO) e espelho de defeito
                if (TemExaustor(p))
                {
                    Conferir(id, "confirmacao do exaustor (DI)", p.TagExaConf, "Bool", existe);
                    Conferir(id, "habilita do exaustor (DO)", p.TagExaHab, "Bool", existe);
                    Declarar(TagDefExa(partidas, p), "Bool", existe);
                }

                // espelho de defeito: a informada ou o nome automatico SUB_EQ_FN_LEI_DEF
                Declarar(p.TagDefEfetiva, "Bool", existe);
            }

            if (partidas.Any(x => { TipoPartida t = BuscarTipo(x.Tipo); return t != null && t.Rede; }))
                Log("INFO: inversor por rede: o pino INV_SET_END (identificador de hardware do drive) fica aberto para preencher no TIA.");

            if (_paraDeclarar.Count > 0)
            {
                Log("");
                Log("Tags usadas nas redes que ainda NAO existem no PLC (" + _paraDeclarar.Count
                    + "). As redes ja usam esses nomes: declare no TIA, com o endereco, e compile.");
                foreach (var kv in _paraDeclarar.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
                {
                    Io io = ios.FirstOrDefault(i => string.Equals((i.Nome ?? "").Trim(), kv.Key,
                        StringComparison.OrdinalIgnoreCase));
                    string tipoTag = io != null && !string.IsNullOrWhiteSpace(io.Tipo) ? io.Tipo : kv.Value;
                    string obs = io != null && string.IsNullOrWhiteSpace(io.Numero)
                        ? "   <- esta na aba IOs sem endereco" : "";
                    Log("   " + kv.Key + "  (" + tipoTag + ")" + obs);
                }
            }
            Log("");
        }

        /// <summary>Bomba que modula por um medidor que nao esta na aba Sensores como "com modulacao de quimicos".</summary>
        private void AvisarModulacao(List<Partida> todas, List<Partida> alvo)
        {
            Func<string, Partida> achar = delegate (string ref_)
            {
                return todas.FirstOrDefault(x =>
                    string.Equals(x.Sub + "." + x.Eq + "." + x.Fn, ref_.Trim(), StringComparison.OrdinalIgnoreCase));
            };

            foreach (Partida p in alvo.Where(TemPidMod))
            {
                Partida pid = achar(p.ModPid);
                if (pid == null || !EhPid(pid))
                    Log("AVISO: " + p.Sub + "." + p.Eq + "." + p.Fn + ": o PID " + p.ModPid.Trim()
                        + " nao esta na aba PIDs; a rede usa " + BasePidRef(p) + "LEI_VEL (tem que existir no projeto).");
            }
            foreach (Partida pid in alvo.Where(EhPid))
            {
                string refPid = pid.Sub + "." + pid.Eq + "." + pid.Fn;
                if (!todas.Any(x => TemPidMod(x) && string.Equals(x.ModPid.Trim(), refPid, StringComparison.OrdinalIgnoreCase)))
                    Log("AVISO: o PID " + refPid + " nao modula nenhum acionamento (LEI_VEL fica sem uso). Na aba Partidas, "
                        + "escolha " + refPid + " na coluna \"Modula pelo PID\" de cada inversor/bomba.");

                if (string.IsNullOrWhiteSpace(pid.TagSensor)) continue;
                Partida sen = achar(pid.TagSensor);
                TipoPartida ts = sen != null ? BuscarTipo(sen.Tipo) : null;
                if (sen == null)
                    Log("AVISO: PID " + pid.Sub + "." + pid.Eq + "." + pid.Fn + ": o sensor " + pid.TagSensor.Trim()
                        + " nao esta na aba Sensores (tem que existir no projeto, com LEI_NIV, SET_OMI, SET_UNM e CMD_LOP).");
                else if (ts == null || !ts.SenAna)
                    Log("AVISO: PID " + pid.Sub + "." + pid.Eq + "." + pid.Fn + ": o sensor " + pid.TagSensor.Trim()
                        + " nao e analogico (nivel analogico ou analitico); o PID le LEI_NIV, SET_OMI e SET_UNM dele.");
            }

            foreach (Partida p in alvo.Where(TemModulacao))
            {
                string ref_ = p.ModVazao.Trim();
                Partida med = todas.FirstOrDefault(x =>
                    string.Equals(x.Sub + "." + x.Eq + "." + x.Fn, ref_, StringComparison.OrdinalIgnoreCase));
                TipoPartida tm = med != null ? BuscarTipo(med.Tipo) : null;

                if (med == null)
                    Log("AVISO: " + p.Sub + "." + p.Eq + "." + p.Fn + ": o medidor " + ref_
                        + " nao esta na aba Sensores; a rede usa " + TagVqp(p) + " (tem que existir no projeto).");
                else if (tm == null || !tm.SenVazaoMod)
                    Log("AVISO: " + p.Sub + "." + p.Eq + "." + p.Fn + ": o sensor " + ref_
                        + " nao e 'Medidor de vazão com modulação de químicos'; o LEI_VQP dele nao e calculado.");
            }
        }

        /// <summary>Campo vazio: avisa (pino aberto / bobina omitida). Com nome: entra na lista se nao existir.</summary>
        private void Conferir(string id, string rotulo, string tag, string tipo, Func<string, bool> existe)
        {
            if (string.IsNullOrWhiteSpace(tag))
                Log("AVISO: " + id + ": " + rotulo + " nao informada; fica aberto/omitido.");
            else
                Declarar(tag, tipo, existe);
        }

        private void Declarar(string tag, string tipo, Func<string, bool> existe)
        {
            if (string.IsNullOrWhiteSpace(tag) || existe(tag)) return;
            string n = tag.Trim();
            if (!_paraDeclarar.ContainsKey(n)) _paraDeclarar[n] = tipo;
        }

        // ===================================================================
        //  Geracao de XML
        // ===================================================================
        private string XmlDb(SubsistemaGerado sg)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            sb.AppendLine("<Document>");
            sb.AppendLine("  <Engineering version=\"V20\" />");
            sb.AppendLine("  <SW.Blocks.GlobalDB ID=\"0\">");
            sb.AppendLine("    <AttributeList>");
            sb.AppendLine("      <AutoNumber>true</AutoNumber>");
            sb.AppendLine("      <Interface><Sections xmlns=\"" + NsIf + "\">");
            sb.AppendLine("        <Section Name=\"Static\">");

            var eqComSis = new HashSet<string>();
            foreach (var eq in sg.Partidas.GroupBy(p => p.Eq))
            {
                sb.AppendLine("          <Member Name=\"" + eq.Key
                    + "\" Datatype=\"Struct\" Remanence=\"Retain\" Accessibility=\"Public\">");

                int ordem = 0;
                foreach (Partida p in eq)
                {
                    bool sensor = EhSensor(p) || EhPid(p);   // sensor e PID nao tem SI
                    if (!sensor) ordem++;
                    sb.AppendLine("            <Member Name=\"" + p.Fn + "\" Datatype=\"&quot;"
                        + UdtDe(p) + "&quot;\">");
                    if (!string.IsNullOrEmpty(p.Descricao))
                    {
                        sb.AppendLine("              <Comment><MultiLanguageText Lang=\"pt-BR\">"
                            + SecurityElement.Escape(Textos.Padrao(p.Descricao)) + "</MultiLanguageText></Comment>");
                    }
                    sb.AppendLine("            </Member>");

                    // maquina: o nucleo ganha o temporizador do automatico; as partes dividem um SIS (visibilidade)
                    if (Maquinas.EhPreparador(p) && !EhSensor(p))
                    {
                        if (BuscarTipo(p.Tipo).MaquinaSis)
                            sb.AppendLine("            <Member Name=\"" + NomeTimerPreparador + "\" Datatype=\"IEC_TIMER\" Version=\"1.0\" />");
                        else if (!eqComSis.Contains(eq.Key))
                        {
                            eqComSis.Add(eq.Key);
                            sb.AppendLine("            <Member Name=\"SIS\" Datatype=\"Struct\">");
                            sb.AppendLine("              <Comment><MultiLanguageText Lang=\"pt-BR\">VISIBILIDADE DA MÁQUINA</MultiLanguageText></Comment>");
                            sb.AppendLine("              <Member Name=\"VIS\" Datatype=\"Struct\">");
                            sb.AppendLine("                <Member Name=\"OFF\" Datatype=\"Bool\" />");
                            sb.AppendLine("              </Member>");
                            sb.AppendLine("            </Member>");
                        }
                        continue;
                    }

                    // visibilidade na IHM: SI<n>.VIS.OFF (n = ordem da partida no equipamento); sensor nao tem
                    if (sensor) continue;
                    sb.AppendLine("            <Member Name=\"SI" + ordem + "\" Datatype=\"Struct\">");
                    sb.AppendLine("              <Comment><MultiLanguageText Lang=\"pt-BR\">VISIBILIDADE</MultiLanguageText></Comment>");
                    sb.AppendLine("              <Member Name=\"VIS\" Datatype=\"Struct\">");
                    sb.AppendLine("                <Member Name=\"OFF\" Datatype=\"Bool\" />");
                    sb.AppendLine("              </Member>");
                    sb.AppendLine("            </Member>");

                    // exaustor: struct EXA / EX<n> (UDT_PARTIDAS), sem SI
                    if (TemExaustor(p))
                    {
                        sb.AppendLine("            <Member Name=\"" + NomeExa(sg.Partidas, p) + "\" Datatype=\"&quot;"
                            + NomeUdt + "&quot;\">");
                        sb.AppendLine("              <Comment><MultiLanguageText Lang=\"pt-BR\">"
                            + SecurityElement.Escape(Textos.Padrao("EXAUSTOR " + (p.Descricao ?? "")))
                            + "</MultiLanguageText></Comment>");
                        sb.AppendLine("            </Member>");
                    }

                    // intermitente: struct ITM / IT<n> logo depois do SI da partida
                    if (TemIntermitente(p))
                    {
                        sb.AppendLine("            <Member Name=\"" + NomeItm(sg.Partidas, p) + "\" Datatype=\"&quot;"
                            + NomeUdtIntermitente + "&quot;\">");
                        sb.AppendLine("              <Comment><MultiLanguageText Lang=\"pt-BR\">"
                            + SecurityElement.Escape(Textos.Padrao("INTERMITENTE " + (p.Descricao ?? "")))
                            + "</MultiLanguageText></Comment>");
                        sb.AppendLine("            </Member>");
                    }
                }

                sb.AppendLine("          </Member>");
            }

            sb.AppendLine("        </Section>");
            sb.AppendLine("      </Sections></Interface>");
            sb.AppendLine("      <MemoryLayout>Optimized</MemoryLayout>");
            sb.AppendLine("      <Name>" + sg.NomeDb + "</Name>");
            sb.AppendLine("      <Namespace />");
            sb.AppendLine("      <ProgrammingLanguage>DB</ProgrammingLanguage>");
            sb.AppendLine("    </AttributeList>");
            sb.AppendLine("  </SW.Blocks.GlobalDB>");
            sb.AppendLine("</Document>");
            return sb.ToString();
        }

        private static string XmlInstancia(string template, string novoNome)
        {
            // troca o primeiro <Name>..</Name> e remove o <Number> (AutoNumber = true)
            string r = new Regex("<Name>[^<]*</Name>")
                .Replace(template, "<Name>" + SecurityElement.Escape(novoNome) + "</Name>", 1);
            r = Regex.Replace(r, @"\s*<Number>\d+</Number>", "");
            return r;
        }

        private string XmlFc(SubsistemaGerado sg, Func<string, bool> existe, IEnumerable<Partida> emitir = null)
        {
            _id = 1;
            var sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            sb.AppendLine("<Document>");
            sb.AppendLine("  <Engineering version=\"V20\" />");
            sb.AppendLine("  <SW.Blocks.FC ID=\"0\">");
            sb.AppendLine("    <AttributeList>");
            sb.AppendLine("      <AutoNumber>true</AutoNumber>");
            sb.AppendLine("      <Interface><Sections xmlns=\"" + NsIf + "\">");
            sb.AppendLine("        <Section Name=\"Input\" />");
            sb.AppendLine("        <Section Name=\"Output\" />");
            sb.AppendLine("        <Section Name=\"InOut\" />");
            List<Partida> comTemp = (emitir ?? sg.Partidas).Where(x => !EhPid(x))
                .Where(x => { TipoPartida t = BuscarTipo(x.Tipo); return t.SenAna && !t.SenAnalitico; }).ToList();
            if (comTemp.Count == 0)
                sb.AppendLine("        <Section Name=\"Temp\" />");
            else
            {
                sb.AppendLine("        <Section Name=\"Temp\">");
                foreach (Partida x in comTemp)
                    sb.AppendLine("          <Member Name=\"" + x.TempLeiAux + "\" Datatype=\"Real\" />");
                sb.AppendLine("        </Section>");
            }
            sb.AppendLine("        <Section Name=\"Constant\" />");
            sb.AppendLine("        <Section Name=\"Return\">");
            sb.AppendLine("          <Member Name=\"Ret_Val\" Datatype=\"Void\" Accessibility=\"Public\" />");
            sb.AppendLine("        </Section>");
            sb.AppendLine("      </Sections></Interface>");
            sb.AppendLine("      <MemoryLayout>Optimized</MemoryLayout>");
            sb.AppendLine("      <Name>" + sg.NomeFc + "</Name>");
            sb.AppendLine("      <Namespace />");
            sb.AppendLine("      <ProgrammingLanguage>LAD</ProgrammingLanguage>");
            sb.AppendLine("      <SetENOAutomatically>false</SetENOAutomatically>");
            sb.AppendLine("    </AttributeList>");
            sb.AppendLine("    <ObjectList>");
            List<Partida> lista = (emitir ?? sg.Partidas).Where(x => !EhPid(x)).ToList();
            foreach (Partida p in lista)
            {
                Partida par;
                if (_pares.TryGetValue(p, out par) && BuscarTipo(p.Tipo).SenAlto && lista.Contains(par))
                    continue;   // vai na rede do nivel baixo do mesmo tanque
                sb.Append(XmlRede(sg, p, existe));
                if (TemExaustor(p)) sb.Append(XmlRedeExaustor(sg, p, existe));
            }
            sb.AppendLine("    </ObjectList>");
            sb.AppendLine("  </SW.Blocks.FC>");
            sb.AppendLine("</Document>");
            return sb.ToString();
        }

        /// <summary>
        /// Bypass: nao ha entrada de confirmacao do contator. O pino recebe
        /// LEI_HAB em serie com o liberar (CMD_LO1 ou CMD_LO2) NEGADO.
        /// </summary>
        private static void LigarCnBypass(Rede r, int call, string pino, string b, string liberar)
        {
            int cHab = r.Contato(b + "LEI_HAB", false);
            r.ParaTrilho(cHab, "in");
            int cLo = r.Contato(b + liberar, true);
            r.Fio(Rede.Pino(cHab, "out"), Rede.Pino(cLo, "in"));
            r.Entrada(call, pino, Rede.Pino(cLo, "out"));
        }

        /// <summary>Ordem (1, 2, 3...) da partida entre as do mesmo equipamento: usada em SI&lt;n&gt;.</summary>
        private static int IndiceSi(SubsistemaGerado sg, Partida p)
        {
            int n;
            if (sg.SiMap != null && sg.SiMap.TryGetValue(p.Eq + "." + p.Fn, out n)) return n;
            return sg.Partidas.Where(x => x.Eq == p.Eq && !EhSensor(x) && !EhPid(x) && !Maquinas.EhPreparador(x)).ToList().IndexOf(p) + 1;
        }

        /// <summary>Par de alternancia: as partidas marcadas do mesmo equipamento formam pares na ordem da lista.</summary>
        private static Partida Parceiro(SubsistemaGerado sg, Partida p)
        {
            List<Partida> marcadas = sg.Partidas.Where(x => x.Eq == p.Eq && x.Alternancia).ToList();
            int i = marcadas.IndexOf(p);
            if (i < 0) return null;
            int j = (i % 2 == 0) ? i + 1 : i - 1;
            return (j >= 0 && j < marcadas.Count) ? marcadas[j] : null;
        }

        private string XmlRede(SubsistemaGerado sg, Partida p, Func<string, bool> existe)
        {
            TipoPartida tipo = BuscarTipo(p.Tipo);
            if (tipo.Sensor) return XmlRedeSensor(sg, p, tipo, existe);
            if (tipo.MaquinaSis) return XmlRedesPreparador(sg, p, tipo, existe);
            if (tipo.MaquinaAgua) return XmlRedeAguaPreparador(sg, p, tipo, existe);

            string b = sg.NomeDb + "." + p.Eq + "." + p.Fn + ".";
            var r = new Rede();

            // na valvula: CMD_LIG -> CMD_ABR, CMD_LO1 -> CMD_LOP, severidade em LEI_SVD
            string cmdLig = tipo.Valvula ? "CMD_ABR" : "CMD_LIG";
            string lo1 = tipo.Valvula ? "CMD_LOP" : "CMD_LO1";
            string pinoInt = tipo.Valvula ? "VAL_CMD_INT" : "MOT_CMD_INT";
            string saidaSev = tipo.Valvula ? "LEI_SVD" : "LEI_SV1";

            // --- Chamada do FB de partida, alimentada pelo trilho ---------------
            int call = r.Chamada(FbReal(tipo.Fb), p.Instancia, tipo.Pinos);
            r.ParaTrilho(call, "en");

            // --- Sistema: reset e emergencia (so se a tag existir no PLC) -------
            if (existe(Cfg.TagReset))
                r.Entrada(call, "SIS_CMD_RST", Rede.Ident(r.Acesso("SYM|" + Cfg.TagReset)));
            else
                r.Entrada(call, "SIS_CMD_RST", Rede.Ident(r.Acesso("LIT|Bool|false")));

            if (existe(Cfg.TagEmergencia))
            {
                int ce = r.Contato(Cfg.TagEmergencia, true);   // contato negado
                r.ParaTrilho(ce, "in");
                r.Entrada(call, "SIS_LEI_EME", Rede.Pino(ce, "out"));
            }

            // --- Intertravamento: MOT_CMD_INT = CMD_INT OU CMD_LO1 ---------------
            //     (com bypass: CMD_INT OU (CMD_LO1 E CMD_LO2))
            int c1 = r.Contato(b + "CMD_INT", false);
            int c2 = r.Contato(b + lo1, false);
            r.ParaTrilho(c1, "in");
            r.ParaTrilho(c2, "in");

            int ramoLo = c2;
            if (tipo.Bypass)
            {
                int c3 = r.Contato(b + "CMD_LO2", false);
                r.Fio(Rede.Pino(c2, "out"), Rede.Pino(c3, "in"));
                ramoLo = c3;
            }

            int ou = r.Ou(2);
            r.Fio(Rede.Pino(c1, "out"), Rede.Pino(ou, "in1"));
            r.Fio(Rede.Pino(ramoLo, "out"), Rede.Pino(ou, "in2"));
            r.Entrada(call, pinoInt, Rede.Pino(ou, "out"));

            // --- Pinos do FB (dependem do bloco: por terminais ou por rede) -------
            if (tipo.Valvula)
                LigarPinosValvula(r, call, tipo, p, b, existe);
            else if (tipo.Rede)
                LigarPinosRede(r, call, tipo, p, b, existe);
            else
                LigarPinosTerminais(r, call, tipo, p, b, existe);

            // intermitente: o LEI_HAB dele entra no intertravamento (que passa a existir mesmo sem a caixa marcada)
            bool temItm = TemIntermitente(p);
            string bi = temItm ? sg.NomeDb + "." + p.Eq + "." + NomeItm(sg.Partidas, p) + "." : null;
            bool temInt = p.Intertravamento || temItm;

            // --- Contato de defeito: espelho, reset do automatico e alternancia ---
            string tagDef = p.TagDefEfetiva;
            bool temDef = existe(tagDef);
            Partida par = p.Alternancia ? Parceiro(sg, p) : null;

            // com protecao ou exaustor o LEI_DEF do bloco fica sozinho no espelho, e o reset vem de
            // LEI_DEF OU EXA.LEI_DEF OU memoria da protecao (como nos sopradores do MBBR)
            bool temProt = TemProtecao(p);
            bool temExa = TemExaustor(p);
            var extrasDef = Partida.Linhas(p.DefeitosExtras);
            bool separar = temProt || temExa || extrasDef.Count > 0;

            if (temDef || p.Auto || separar)
            {
                string origem;   // pino que alimenta os resets
                if (separar)
                {
                    if (temDef)
                    {
                        int cdEsp = r.Contato(b + "LEI_DEF", false);
                        r.ParaTrilho(cdEsp, "in");
                        int bd = r.Bobina("Coil", tagDef);
                        r.Fio(Rede.Pino(cdEsp, "out"), Rede.Pino(bd, "in"));
                    }

                    var fontes = new List<string>();
                    int cDef = r.Contato(b + "LEI_DEF", false);
                    r.ParaTrilho(cDef, "in");
                    fontes.Add(Rede.Pino(cDef, "out"));
                    if (temExa)
                    {
                        int cExa = r.Contato(sg.NomeDb + "." + p.Eq + "." + NomeExa(sg.Partidas, p) + ".LEI_DEF", false);
                        r.ParaTrilho(cExa, "in");
                        fontes.Add(Rede.Pino(cExa, "out"));
                    }
                    if (temProt)
                    {
                        int cPrt = r.Contato(p.TagProtecaoMemEfetiva, false);
                        r.ParaTrilho(cPrt, "in");
                        fontes.Add(Rede.Pino(cPrt, "out"));
                    }
                    foreach (var ex in extrasDef)   // defeitos externos (ex.: pas do raspador)
                    {
                        int cEx = r.Contato(SimboloUsuario(ex.Key), ex.Value);
                        r.ParaTrilho(cEx, "in");
                        fontes.Add(Rede.Pino(cEx, "out"));
                    }
                    int ouDef = r.Ou(fontes.Count);
                    for (int k = 0; k < fontes.Count; k++) r.Fio(fontes[k], Rede.Pino(ouDef, "in" + (k + 1)));
                    origem = Rede.Pino(ouDef, "out");
                }
                else
                {
                    int cd = r.Contato(b + "LEI_DEF", false);
                    r.ParaTrilho(cd, "in");
                    if (temDef)
                    {
                        int bd = r.Bobina("Coil", tagDef);
                        r.Fio(Rede.Pino(cd, "out"), Rede.Pino(bd, "in"));
                    }
                    origem = Rede.Pino(cd, "out");
                }

                // sem automatico, a protecao/exaustor ainda precisa desligar o comando
                if (p.Auto || separar || Maquinas.EhParte(p))
                {
                    // defeito desliga o comando; nas partes do preparador, o mesmo contato LEI_DEF
                    // tambem alimenta a bobina de espelho TagDef, evitando contato duplicado.
                    int rlig = r.Bobina("RCoil", b + cmdLig);
                    r.Fio(origem, Rede.Pino(rlig, "in"));
                }

                if (p.Auto)
                {
                    if (par == null)
                    {
                        // sem alternancia: defeito tira da automatico
                        int raut = r.Bobina("RCoil", b + "CMD_AUT");
                        r.Fio(origem, Rede.Pino(raut, "in"));
                    }
                    else
                    {
                        // alternancia: na falha (em automatico), a outra bomba e liberada
                        // se estiver em automatico, e esta sai do automatico
                        string o = sg.NomeDb + "." + par.Eq + "." + par.Fn + ".";

                        int caut = r.Contato(b + "CMD_AUT", false);
                        r.Fio(origem, Rede.Pino(caut, "in"));

                        int coAut = r.Contato(o + "CMD_AUT", false);
                        int coNao = r.Contato(o + "CMD_AUT", true);
                        r.Fio(Rede.Pino(caut, "out"), Rede.Pino(coAut, "in"));
                        r.Fio(Rede.Pino(caut, "out"), Rede.Pino(coNao, "in"));

                        int rLo = r.Bobina("RCoil", o + lo1);
                        int rA1 = r.Bobina("RCoil", b + "CMD_AUT");
                        int rA2 = r.Bobina("RCoil", b + "CMD_AUT");
                        r.Fio(Rede.Pino(coAut, "out"), Rede.Pino(rLo, "in"));
                        r.Fio(Rede.Pino(rLo, "out"), Rede.Pino(rA1, "in"));
                        r.Fio(Rede.Pino(coNao, "out"), Rede.Pino(rA2, "in"));
                    }
                }
            }

            // --- Protecao: NAO DI -> S memoria; reset geral -> R memoria -------------------
            if (temProt)
            {
                int cPro = r.Contato(p.TagProtecao.Trim(), true);
                r.ParaTrilho(cPro, "in");
                int sPro = r.Bobina("SCoil", p.TagProtecaoMemEfetiva);
                r.Fio(Rede.Pino(cPro, "out"), Rede.Pino(sPro, "in"));

                if (existe(Cfg.TagReset))
                {
                    int cRst = r.Contato(Cfg.TagReset, false);
                    r.ParaTrilho(cRst, "in");
                    int rPro = r.Bobina("RCoil", p.TagProtecaoMemEfetiva);
                    r.Fio(Rede.Pino(cRst, "out"), Rede.Pino(rPro, "in"));
                }
            }

            // --- Espelho de habilita: saida que habilita o drive/contator (todos os tipos) ---
            // valvula: LEI_FEC -> saida "fecha" (so com 2 DO)
            if (tipo.Valvula && tipo.Do2 && existe(p.TagFec))
            {
                int cf = r.Contato(b + "LEI_FEC", false);
                r.ParaTrilho(cf, "in");
                int bf = r.Bobina("Coil", p.TagFec);
                r.Fio(Rede.Pino(cf, "out"), Rede.Pino(bf, "in"));
            }

            // habilita do drive/contator (LEI_HAB) ou, na valvula, a saida "abre" (LEI_ABR)
            if (!tipo.Rede && !tipo.Analogica && existe(p.TagHab))
            {
                int ch = r.Contato(b + (tipo.Valvula ? "LEI_ABR" : "LEI_HAB"), false);
                r.ParaTrilho(ch, "in");
                int bh = r.Bobina("Coil", p.TagHab);
                r.Fio(Rede.Pino(ch, "out"), Rede.Pino(bh, "in"));
            }

            // --- Bypass: CMD_LO1 negado SETA o CMD_LO2 --------------------------------
            if (tipo.Bypass)
            {
                int cNaoLo1 = r.Contato(b + lo1, true);
                r.ParaTrilho(cNaoLo1, "in");
                int sLo2 = r.Bobina("SCoil", b + "CMD_LO2");
                r.Fio(Rede.Pino(cNaoLo1, "out"), Rede.Pino(sLo2, "in"));
            }

            // --- Intertravamento: [condicao] E AUT E NAO BYP -> CMD_INT --------------
            //     O contato da condicao fica sem tag, para preencher no TIA.
            //     Com intermitente: ([condicao] OU (NAO ITM.LEI_HAB E ITM.CMD_LIG)) E AUT E NAO BYP
            if (temInt)
            {
                string ultimo = null;   // saida da condicao (pino)

                if (p.Intertravamento)
                {
                    // condicoes informadas (em OU); sem nenhuma, um contato sem tag para preencher no TIA
                    ultimo = OuDeContatos(r, Partida.LinhasE(p.CondicoesInt));
                    if (ultimo == null)
                    {
                        int cCond = r.ContatoVazio();
                        r.ParaTrilho(cCond, "in");
                        ultimo = Rede.Pino(cCond, "out");
                    }
                }

                if (temItm)
                {
                    int nHab = r.Contato(bi + "LEI_HAB", true);
                    r.ParaTrilho(nHab, "in");
                    int cItm = r.Contato(bi + "CMD_LIG", false);
                    r.Fio(Rede.Pino(nHab, "out"), Rede.Pino(cItm, "in"));

                    if (ultimo == null)
                    {
                        ultimo = Rede.Pino(cItm, "out");
                    }
                    else
                    {
                        int ouC = r.Ou(2);
                        r.Fio(ultimo, Rede.Pino(ouC, "in1"));
                        r.Fio(Rede.Pino(cItm, "out"), Rede.Pino(ouC, "in2"));
                        ultimo = Rede.Pino(ouC, "out");
                    }
                }

                if (p.Auto)
                {
                    int cA = r.Contato(b + "CMD_AUT", false);
                    r.Fio(ultimo, Rede.Pino(cA, "in"));
                    int cB = r.Contato(b + "CMD_BYP", true);
                    r.Fio(Rede.Pino(cA, "out"), Rede.Pino(cB, "in"));
                    ultimo = Rede.Pino(cB, "out");
                }

                int bInt = r.Bobina("Coil", b + "CMD_INT");
                r.Fio(ultimo, Rede.Pino(bInt, "in"));
            }

            // --- Operacao automatica -------------------------------------------------
            //   Set:   AUT E NAO INT E NAO LO1   -> CMD_LIG
            //   Reset: AUT E (INT OU LO1)        -> CMD_LIG
            //   Os contatos do CMD_INT entram SEMPRE (mesmo sem a caixa Intertravamento marcada): sem a
            //   rede do CMD_INT eles nao fazem nada, e para intertravar depois basta criar essa rede no TIA.
            if (p.Auto)
            {
                int cAut = r.Contato(b + "CMD_AUT", false);
                r.ParaTrilho(cAut, "in");

                int ramo = cAut;
                int nInt = r.Contato(b + "CMD_INT", true);
                r.Fio(Rede.Pino(ramo, "out"), Rede.Pino(nInt, "in"));
                ramo = nInt;
                int nLo = r.Contato(b + lo1, true);
                r.Fio(Rede.Pino(ramo, "out"), Rede.Pino(nLo, "in"));
                int sLig = r.Bobina("SCoil", b + cmdLig);

                if (tipo.Bypass)
                {
                    // bypass: NAO LO1 em paralelo com NAO LO2
                    int nLo2 = r.Contato(b + "CMD_LO2", true);
                    r.Fio(Rede.Pino(ramo, "out"), Rede.Pino(nLo2, "in"));
                    int ouS = r.Ou(2);
                    r.Fio(Rede.Pino(nLo, "out"), Rede.Pino(ouS, "in1"));
                    r.Fio(Rede.Pino(nLo2, "out"), Rede.Pino(ouS, "in2"));
                    r.Fio(Rede.Pino(ouS, "out"), Rede.Pino(sLig, "in"));
                }
                else
                {
                    r.Fio(Rede.Pino(nLo, "out"), Rede.Pino(sLig, "in"));
                }

                int rLig = r.Bobina("RCoil", b + cmdLig);
                int pLo = r.Contato(b + lo1, false);
                r.Fio(Rede.Pino(cAut, "out"), Rede.Pino(pLo, "in"));

                // bypass: LO2 (aberto) em serie com LO1
                int ramoRst = pLo;
                if (tipo.Bypass)
                {
                    int pLo2 = r.Contato(b + "CMD_LO2", false);
                    r.Fio(Rede.Pino(pLo, "out"), Rede.Pino(pLo2, "in"));
                    ramoRst = pLo2;
                }

                int pInt = r.Contato(b + "CMD_INT", false);
                r.Fio(Rede.Pino(cAut, "out"), Rede.Pino(pInt, "in"));
                int ouR = r.Ou(2);
                r.Fio(Rede.Pino(pInt, "out"), Rede.Pino(ouR, "in1"));
                r.Fio(Rede.Pino(ramoRst, "out"), Rede.Pino(ouR, "in2"));
                r.Fio(Rede.Pino(ouR, "out"), Rede.Pino(rLig, "in"));
            }

            // --- Modulacao por PID: saida do PID (%) -> velocidade -----------------------
            if (TemPidMod(p))
                LogicaPidVelocidade(r, tipo, p, b);

            // --- Intermitente: BL_INTERMITENTE ------------------------------------------
            if (temItm)
                LogicaIntermitente(r, tipo, p, b, bi, existe);

            // --- Valvula analogica: posicao (MOVE, BL_ANALOGICA, SUB) -------------------
            if (tipo.Analogica)
                LogicaPosicao(r, tipo, p, b, existe);

            // --- Parte de maquina: intertravamento vem do bloco da maquina; sem visualizacao propria ---
            if (Maquinas.EhParte(p))
            {
                LogicaParteMaquina(r, sg, p, b);
            }
            else
            {
            // --- Visualizacao na IHM: (REM/LOC OU AUT) -> SI<n>.VIS.OFF --------------
            string visOff = sg.NomeDb + "." + p.Eq + ".SI" + IndiceSi(sg, p) + ".VIS.OFF";
            int vAut = r.Contato(b + "CMD_AUT", false);
            r.ParaTrilho(vAut, "in");
            int vBob = r.Bobina("Coil", visOff);

            if (_remLoc != null)
            {
                int vRem = r.Contato(_remLoc, false);
                r.ParaTrilho(vRem, "in");
                int vOu = r.Ou(2);
                r.Fio(Rede.Pino(vRem, "out"), Rede.Pino(vOu, "in1"));
                r.Fio(Rede.Pino(vAut, "out"), Rede.Pino(vOu, "in2"));
                r.Fio(Rede.Pino(vOu, "out"), Rede.Pino(vBob, "in"));
            }
            else
            {
                r.Fio(Rede.Pino(vAut, "out"), Rede.Pino(vBob, "in"));
            }
            }

            // --- Severidade --------------------------------------------------------
            int sv = r.Chamada(FbReal(FbSeveridade), p.InstanciaSev, PinosSeveridade);
            r.ParaTrilho(sv, "en");
            string sv1 = FonteSv1(r, p);   // protecao do equipamento e defeitos externos: severidade 1
            if (sv1 != null) r.Entrada(sv, "MOT_LEI_SV1", sv1);
            r.Entrada(sv, "MOT_LEI_SV2", Rede.Ident(r.Acesso("SYM|" + b + "LEI_DEF")));
            r.Entrada(sv, "MOT_LEI_SV4", Rede.Ident(r.Acesso("SYM|" + b + lo1)));
            r.Saida(sv, "MOT_LEI_SVD", Rede.Ident(r.Acesso("SYM|" + b + saidaSev)));
            r.FecharPinos(sv, PinosSeveridade);

            // bypass: severidade do motor 2 (mesmo LEI_DEF, com LO2 e SV2)
            if (tipo.Bypass)
            {
                int sv2 = r.Chamada(FbReal(FbSeveridade), p.InstanciaSev2, PinosSeveridade);
                r.ParaTrilho(sv2, "en");
                string sv1b = FonteSv1(r, p);
                if (sv1b != null) r.Entrada(sv2, "MOT_LEI_SV1", sv1b);
                r.Entrada(sv2, "MOT_LEI_SV2", Rede.Ident(r.Acesso("SYM|" + b + "LEI_DEF")));
                r.Entrada(sv2, "MOT_LEI_SV4", Rede.Ident(r.Acesso("SYM|" + b + "CMD_LO2")));
                r.Saida(sv2, "MOT_LEI_SVD", Rede.Ident(r.Acesso("SYM|" + b + "LEI_SV2")));
                r.FecharPinos(sv2, PinosSeveridade);
            }

            return Embrulhar(r, p.Descricao);
        }

        /// <summary>Rede (CompileUnit) com as partes, os fios e o titulo.</summary>
        private string Embrulhar(Rede r, string titulo)
        {
            titulo = Textos.Padrao(titulo);
            var sb = new StringBuilder();
            sb.AppendLine("<SW.Blocks.CompileUnit ID=\"" + NovoId() + "\" CompositionName=\"CompileUnits\">");
            sb.AppendLine("<AttributeList>");
            sb.AppendLine("<NetworkSource><FlgNet xmlns=\"" + NsFlg + "\">");
            sb.Append(r.XmlPartes());
            sb.AppendLine("<Wires>");
            sb.Append(r.XmlFios());
            sb.AppendLine("</Wires>");
            sb.AppendLine("</FlgNet></NetworkSource>");
            sb.AppendLine("<ProgrammingLanguage>LAD</ProgrammingLanguage>");
            sb.AppendLine("</AttributeList>");
            sb.AppendLine("<ObjectList>");
            sb.Append(TextoMulti("Comment", ""));
            sb.Append(TextoMulti("Title", titulo));
            sb.AppendLine("</ObjectList>");
            sb.AppendLine("</SW.Blocks.CompileUnit>");
            return sb.ToString();
        }

        // ===================================================================
        //  Sensores (como no SENSORES do projeto)
        // ===================================================================
        private static string PrefixoSensor(SubsistemaGerado sg, Partida s)
        {
            return sg.NomeDb + "." + s.Eq + "." + s.Fn + ".";
        }

        private string XmlRedeSensor(SubsistemaGerado sg, Partida p, TipoPartida tipo, Func<string, bool> existe)
        {
            string b = sg.NomeDb + "." + p.Eq + "." + p.Fn + ".";
            var r = new Rede();
            Func<string, string> campo = delegate (string n) { return Rede.Ident(r.Acesso("SYM|" + b + n)); };

            if (tipo.SenVazao) return XmlRedeVazao(sg, p, tipo, existe);
            if (tipo.SenRaspador) return XmlRedeRaspador(sg, p, tipo, existe);

            if (!tipo.SenAna)
            {
                // nivel digital: um lado (alto ou baixo); com par do mesmo tanque, os dois lados
                Partida par;
                _pares.TryGetValue(p, out par);
                Partida baixo = tipo.SenAlto ? par : p;
                Partida alto = tipo.SenAlto ? p : par;

                int call = r.Chamada(FbReal(tipo.Fb), InstanciaSensor(p), tipo.Pinos);
                r.ParaTrilho(call, "en");

                // libera operacao: do nivel baixo (ou do unico sensor)
                Partida dono = baixo ?? alto;
                r.Entrada(call, "SEN_CMD_MAN", Rede.Ident(r.Acesso("SYM|" + PrefixoSensor(sg, dono) + "CMD_LOP")));

                if (baixo != null)
                {
                    string bb = PrefixoSensor(sg, baixo);
                    if (existe(baixo.TagSensor)) r.Entrada(call, "SEN_SET_BAI", Rede.Ident(r.Acesso("SYM|" + baixo.TagSensor)));
                    r.Entrada(call, "SET_TPO_BAI", !Maquinas.EhPreparador(baixo)
                        ? Rede.Ident(r.Acesso("SYM|" + bb + "SET_TPB"))
                        : Rede.Ident(r.Acesso("LIT|Int|0")));   // maquina (preparador): sem tempo
                    r.Entrada(call, "CMD_SKL_BAI", Rede.Ident(r.Acesso("SYM|" + bb + "CMD_SKB")));
                    r.Saida(call, "SEN_LEI_BAI", Rede.Ident(r.Acesso("SYM|" + bb + "LEI_BAI")));
                    r.Saida(call, "SEN_LEI_TPB", Rede.Ident(r.Acesso("SYM|" + bb + "LEI_TPB")));
                    r.Saida(call, "SEN_LEI_STB", Rede.Ident(r.Acesso("SYM|" + bb + "LEI_STB")));
                }
                if (alto != null)
                {
                    string ba = PrefixoSensor(sg, alto);
                    if (existe(alto.TagSensor)) r.Entrada(call, "SEN_SET_ALT", Rede.Ident(r.Acesso("SYM|" + alto.TagSensor)));
                    r.Entrada(call, "SET_TPO_ALT", !Maquinas.EhPreparador(alto)
                        ? Rede.Ident(r.Acesso("SYM|" + ba + "SET_TPA"))
                        : Rede.Ident(r.Acesso("LIT|Int|0")));
                    r.Entrada(call, "CMD_SKL_ALT", Rede.Ident(r.Acesso("SYM|" + ba + "CMD_SKA")));
                    r.Saida(call, "SEN_LEI_ALT", Rede.Ident(r.Acesso("SYM|" + ba + "LEI_ALT")));
                    r.Saida(call, "SEN_LEI_TPA", Rede.Ident(r.Acesso("SYM|" + ba + "LEI_TPA")));
                    r.Saida(call, "SEN_LEI_STA", Rede.Ident(r.Acesso("SYM|" + ba + "LEI_STA")));
                }
                r.FecharPinos(call, tipo.Pinos);

                string titulo = p.Descricao;
                if (baixo != null && alto != null && !string.IsNullOrWhiteSpace(alto.Descricao)
                    && alto.Descricao != baixo.Descricao)
                    titulo = (baixo.Descricao ?? "") + " / " + alto.Descricao;
                return Embrulhar(r, titulo);
            }

            // --- BL_ANALOGICA: escala da entrada analogica -------------------------------
            // (o acesso so e criado se for ligado: o TIA recusa tag declarada e nao usada na rede)
            string entrada = existe(p.TagSensor) ? Rede.Ident(r.Acesso("SYM|" + p.TagSensor)) : null;
            int ana = r.Chamada(FbReal(FbAnalogica), p.InstanciaAna, PinosAnalogica);
            r.ParaTrilho(ana, "en");
            r.Entrada(ana, "IN MIN", campo("SET_MIN"));
            r.Entrada(ana, "IN MAX", campo("SET_MAX"));
            if (entrada != null) r.Entrada(ana, "IN ENTRADA", entrada);
            r.Entrada(ana, "OUT MIN", campo("SET_OMI"));
            r.Entrada(ana, "OUT MAX", campo("SET_UNM"));
            // nivel: vai para a variavel Temp (o valor pode ser forcado); analitico: direto no LEI_NIV
            r.Saida(ana, "OUT VALOR", tipo.SenAnalitico
                ? campo("LEI_NIV")
                : Rede.Ident(r.Acesso("LOC|" + p.TempLeiAux)));
            r.Saida(ana, "OUT_POR", campo("LEI_POR"));
            r.Saida(ana, "OUT_ANA", campo("LEI_ANA"));
            r.FecharPinos(ana, PinosAnalogica);

            // --- BL_SENSOR_NIVEL_ANALOGICA, ligado no ENO do BL_ANALOGICA ------------------
            int sen = r.Chamada(FbReal(tipo.Fb), p.Instancia, tipo.Pinos);
            r.Fio(Rede.Pino(ana, "eno"), Rede.Pino(sen, "en"));
            r.Entrada(sen, "SEN_LEI_ENT", campo("LEI_NIV"));
            r.Entrada(sen, "SEN_SET_SBA", campo("SET_SBA"));
            r.Entrada(sen, "SEN_SET_RBA", campo("SET_RBA"));
            r.Entrada(sen, "SEN_SET_SAL", campo("SET_SAL"));
            r.Entrada(sen, "SEN_SET_RAL", campo("SET_RAL"));
            r.Entrada(sen, "SEN_CMD_MAN", campo("CMD_LOP"));
            r.Saida(sen, "SEN_LEI_BAI", campo("LEI_BAI"));
            r.Saida(sen, "SEN_LEI_ALT", campo("LEI_ALT"));
            if (!tipo.SenAnalitico)
            {
                r.Saida(sen, "SEN_LEI_STB", campo("LEI_STB"));
                r.Saida(sen, "SEN_LEI_STA", campo("LEI_STA"));
            }
            r.FecharPinos(sen, tipo.Pinos);

            if (!tipo.SenAnalitico)
            {
                // forcar valor: NAO CMD_VLF -> Temp para LEI_NIV;  CMD_VLF -> SET_VLF para LEI_NIV
                int cNao = r.Contato(b + "CMD_VLF", true);
                r.ParaTrilho(cNao, "in");
                r.Mover(Rede.Pino(cNao, "out"), Rede.Ident(r.Acesso("LOC|" + p.TempLeiAux)), campo("LEI_NIV"));

                int cSim = r.Contato(b + "CMD_VLF", false);
                r.ParaTrilho(cSim, "in");
                r.Mover(Rede.Pino(cSim, "out"), campo("SET_VLF"), campo("LEI_NIV"));
            }
            else
            {
                // severidade: SV3 = nivel baixo OU nivel alto; SV4 = libera operacao
                int sv = r.Chamada(FbReal(FbSeveridade), p.InstanciaSev, PinosSeveridade);
                r.ParaTrilho(sv, "en");
                int cBai = r.Contato(b + "LEI_BAI", false);
                r.ParaTrilho(cBai, "in");
                int cAlt = r.Contato(b + "LEI_ALT", false);
                r.ParaTrilho(cAlt, "in");
                int ou = r.Ou(2);
                r.Fio(Rede.Pino(cBai, "out"), Rede.Pino(ou, "in1"));
                r.Fio(Rede.Pino(cAlt, "out"), Rede.Pino(ou, "in2"));
                r.Entrada(sv, "MOT_LEI_SV3", Rede.Pino(ou, "out"));
                r.Entrada(sv, "MOT_LEI_SV4", campo("CMD_LOP"));
                r.Saida(sv, "MOT_LEI_SVD", campo("LEI_SVD"));
                r.FecharPinos(sv, PinosSeveridade);
            }

            return Embrulhar(r, p.Descricao);
        }

        /// <summary>
        /// Medidor de vazao (como no SENSORES do projeto):
        ///  BL_ANALOGICA: AI -> LEI_VAZ (instantanea), LEI_POR, LEI_ANA
        ///  BL_SENSOR_MEDIDOR_VAZAO: alarmes de vazao baixa/alta e totalizador pelo pulso (DI)
        ///  [opcional] modulacao de quimicos: 2o BL_ANALOGICA (LEI_VAZ em % de SET_VZQ -> LEI_AUX) e
        ///     (NAO CMD_LOP E LEI_AUX &gt;= 100) OU CMD_LOP -> MOVE 100     -> LEI_VQP
        ///      NAO CMD_LOP E LEI_AUX &lt; 100              -> MOVE LEI_AUX -> LEI_VQP
        ///  BL_SEVERIDADE: SV3 = LEI_BAI OU LEI_ALT; SV4 = CMD_LOP -> LEI_SVD
        /// </summary>
        private string XmlRedeVazao(SubsistemaGerado sg, Partida p, TipoPartida tipo, Func<string, bool> existe)
        {
            string b = sg.NomeDb + "." + p.Eq + "." + p.Fn + ".";
            var r = new Rede();
            Func<string, string> campo = delegate (string n) { return Rede.Ident(r.Acesso("SYM|" + b + n)); };
            Func<string, string> real = delegate (string v) { return Rede.Ident(r.Acesso("LIT|Real|" + v)); };

            // --- BL_ANALOGICA: vazao instantanea ------------------------------------------
            // (o acesso so e criado se for ligado: o TIA recusa tag declarada e nao usada na rede)
            string entrada = existe(p.TagSensor) ? Rede.Ident(r.Acesso("SYM|" + p.TagSensor)) : null;
            int ana = r.Chamada(FbReal(FbAnalogica), p.InstanciaAna, PinosAnalogica);
            r.ParaTrilho(ana, "en");
            r.Entrada(ana, "IN MIN", campo("SET_MIN"));
            r.Entrada(ana, "IN MAX", campo("SET_MAX"));
            if (entrada != null) r.Entrada(ana, "IN ENTRADA", entrada);
            r.Entrada(ana, "OUT MIN", campo("SET_OMI"));
            r.Entrada(ana, "OUT MAX", campo("SET_UNM"));
            r.Saida(ana, "OUT VALOR", campo("LEI_VAZ"));
            r.Saida(ana, "OUT_POR", campo("LEI_POR"));
            r.Saida(ana, "OUT_ANA", campo("LEI_ANA"));
            r.FecharPinos(ana, PinosAnalogica);

            // --- BL_SENSOR_MEDIDOR_VAZAO: alarmes e totalizador ------------------------
            // medidor de uma maquina (preparador): so roda em automatico, totaliza por tempo com a agua aberta
            Partida sisMaq = Maquinas.EhPreparador(p) ? Maquinas.Parte(_todas, p.Maquina, Maquinas.PapelSis) : null;
            int med = r.Chamada(FbReal(tipo.Fb), p.Instancia, tipo.Pinos);
            if (sisMaq != null)
            {
                int cAut = r.Contato(SimboloParte(sisMaq, "CMD_SEL"), false);
                r.ParaTrilho(cAut, "in");
                r.Fio(Rede.Pino(cAut, "out"), Rede.Pino(med, "en"));
            }
            else r.ParaTrilho(med, "en");
            r.Entrada(med, "SEN_LEI_VAZ", campo("LEI_VAZ"));
            r.Entrada(med, "SEN_SET_SBA", campo("SET_SBA"));
            r.Entrada(med, "SEN_SET_RBA", campo("SET_RBA"));
            r.Entrada(med, "SEN_SET_SAL", campo("SET_SAL"));
            r.Entrada(med, "SEN_SET_RAL", campo("SET_RAL"));
            r.Entrada(med, "SEN_CMD_MAN", campo("CMD_LOP"));
            if (sisMaq != null)
            {
                // pulso = agua habilitada E clock de 1 Hz; fator = vazao instantanea; fim da batelada zera
                int cAgua = r.Contato(SimboloParte(sisMaq, "LEI_EAG"), false);
                r.ParaTrilho(cAgua, "in");
                string pulsoMaq = Rede.Pino(cAgua, "out");
                if (existe(Cfg.TagClock1Hz))
                {
                    int cClk = r.Contato(Cfg.TagClock1Hz.Trim(), false);
                    r.Fio(pulsoMaq, Rede.Pino(cClk, "in"));
                    pulsoMaq = Rede.Pino(cClk, "out");
                }
                r.Entrada(med, "SEN_LEI_PUL", pulsoMaq);
                r.Entrada(med, "SEN_SET_TOT", campo("LEI_VAZ"));
                r.Entrada(med, "SEN_SET_CTO", Rede.Ident(r.Acesso("LIT|DInt|0")));
                r.Entrada(med, "SEN_CMD_CTO", Rede.Ident(r.Acesso("LIT|Bool|false")));
                r.Entrada(med, "SEN_CMD_RST", Rede.Ident(r.Acesso("SYM|" + SimboloParte(sisMaq, "LEI_FIM"))));
            }
            else
            {
                string pulso = existe(p.TagPulso) ? Rede.Ident(r.Acesso("SYM|" + p.TagPulso)) : null;
                if (pulso != null) r.Entrada(med, "SEN_LEI_PUL", pulso);
                r.Entrada(med, "SEN_SET_TOT", campo("SET_TOT"));
                r.Entrada(med, "SEN_SET_CTO", campo("SET_CTO"));
                r.Entrada(med, "SEN_CMD_CTO", campo("CMD_CTO"));
            }
            r.Saida(med, "SEN_LEI_BAI", campo("LEI_BAI"));
            r.Saida(med, "SEN_LEI_ALT", campo("LEI_ALT"));
            r.Saida(med, "SEN_LEI_TOT", campo("LEI_TOT"));
            r.FecharPinos(med, tipo.Pinos);   // SEN_CMD_RST fica aberto, como no projeto (fora das maquinas)

            // --- opcional: modulacao de quimicos ---------------------------------------
            if (tipo.SenVazaoMod)
            {
                int mod = r.Chamada(FbReal(FbAnalogica), p.InstanciaMod, PinosAnalogica);
                r.ParaTrilho(mod, "en");
                r.Entrada(mod, "IN MIN", real("0.0"));
                r.Entrada(mod, "IN MAX", campo("SET_VZQ"));
                r.Entrada(mod, "IN ENTRADA", campo("LEI_VAZ"));
                r.Entrada(mod, "OUT MIN", real("0.0"));
                r.Entrada(mod, "OUT MAX", real("100.0"));
                r.Saida(mod, "OUT_POR", campo("LEI_AUX"));
                r.FecharPinos(mod, PinosAnalogica);

                // (NAO CMD_LOP E LEI_AUX >= 100) OU CMD_LOP -> MOVE 100 -> LEI_VQP
                int cNao = r.Contato(b + "CMD_LOP", true);
                r.ParaTrilho(cNao, "in");
                int ge = r.Comparar("Ge", "Int", Rede.Pino(cNao, "out"), campo("LEI_AUX"),
                    Rede.Ident(r.Acesso("LIT|Int|100")));
                int cSim = r.Contato(b + "CMD_LOP", false);
                r.ParaTrilho(cSim, "in");
                int ou100 = r.Ou(2);
                r.Fio(Rede.Pino(ge, "out"), Rede.Pino(ou100, "in1"));
                r.Fio(Rede.Pino(cSim, "out"), Rede.Pino(ou100, "in2"));
                r.Mover(Rede.Pino(ou100, "out"), Rede.Ident(r.Acesso("LIT|Int|100")), campo("LEI_VQP"));

                // NAO CMD_LOP E LEI_AUX < 100 -> MOVE LEI_AUX -> LEI_VQP
                int cNao2 = r.Contato(b + "CMD_LOP", true);
                r.ParaTrilho(cNao2, "in");
                int lt = r.Comparar("Lt", "Int", Rede.Pino(cNao2, "out"), campo("LEI_AUX"),
                    Rede.Ident(r.Acesso("LIT|Int|100")));
                r.Mover(Rede.Pino(lt, "out"), campo("LEI_AUX"), campo("LEI_VQP"));
            }

            // --- severidade: SV3 = vazao baixa OU vazao alta; SV4 = libera operacao --------
            int sv = r.Chamada(FbReal(FbSeveridade), p.InstanciaSev, PinosSeveridade);
            r.ParaTrilho(sv, "en");
            int cBai = r.Contato(b + "LEI_BAI", false);
            r.ParaTrilho(cBai, "in");
            int cAlt = r.Contato(b + "LEI_ALT", false);
            r.ParaTrilho(cAlt, "in");
            int ou = r.Ou(2);
            r.Fio(Rede.Pino(cBai, "out"), Rede.Pino(ou, "in1"));
            r.Fio(Rede.Pino(cAlt, "out"), Rede.Pino(ou, "in2"));
            r.Entrada(sv, "MOT_LEI_SV3", Rede.Pino(ou, "out"));
            r.Entrada(sv, "MOT_LEI_SV4", campo("CMD_LOP"));
            r.Saida(sv, "MOT_LEI_SVD", campo("LEI_SVD"));
            r.FecharPinos(sv, PinosSeveridade);

            return Embrulhar(r, p.Descricao);
        }

        /// <summary>
        /// Pas do raspador do flotador (como no SENSORES do projeto):
        ///  BL_SENSOR_RASPADOR: sensores direito/esquerdo (DI), libera operacao (CMD_LOP), reset geral,
        ///  confirmacao da partida do raspador (DI), tempos SET_TPD/SET_TPE;
        ///  leituras LEI_TPD, LEI_TPE, LEI_DED, LEI_DEE no DB;
        ///  LEI_DED -> bobina SUB_EQ_FN_LEI_DED   e   LEI_DEE -> bobina SUB_EQ_FN_LEI_DEE (memorias globais)
        /// </summary>
        private string XmlRedeRaspador(SubsistemaGerado sg, Partida p, TipoPartida tipo, Func<string, bool> existe)
        {
            string b = sg.NomeDb + "." + p.Eq + "." + p.Fn + ".";
            var r = new Rede();
            Func<string, string> campo = delegate (string n) { return Rede.Ident(r.Acesso("SYM|" + b + n)); };
            Func<string, string> tag = delegate (string n) { return Rede.Ident(r.Acesso("SYM|" + n.Trim())); };

            int call = r.Chamada(FbReal(tipo.Fb), p.Instancia, tipo.Pinos);
            r.ParaTrilho(call, "en");
            if (existe(p.TagSensor)) r.Entrada(call, "SEN_LEI_DIR", tag(p.TagSensor));
            if (existe(p.TagSensor2)) r.Entrada(call, "SEN_LEI_LES", tag(p.TagSensor2));
            r.Entrada(call, "SIS_CMD_MAN", campo("CMD_LOP"));
            r.Entrada(call, "SIS_CMD_RST", existe(Cfg.TagReset)
                ? tag(Cfg.TagReset)
                : Rede.Ident(r.Acesso("LIT|Bool|false")));
            if (existe(p.TagConf)) r.Entrada(call, "MOT_LEI_CNF", tag(p.TagConf));
            r.Entrada(call, "SEN_SET_TPD", campo("SET_TPD"));
            r.Entrada(call, "SEN_SET_TPE", campo("SET_TPE"));
            r.Saida(call, "LDI_LEI_TPO", campo("LEI_TPD"));
            r.Saida(call, "LES_LEI_TPO", campo("LEI_TPE"));
            r.Saida(call, "LDI_LEI_DEF", campo("LEI_DED"));
            r.Saida(call, "LES_LEI_DEF", campo("LEI_DEE"));
            r.FecharPinos(call, tipo.Pinos);

            // memorias globais de defeito, uma por lado
            int cDir = r.Contato(b + "LEI_DED", false);
            r.ParaTrilho(cDir, "in");
            int bDir = r.Bobina("Coil", p.TagDefDireito);
            r.Fio(Rede.Pino(cDir, "out"), Rede.Pino(bDir, "in"));

            int cEsq = r.Contato(b + "LEI_DEE", false);
            r.ParaTrilho(cEsq, "in");
            int bEsq = r.Bobina("Coil", p.TagDefEsquerdo);
            r.Fio(Rede.Pino(cEsq, "out"), Rede.Pino(bEsq, "in"));

            return Embrulhar(r, p.Descricao);
        }

        // ===================================================================
        //  Ligacao dos pinos do FB
        // ===================================================================

        /// <summary>BL_PARTIDA_HIBRIDA: direta, inversor/soft por terminais, diafragma (com ou sem bypass).</summary>
        private static void LigarPinosTerminais(
            Rede r, int call, TipoPartida tipo, Partida p, string b, Func<string, bool> existe)
        {
            // --- Pinos de comando / configuracao --------------------------------
            r.Entrada(call, "MOT_CMD_LIG", Rede.Ident(r.Acesso("SYM|" + b + "CMD_LIG")));
            r.Entrada(call, "MOT_CMD_RH1", Rede.Ident(r.Acesso("SYM|" + b + "CMD_RH1")));
            r.Entrada(call, "MOT_SET_TPD", Rede.Ident(r.Acesso("LIT|Int|8")));

            // --- Confirmacao e defeito, conforme o tipo ----------------------------
            if (tipo.Bypass)
            {
                // dois contatores (titular / reserva), sem entrada de confirmacao:
                //   CN1 = LEI_HAB E NAO CMD_LO1     CN2 = LEI_HAB E NAO CMD_LO2
                LigarCnBypass(r, call, "MOT_LEI_CN1", b, "CMD_LO1");
                LigarCnBypass(r, call, "MOT_LEI_CN2", b, "CMD_LO2");
                r.Entrada(call, "MOT_CMD_SEL", Rede.Ident(r.Acesso("SYM|" + b + "CMD_LO1")));
                r.Entrada(call, "MOT_CMD_RH2", Rede.Ident(r.Acesso("SYM|" + b + "CMD_RH2")));
            }
            else if (tipo.Drive)
            {
                // inversor / soft: nao ha retorno de contator, CN1 recebe a propria habilitacao
                r.Entrada(call, "MOT_LEI_CN1", Rede.Ident(r.Acesso("SYM|" + b + "LEI_HAB")));
            }
            else
            {
                // direta / diafragma: confirmacao digital do contator
                r.Entrada(call, "MOT_LEI_CN1", existe(p.TagConf)
                    ? Rede.Ident(r.Acesso("SYM|" + p.TagConf))
                    : Rede.Ident(r.Acesso("LIT|Bool|false")));
            }

            // defeito do drive (inversor / soft): contato NEGADO em MOT_LEI_TRP
            if (tipo.Drive && existe(p.TagDefDrive))
            {
                int cDrv = r.Contato(p.TagDefDrive, true);
                r.ParaTrilho(cDrv, "in");
                r.Entrada(call, "MOT_LEI_TRP", Rede.Pino(cDrv, "out"));
            }

            // velocidade (inversor / diafragma); modulada por PID: a memoria SET_AUX
            if (tipo.Velocidade)
                r.Entrada(call, "MOT_SET_VEL", Rede.Ident(r.Acesso("SYM|"
                    + (TemPidMod(p) ? p.TagSetVelAux : b + "SET_VEL"))));

            // --- Leituras para o DB ----------------------------------------------
            r.Saida(call, "MOT_LEI_HAB", Rede.Ident(r.Acesso("SYM|" + b + "LEI_HAB")));
            r.Saida(call, "MOT_LEI_DEF", Rede.Ident(r.Acesso("SYM|" + b + "LEI_DEF")));
            r.Saida(call, "MOT_LEI_ST1", Rede.Ident(r.Acesso("SYM|" + b + "LEI_ST1")));
            r.Saida(call, "MOT_LEI_HO1", Rede.Ident(r.Acesso("SYM|" + b + "LEI_HO1")));
            if (tipo.Velocidade && existe(p.TagRef))
                r.Saida(call, "MOT_LEI_VEL", Rede.Ident(r.Acesso("SYM|" + p.TagRef)));

            if (tipo.Bypass)
            {
                r.Saida(call, "MOT_LEI_ST2", Rede.Ident(r.Acesso("SYM|" + b + "LEI_ST2")));
                r.Saida(call, "MOT_LEI_HO2", Rede.Ident(r.Acesso("SYM|" + b + "LEI_HO2")));

                // habilita de cada contator: saidas digitais ligadas direto nos pinos
                // (a habilita do drive e a bobina LEI_HAB, como nos outros tipos)
                if (existe(p.TagHabC1))
                    r.Saida(call, "MOT_CMD_HA1", Rede.Ident(r.Acesso("SYM|" + p.TagHabC1)));
                if (existe(p.TagHabC2))
                    r.Saida(call, "MOT_CMD_HA2", Rede.Ident(r.Acesso("SYM|" + p.TagHabC2)));
            }

            LigarModulacao(r, call, p, b);
            r.FecharPinos(call, tipo.Pinos);
        }

        /// <summary>
        /// BL_INVERSOR_6 (inversor por rede). Como nas redes reais do projeto:
        ///  SET_VEL, SET_FES e as leituras (RUN, DEF, ST, VEL, COR, ALA, FAL, HO) vao no DB;
        ///  sem bypass: CN1 = LEI_RUN; com bypass: CN1 = NAO CMD_LO1 e CN2 = NAO CMD_LO2 (sem LEI_RUN).
        ///  INV_SET_END (identificador de hardware do drive) fica aberto, para preencher no TIA.
        /// </summary>
        private static void LigarPinosRede(
            Rede r, int call, TipoPartida tipo, Partida p, string b, Func<string, bool> existe)
        {
            Func<string, string> campo = delegate (string nome)
            {
                return Rede.Ident(r.Acesso("SYM|" + b + nome));
            };

            // --- comandos / ajustes --------------------------------------------------
            r.Entrada(call, "SIS_CMD_CLP", Rede.Ident(r.Acesso("LIT|Bool|false")));
            r.Entrada(call, "MOT_CMD_LIG", campo("CMD_LIG"));
            r.Entrada(call, "MOT_CMD_RH1", campo("CMD_RH1"));
            r.Entrada(call, "MOT_SET_VEL", TemPidMod(p)
                ? Rede.Ident(r.Acesso("SYM|" + p.TagSetVelAux))
                : campo("SET_VEL"));
            r.Entrada(call, "MOT_SET_VMI", Rede.Ident(r.Acesso("LIT|Real|0.0")));
            r.Entrada(call, "MOT_SET_FES", campo("SET_FES"));

            // --- confirmacao ------------------------------------------------------------
            if (tipo.Bypass)
            {
                LigarContatoNegado(r, call, "MOT_LEI_CN1", b + "CMD_LO1");
                LigarContatoNegado(r, call, "MOT_LEI_CN2", b + "CMD_LO2");
                r.Entrada(call, "MOT_CMD_SEL", campo("CMD_LO1"));
                r.Entrada(call, "MOT_CMD_RH2", campo("CMD_RH2"));
            }
            else
            {
                r.Entrada(call, "MOT_LEI_CN1", campo("LEI_RUN"));
            }

            // --- leituras para o DB -----------------------------------------------------
            r.Saida(call, "MOT_LEI_RUN", campo("LEI_RUN"));
            r.Saida(call, "MOT_LEI_DEF", campo("LEI_DEF"));
            r.Saida(call, "MOT_LEI_ST1", campo("LEI_ST1"));
            r.Saida(call, "MOT_LEI_VEL", campo("LEI_VEL"));
            r.Saida(call, "MOT_LEI_COR", campo("LEI_COR"));
            r.Saida(call, "MOT_LEI_ALA", campo("LEI_ALA"));
            r.Saida(call, "MOT_LEI_FAL", campo("LEI_FAL"));
            r.Saida(call, "MOT_LEI_HO1", campo("LEI_HO1"));

            if (tipo.Bypass)
            {
                r.Saida(call, "MOT_LEI_ST2", campo("LEI_ST2"));
                r.Saida(call, "MOT_LEI_HO2", campo("LEI_HO2"));

                // habilita de cada contator: saidas digitais direto nos pinos
                if (existe(p.TagHabC1))
                    r.Saida(call, "MOT_LEI_HA1", Rede.Ident(r.Acesso("SYM|" + p.TagHabC1)));
                if (existe(p.TagHabC2))
                    r.Saida(call, "MOT_LEI_HA2", Rede.Ident(r.Acesso("SYM|" + p.TagHabC2)));
            }

            LigarModulacao(r, call, p, b);
            r.FecharPinos(call, tipo.Pinos);   // INV_SET_END e os demais nao usados ficam abertos
        }

        /// <summary>
        /// BL_VALVULA_5 (valvula digital):
        ///  CMD_ABR e CMD_MAN (= CMD_LOP) do DB; CLP fixo em FALSE; TPO_FAL = SET_TPO;
        ///  2 DI: CNF_ABR / CNF_FEC = entradas declaradas;
        ///  0 DI: CNF_ABR = LEI_ABR e CNF_FEC = LEI_FEC (do DB);
        ///  saidas HAB_ABR, HAB_FEC, LEI_DEF e LEI_STS no DB.
        /// </summary>
        private static void LigarPinosValvula(
            Rede r, int call, TipoPartida tipo, Partida p, string b, Func<string, bool> existe)
        {
            Func<string, string> campo = delegate (string nome)
            {
                return Rede.Ident(r.Acesso("SYM|" + b + nome));
            };

            r.Entrada(call, "SIS_CMD_CLP", Rede.Ident(r.Acesso("LIT|Bool|false")));
            r.Entrada(call, "VAL_CMD_ABR", campo("CMD_ABR"));
            r.Entrada(call, "VAL_CMD_MAN", campo("CMD_LOP"));

            if (tipo.Di2)
            {
                r.Entrada(call, "VAL_CNF_ABR", existe(p.TagConf)
                    ? Rede.Ident(r.Acesso("SYM|" + p.TagConf))
                    : Rede.Ident(r.Acesso("LIT|Bool|false")));
                r.Entrada(call, "VAL_CNF_FEC", existe(p.TagConfFec)
                    ? Rede.Ident(r.Acesso("SYM|" + p.TagConfFec))
                    : Rede.Ident(r.Acesso("LIT|Bool|false")));
            }
            else if (tipo.Ai)
            {
                // aberta: LEI_ABR E (-5 <= SET_POS - LEI_POS <= 5); fechada: (-5 <= LEI_POS <= 5)
                int cAbr = r.Contato(b + "LEI_ABR", false);
                r.ParaTrilho(cAbr, "in");
                int fAbr = r.Faixa(Rede.Pino(cAbr, "out"), Rede.Ident(r.Acesso("SYM|" + p.TagLeiPosAux)), -5, 5);
                r.Entrada(call, "VAL_CNF_ABR", Rede.Pino(fAbr, "out"));

                int fFec = r.Faixa(null, campo("LEI_POS"), -5, 5);
                r.Entrada(call, "VAL_CNF_FEC", Rede.Pino(fFec, "out"));
            }
            else
            {
                r.Entrada(call, "VAL_CNF_ABR", campo("LEI_ABR"));
                r.Entrada(call, "VAL_CNF_FEC", campo("LEI_FEC"));
            }
            r.Entrada(call, "VAL_TPO_FAL", campo("SET_TPO"));

            r.Saida(call, "VAL_HAB_ABR", campo("LEI_ABR"));
            r.Saida(call, "VAL_HAB_FEC", campo("LEI_FEC"));
            r.Saida(call, "VAL_LEI_DEF", campo("LEI_DEF"));
            r.Saida(call, "VAL_LEI_STS", campo("LEI_STS"));

            r.FecharPinos(call, tipo.Pinos);
        }

        /// <summary>
        /// Valvula analogica (como nos exemplos VALV_AO e VALV_1AI_1AO):
        ///  LEI_ABR      -> MOVE SET_POS -> SET_POS_AUX
        ///  NAO LEI_ABR  -> MOVE 0       -> SET_POS_AUX
        ///  1AO:   MOVE SET_POS_AUX -> LEI_POS (sem realimentacao)
        ///  1AI:   BL_ANALOGICA AI (0..27648 -> 0..100) -> LEI_POS;  SUB SET_POS - LEI_POS -> LEI_POS_AUX
        ///  saida: BL_ANALOGICA SET_POS_AUX (0..100 -> 0..27648) -> AO
        /// </summary>
        private void LogicaPosicao(Rede r, TipoPartida tipo, Partida p, string b, Func<string, bool> existe)
        {
            Func<string, string> sym = delegate (string nome) { return Rede.Ident(r.Acesso("SYM|" + nome)); };
            Func<string, string> real = delegate (string v) { return Rede.Ident(r.Acesso("LIT|Real|" + v)); };

            int cAbr = r.Contato(b + "LEI_ABR", false);
            r.ParaTrilho(cAbr, "in");
            r.Mover(Rede.Pino(cAbr, "out"), sym(b + "SET_POS"), sym(p.TagSetPosAux));

            int cFec = r.Contato(b + "LEI_ABR", true);
            r.ParaTrilho(cFec, "in");
            r.Mover(Rede.Pino(cFec, "out"), Rede.Ident(r.Acesso("LIT|Int|0")), sym(p.TagSetPosAux));

            if (tipo.Ai)
            {
                int lei = r.Chamada(FbReal(FbAnalogica), p.InstanciaLeiPos, PinosAnalogica);
                r.ParaTrilho(lei, "en");
                r.Entrada(lei, "IN MIN", real("0.0"));
                r.Entrada(lei, "IN MAX", real("27648.0"));
                if (existe(p.TagPosAi)) r.Entrada(lei, "IN ENTRADA", sym(p.TagPosAi));
                r.Entrada(lei, "OUT MIN", real("0.0"));
                r.Entrada(lei, "OUT MAX", real("100.0"));
                r.Saida(lei, "OUT VALOR", sym(b + "LEI_POS"));
                r.FecharPinos(lei, PinosAnalogica);

                r.Subtrair(null, sym(b + "SET_POS"), sym(b + "LEI_POS"), sym(p.TagLeiPosAux));
            }
            else
            {
                r.Mover(null, sym(p.TagSetPosAux), sym(b + "LEI_POS"));
            }

            int set = r.Chamada(FbReal(FbAnalogica), p.InstanciaSetPos, PinosAnalogica);
            r.ParaTrilho(set, "en");
            r.Entrada(set, "IN MIN", real("0.0"));
            r.Entrada(set, "IN MAX", real("100.0"));
            r.Entrada(set, "IN ENTRADA", sym(p.TagSetPosAux));
            r.Entrada(set, "OUT MIN", real("0.0"));
            r.Entrada(set, "OUT MAX", real("27648.0"));
            if (existe(p.TagRef)) r.Saida(set, "OUT VALOR", sym(p.TagRef));
            r.FecharPinos(set, PinosAnalogica);
        }

        /// <summary>
        /// BL_INTERMITENTE (como no raspador do flotador):
        ///  ITM_CMD_LIG = ITM.CMD_LIG E CMD_AUT;  MOT_LEI_CNF = confirmacao do acionamento;
        ///  TPO_SET_LIG_&lt;un&gt; = ITM.SET_TPL e TPO_SET_DES_&lt;un&gt; = ITM.SET_TPD, cada um na sua unidade
        ///  (SEG, MIN ou HOR; os outros pinos de tempo recebem 0);
        ///  TPO_LEI_LIG -> ITM.LEI_TPL, TPO_LEI_DES -> ITM.LEI_TPD, MOT_CMD_HAB -> ITM.LEI_HAB.
        /// </summary>
        private void LogicaIntermitente(Rede r, TipoPartida tipo, Partida p, string b, string bi, Func<string, bool> existe)
        {
            Func<string, string> sym = delegate (string nome) { return Rede.Ident(r.Acesso("SYM|" + nome)); };

            int it = r.Chamada(FbReal(FbIntermitente), p.InstanciaItm, PinosIntermitente);
            r.ParaTrilho(it, "en");

            int cLig = r.Contato(bi + "CMD_LIG", false);
            r.ParaTrilho(cLig, "in");
            int cAut = r.Contato(b + "CMD_AUT", false);
            r.Fio(Rede.Pino(cLig, "out"), Rede.Pino(cAut, "in"));
            r.Entrada(it, "ITM_CMD_LIG", Rede.Pino(cAut, "out"));

            // confirmacao: valvula = LEI_ABR; inversor por rede = LEI_RUN; inversor/soft/bypass = LEI_HAB;
            // direta/diafragma = entrada de confirmacao (ou LEI_HAB, se nao houver)
            string cnf;
            if (tipo.Valvula) cnf = sym(b + "LEI_ABR");
            else if (tipo.Rede) cnf = sym(b + "LEI_RUN");
            else if (tipo.Drive || tipo.Bypass || !existe(p.TagConf)) cnf = sym(b + "LEI_HAB");
            else cnf = sym(p.TagConf.Trim());
            r.Entrada(it, "MOT_LEI_CNF", cnf);

            // cada tempo na sua unidade (ex.: ligado em horas e desligado em minutos)
            r.Entrada(it, "TPO_SET_LIG_" + SufixoUnidade(UnidadeLigado(p)), sym(bi + "SET_TPL"));
            r.Entrada(it, "TPO_SET_DES_" + SufixoUnidade(UnidadeDesligado(p)), sym(bi + "SET_TPD"));
            r.Saida(it, "TPO_LEI_LIG", sym(bi + "LEI_TPL"));
            r.Saida(it, "TPO_LEI_DES", sym(bi + "LEI_TPD"));
            r.Saida(it, "MOT_CMD_HAB", sym(bi + "LEI_HAB"));

            // pinos de tempo nao usados: 0 (DInt). Aberto, a logica interna do bloco nao funciona.
            foreach (string[] pino in PinosIntermitente)
            {
                if (pino[1] == "Input" && pino[0].StartsWith("TPO_SET_") && !r.Ligado(it, pino[0]))
                    r.Entrada(it, pino[0], Rede.Ident(r.Acesso("LIT|DInt|0")));
            }
            r.FecharPinos(it, PinosIntermitente);
        }

        /// <summary>
        /// Exaustor da cabine do soprador (rede propria, logo depois da do soprador), como no MBBR:
        ///  BL_PARTIDA_HIBRIDA: reset e emergencia como as partidas; MOT_CMD_LIG = soprador ligado
        ///  (LEI_RUN no inversor por rede, LEI_HAB nos demais); MOT_LEI_CN1 = confirmacao do exaustor (DI);
        ///  leituras HAB, DEF, ST1, HO1 no struct EXA;  EXA.LEI_DEF -> espelho; EXA.LEI_HAB -> habilita (DO);
        ///  BL_SEVERIDADE: SV2 = EXA.LEI_DEF, SV4 = CMD_LO1 do soprador -> EXA.LEI_SV1
        /// </summary>
        private string XmlRedeExaustor(SubsistemaGerado sg, Partida p, Func<string, bool> existe)
        {
            TipoPartida tipo = BuscarTipo(p.Tipo);
            string b = sg.NomeDb + "." + p.Eq + "." + p.Fn + ".";
            string be = sg.NomeDb + "." + p.Eq + "." + NomeExa(sg.Partidas, p) + ".";
            var r = new Rede();
            Func<string, string> sym = delegate (string nome) { return Rede.Ident(r.Acesso("SYM|" + nome)); };

            int call = r.Chamada(FbReal(FbPartida), p.InstanciaExa, PinosPartida);
            r.ParaTrilho(call, "en");
            r.Entrada(call, "SIS_CMD_RST", existe(Cfg.TagReset) ? sym(Cfg.TagReset) : Rede.Ident(r.Acesso("LIT|Bool|false")));
            if (existe(Cfg.TagEmergencia))
            {
                int ce = r.Contato(Cfg.TagEmergencia, true);
                r.ParaTrilho(ce, "in");
                r.Entrada(call, "SIS_LEI_EME", Rede.Pino(ce, "out"));
            }

            // liga junto com o soprador
            string ligado;
            if (tipo.Rede) ligado = b + "LEI_RUN";
            else if (!tipo.Drive && !tipo.Bypass && existe(p.TagConf)) ligado = p.TagConf.Trim();
            else ligado = b + "LEI_HAB";
            r.Entrada(call, "MOT_CMD_LIG", sym(ligado));

            r.Entrada(call, "MOT_LEI_CN1", existe(p.TagExaConf) ? sym(p.TagExaConf.Trim()) : Rede.Ident(r.Acesso("LIT|Bool|false")));
            r.Entrada(call, "MOT_SET_TPD", Rede.Ident(r.Acesso("LIT|Int|8")));
            r.Saida(call, "MOT_LEI_HAB", sym(be + "LEI_HAB"));
            r.Saida(call, "MOT_LEI_DEF", sym(be + "LEI_DEF"));
            r.Saida(call, "MOT_LEI_ST1", sym(be + "LEI_ST1"));
            r.Saida(call, "MOT_LEI_HO1", sym(be + "LEI_HO1"));
            r.FecharPinos(call, PinosPartida);

            // espelho do defeito do exaustor
            int cDef = r.Contato(be + "LEI_DEF", false);
            r.ParaTrilho(cDef, "in");
            int bDef = r.Bobina("Coil", TagDefExa(sg.Partidas, p));
            r.Fio(Rede.Pino(cDef, "out"), Rede.Pino(bDef, "in"));

            // habilita do exaustor
            if (existe(p.TagExaHab))
            {
                int cHab = r.Contato(be + "LEI_HAB", false);
                r.ParaTrilho(cHab, "in");
                int bHab = r.Bobina("Coil", p.TagExaHab.Trim());
                r.Fio(Rede.Pino(cHab, "out"), Rede.Pino(bHab, "in"));
            }

            // severidade do exaustor
            int sv = r.Chamada(FbReal(FbSeveridade), p.InstanciaSevExa, PinosSeveridade);
            r.ParaTrilho(sv, "en");
            r.Entrada(sv, "MOT_LEI_SV2", sym(be + "LEI_DEF"));
            r.Entrada(sv, "MOT_LEI_SV4", sym(b + "CMD_LO1"));
            r.Saida(sv, "MOT_LEI_SVD", sym(be + "LEI_SV1"));
            r.FecharPinos(sv, PinosSeveridade);

            return Embrulhar(r, ("EXAUSTOR " + (p.Descricao ?? "")).Trim());
        }

        /// <summary>
        /// Acionamento modulado por PID (como a bomba de alcalinizante e os sopradores):
        ///  CMD_AUT     -> BL_ANALOGICA: PID.LEI_VEL (0..100) -> 0..SET_VEL -> SET_AUX
        ///  NAO CMD_AUT -> MOVE SET_VEL -> SET_AUX            (SET_AUX vai no MOT_SET_VEL)
        ///  BL_PARTIDA_HIBRIDA tambem: LEI_HAB -> MOVE SET_AUX -> LEI_VEL;  NAO LEI_HAB -> MOVE 0 -> LEI_VEL
        ///  (no inversor por rede o LEI_VEL vem do proprio drive)
        /// </summary>
        private void LogicaPidVelocidade(Rede r, TipoPartida tipo, Partida p, string b)
        {
            Func<string, string> sym = delegate (string nome) { return Rede.Ident(r.Acesso("SYM|" + nome)); };
            Func<string, string> real = delegate (string v) { return Rede.Ident(r.Acesso("LIT|Real|" + v)); };
            string aux = p.TagSetVelAux;

            int cAut = r.Contato(b + "CMD_AUT", false);
            r.ParaTrilho(cAut, "in");
            int ana = r.Chamada(FbReal(FbAnalogica), p.InstanciaPidVel, PinosAnalogica);
            r.Fio(Rede.Pino(cAut, "out"), Rede.Pino(ana, "en"));
            r.Entrada(ana, "IN MIN", real("0.0"));
            r.Entrada(ana, "IN MAX", real("100.0"));
            r.Entrada(ana, "IN ENTRADA", sym(BasePidRef(p) + "LEI_VEL"));
            r.Entrada(ana, "OUT MIN", real("0.0"));
            r.Entrada(ana, "OUT MAX", sym(b + "SET_VEL"));
            r.Saida(ana, "OUT VALOR", sym(aux));
            r.FecharPinos(ana, PinosAnalogica);

            int cMan = r.Contato(b + "CMD_AUT", true);
            r.ParaTrilho(cMan, "in");
            r.Mover(Rede.Pino(cMan, "out"), sym(b + "SET_VEL"), sym(aux));

            if (!tipo.Rede)
            {
                int cHab = r.Contato(b + "LEI_HAB", false);
                r.ParaTrilho(cHab, "in");
                r.Mover(Rede.Pino(cHab, "out"), sym(aux), sym(b + "LEI_VEL"));

                int cNaoHab = r.Contato(b + "LEI_HAB", true);
                r.ParaTrilho(cNaoHab, "in");
                r.Mover(Rede.Pino(cNaoHab, "out"), Rede.Ident(r.Acesso("LIT|Int|0")), sym(b + "LEI_VEL"));
            }
        }

        /// <summary>
        /// Rede do PID (como no Cyclic interrupt do projeto):
        ///  PID_Compact: Setpoint = PID.SET_SP; Input = sensor.LEI_NIV;
        ///  ManualEnable = NAO PID.CMD_HAB OU sensor.CMD_LOP; ManualValue = 100.0; Output -> PID.LEI_VEL;
        ///  MOVEs: sensor.SET_OMI / SET_UNM -> Config.InputLowerLimit / InputUpperLimit;
        ///         PID.SET_KP / SET_KI / SET_KD -> Retain.CtrlParams.Gain / Ti / Td
        /// </summary>
        private string XmlRedePid(SubsistemaGerado sg, Partida pid)
        {
            string b = sg.NomeDb + "." + pid.Eq + "." + pid.Fn + ".";
            string s = BaseSensorPid(pid);
            string to = pid.InstanciaPid;
            var r = new Rede();
            Func<string, string> sym = delegate (string nome) { return Rede.Ident(r.Acesso("SYM|" + nome)); };

            int c = r.Pid(_versaoPid ?? VersoesPid[0], to);
            r.ParaTrilho(c, "en");
            r.Entrada(c, "Setpoint", sym(b + "SET_SP"));
            if (s != null) r.Entrada(c, "Input", sym(s + "LEI_NIV"));

            int nHab = r.Contato(b + "CMD_HAB", true);
            r.ParaTrilho(nHab, "in");
            if (s != null)
            {
                int cLop = r.Contato(s + "CMD_LOP", false);
                r.ParaTrilho(cLop, "in");
                int ou = r.Ou(2);
                r.Fio(Rede.Pino(nHab, "out"), Rede.Pino(ou, "in1"));
                r.Fio(Rede.Pino(cLop, "out"), Rede.Pino(ou, "in2"));
                r.Entrada(c, "ManualEnable", Rede.Pino(ou, "out"));
            }
            else
            {
                r.Entrada(c, "ManualEnable", Rede.Pino(nHab, "out"));
            }
            r.Entrada(c, "ManualValue", Rede.Ident(r.Acesso("LIT|Real|100.0")));
            r.Saida(c, "Output", sym(b + "LEI_VEL"));
            r.FecharPinos(c, PinosPid);

            if (s != null)
            {
                r.Mover(null, sym(s + "SET_OMI"), sym(to + ".Config.InputLowerLimit"));
                r.Mover(null, sym(s + "SET_UNM"), sym(to + ".Config.InputUpperLimit"));
            }
            r.Mover(null, sym(b + "SET_KP"), sym(to + ".Retain.CtrlParams.Gain"));
            r.Mover(null, sym(b + "SET_KI"), sym(to + ".Retain.CtrlParams.Ti"));
            r.Mover(null, sym(b + "SET_KD"), sym(to + ".Retain.CtrlParams.Td"));

            return Embrulhar(r, string.IsNullOrWhiteSpace(pid.Descricao) ? "PID " + pid.Eq : pid.Descricao);
        }

        /// <summary>FC com as redes dos PIDs (para chamar no OB de interrupcao ciclica).</summary>
        private string XmlFcPid(SubsistemaGerado sg, string nomeFc, IEnumerable<Partida> pids)
        {
            _id = 1;
            var sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            sb.AppendLine("<Document>");
            sb.AppendLine("  <Engineering version=\"V20\" />");
            sb.AppendLine("  <SW.Blocks.FC ID=\"0\">");
            sb.AppendLine("    <AttributeList>");
            sb.AppendLine("      <AutoNumber>true</AutoNumber>");
            sb.AppendLine("      <Interface><Sections xmlns=\"" + NsIf + "\">");
            sb.AppendLine("        <Section Name=\"Input\" />");
            sb.AppendLine("        <Section Name=\"Output\" />");
            sb.AppendLine("        <Section Name=\"InOut\" />");
            sb.AppendLine("        <Section Name=\"Temp\" />");
            sb.AppendLine("        <Section Name=\"Constant\" />");
            sb.AppendLine("        <Section Name=\"Return\">");
            sb.AppendLine("          <Member Name=\"Ret_Val\" Datatype=\"Void\" Accessibility=\"Public\" />");
            sb.AppendLine("        </Section>");
            sb.AppendLine("      </Sections></Interface>");
            sb.AppendLine("      <MemoryLayout>Optimized</MemoryLayout>");
            sb.AppendLine("      <Name>" + nomeFc + "</Name>");
            sb.AppendLine("      <Namespace />");
            sb.AppendLine("      <ProgrammingLanguage>LAD</ProgrammingLanguage>");
            sb.AppendLine("      <SetENOAutomatically>false</SetENOAutomatically>");
            sb.AppendLine("    </AttributeList>");
            sb.AppendLine("    <ObjectList>");
            foreach (Partida pid in pids) sb.Append(XmlRedePid(sg, pid));
            sb.AppendLine("    </ObjectList>");
            sb.AppendLine("  </SW.Blocks.FC>");
            sb.AppendLine("</Document>");
            return sb.ToString();
        }

        // ===================================================================
        //  PID_Compact: objeto tecnologico (criado pela API; nao e DB de instancia comum)
        // ===================================================================

        /// <summary>
        /// Garante o objeto tecnologico de cada PID. O que ja existe fica como esta (mantem a sintonia);
        /// o que falta e criado e configurado: General %, Input / Output, sem inverter,
        /// modo automatico depois do restart da CPU.
        /// </summary>
        private bool GarantirPids(PlcSoftware plc, IEnumerable<Partida> pids)
        {
            List<Partida> lista = pids.ToList();
            if (lista.Count == 0) return true;

            TechnologicalInstanceDBGroup grupoTo = plc.TechnologicalObjectGroup;
            if (_versaoPid == null) _versaoPid = DescobrirVersaoPid(plc, grupoTo);

            bool ok = true;
            foreach (Partida pid in lista)
            {
                string nome = pid.InstanciaPid;
                TechnologicalInstanceDB existente = AcharTo(grupoTo, nome);
                if (existente != null)
                {
                    Log("PID " + nome + ": objeto tecnologico ja existe (mantido, com a sintonia).");
                    // acao inversa marcada: aplica tambem no objeto que ja existe (desmarcar nao mexe no que esta no TIA)
                    if (pid.PidInverso)
                    {
                        if (DefinirParametro(existente, new[] { "Config.InvertControl", "_Config.InvertControl" }, true))
                            Log("PID " + nome + ": acao do controle INVERTIDA (Invert control action).");
                        else
                            Log("AVISO: PID " + nome + ": nao consegui inverter a acao do controle; marque \"Invert control action\" no TIA.");
                    }
                    continue;
                }
                if (ExisteBloco(plc.BlockGroup, nome))
                {
                    Log("ERRO: PID " + pid.Sub + "." + pid.Eq + "." + pid.Fn + ": ja existe um bloco chamado " + nome
                        + " que nao e PID_Compact.");
                    ok = false;
                    continue;
                }

                TechnologicalInstanceDB novo = CriarPid(grupoTo, nome);
                if (novo == null) { ok = false; continue; }
                ConfigurarPid(novo, nome, pid.PidInverso);
            }
            return ok;
        }

        /// <summary>Cria o PID_Compact na versao desta geracao; sem versao definida, tenta as conhecidas.</summary>
        private TechnologicalInstanceDB CriarPid(TechnologicalInstanceDBGroup grupoTo, string nome)
        {
            IEnumerable<string> tentar = _versaoPid != null ? new[] { _versaoPid } : (IEnumerable<string>)VersoesPid;
            string ultimoErro = null;
            foreach (string v in tentar)
            {
                try
                {
                    TechnologicalInstanceDB to = grupoTo.TechnologicalObjects.Create(nome, TipoPid, new Version(v));
                    if (_versaoPid == null)
                    {
                        _versaoPid = v;
                        Log("PID_Compact: usando a versao " + v + " (nao havia PID no projeto para copiar a versao).");
                    }
                    Log("PID " + nome + ": objeto tecnologico PID_Compact V" + v + " criado.");
                    return to;
                }
                catch (Exception ex)
                {
                    ultimoErro = PrimeiraLinha(ex);
                }
            }
            Log("ERRO: nao consegui criar o PID_Compact " + nome + " (" + ultimoErro + ").");
            Log("      Informe a versao do PID_Compact na configuracao (ex.: 2.4) e gere de novo.");
            return null;
        }

        /// <summary>Configuracao das telas do PID (Controller type e Input/Output parameters).</summary>
        private void ConfigurarPid(TechnologicalInstanceDB to, string nome, bool inverter)
        {
            var faltou = new List<string>();
            if (!DefinirParametro(to, new[] { "Config.PhysicalQuantity", "_Config.PhysicalQuantity" }, 0)) faltou.Add("tipo General");
            if (!DefinirParametro(to, new[] { "Config.PhysicalUnit", "_Config.PhysicalUnit" }, 0)) faltou.Add("unidade %");
            if (!DefinirParametro(to, new[] { "Config.InvertControl", "_Config.InvertControl" }, inverter)) faltou.Add(inverter ? "inverter acao do controle" : "nao inverter");
            if (!DefinirParametro(to, new[] { "RunModeByStartup", "Config.RunModeByStartup" }, true)) faltou.Add("ativar modo apos restart");
            if (!DefinirParametro(to, new[] { "Mode" }, 3)) faltou.Add("modo automatico");
            if (!DefinirParametro(to, new[] { "_Config.InputPerOn", "Config.InputPerOn" }, false)) faltou.Add("Input (nao Input_PER)");
            // saida: 1 = "Output" (0 deixou em "Output_PER (analog)" no teste no TIA)
            if (!DefinirParametro(to, new[] { "_Config.OutputSelect", "Config.OutputSelect" }, 1)) faltou.Add("Output");

            if (faltou.Count > 0)
                Log("AVISO: PID " + nome + ": confira no TIA (Controller type / Input-output): "
                    + string.Join(", ", faltou.ToArray()) + ".");
        }

        /// <summary>Grava um parametro do objeto tecnologico; tenta os nomes e tipos numericos possiveis.</summary>
        private static bool DefinirParametro(TechnologicalInstanceDB to, string[] nomes, object valor)
        {
            foreach (string n in nomes)
            {
                TechnologicalParameter par;
                try { par = to.Parameters.Find(n); }
                catch { par = null; }
                if (par == null) continue;

                var candidatos = new List<object> { valor };
                if (valor is int)
                {
                    int i = (int)valor;
                    candidatos.Add((short)i); candidatos.Add((byte)i); candidatos.Add((ushort)i);
                    candidatos.Add((uint)i); candidatos.Add((long)i);
                }
                foreach (object v in candidatos)
                {
                    try { par.Value = v; return true; }
                    catch { /* tenta o proximo tipo */ }
                }
            }
            return false;
        }

        private static TechnologicalInstanceDB AcharTo(TechnologicalInstanceDBGroup grupo, string nome)
        {
            TechnologicalInstanceDB t = grupo.TechnologicalObjects.Find(nome);
            if (t != null) return t;
            foreach (TechnologicalInstanceDBGroup sub in SubgruposTo(grupo))
            {
                t = AcharTo(sub, nome);
                if (t != null) return t;
            }
            return null;
        }

        private static IEnumerable<TechnologicalInstanceDBGroup> SubgruposTo(TechnologicalInstanceDBGroup grupo)
        {
            var prop = grupo.GetType().GetProperty("Groups");
            var col = prop != null ? prop.GetValue(grupo, null) as System.Collections.IEnumerable : null;
            if (col == null) yield break;
            foreach (object o in col)
            {
                var g = o as TechnologicalInstanceDBGroup;
                if (g != null) yield return g;
            }
        }

        /// <summary>
        /// Versao do PID_Compact: a da configuracao; senao, a de um PID ja chamado no projeto
        /// (procura nos OBs e nas FCs de PID geradas); senao, null (decide ao criar).
        /// </summary>
        private string DescobrirVersaoPid(PlcSoftware plc, TechnologicalInstanceDBGroup grupoTo)
        {
            if (!string.IsNullOrWhiteSpace(Cfg.VersaoPid))
            {
                Log("PID_Compact: versao " + Cfg.VersaoPid.Trim() + " (da configuracao).");
                return Cfg.VersaoPid.Trim();
            }

            string pasta = Path.Combine(Path.GetTempPath(), "Genesis");
            Directory.CreateDirectory(pasta);
            var rx = new Regex("<Part Name=\"PID_Compact\" Version=\"([0-9.]+)\"");

            foreach (PlcBlock b in BlocosDe(plc.BlockGroup))
            {
                bool candidato = b is OB || (b is FC && b.Name.StartsWith(Cfg.Prefixo + "PID_"));
                if (!candidato) continue;

                string arq = Path.Combine(pasta, "versao_pid.xml");
                try
                {
                    if (File.Exists(arq)) File.Delete(arq);
                    b.Export(new FileInfo(arq), ExportOptions.None);
                    Match m = rx.Match(File.ReadAllText(arq, Encoding.UTF8));
                    File.Delete(arq);
                    if (m.Success)
                    {
                        Log("PID_Compact: versao " + m.Groups[1].Value + " (a mesma do PID chamado em " + b.Name + ").");
                        return m.Groups[1].Value;
                    }
                }
                catch
                {
                    // bloco inconsistente / protegido: segue
                }
            }
            return null;
        }

        private static IEnumerable<PlcBlock> BlocosDe(PlcBlockGroup grupo)
        {
            foreach (PlcBlock b in grupo.Blocks) yield return b;
            foreach (PlcBlockGroup sub in grupo.Groups)
                foreach (PlcBlock b in BlocosDe(sub)) yield return b;
        }

        /// <summary>Contato NEGADO, alimentado pelo trilho, ligado direto no pino.</summary>
        private static void LigarContatoNegado(Rede r, int call, string pino, string simbolo)
        {
            int c = r.Contato(simbolo, true);
            r.ParaTrilho(c, "in");
            r.Entrada(call, pino, Rede.Pino(c, "out"));
        }

        // ===================================================================
        //  Construtor de rede LAD: acessos, contatos, bobinas, OU, chamadas e fios.
        //  Cada origem de sinal vira um unico <Wire>, com todos os destinos
        //  (igual ao que o TIA exporta).
        // ===================================================================
        private class Rede
        {
            private const string Trilho = "<Powerrail />";

            private int _uid = 21;
            private readonly StringBuilder _acessos = new StringBuilder();
            private readonly StringBuilder _partes = new StringBuilder();
            private readonly List<string> _origens = new List<string>();
            private readonly Dictionary<string, List<string>> _destinos =
                new Dictionary<string, List<string>>();
            private readonly Dictionary<int, HashSet<string>> _ligados =
                new Dictionary<int, HashSet<string>>();

            public static string Ident(int uid)
            {
                return "<IdentCon UId=\"" + uid + "\" />";
            }

            public static string Pino(int uid, string nome)
            {
                return "<NameCon UId=\"" + uid + "\" Name=\"" + nome + "\" />";
            }

            private static string Aberto(int uid)
            {
                return "<OpenCon UId=\"" + uid + "\" />";
            }

            /// <summary>Liga uma origem a um destino (destinos da mesma origem dividem o fio).</summary>
            public void Fio(string origem, string destino)
            {
                List<string> lista;
                if (!_destinos.TryGetValue(origem, out lista))
                {
                    lista = new List<string>();
                    _destinos[origem] = lista;
                    _origens.Add(origem);
                }
                lista.Add(destino);
            }

            public void ParaTrilho(int uidParte, string nomePino)
            {
                Fio(Trilho, Pino(uidParte, nomePino));
            }

            /// <summary>expr: "SYM|a.b.c" (variavel global) ou "LIT|Tipo|valor".</summary>
            public int Acesso(string expr)
            {
                string[] x = expr.Split('|');
                int u = _uid++;

                if (x[0] == "LOC")
                {
                    _acessos.AppendLine("<Access Scope=\"LocalVariable\" UId=\"" + u + "\"><Symbol>"
                        + "<Component Name=\"" + SecurityElement.Escape(x[1]) + "\" /></Symbol></Access>");
                }
                else if (x[0] == "SYM")
                {
                    _acessos.Append("<Access Scope=\"GlobalVariable\" UId=\"" + u + "\"><Symbol>");
                    string[] comps = x[1].Split('.');
                    for (int k = 0; k < comps.Length; k++)
                    {
                        // acesso a bit de word: "STATUS_WORD_1.%X3" -> SliceAccessModifier no componente anterior
                        string fatia = k + 1 < comps.Length && comps[k + 1].StartsWith("%X") ? "x" + comps[k + 1].Substring(2) : null;
                        _acessos.Append("<Component Name=\"" + SecurityElement.Escape(comps[k]) + "\""
                            + (fatia != null ? " SliceAccessModifier=\"" + fatia + "\"" : "") + " />");
                        if (fatia != null) k++;
                    }
                    _acessos.AppendLine("</Symbol></Access>");
                }
                else if (x[0] == "LIT0")
                {
                    // constante sem ConstantType: o TIA deduz o tipo pelo valor (ex.: ponteiro P#DB6.DBX0.0 BYTE 224)
                    _acessos.AppendLine("<Access Scope=\"LiteralConstant\" UId=\"" + u + "\"><Constant>"
                        + "<ConstantValue>" + SecurityElement.Escape(x[1]) + "</ConstantValue></Constant></Access>");
                }
                else if (x[0] == "TYP")
                {
                    // constante com tipo (ex.: ponteiro P#DB6.DBX0.0 BYTE 220)
                    _acessos.AppendLine("<Access Scope=\"TypedConstant\" UId=\"" + u + "\"><Constant>"
                        + "<ConstantValue>" + SecurityElement.Escape(x[1]) + "</ConstantValue></Constant></Access>");
                }
                else
                {
                    _acessos.AppendLine("<Access Scope=\"LiteralConstant\" UId=\"" + u + "\"><Constant>"
                        + "<ConstantType>" + x[1] + "</ConstantType>"
                        + "<ConstantValue>" + x[2] + "</ConstantValue></Constant></Access>");
                }
                return u;
            }

            public int Contato(string simbolo, bool negado)
            {
                int a = Acesso("SYM|" + simbolo);
                int c = _uid++;
                _partes.AppendLine(negado
                    ? "<Part Name=\"Contact\" UId=\"" + c + "\"><Negated Name=\"operand\" /></Part>"
                    : "<Part Name=\"Contact\" UId=\"" + c + "\" />");
                Fio(Ident(a), Pino(c, "operand"));
                return c;
            }

            /// <summary>Contato sem operando (para preencher manualmente no TIA).</summary>
            public int ContatoVazio()
            {
                int c = _uid++;
                _partes.AppendLine("<Part Name=\"Contact\" UId=\"" + c + "\" />");

                // o TIA exige o operando ligado: sem tag, vai uma ponta aberta
                int aberto = _uid++;
                Fio(Aberto(aberto), Pino(c, "operand"));
                return c;
            }

            /// <summary>tipo: "Coil", "SCoil" ou "RCoil".</summary>
            public int Bobina(string tipo, string simbolo)
            {
                int a = Acesso("SYM|" + simbolo);
                int c = _uid++;
                _partes.AppendLine("<Part Name=\"" + tipo + "\" UId=\"" + c + "\" />");
                Fio(Ident(a), Pino(c, "operand"));
                return c;
            }

            public int Ou(int entradas)
            {
                int o = _uid++;
                _partes.AppendLine("<Part Name=\"O\" UId=\"" + o + "\">"
                    + "<TemplateValue Name=\"Card\" Type=\"Cardinality\">" + entradas + "</TemplateValue></Part>");
                return o;
            }

            private void LigarEn(int parte, string pino, string origem)
            {
                if (origem == null) ParaTrilho(parte, pino);
                else Fio(origem, Pino(parte, pino));
            }

            /// <summary>MOVE: en (null = trilho), in -> out1.</summary>
            public int Mover(string en, string entrada, string saida)
            {
                int m = _uid++;
                _partes.AppendLine("<Part Name=\"Move\" UId=\"" + m + "\" DisabledENO=\"true\">"
                    + "<TemplateValue Name=\"Card\" Type=\"Cardinality\">1</TemplateValue></Part>");
                LigarEn(m, "en", en);
                Fio(entrada, Pino(m, "in"));
                Fio(Pino(m, "out1"), saida);
                return m;
            }

            /// <summary>SUB (tipo automatico): en (null = trilho), in1 - in2 -> out.</summary>
            public int Subtrair(string en, string in1, string in2, string saida)
            {
                int s = _uid++;
                _partes.AppendLine("<Part Name=\"Sub\" UId=\"" + s + "\" DisabledENO=\"true\">"
                    + "<AutomaticTyped Name=\"SrcType\" /></Part>");
                LigarEn(s, "en", en);
                Fio(in1, Pino(s, "in1"));
                Fio(in2, Pino(s, "in2"));
                Fio(Pino(s, "out"), saida);
                return s;
            }

            /// <summary>DIV (tipo automatico): en (null = trilho), in1 / in2 -> out.</summary>
            public int Dividir(string en, string in1, string in2, string saida)
            {
                int d = _uid++;
                _partes.AppendLine("<Part Name=\"Div\" UId=\"" + d + "\" DisabledENO=\"true\">"
                    + "<AutomaticTyped Name=\"SrcType\" /></Part>");
                LigarEn(d, "en", en);
                Fio(in1, Pino(d, "in1"));
                Fio(in2, Pino(d, "in2"));
                Fio(Pino(d, "out"), saida);
                return d;
            }

            /// <summary>Comparador (op = "Ge", "Lt", "Gt", "Le", "Eq", "Ne"): pre (null = trilho), in1 op in2; saida no pino "out".</summary>
            public int Comparar(string op, string tipo, string pre, string in1, string in2)
            {
                int c = _uid++;
                _partes.AppendLine("<Part Name=\"" + op + "\" UId=\"" + c + "\">"
                    + "<TemplateValue Name=\"SrcType\" Type=\"Type\">" + tipo + "</TemplateValue></Part>");
                LigarEn(c, "pre", pre);
                Fio(in1, Pino(c, "in1"));
                Fio(in2, Pino(c, "in2"));
                return c;
            }

            /// <summary>IN_RANGE Int: pre (null = trilho), min &lt;= valor &lt;= max; saida no pino "out".</summary>
            public int Faixa(string pre, string valor, int min, int max)
            {
                int f = _uid++;
                _partes.AppendLine("<Part Name=\"InRange\" UId=\"" + f + "\">"
                    + "<TemplateValue Name=\"SrcType\" Type=\"Type\">Int</TemplateValue></Part>");
                LigarEn(f, "pre", pre);
                Fio(Ident(Acesso("LIT|Int|" + min)), Pino(f, "min"));
                Fio(valor, Pino(f, "in"));
                Fio(Ident(Acesso("LIT|Int|" + max)), Pino(f, "max"));
                return f;
            }

            public int Chamada(string fb, string instancia, string[][] pinos)
            {
                int call = _uid++;
                int inst = _uid++;

                _partes.AppendLine("<Call UId=\"" + call + "\">");
                _partes.AppendLine("<CallInfo Name=\"" + fb + "\" BlockType=\"FB\">");
                _partes.AppendLine("<Instance Scope=\"GlobalVariable\" UId=\"" + inst + "\">");
                _partes.AppendLine("<Component Name=\"" + SecurityElement.Escape(instancia) + "\" />");
                _partes.AppendLine("</Instance>");
                foreach (string[] pino in pinos)
                {
                    _partes.AppendLine("<Parameter Name=\"" + pino[0] + "\" Section=\"" + pino[1]
                        + "\" Type=\"" + pino[2] + "\" />");
                }
                _partes.AppendLine("</CallInfo>");
                _partes.AppendLine("</Call>");

                _ligados[call] = new HashSet<string>();
                return call;
            }

            public void Entrada(int call, string pino, string origem)
            {
                Fio(origem, Pino(call, pino));
                _ligados[call].Add(pino);
            }

            /// <summary>Temporizador IEC (TON/TOF/TP) com a instancia num membro IEC_TIMER de um DB ("PP1".SIS.DES).</summary>
            public int Temporizador(string tipo, string instancia)
            {
                int p = _uid++;
                int inst = _uid++;
                var comps = new StringBuilder();
                foreach (string c in instancia.Split('.')) comps.Append("<Component Name=\"" + SecurityElement.Escape(c) + "\" />");
                _partes.AppendLine("<Part Name=\"" + tipo + "\" Version=\"1.0\" UId=\"" + p + "\">"
                    + "<Instance Scope=\"GlobalVariable\" UId=\"" + inst + "\">" + comps + "</Instance>"
                    + "<TemplateValue Name=\"time_type\" Type=\"Type\">Time</TemplateValue></Part>");
                _ligados[p] = new HashSet<string>();
                return p;
            }

            /// <summary>Instrucao de sistema com instancia (ex.: MB_SERVER).</summary>
            /// escopo: "GlobalVariable" (DB de instancia) ou "LocalVariable" (multi-instancia dentro de um FB).
            public int Parte(string nome, string versao, string instancia, string escopo = "GlobalVariable")
            {
                int p = _uid++;
                int inst = _uid++;
                _partes.AppendLine("<Part Name=\"" + nome + "\" Version=\"" + versao + "\" UId=\"" + p + "\">"
                    + "<Instance Scope=\"" + escopo + "\" UId=\"" + inst + "\"><Component Name=\""
                    + SecurityElement.Escape(instancia) + "\" /></Instance></Part>");
                _ligados[p] = new HashSet<string>();
                return p;
            }

            /// <summary>Instrucao PID_Compact (objeto tecnologico como instancia).</summary>
            public int Pid(string versao, string instancia)
            {
                int p = _uid++;
                int inst = _uid++;
                _partes.AppendLine("<Part Name=\"PID_Compact\" Version=\"" + versao + "\" UId=\"" + p + "\">"
                    + "<Instance Scope=\"GlobalVariable\" UId=\"" + inst + "\"><Component Name=\""
                    + SecurityElement.Escape(instancia) + "\" /></Instance></Part>");
                _ligados[p] = new HashSet<string>();
                return p;
            }

            /// <summary>O pino da chamada ja tem ligacao?</summary>
            public bool Ligado(int call, string pino)
            {
                return _ligados[call].Contains(pino);
            }

            public void Saida(int call, string pino, string destino)
            {
                Fio(Pino(call, pino), destino);
                _ligados[call].Add(pino);
            }

            /// <summary>Pinos sem ligacao ganham uma ponta aberta (OpenCon), como no TIA.</summary>
            public void FecharPinos(int call, string[][] pinos)
            {
                foreach (string[] pino in pinos)
                {
                    if (_ligados[call].Contains(pino[0])) continue;

                    int o = _uid++;
                    if (pino[1] == "Input" || pino[1] == "InOut") Fio(Aberto(o), Pino(call, pino[0]));
                    else Fio(Pino(call, pino[0]), Aberto(o));
                }
            }

            public string XmlPartes()
            {
                return "<Parts>" + Environment.NewLine
                    + _acessos + _partes
                    + "</Parts>" + Environment.NewLine;
            }

            /// <summary>Chamar por ultimo: os UIds dos fios vem depois de todas as partes.</summary>
            public string XmlFios()
            {
                var sb = new StringBuilder();
                foreach (string origem in _origens)
                {
                    sb.Append("<Wire UId=\"" + (_uid++) + "\">" + origem);
                    foreach (string destino in _destinos[origem]) sb.Append(destino);
                    sb.AppendLine("</Wire>");
                }
                return sb.ToString();
            }
        }

        private string TextoMulti(string composicao, string textoPtBr)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<MultilingualText ID=\"" + NovoId() + "\" CompositionName=\"" + composicao + "\">");
            sb.AppendLine("<ObjectList>");
            foreach (string cultura in Culturas)
            {
                string txt = cultura == "pt-BR" ? SecurityElement.Escape(textoPtBr) : "";
                sb.AppendLine("<MultilingualTextItem ID=\"" + NovoId() + "\" CompositionName=\"Items\">");
                sb.AppendLine("<AttributeList><Culture>" + cultura + "</Culture>"
                    + (string.IsNullOrEmpty(txt) ? "<Text />" : "<Text>" + txt + "</Text>")
                    + "</AttributeList>");
                sb.AppendLine("</MultilingualTextItem>");
            }
            sb.AppendLine("</ObjectList>");
            sb.AppendLine("</MultilingualText>");
            return sb.ToString();
        }

        private string NovoId()
        {
            return (_id++).ToString("X");
        }

        // ===================================================================
        //  Importacao e compilacao
        // ===================================================================
        private bool LimparBlocosAnteriores(
            PlcSoftware plc, PlcBlockUserGroup grupo, SubsistemaGerado sg)
        {
            // FCs primeiro, depois instancias, depois o DB (os PID_Compact ficam: guardam a sintonia)
            var nomes = new List<string>();
            if (sg.ArqFc != null) nomes.Add(sg.NomeFc);
            if (sg.ArqFcPid != null) nomes.Add(sg.NomeFcPid);
            nomes.AddRange(sg.Partidas.SelectMany(p => InstanciasDaPartida(p).Select(x => x.Key)).Distinct());
            nomes.Add(sg.NomeDb);

            // Antes de apagar qualquer coisa, garante que nenhum nome colide
            // com bloco existente FORA do grupo FAST_GERADO.
            foreach (string n in nomes)
            {
                if (grupo.Blocks.Find(n) == null && ExisteBloco(plc.BlockGroup, n))
                {
                    Log("Subsistema " + sg.Sub + ": o bloco " + n
                        + " existe FORA do grupo " + NomeGrupo + ". Subsistema ignorado.");
                    return false;
                }
            }

            foreach (string n in nomes)
            {
                PlcBlock b = grupo.Blocks.Find(n);
                if (b != null)
                {
                    Log("Removendo bloco anterior: " + n);
                    b.Delete();
                }
            }
            return true;
        }

        private bool ImportarECompilar(PlcBlockUserGroup grupo, SubsistemaGerado sg)
        {
            Log("--- Subsistema " + sg.Sub + " (" + sg.Partidas.Count + " partidas) ---");

            Log("Importando " + sg.NomeDb + "...");
            ImportarArquivo(grupo, sg.ArqDb);

            foreach (string arq in sg.ArqsIdb)
            {
                Log("Importando " + Path.GetFileNameWithoutExtension(arq) + "...");
                ImportarArquivo(grupo, arq);
            }

            if (sg.ArqFc != null)
            {
                Log("Importando " + sg.NomeFc + "...");
                ImportarArquivo(grupo, sg.ArqFc);
            }
            if (sg.ArqFcPid != null)
            {
                Log("Importando " + sg.NomeFcPid + "...");
                ImportarFcPid(grupo, sg);
            }

            if (sg.Partidas.Any(x => x.Intertravamento))
            {
                Log("Obs.: partidas com intertravamento tem um contato sem tag (condicao) para preencher no TIA;");
                Log("      erros de compilacao na FC por esse contato sao esperados ate la.");
            }

            Log("Compilando...");

            // DBs antes da FC, que acessa todos eles
            var ordem = new List<string> { sg.NomeDb };
            ordem.AddRange(sg.Partidas.SelectMany(p => InstanciasDaPartida(p).Select(x => x.Key)).Distinct());
            if (sg.ArqFc != null) ordem.Add(sg.NomeFc);
            if (sg.ArqFcPid != null) ordem.Add(sg.NomeFcPid);

            int totalErros = 0;
            foreach (string nome in ordem)
            {
                PlcBlock bloco = grupo.Blocks.Find(nome);
                CompilerResult r = bloco.GetService<ICompilable>().Compile();
                totalErros += r.ErrorCount;

                if (r.ErrorCount > 0 || r.WarningCount > 0)
                {
                    Log("  " + nome + ": " + r.State + " (erros: "
                        + r.ErrorCount + ", avisos: " + r.WarningCount + ")");
                    foreach (CompilerResultMessage m in r.Messages)
                        LogMensagem(m, 2);
                }
            }

            Log(totalErros == 0
                ? "OK: " + string.Join(" e ", ordem.Where(n => n == sg.NomeFc || n == sg.NomeFcPid).ToArray())
                    + " e DBs compilados sem erros."
                : "ATENCAO: " + totalErros + " erro(s) de compilacao no subsistema " + sg.Sub + ".");
            if (sg.ArqFcPid != null) AvisarChamadaPid(sg.NomeFcPid);
            Log("");
            return totalErros == 0;
        }

        /// <summary>
        /// Importa a FC dos PIDs; se o TIA recusar (versao do PID_Compact diferente da dos objetos tecnologicos),
        /// tenta as outras versoes conhecidas.
        /// </summary>
        private void ImportarFcPid(PlcBlockGroup grupo, SubsistemaGerado sg)
        {
            List<Partida> pids = sg.Partidas.Where(EhPid).ToList();
            var tentar = new List<string>();
            foreach (string v in new[] { _versaoPid, Cfg.VersaoPid }.Concat(VersoesPid))
                if (!string.IsNullOrWhiteSpace(v) && !tentar.Contains(v.Trim())) tentar.Add(v.Trim());

            Exception ultimo = null;
            foreach (string v in tentar)
            {
                _versaoPid = v;
                File.WriteAllText(sg.ArqFcPid, XmlFcPid(sg, sg.NomeFcPid, pids), new UTF8Encoding(false));
                try
                {
                    ImportarArquivo(grupo, sg.ArqFcPid);
                    if (ultimo != null) Log("PID_Compact: a FC entrou com a versao " + v + " (a dos objetos tecnologicos do projeto).");
                    return;
                }
                catch (Exception ex)
                {
                    ultimo = ex;
                }
            }
            throw ultimo;
        }

        private void AvisarChamadaPid(string nomeFc)
        {
            Log(">>> Chame a FC " + nomeFc + " no OB de interrupcao ciclica (ex.: Cyclic interrupt [OB30]):");
            Log(">>> o PID_Compact precisa rodar em tempo fixo. Se ela ja e chamada la, nada a fazer.");
        }

        /// <summary>
        /// Importa um XML. Se o TIA reclamar de atributo 'ReadOnly' ausente
        /// (templates exportados de projeto/CPU mais antigo), tenta por etapas,
        /// para o campo citado: ReadOnly="false", depois ReadOnly="true" e,
        /// por fim, remove o campo (atributo opcional).
        /// </summary>
        private void ImportarArquivo(PlcBlockGroup grupo, string arquivo, ImportOptions opcoes = ImportOptions.None)
        {
            // Importacoes feitas a partir da raiz FAST_GERADO sao organizadas por tipo:
            // DBs globais em DBs DE TAGS, FCs/FBs em FCs e DBs de instancia dentro de FCs.
            PlcBlockGroup destino = GrupoDestinoImportacao(grupo, arquivo);
            var etapas = new Dictionary<string, int>();

            for (int tentativa = 0; tentativa < 12; tentativa++)
            {
                try
                {
                    destino.Blocks.Import(new FileInfo(arquivo), opcoes);
                    return;
                }
                catch (Exception ex)
                {
                    Match m = Regex.Match(ex.Message ?? "",
                        "Missing 'ReadOnly' XML attribute for the '(\\w+)' attribute");
                    if (!m.Success) throw;

                    string attr = m.Groups[1].Value;
                    int etapa;
                    etapas.TryGetValue(attr, out etapa);

                    string txt = File.ReadAllText(arquivo, Encoding.UTF8);
                    string novo;

                    if (etapa == 0)
                    {
                        novo = Regex.Replace(txt, "<" + attr + @"(\s*/?>)",
                            "<" + attr + " ReadOnly=\"false\"$1");
                        Log("  (ajuste automatico: " + attr + " ReadOnly=\"false\")");
                    }
                    else if (etapa == 1)
                    {
                        novo = txt.Replace("<" + attr + " ReadOnly=\"false\"",
                                           "<" + attr + " ReadOnly=\"true\"");
                        Log("  (ajuste automatico: " + attr + " ReadOnly=\"true\")");
                    }
                    else
                    {
                        novo = Regex.Replace(txt,
                            @"[ \t]*<" + attr + @"\b[^>]*?(?:/>|>[^<]*</" + attr + @">)\r?\n?",
                            "");
                        Log("  (ajuste automatico: campo " + attr + " removido do XML)");
                    }

                    if (novo == txt) throw;

                    etapas[attr] = etapa + 1;
                    File.WriteAllText(arquivo, novo, new UTF8Encoding(false));
                }
            }

            // ultima tentativa: se ainda falhar, o erro real aparece
            destino.Blocks.Import(new FileInfo(arquivo), opcoes);
        }

        private PlcBlockGroup GrupoDestinoImportacao(PlcBlockGroup grupo, string arquivo)
        {
            if (grupo == null || !string.Equals(grupo.Name, NomeGrupo, StringComparison.OrdinalIgnoreCase))
                return grupo;

            // Garante que a arvore de pastas exista sempre, inclusive quando algum tipo
            // de bloco nao for gerado nesta execucao.
            ObterSubgrupo(grupo, "DBs DE TAGS");
            ObterSubgrupo(grupo, "FCs");
            ObterSubgrupo(grupo, "FCs\\DBs DE INSTANCIA");

            string xml = File.ReadAllText(arquivo, Encoding.UTF8);
            string pasta;
            if (xml.IndexOf("SW.Blocks.InstanceDB", StringComparison.OrdinalIgnoreCase) >= 0)
                pasta = "FCs\\DBs DE INSTANCIA";
            else if (xml.IndexOf("SW.Blocks.GlobalDB", StringComparison.OrdinalIgnoreCase) >= 0)
                pasta = "DBs DE TAGS";
            else if (xml.IndexOf("SW.Blocks.FC", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     xml.IndexOf("SW.Blocks.FB", StringComparison.OrdinalIgnoreCase) >= 0)
                pasta = "FCs";
            else
                return grupo;

            Log("  Pasta de destino: " + pasta.Replace("\\", " / "));
            return ObterSubgrupo(grupo, pasta);
        }

        private static PlcBlockGroup ObterSubgrupo(PlcBlockGroup grupo, string caminho)
        {
            PlcBlockGroup atual = grupo;
            foreach (string nome in caminho.Split('\\'))
            {
                PlcBlockUserGroup sub = atual.Groups.Find(nome) ?? atual.Groups.Create(nome);
                atual = sub;
            }
            return atual;
        }

        private static bool GrupoEstaDentroDe(PlcBlockGroup candidato, PlcBlockGroup raiz)
        {
            if (candidato == null || raiz == null) return false;
            if (ReferenceEquals(candidato, raiz)) return true;
            foreach (PlcBlockGroup sub in raiz.Groups)
                if (GrupoEstaDentroDe(candidato, sub)) return true;
            return false;
        }

        // ===================================================================
        //  Gerar somente as partidas marcadas
        //  Ignora as outras partidas e NAO le nem altera a FC que ja existe
        //  (o TIA nao exporta FC com erros, e ela costuma ter contatos de
        //  intertravamento a preencher):
        //   - DBs de instancia: cria so os que faltam
        //   - DB do subsistema: acrescenta so os membros que faltam
        //   - Redes: vao para uma FC NOVA (<FC>_NOVAS); o usuario copia para a FC dele
        // ===================================================================
        private bool ExecutarSelecionadas(
            PlcSoftware plc, List<Partida> partidas, List<Partida> selecionadas,
            Dictionary<string, string> templates, Func<string, bool> existe)
        {
            LimparXmlAntigos();
            string saida = Path.Combine(Path.GetTempPath(), "Genesis",
                "Gerado_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            Directory.CreateDirectory(saida);

            bool importou = false;
            try
            {
                PlcBlockUserGroup grupo =
                    plc.BlockGroup.Groups.Find(NomeGrupo)
                    ?? plc.BlockGroup.Groups.Create(NomeGrupo);

                var marcadas = new HashSet<Partida>(selecionadas);
                bool tudoOk = true;

                foreach (var g in partidas.GroupBy(p => p.Sub))
                {
                    List<Partida> sel = g.Where(p => marcadas.Contains(p)).ToList();
                    if (sel.Count == 0) continue;

                    var sg = new SubsistemaGerado();
                    sg.Sub = g.Key;
                    sg.NomeDb = Cfg.Prefixo + g.Key;
                    sg.NomeFc = NomeFcDe(g.Key, g);
                    sg.Partidas = g.ToList();

                    try
                    {
                        if (!AtualizarSubsistema(plc, grupo, sg, sel, saida, templates, existe))
                            tudoOk = false;
                    }
                    catch (Exception ex)
                    {
                        tudoOk = false;
                        Log("ERRO no subsistema " + sg.Sub + ": " + DetalheErro(ex));
                        Log("      Os outros subsistemas continuam.");
                        Log("");
                    }
                }

                // PIDs marcados: o OB ciclico e refeito com todos os PIDs do projeto
                if (Cfg.PidNoOb && selecionadas.Any(EhPid))
                {
                    try
                    {
                        if (!GerarPidsNoOb(plc, grupo, partidas, saida, partidas.Where(EhPid).Except(selecionadas).ToList()))
                            tudoOk = false;
                    }
                    catch (Exception ex)
                    {
                        tudoOk = false;
                        Log("ERRO nos PIDs do OB" + NumeroObPid + ": " + DetalheErro(ex));
                    }
                }

                // comunicacao: no "Gerar marcadas" so e criada se o DB COM ainda nao existe (senao, botao da pagina Modbus)
                if (Cfg.ComAtivo)
                {
                    PlcBlockGroup ondeCom;
                    if (AcharBloco(plc.BlockGroup, NomeCom, out ondeCom) == null)
                    {
                        try
                        {
                            if (!GerarComunicacao(plc, grupo, partidas, existe, null, saida)) tudoOk = false;
                        }
                        catch (Exception ex)
                        {
                            tudoOk = false;
                            Log("ERRO na comunicacao Modbus: " + DetalheErro(ex));
                        }
                    }
                    else
                        Log("Obs.: o DB " + NomeCom + " ja existe e nao foi alterado no \"Gerar marcadas\". Para atualizar o mapa, use o botao da pagina Mapa Modbus.");
                }

                importou = true;
                Log(tudoOk ? "CONCLUIDO sem erros." : "CONCLUIDO com pendencias (veja acima).");
                return tudoOk;
            }
            finally
            {
                LimparSaida(saida, importou);
            }
        }

        private bool AtualizarSubsistema(
            PlcSoftware plc, PlcBlockUserGroup grupo, SubsistemaGerado sg, List<Partida> sel,
            string saida, Dictionary<string, string> templates, Func<string, bool> existe)
        {
            var utf8 = new UTF8Encoding(false);
            Log("--- Subsistema " + sg.Sub + ": atualizando " + sel.Count + " partida(s) selecionada(s) ---");

            PlcBlockGroup grupoDb, grupoFc;
            PlcBlock dbBloco = AcharBloco(plc.BlockGroup, sg.NomeDb, out grupoDb);
            PlcBlock fcBloco = AcharBloco(plc.BlockGroup, sg.NomeFc, out grupoFc);

            var paraCompilar = new List<string>();

            // 1) DBs de instancia que ainda nao existem (os que existem ficam como estao)
            foreach (Partida p in sel)
            {
                foreach (var inst in InstanciasDaPartida(p))
                    GarantirInstancia(plc, grupo, saida, inst.Key, templates[FbReal(inst.Value)],
                        paraCompilar, FbReal(inst.Value));
            }

            // 2) DB do subsistema
            string arqDbFinal = null;
            if (dbBloco == null)
            {
                Log("DB " + sg.NomeDb + " nao existe: criando com todas as partidas do subsistema.");
                string arq = Path.Combine(saida, sg.NomeDb + ".xml");
                File.WriteAllText(arq, XmlDb(sg), utf8);
                ImportarArquivo(grupo, arq);
                paraCompilar.Add(sg.NomeDb);
            }
            else
            {
                string exportado = Path.Combine(saida, sg.NomeDb + "_atual.xml");
                string alterado = Path.Combine(saida, sg.NomeDb + "_novo.xml");
                int acrescentadas = 0;
                bool lido = false;

                // o TIA so exporta bloco consistente: compila o DB antes de ler
                try
                {
                    dbBloco.GetService<ICompilable>().Compile();
                    dbBloco.Export(new FileInfo(exportado), ExportOptions.WithDefaults);
                    AtualizadorXml.AtualizarDb(exportado, alterado, sel, UdtDe, out acrescentadas, x => !EhSensor(x) && !EhPid(x) && !Maquinas.EhPreparador(x),
                        x => TemIntermitente(x) ? NomeItm(sg.Partidas, x) : null, NomeUdtIntermitente,
                        x => TemExaustor(x) ? NomeExa(sg.Partidas, x) : null, NomeUdt, ExtrasMaquinaDb);
                    lido = true;
                }
                catch (Exception ex)
                {
                    Log("AVISO: nao consegui ler o DB " + sg.NomeDb + " (" + PrimeiraLinha(ex) + ").");
                    Log("       Acrescente no DB, a mao, o membro das partidas novas (UDT_PARTIDAS e SI<n>).");
                }

                if (!lido)
                {
                    // segue sem mexer no DB; SI<n> pela ordem da lista
                }
                else if (acrescentadas > 0)
                {
                    Log("DB " + sg.NomeDb + ": acrescentando " + acrescentadas
                        + " partida(s); o que ja existe e mantido.");
                    ImportarArquivo(grupoDb, alterado, ImportOptions.Override);
                    paraCompilar.Add(sg.NomeDb);
                    arqDbFinal = alterado;
                }
                else
                {
                    Log("DB " + sg.NomeDb + ": ja tem as partidas selecionadas, nada a alterar.");
                    arqDbFinal = exportado;
                }
            }

            // numero do SI<n> de cada partida, lido do proprio DB
            sg.SiMap = arqDbFinal != null ? AtualizadorXml.MapaSi(arqDbFinal, new[] { NomeUdt, NomeUdtValvula }) : null;

            // 3) Redes das partidas marcadas
            //    A FC que ja existe NAO e lida nem alterada. Se ela ainda nao existe, e criada;
            //    se existe, as redes vao para uma FC nova, para o usuario copiar.
            List<Partida> selPid = sel.Where(EhPid).ToList();
            sel = sel.Where(x => !EhPid(x)).ToList();
            if (selPid.Count > 0 && !Cfg.PidNoOb)
                AtualizarFcPid(plc, grupo, sg, selPid, saida, paraCompilar);
            if (sel.Count == 0)
                return CompilarPorNome(plc, paraCompilar);

            string fcDestino = fcBloco == null ? sg.NomeFc : NomeLivre(plc, sg.NomeFc + "_NOVAS");

            var sgFc = new SubsistemaGerado
            {
                Sub = sg.Sub,
                NomeDb = sg.NomeDb,
                NomeFc = fcDestino,
                Partidas = sg.Partidas,
                SiMap = sg.SiMap
            };

            string arqFc = Path.Combine(saida, fcDestino + ".xml");
            File.WriteAllText(arqFc, XmlFc(sgFc, existe, sel), utf8);
            int nRedes = sel.Count(x =>
            {
                Partida par;
                return !(_pares.TryGetValue(x, out par) && BuscarTipo(x.Tipo).SenAlto && sel.Contains(par));
            });
            Log("Importando " + fcDestino + " (" + nRedes + " rede(s))...");
            ImportarArquivo(grupo, arqFc);
            paraCompilar.Add(fcDestino);

            if (fcBloco != null)
            {
                // a FC principal fica "a recompilar" quando o DB muda: compila para ela voltar ao normal
                paraCompilar.Add(sg.NomeFc);

                Log("");
                Log(">>> As redes foram criadas na FC " + fcDestino + ". A FC " + sg.NomeFc + " nao foi alterada.");
                Log(">>> No TIA: copie as redes de " + fcDestino + " para " + sg.NomeFc
                    + " (se a partida ja existia, troque a rede antiga) e depois apague " + fcDestino + ".");

                // variaveis Temp (sensor de nivel analogico) nao vao junto ao copiar redes
                List<string> temps = sel
                    .Where(x => { TipoPartida t = BuscarTipo(x.Tipo); return t.SenAna && !t.SenAnalitico; })
                    .Select(x => x.TempLeiAux).ToList();
                if (temps.Count > 0)
                    Log(">>> Crie tambem na FC " + sg.NomeFc + " as variaveis Temp (Real): "
                        + string.Join(", ", temps.ToArray()) + ".");
                Log("");
            }

            if (sel.Any(x => x.Intertravamento))
            {
                Log("Obs.: partidas com intertravamento tem um contato sem tag (condicao) para preencher no TIA;");
                Log("      erros de compilacao na FC por esse contato sao esperados ate la.");
            }

            return CompilarPorNome(plc, paraCompilar);
        }

        /// <summary>Gerar marcadas: redes dos PIDs marcados na FC de PID (ou numa _NOVAS, se ela ja existe).</summary>
        private void AtualizarFcPid(
            PlcSoftware plc, PlcBlockUserGroup grupo, SubsistemaGerado sg, List<Partida> selPid,
            string saida, List<string> paraCompilar)
        {
            string nomeFc = NomeFcPidDe(sg.Sub);
            PlcBlockGroup onde;
            bool existe = AcharBloco(plc.BlockGroup, nomeFc, out onde) != null;
            string destino = existe ? NomeLivre(plc, nomeFc + "_NOVAS") : nomeFc;

            string arq = Path.Combine(saida, destino + ".xml");
            File.WriteAllText(arq, XmlFcPid(sg, destino, selPid), new UTF8Encoding(false));
            Log("Importando " + destino + " (" + selPid.Count + " PID(s))...");
            ImportarArquivo(grupo, arq);
            paraCompilar.Add(destino);

            if (existe)
            {
                Log("");
                Log(">>> As redes dos PIDs foram criadas na FC " + destino + ". A FC " + nomeFc + " nao foi alterada.");
                Log(">>> No TIA: copie as redes para " + nomeFc + " (troque a rede antiga, se o PID ja existia) e apague "
                    + destino + ".");
                Log("");
            }
            else
            {
                AvisarChamadaPid(nomeFc);
            }
        }

        private void GarantirInstancia(
            PlcSoftware plc, PlcBlockUserGroup grupo, string saida, string nome,
            string template, List<string> compilar, string fb)
        {
            PlcBlockGroup onde;
            PlcBlock existente = AcharBloco(plc.BlockGroup, nome, out onde);
            if (existente != null)
            {
                string deQue = null;
                try
                {
                    InstanceDB idb = existente as InstanceDB;
                    if (idb != null) deQue = idb.InstanceOfName;
                }
                catch
                {
                    // sem como saber: mantem como esta
                }

                if (deQue == null || deQue == fb) return;   // ja existe e e do FB certo

                Log("O DB de instancia " + nome + " e do " + deQue + ", mas a partida agora usa o "
                    + fb + ": recriando.");
                existente.Delete();
            }

            string arq = Path.Combine(saida, nome + ".xml");
            File.WriteAllText(arq, XmlInstancia(template, nome), new UTF8Encoding(false));
            Log("Importando " + nome + "...");
            ImportarArquivo(grupo, arq);
            compilar.Add(nome);
        }

        /// <summary>Nome de bloco ainda nao usado: base, base2, base3...</summary>
        private static string NomeLivre(PlcSoftware plc, string baseNome)
        {
            string nome = baseNome;
            int n = 2;
            while (ExisteBloco(plc.BlockGroup, nome)) nome = baseNome + n++;
            return nome;
        }

        private static PlcBlock AcharBloco(PlcBlockGroup grupo, string nome, out PlcBlockGroup onde)
        {
            PlcBlock b = grupo.Blocks.Find(nome);
            if (b != null)
            {
                onde = grupo;
                return b;
            }

            foreach (PlcBlockGroup sub in grupo.Groups)
            {
                PlcBlock achado = AcharBloco(sub, nome, out onde);
                if (achado != null) return achado;
            }

            onde = null;
            return null;
        }

        private bool CompilarPorNome(PlcSoftware plc, List<string> nomes)
        {
            Log("Compilando...");
            int totalErros = 0;

            foreach (string nome in nomes)
            {
                PlcBlockGroup onde;
                PlcBlock bloco = AcharBloco(plc.BlockGroup, nome, out onde);
                if (bloco == null)
                {
                    Log("  " + nome + ": nao encontrado para compilar.");
                    continue;
                }

                CompilerResult r = bloco.GetService<ICompilable>().Compile();
                totalErros += r.ErrorCount;

                if (r.ErrorCount > 0 || r.WarningCount > 0)
                {
                    Log("  " + nome + ": " + r.State + " (erros: " + r.ErrorCount
                        + ", avisos: " + r.WarningCount + ")");
                    foreach (CompilerResultMessage m in r.Messages)
                        LogMensagem(m, 2);
                }
            }

            Log(totalErros == 0
                ? "OK: blocos compilados sem erros."
                : "ATENCAO: " + totalErros + " erro(s) de compilacao.");
            Log("");
            return totalErros == 0;
        }

        private void LogMensagem(CompilerResultMessage m, int nivel)
        {
            if (!string.IsNullOrWhiteSpace(m.Description))
                Log(new string(' ', nivel * 2) + "[" + m.State + "] " + m.Description);
            foreach (CompilerResultMessage sub in m.Messages)
                LogMensagem(sub, nivel + 1);
        }

        // ===================================================================
        //  Utilitarios
        // ===================================================================
        private void LimparSaida(string pasta, bool importou)
        {
            if (!importou)
            {
                Log("XMLs mantidos para diagnostico em: " + pasta);
                return;
            }
            if (Cfg.ManterXml)
            {
                Log("XMLs mantidos em: " + pasta);
                return;
            }
            try { Directory.Delete(pasta, true); }
            catch { /* pasta temporaria: se nao der para apagar, o Windows limpa depois */ }
        }

        /// <summary>Apaga pastas de XML temporarios com mais de 3 dias (sobras de falhas).</summary>
        private static void LimparXmlAntigos()
        {
            try
            {
                string raiz = Path.Combine(Path.GetTempPath(), "Genesis");
                if (!Directory.Exists(raiz)) return;

                foreach (string d in Directory.GetDirectories(raiz, "Gerado_*"))
                {
                    if (Directory.GetCreationTime(d) < DateTime.Now.AddDays(-3))
                        Directory.Delete(d, true);
                }
            }
            catch
            {
                // limpeza e opcional
            }
        }

        // ===================================================================
        //  Modelos de DB de instancia
        // ===================================================================

        // nome do FB usado no projeto para cada nome padrao do programa
        private readonly Dictionary<string, string> _fbs = new Dictionary<string, string>();

        private string FbReal(string nominal)
        {
            string real;
            return _fbs.TryGetValue(nominal, out real) ? real : nominal;
        }

        /// <summary>
        /// Acha no projeto a versao de cada FB. Blocos numerados (BL_VALVULA_5, BL_INVERSOR_6...)
        /// usam a versao MAIS ALTA da familia que existir no projeto. Devolve false se faltar algum.
        /// </summary>
        private bool ResolverFbs(PlcSoftware plc, IEnumerable<string> nominais)
        {
            var noProjeto = new List<string>();
            ColetarFbs(plc.BlockGroup, noProjeto);
            _fbs.Clear();

            if (noProjeto.Count == 0)
            {
                Log("AVISO: nao consegui listar os FBs do projeto; usando os nomes padrao.");
                return true;
            }

            foreach (string nominal in nominais.Distinct())
            {
                string real = null;
                Match m = Regex.Match(nominal, @"^(.*_)(\d+)$");
                if (m.Success)
                {
                    string familia = m.Groups[1].Value;
                    real = noProjeto
                        .Select(n => Regex.Match(n, "^" + Regex.Escape(familia) + @"(\d+)$"))
                        .Where(x => x.Success)
                        .OrderByDescending(x => int.Parse(x.Groups[1].Value))
                        .Select(x => x.Value)
                        .FirstOrDefault();
                }
                else if (noProjeto.Contains(nominal))
                {
                    real = nominal;
                }

                if (real == null)
                {
                    Log("O FB " + nominal + (m.Success ? " (nem outra versao " + m.Groups[1].Value + "N)" : "")
                        + " nao existe no projeto. Coloque o bloco no projeto e gere de novo.");
                    return false;
                }

                _fbs[nominal] = real;
                if (real != nominal) Log("FB do projeto: " + real + " (versao encontrada para " + nominal + ")");
            }
            return true;
        }

        private static void ColetarFbs(PlcBlockGroup grupo, List<string> destino)
        {
            foreach (PlcBlock b in grupo.Blocks)
            {
                if (b is FB) destino.Add(b.Name);
            }
            foreach (PlcBlockGroup sub in grupo.Groups)
                ColetarFbs(sub, destino);
        }

        /// <summary>Modelo de instancia de outra versao da mesma familia (ex.: BL_VALVULA_4 para BL_VALVULA_6).</summary>
        private static string AcharTemplateDaFamilia(string pastaExport, string fb, out string encontrado)
        {
            encontrado = null;
            Match m = Regex.Match(fb, @"^(.*_)\d+$");
            if (!m.Success) return null;

            var rx = new Regex("<InstanceOfName>(" + Regex.Escape(m.Groups[1].Value) + @"\d+)</InstanceOfName>");
            foreach (string f in Directory.GetFiles(pastaExport, "*.xml", SearchOption.AllDirectories))
            {
                string txt = File.ReadAllText(f, Encoding.UTF8);
                if (!txt.Contains("<SW.Blocks.InstanceDB")) continue;

                Match a = rx.Match(txt);
                if (a.Success)
                {
                    encontrado = a.Groups[1].Value;
                    return f;
                }
            }
            return null;
        }

        /// <summary>
        /// Modelo de DB de instancia de cada FB, nesta ordem:
        ///  1) um DB de instancia que ja exista no projeto aberto (mesma versao do FB);
        ///  2) a pasta de export;
        ///  3) a pasta de export, com o modelo de outra versao do mesmo bloco (troca so o nome do FB).
        /// Devolve null se faltar modelo de algum.
        /// </summary>
        private Dictionary<string, string> CarregarTemplates(PlcSoftware plc, List<string> fbs, string pastaExport)
        {
            var templates = new Dictionary<string, string>();

            foreach (string fb in fbs)
            {
                string doProjeto = TemplateDoProjeto(plc, fb);
                if (doProjeto != null)
                {
                    templates[fb] = doProjeto;
                    Log("Modelo de instancia (" + fb + "): do proprio projeto");
                    continue;
                }

                if (pastaExport != null)
                {
                    string arq = AcharTemplateInstancia(pastaExport, fb);
                    if (arq != null)
                    {
                        templates[fb] = File.ReadAllText(arq, Encoding.UTF8);
                        Log("Modelo de instancia (" + fb + "): pasta de export (" + Path.GetFileName(arq) + ")");
                        continue;
                    }

                }

                // o proprio FB do projeto (mesma versao): monta a instancia pela interface dele
                string doFbProjeto = TemplateDoFbDoProjeto(plc, fb);
                if (doFbProjeto != null)
                {
                    templates[fb] = doFbProjeto;
                    Log("Modelo de instancia (" + fb + "): montado a partir do FB do proprio projeto (ainda nao ha DB de instancia dele).");
                    continue;
                }

                if (pastaExport != null)
                {
                    string equivalente;
                    string arq = AcharTemplateDaFamilia(pastaExport, fb, out equivalente);
                    {
                        if (arq != null)
                        {
                            templates[fb] = File.ReadAllText(arq, Encoding.UTF8).Replace(
                                "<InstanceOfName>" + equivalente + "</InstanceOfName>",
                                "<InstanceOfName>" + fb + "</InstanceOfName>");
                            Log("AVISO: nao achei DB de instancia do " + fb + " no projeto nem no export;");
                            Log("       usei o modelo do " + equivalente + " (outra versao do mesmo bloco). Se o TIA reclamar do");
                            Log("       DB de instancia, crie uma chamada do " + fb + " a mao uma vez e gere de novo.");
                            continue;
                        }
                    }
                }

                if (pastaExport != null)
                {
                    string doFb = TemplateDoFb(pastaExport, fb);
                    if (doFb != null)
                    {
                        templates[fb] = doFb;
                        Log("Modelo de instancia (" + fb + "): montado a partir do proprio " + fb + " (export do FB),");
                        Log("       pois nao ha DB de instancia dele no projeto nem no export.");
                        continue;
                    }
                }

                PlcBlockGroup ondeFb;
                if (AcharBloco(plc.BlockGroup, fb, out ondeFb) == null)
                {
                    Log("ERRO: o " + fb + " nao esta na pasta Program blocks do PLC (nem instancia dele, nem na pasta de export).");
                    Log("      Se ele esta so na biblioteca, arraste para o PLC (Program blocks) e gere de novo.");
                }
                return null;
            }
            return templates;
        }

        /// <summary>
        /// Exporta o FB do projeto (numa pasta temporaria) e monta o DB de instancia pela interface dele.
        /// Serve para FB que esta no projeto mas ainda nao foi chamado (sem nenhum DB de instancia).
        /// </summary>
        private string TemplateDoFbDoProjeto(PlcSoftware plc, string fb)
        {
            PlcBlockGroup onde;
            PlcBlock bloco = AcharBloco(plc.BlockGroup, fb, out onde);
            if (bloco == null || !(bloco is FB)) return null;

            string pasta = Path.Combine(Path.GetTempPath(), "Genesis", "FB_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(pasta);
            try
            {
                // 1) exporta o FB e monta a instancia pela interface dele
                string erro = ExportarBloco(bloco, Path.Combine(pasta, "fb.xml"));
                if (erro != null)
                {
                    // 2) FB recem-copiado da biblioteca costuma estar inconsistente: compila e tenta de novo
                    Log("INFO: o TIA nao exportou o " + fb + " (" + erro + "); compilando o FB e tentando de novo...");
                    try { bloco.GetService<ICompilable>().Compile(); }
                    catch (Exception ex) { Log("      compilacao do " + fb + " falhou: " + DetalheErro(ex)); }
                    erro = ExportarBloco(bloco, Path.Combine(pasta, "fb.xml"));
                }
                if (erro == null)
                {
                    string t = TemplateDoFb(pasta, fb);
                    if (t != null) return t;
                }

                // 3) cria uma instancia temporaria pela API, exporta como modelo e apaga
                Log("INFO: criando uma instancia temporaria do " + fb + " para usar de modelo...");
                string nomeTmp = "FAST_MODELO_" + fb;
                InstanceDB tmp = null;
                try
                {
                    PlcBlock velho = plc.BlockGroup.Blocks.Find(nomeTmp);
                    if (velho != null) velho.Delete();
                    tmp = plc.BlockGroup.Blocks.CreateInstanceDB(nomeTmp, true, 1, fb);
                    string arq = Path.Combine(pasta, "inst.xml");
                    erro = ExportarBloco(tmp, arq);
                    if (erro == null) return File.ReadAllText(arq, Encoding.UTF8);
                    Log("AVISO: a instancia temporaria do " + fb + " tambem nao exportou (" + erro + ").");
                }
                catch (Exception ex)
                {
                    Log("AVISO: nao consegui criar a instancia temporaria do " + fb + " (" + DetalheErro(ex) + ").");
                }
                finally
                {
                    try { if (tmp != null) tmp.Delete(); } catch { /* fica para apagar a mao */ }
                }

                Log("ERRO: o " + fb + " esta no projeto, mas o TIA nao deixou exportar nem instanciar.");
                Log("      No TIA: abra o " + fb + ", compile (e veja se nao tem protecao de know-how) e gere de novo.");
                return null;
            }
            finally
            {
                try { Directory.Delete(pasta, true); } catch { /* pasta temporaria */ }
            }
        }

        /// <summary>Exporta um bloco; devolve null se deu certo ou o motivo do erro.</summary>
        private static string ExportarBloco(PlcBlock bloco, string arquivo)
        {
            try
            {
                if (File.Exists(arquivo)) File.Delete(arquivo);
                bloco.Export(new FileInfo(arquivo), ExportOptions.None);
                return null;
            }
            catch (Exception ex)
            {
                return DetalheErro(ex);
            }
        }

        /// <summary>Mensagem util de uma excecao do Openness (a de fora costuma ser generica; o motivo vem dentro).</summary>
        private static string DetalheErro(Exception ex)
        {
            // o TIA manda o motivo real nas linhas de baixo da mensagem (e nas excecoes internas)
            var partes = new List<string>();
            for (Exception e = ex; e != null; e = e.InnerException)
                foreach (string linha in (e.Message ?? "").Split('\n'))
                {
                    string m = linha.Trim();
                    if (m.Length > 0 && !partes.Contains(m)) partes.Add(m);
                }
            string r = string.Join(" | ", partes.ToArray());
            return r.Length > 900 ? r.Substring(0, 900) + "..." : r;
        }

        /// <summary>
        /// Ultimo recurso: monta o DB de instancia a partir do XML do proprio FB na pasta de export.
        /// A interface da instancia e a do FB sem Temp/Constant (e sem comentarios); o TIA completa no compilar.
        /// </summary>
        private static string TemplateDoFb(string pastaExport, string fb)
        {
            string marcaNome = "<Name>" + SecurityElement.Escape(fb) + "</Name>";
            foreach (string f in Directory.GetFiles(pastaExport, "*.xml", SearchOption.AllDirectories))
            {
                string txt = File.ReadAllText(f, Encoding.UTF8);
                if (!txt.Contains("<SW.Blocks.FB ") || !txt.Contains(marcaNome)) continue;

                int i = txt.IndexOf("<Interface>", StringComparison.Ordinal);
                int j = txt.IndexOf("</Interface>", StringComparison.Ordinal);
                if (i < 0 || j < i) continue;

                string itf = txt.Substring(i, j + "</Interface>".Length - i);
                // Temp e Constant sao sempre as ultimas secoes: corta de Temp ate o fim das secoes
                int iTemp = itf.IndexOf("<Section Name=\"Temp\"", StringComparison.Ordinal);
                int fim = itf.LastIndexOf("</Sections>", StringComparison.Ordinal);
                if (iTemp > 0 && fim > iTemp) itf = itf.Remove(iTemp, fim - iTemp);
                itf = Regex.Replace(itf, @"\s*<Comment>.*?</Comment>", "", RegexOptions.Singleline);

                var sb = new StringBuilder();
                sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
                sb.AppendLine("<Document>");
                sb.AppendLine("  <Engineering version=\"V20\" />");
                sb.AppendLine("  <SW.Blocks.InstanceDB ID=\"0\">");
                sb.AppendLine("    <AttributeList>");
                sb.AppendLine("      <AutoNumber>true</AutoNumber>");
                sb.AppendLine("      <InstanceOfName>" + SecurityElement.Escape(fb) + "</InstanceOfName>");
                sb.AppendLine("      <InstanceOfType>FB</InstanceOfType>");
                sb.AppendLine("      " + itf);
                sb.AppendLine("      <Name>" + SecurityElement.Escape(fb) + "_MODELO</Name>");
                sb.AppendLine("      <Namespace />");
                sb.AppendLine("      <ProgrammingLanguage>DB</ProgrammingLanguage>");
                sb.AppendLine("    </AttributeList>");
                sb.AppendLine("  </SW.Blocks.InstanceDB>");
                sb.AppendLine("</Document>");
                return sb.ToString();
            }
            return null;
        }

        /// <summary>Exporta um DB de instancia do FB que ja exista no projeto (o primeiro que der certo).</summary>
        private string TemplateDoProjeto(PlcSoftware plc, string fb)
        {
            string pasta = Path.Combine(Path.GetTempPath(), "Genesis");
            string arq = Path.Combine(pasta, "modelo_" + fb + ".xml");

            foreach (InstanceDB idb in InstanciasDe(plc.BlockGroup, fb))
            {
                try
                {
                    Directory.CreateDirectory(pasta);
                    if (File.Exists(arq)) File.Delete(arq);

                    idb.GetService<ICompilable>().Compile();   // o TIA so exporta bloco consistente
                    idb.Export(new FileInfo(arq), ExportOptions.WithDefaults);

                    string txt = File.ReadAllText(arq, Encoding.UTF8);
                    File.Delete(arq);
                    return txt;
                }
                catch
                {
                    // essa instancia nao deu: tenta a proxima
                }
            }
            return null;
        }

        private static IEnumerable<InstanceDB> InstanciasDe(PlcBlockGroup grupo, string fb)
        {
            foreach (PlcBlock b in grupo.Blocks)
            {
                InstanceDB idb = b as InstanceDB;
                if (idb == null) continue;

                string deQue = null;
                try { deQue = idb.InstanceOfName; }
                catch { /* ignora */ }

                if (deQue == fb) yield return idb;
            }

            foreach (PlcBlockGroup sub in grupo.Groups)
                foreach (InstanceDB x in InstanciasDe(sub, fb))
                    yield return x;
        }

        /// <summary>
        /// Procura a pasta de templates (export do TIA), do mais perto para o mais longe:
        /// a pasta do programa e seus ancestrais (cobre a pasta do projeto) e, por fim,
        /// a Area de Trabalho. Em cada lugar, aceita:
        ///   Templates\FAST_Export_*   (usa a mais recente)
        ///   Templates                 (pasta enxuta so com os XMLs)
        ///   FAST_Export_*             (usa a mais recente)
        /// </summary>
        private static string AcharPastaExport(string desktop)
        {
            var bases = new List<string>();
            try
            {
                DirectoryInfo d = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
                for (int i = 0; i < 7 && d != null; i++)
                {
                    bases.Add(d.FullName);
                    d = d.Parent;
                }
            }
            catch
            {
                // sem acesso: segue so com a Area de Trabalho
            }
            bases.Add(desktop);

            foreach (string b in bases)
            {
                string templates = Path.Combine(b, "Templates");

                string achada = ExportMaisRecente(templates);
                if (achada != null) return achada;

                if (TemXml(templates)) return templates;

                achada = ExportMaisRecente(b);
                if (achada != null) return achada;
            }
            return null;
        }

        private static string ExportMaisRecente(string raiz)
        {
            try
            {
                if (!Directory.Exists(raiz)) return null;
                string[] dirs = Directory.GetDirectories(raiz, "FAST_Export_*");
                if (dirs.Length == 0) return null;
                Array.Sort(dirs);
                return dirs[dirs.Length - 1];
            }
            catch
            {
                return null;
            }
        }

        private static bool TemXml(string pasta)
        {
            try
            {
                return Directory.Exists(pasta)
                    && Directory.GetFiles(pasta, "*.xml", SearchOption.AllDirectories).Length > 0;
            }
            catch
            {
                return false;
            }
        }

        private static string AcharTemplateInstancia(string pastaExport, string nomeFb)
        {
            string marca = "<InstanceOfName>" + nomeFb + "</InstanceOfName>";
            foreach (string f in Directory.GetFiles(pastaExport, "*.xml", SearchOption.AllDirectories))
            {
                string txt = File.ReadAllText(f, Encoding.UTF8);
                if (txt.Contains("<SW.Blocks.InstanceDB") && txt.Contains(marca))
                    return f;
            }
            return null;
        }

        private static bool ExisteBloco(PlcBlockGroup grupo, string nome)
        {
            if (grupo.Blocks.Find(nome) != null) return true;
            foreach (PlcBlock b in grupo.Blocks)
            {
                if (string.Equals(b.Name, nome, StringComparison.OrdinalIgnoreCase)) return true;
            }
            foreach (PlcBlockGroup sub in grupo.Groups)
            {
                if (ExisteBloco(sub, nome)) return true;
            }
            return false;
        }

        private static string PrimeiraLinha(Exception ex)
        {
            string msg = ex.Message ?? "";
            int i = msg.IndexOf('\n');
            return i >= 0 ? msg.Substring(0, i).Trim() : msg.Trim();
        }

        // ===================================================================
        //  Localizacao do PLC
        // ===================================================================
        private static PlcSoftware EncontrarPlc(Project projeto)
        {
            foreach (Device dispositivo in projeto.Devices)
            {
                PlcSoftware plc = EncontrarPlc(dispositivo.DeviceItems);
                if (plc != null) return plc;
            }
            return null;
        }

        private static PlcSoftware EncontrarPlc(DeviceItemComposition itens)
        {
            foreach (DeviceItem item in itens)
            {
                try
                {
                    SoftwareContainer container = item.GetService<SoftwareContainer>();
                    if (container != null)
                    {
                        PlcSoftware plc = container.Software as PlcSoftware;
                        if (plc != null) return plc;
                    }
                }
                catch
                {
                    // Continua procurando nos demais itens.
                }

                PlcSoftware filho = EncontrarPlc(item.DeviceItems);
                if (filho != null) return filho;
            }
            return null;
        }
    }
}