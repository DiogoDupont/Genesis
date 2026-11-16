using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Genesis
{
    // =======================================================================
    //  Maquinas: conjuntos de partidas e sensores que trabalham juntos.
    //  Cada maquina do catalogo vira linhas normais (Partidas e Sensores), marcadas com
    //  Maquina (id = DB da maquina, ex.: PP1) e Papel (ROS, AG1, VAZ...). Na geracao,
    //  o nucleo (SIS.AUT) gera o bloco da maquina e o automatico; as partes ganham o
    //  intertravamento que vem do bloco.
    // =======================================================================
    public class OpcoesPreparador
    {
        public string Db = "PP1";
        public string Descricao = "PREPARADOR DE POLÍMERO 1";
        public int Agitadores = 2;
        public bool NivelAnalogico;
        public bool NivelPolimero = true;
        public string TipoRosca = "Partida inversor por rede";
        public string TipoAgitador = "Partida direta";
    }

    /// <summary>Um quimico do flotador (bomba(s) de dosagem, agitador do tanque, nivel e modulacao).</summary>
    public class QuimicoFlotador
    {
        public string Codigo;          // ALC, FLC, COA, ANT, ACI, DES
        public string Nome;            // ALCALINIZANTE...
        public string DescricaoBomba = "";
        public bool Incluir;
        public int Bombas = 1;         // 1 a 3
        public string TipoBomba = "Partida diafragma";
        public bool Agitador;
        public string TipoAgitador = "Partida direta";
        public string Nivel = NivelDigital;   // nivel baixo do tanque
        public string Modulacao = ModNenhuma;
        public bool Alternancia;       // com 2 ou mais bombas: alternam em pares
        public bool PodePid;           // acido e alcalinizante: PID de pH

        public const string NivelDigital = "Nível baixo digital", NivelAnalogico = "Nível analógico", NivelNenhum = "Sem nível";
        public const string NivelDoPreparador = "Do preparador ";   // + id: usa o nivel do tanque do preparador (ex.: "Do preparador PP1")
        public const string ModNenhuma = "Sem modulação", ModVazao = "Pela vazão", ModPid = "PID de pH";
    }

    public class OpcoesFlotador
    {
        public string Sub = "FL1";
        public string Descricao = "FLOTADOR PRIMÁRIO";

        public bool AgitEqualizacao = true;   public string TipoAgitEqu = "Partida inversor por rede";
        public string NivelEqualizacao = QuimicoFlotador.NivelAnalogico;
        public bool Alimentacao = true;       public string TipoAlim = "Partida direta";
        public int QtdAlim = 1;               public bool AltAlim;
        public bool Microbolhas = true;       public string TipoMic = "Partida soft-starter com bypass";
        public int QtdMic = 1;                public bool AltMic;
        public bool Raspador = true;          public string TipoRas = "Partida direta";
        public bool SensoresPas = true;
        public string RaspadorIntermitente = "Minutos", RaspadorIntermitenteDes = "";   // unidade do tempo ligado / desligado: "", "Segundos", "Minutos" ou "Horas" (ligado "" = sem intermitente; desligado "" = igual ao ligado)
        public bool RaspadorCircular;         public string TipoRasCircular = "Partida direta";
        public int Floculadores = 2;          public string TipoFloculador = "Partida inversor por rede";
        public string FloculadorIntermitente = "Minutos", FloculadorIntermitenteDes = "";   // idem
        public bool BombaLodo = true;         public string TipoBombaLodo = "Partida inversor por rede";
        public int QtdLodo = 1;               public bool AltLodo;
        public bool AgitLodo = true;          public string TipoAgitLodo = "Partida direta";
        public string NivelLodo = QuimicoFlotador.NivelAnalogico;
        public bool Clarificado;              public string TipoClarificado = "Partida direta";
        public int QtdCla = 1;                public bool AltCla;
        public bool MedidorVazao = true;
        public string PreparadorFloculante = "";   // (antigo) hoje o nivel do floculante e escolhido na tabela de quimicos

        public List<QuimicoFlotador> Quimicos = new List<QuimicoFlotador>();

        public OpcoesFlotador()
        {
            Quimicos = CatalogoMaquinas.QuimicosPadrao();
        }
    }

    /// <summary>O que muda numa maquina preparador quando ela e reconfigurada (nada e alterado ate aplicar).</summary>
    internal class PlanoPreparador
    {
        public string DbAntigo, DbNovo;
        public OpcoesPreparador Antigas, Novas;
        public List<KeyValuePair<Partida, Partida>> Mantidas = new List<KeyValuePair<Partida, Partida>>();   // (linha atual, linha gerada pelas opcoes novas)
        public List<Partida> Novos = new List<Partida>();       // linhas que passam a existir
        public List<Partida> Removidas = new List<Partida>();   // linhas atuais que deixam de existir
        public List<Partida> DescricaoPadraoAntiga = new List<Partida>();   // (uso interno) linhas geradas com as opcoes antigas
    }

    internal static partial class Maquinas
    {
        public const string Flotador = "Flotador";

        /// <summary>Insere o numero da unidade logo apos o tipo do equipamento.</summary>
        internal static string NumerarEquipamento(string descricao, int numero)
        {
            string[] partes = (descricao ?? "").Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (partes.Length == 0) return descricao ?? "";

            string tipo = partes[0];
            switch (tipo.ToUpperInvariant())
            {
                case "BOMBAS": tipo = "BOMBA"; break;
                case "SOPRADORES": tipo = "SOPRADOR"; break;
                case "AGITADORES": tipo = "AGITADOR"; break;
                case "MOTORES": tipo = "MOTOR"; break;
                case "VÁLVULAS": tipo = "VÁLVULA"; break;
                case "VALVULAS": tipo = "VALVULA"; break;
                case "MISTURADORES": tipo = "MISTURADOR"; break;
                case "EXAUSTORES": tipo = "EXAUSTOR"; break;
                case "VENTILADORES": tipo = "VENTILADOR"; break;
            }

            int inicioRestante = 1;
            int numeroExistente;
            if (partes.Length > 1 && int.TryParse(partes[1], out numeroExistente))
                inicioRestante = 2;

            string restante = inicioRestante < partes.Length
                ? " " + string.Join(" ", partes.Skip(inicioRestante).ToArray())
                : "";
            return tipo + " " + numero + restante;
        }

        /// <summary>
        /// Subsistema flotador (como o FLOTADOR PRIMARIO): partidas, sensores e PIDs com os intertravamentos
        /// e modulacoes ja ligados. Sensores de tanques de quimicos que ja existirem no projeto sao reaproveitados.
        /// </summary>
                public static List<Partida> CriarFlotador(OpcoesFlotador o, IEnumerable<Partida> existentes)
        {
            string sub = o.Sub.Trim().ToUpperInvariant();
            var l = new List<Partida>();
            var jaHa = existentes.ToList();
            Func<string, string, string, bool> existe = delegate (string s, string e, string f)
            {
                return jaHa.Concat(l).Any(x => x.Sub == s && x.Eq == e && x.Fn == f);
            };
            Func<string, string, string, string, string, Partida> nova = delegate (string s, string e, string f, string tipo, string desc)
            {
                var p = new Partida { Sub = s, Eq = e, Fn = f, Tipo = tipo, Maquina = sub, Receita = Flotador, Descricao = desc, Auto = true };
                l.Add(p);
                return p;
            };
            Func<string, string, string, string, string, Partida> sensor = delegate (string s, string e, string f, string tipo, string desc)
            {
                if (existe(s, e, f)) return null;   // tanque de quimico compartilhado: ja existe
                var p = nova(s, e, f, tipo, desc);
                p.Auto = false;
                return p;
            };
            Func<string, string> nivelTipo = delegate (string n)
            {
                return n == QuimicoFlotador.NivelAnalogico ? "Sensor nível analógico" : "Sensor nível baixo digital";
            };
            
            // NOVO: Agora usa a geração inteligente TBA/TB1/TB2/TAN/TN1...
            Func<string, string, string> nivelFn = delegate (string eq, string n) 
            { 
                string tipo = n == QuimicoFlotador.NivelAnalogico ? "Sensor nível analógico" : "Sensor nível baixo digital";
                string tag = GerarTagNivel(tipo, eq, jaHa.Concat(l));
                return tag.Replace("SEN_" + eq + "_", ""); // Retorna só o final (TAN, TBA, TB1, etc)
            };

            // 1 bomba = BBA; varias = BB1, BB2, BB3 (com alternancia, se marcada)
            Func<string, int, string, string, bool, List<Partida>> bombas = delegate (string eq, int qtd, string tipo, string desc, bool alt)
            {
                int n = Math.Max(1, Math.Min(3, qtd));
                var lst = new List<Partida>();
                for (int i = 1; i <= n; i++)
                {
                    string descricaoBomba = n == 1 ? desc : NumerarEquipamento(desc, i);
                    Partida b = nova(sub, eq, n == 1 ? "BBA" : "BB" + i, tipo, descricaoBomba);
                    b.Alternancia = alt && n > 1;
                    lst.Add(b);
                }
                return lst;
            };

            // --- sensores do proprio flotador ---
            string vaz = null;
            bool moduladoPorVazao = o.Quimicos.Any(q => q.Incluir && q.Modulacao == QuimicoFlotador.ModVazao);
            if (o.MedidorVazao)
            {
                sensor("SEN", sub, "VAZ", moduladoPorVazao ? "Medidor de vazão com modulação de químicos" : "Medidor de vazão",
                    "MEDIDOR VAZÃO " + o.Descricao);
                vaz = "SEN." + sub + ".VAZ";
            }
            string nivEqu = null;
            if (o.NivelEqualizacao != QuimicoFlotador.NivelNenhum)
            {
                sensor("SEN", "EQU", nivelFn("EQU", o.NivelEqualizacao), nivelTipo(o.NivelEqualizacao), "NÍVEL TANQUE EQUALIZAÇÃO");
                nivEqu = "SEN.EQU." + nivelFn("EQU", o.NivelEqualizacao);
            }
            string nivLodo = null;
            if (o.NivelLodo != QuimicoFlotador.NivelNenhum && (o.BombaLodo || o.Raspador))
            {
                sensor("SEN", "LFL", nivelFn("LFL", o.NivelLodo), nivelTipo(o.NivelLodo), "NÍVEL TANQUE LODO FLOTADO");
                nivLodo = "SEN.LFL." + nivelFn("LFL", o.NivelLodo);
            }

            // --- equipamentos ---
            if (o.AgitEqualizacao) nova(sub, "EQU", "AGT", o.TipoAgitEqu, "AGITADOR TANQUE EQUALIZAÇÃO");

            for (int i = 1; i <= Math.Max(0, Math.Min(2, o.Floculadores)); i++)
            {
                Partida f = nova(sub, "FME", "AG" + i, o.TipoFloculador, "AGITADOR " + i + " FLOCULADOR MECÂNICO");
                f.Intermitente = o.FloculadorIntermitente ?? ""; f.IntermitenteDes = string.IsNullOrEmpty(f.Intermitente) ? "" : (o.FloculadorIntermitenteDes ?? "");
            }

            string alimentacaoDesligada = null;   // condicao "nenhuma bomba de alimentacao ligada"
            if (o.Alimentacao)
            {
                List<Partida> alim = bombas("ALM", o.QtdAlim, o.TipoAlim, "BOMBA ALIMENTAÇÃO " + o.Descricao, o.AltAlim);
                foreach (Partida a in alim)
                    if (nivEqu != null)
                    {
                        a.Intertravamento = true;
                        a.CondicoesInt = nivEqu + ".LEI_BAI";   // nivel baixo da equalizacao
                    }
                alimentacaoDesligada = string.Join(" E ", alim.Select(a => "NÃO " + sub + ".ALM." + a.Fn + ".LEI_HAB").ToArray());
            }

            if (o.Raspador)
            {
                Partida r = nova(sub, "RAS", "MOT", o.TipoRas, "RASPADOR " + o.Descricao);
                r.Intermitente = o.RaspadorIntermitente ?? ""; r.IntermitenteDes = string.IsNullOrEmpty(r.Intermitente) ? "" : (o.RaspadorIntermitenteDes ?? "");
                if (nivLodo != null)
                {
                    r.Intertravamento = true;
                    r.CondicoesInt = nivLodo + ".LEI_ALT";   // tanque de lodo cheio
                }
                if (o.SensoresPas)
                {
                    sensor("SEN", sub, "RAS", "Sensor pás raspador", "SENSORES PÁS RASPADOR " + o.Descricao);
                    r.DefeitosExtras = "SEN." + sub + ".RAS.LEI_DED\nSEN." + sub + ".RAS.LEI_DEE";
                }
            }
            if (o.RaspadorCircular) nova(sub, "RAC", "MOT", o.TipoRasCircular, "RASPADOR CIRCULAR " + o.Descricao);

            if (o.Microbolhas) bombas("MIC", o.QtdMic, o.TipoMic, "BOMBAS MICROBOLHAS " + o.Descricao, o.AltMic);

            if (o.BombaLodo)
            {
                foreach (Partida b in bombas("LFL", o.QtdLodo, o.TipoBombaLodo, "BOMBA LODO FLOTADO", o.AltLodo))
                    if (nivLodo != null)
                    {
                        b.Intertravamento = true;
                        b.CondicoesInt = nivLodo + ".LEI_BAI";   // tanque de lodo vazio
                    }
            }
            if (o.AgitLodo) nova(sub, "LFL", "AGT", o.TipoAgitLodo, "AGITADOR TANQUE LODO FLOTADO");

            // --- quimicos ---
            string ph = null;
            foreach (QuimicoFlotador q in o.Quimicos.Where(x => x.Incluir))
            {
                // nivel baixo do tanque (o floculante pode vir do preparador de polimero)
                string nivel = null;
                string pp = q.Nivel.StartsWith(QuimicoFlotador.NivelDoPreparador)
                    ? q.Nivel.Substring(QuimicoFlotador.NivelDoPreparador.Length).Trim()
                    : (q.Codigo == "FLC" && !string.IsNullOrWhiteSpace(o.PreparadorFloculante) ? o.PreparadorFloculante.Trim() : null);
                if (pp != null)
                {
                    // nivel baixo do tanque do preparador (digital TBA ou analogico TAN)
                    pp = pp.ToUpperInvariant();
                    bool ana = jaHa.Any(x => x.Sub == "SEN" && x.Eq == pp && x.Fn == "TAN");
                    nivel = "SEN." + pp + (ana ? ".TAN" : ".TBA") + ".LEI_BAI";
                }
                else if (q.Nivel != QuimicoFlotador.NivelNenhum)
                {
                    sensor("SEN", q.Codigo, nivelFn(q.Codigo, q.Nivel), nivelTipo(q.Nivel), "NÍVEL TANQUE " + q.Nome);
                    nivel = "SEN." + q.Codigo + "." + nivelFn(q.Codigo, q.Nivel) + ".LEI_BAI";
                }

                // PID de pH (acido / alcalinizante)
                string refPid = null;
                if (q.PodePid && q.Modulacao == QuimicoFlotador.ModPid)
                {
                    if (ph == null)
                    {
                        sensor("SEN", sub, "PHI", "Sensor analítico", "MEDIDOR PH " + o.Descricao);
                        ph = "SEN." + sub + ".PHI";
                    }
                    Partida pid = nova(sub, q.Codigo, "PID", "PID", "PID PH " + q.Nome);
                    pid.Auto = false;
                    pid.TagSensor = ph;
                    pid.PidInverso = q.Codigo == "ACI";   // acido: aumentar a saida baixa o pH -> acao inversa
                    refPid = sub + "." + q.Codigo + ".PID";
                }

                // intertravamento das bombas: nivel baixo OU vazao baixa OU alimentacao desligada
                var conds = new List<string>();
                if (nivel != null) conds.Add(nivel);
                if (vaz != null) conds.Add(vaz + ".LEI_BAI");
                if (alimentacaoDesligada != null) conds.Add(alimentacaoDesligada);

                int n = Math.Max(1, Math.Min(3, q.Bombas));
                for (int i = 1; i <= n; i++)
                {
                    string descricaoBase = string.IsNullOrWhiteSpace(q.DescricaoBomba) ? "BOMBA " + q.Nome : q.DescricaoBomba.Replace("{NOME}", q.Nome).Replace("{DESC}", o.Descricao);
                    string descricaoBombaQuimico = n == 1 ? descricaoBase : NumerarEquipamento(descricaoBase, i);
                    Partida b = nova(sub, q.Codigo, n == 1 ? "BBA" : "BB" + i, q.TipoBomba,
                        descricaoBombaQuimico);
                    b.Intertravamento = conds.Count > 0;
                    b.Alternancia = q.Alternancia && n > 1;
                    b.CondicoesInt = string.Join("\n", conds.ToArray());
                    if (q.Modulacao == QuimicoFlotador.ModVazao && vaz != null) b.ModVazao = vaz;
                    if (refPid != null) b.ModPid = refPid;
                }
                if (q.Agitador) nova(sub, q.Codigo, "AGT", q.TipoAgitador, "AGITADOR TANQUE " + q.Nome);
            }

            if (o.Clarificado) bombas("CLA", o.QtdCla, o.TipoClarificado, "BOMBA CLARIFICADO", o.AltCla);
            return l;
        }

        public const string Preparador = "Preparador de polímero";

        // papeis do preparador
        public const string PapelSis = "SIS", PapelRosca = "ROS", PapelAg1 = "AG1", PapelAg2 = "AG2", PapelAgua = "EAG";
        public const string PapelVazao = "VAZ", PapelNivBaixo = "NBA", PapelNivAlto = "NAL", PapelNivAna = "NAN", PapelNivPolimero = "NPO";

        public const string TipoSisPreparador = "Preparador de polímero (sistema)";
        public const string TipoAguaPreparador = "Válvula Alimentação Preparador";
        public const string TipoAguaPreparadorAntigo = "Válvula de água do preparador";   // nome antigo (arquivos salvos)

        /// <summary>Linhas (partidas e sensores) de um preparador de polimero, com a nomenclatura padrao.</summary>
        public static List<Partida> CriarPreparador(OpcoesPreparador o)
        {
            string db = o.Db.Trim().ToUpperInvariant();
            string d = (o.Descricao ?? "").Trim();
            var l = new List<Partida>();
            Func<string, string, string, string, string, string, Partida> nova =
                delegate (string sub, string eq, string fn, string tipo, string papel, string desc)
                {
                    var p = new Partida { Sub = sub, Eq = eq, Fn = fn, Tipo = tipo, Maquina = db, Papel = papel, Receita = Preparador,
                        Descricao = (d + " - " + desc).Trim(' ', '-') };
                    l.Add(p);
                    return p;
                };

            nova(db, "SIS", "AUT", TipoSisPreparador, PapelSis, "AUTOMÁTICO");
            nova(db, "PPF", "ROS", o.TipoRosca, PapelRosca, "ROSCA");
            nova(db, "PPF", "AG1", o.TipoAgitador, PapelAg1, o.Agitadores > 1 ? "AGITADOR 1" : "AGITADOR");
            if (o.Agitadores > 1) nova(db, "PPF", "AG2", o.TipoAgitador, PapelAg2, "AGITADOR 2");
            nova(db, "PPF", "EAG", "Válvula 0DI e 1DO", PapelAgua, "VÁLVULA ALIMENTAÇÃO PREPARADOR");

            nova("SEN", db, "VAZ", "Medidor de vazão", PapelVazao, "MEDIDOR DE VAZÃO");
            if (o.NivelAnalogico)
            {
                nova("SEN", db, "TAN", "Sensor nível analógico", PapelNivAna, "NÍVEL TANQUE");
            }
            else
            {
                nova("SEN", db, "TBA", "Sensor nível baixo digital", PapelNivBaixo, "NÍVEL BAIXO TANQUE");
                nova("SEN", db, "TAL", "Sensor nível alto digital", PapelNivAlto, "NÍVEL ALTO TANQUE");
            }
            if (o.NivelPolimero) nova("SEN", db, "NPO", "Sensor nível baixo digital", PapelNivPolimero, "NÍVEL BAIXO POLÍMERO");

            // partes nao tem automatico/intertravamento proprios: vem do bloco da maquina
            foreach (Partida p in l) { p.Auto = false; p.Intertravamento = false; p.Alternancia = false; }
            return l;
        }

        // --- reconfigurar um preparador ja criado -------------------------------------

        /// <summary>Le as opcoes atuais de um preparador a partir das linhas dele (a janela de criacao abre com elas).</summary>
        public static OpcoesPreparador OpcoesDoPreparador(IEnumerable<Partida> itens, string id)
        {
            var l = itens.Where(x => string.Equals(x.Maquina, id, StringComparison.OrdinalIgnoreCase)).ToList();
            Func<string, Partida> por = papel => l.FirstOrDefault(x => x.Papel == papel);
            var o = new OpcoesPreparador { Db = id };
            Partida sis = por(PapelSis), ros = por(PapelRosca), ag1 = por(PapelAg1);
            if (sis != null && !string.IsNullOrWhiteSpace(sis.Descricao))
                o.Descricao = Regex.Replace(sis.Descricao, @"\s*-\s*AUTOM[ÁA]TICO\s*$", "").Trim();
            if (ros != null && !string.IsNullOrEmpty(ros.Tipo)) o.TipoRosca = ros.Tipo;
            if (ag1 != null && !string.IsNullOrEmpty(ag1.Tipo)) o.TipoAgitador = ag1.Tipo;
            o.Agitadores = por(PapelAg2) != null ? 2 : 1;
            o.NivelAnalogico = por(PapelNivAna) != null;
            o.NivelPolimero = por(PapelNivPolimero) != null;
            return o;
        }

        /// <summary>
        /// Compara a maquina atual com as opcoes novas: o que continua (casando pelo papel), o que e novo e o que sai.
        /// Itens colocados a mao na maquina (sem papel) nao entram: ficam como estao.
        /// </summary>
        public static PlanoPreparador PlanejarPreparador(string id, IEnumerable<Partida> itens, OpcoesPreparador novas)
        {
            var plano = new PlanoPreparador { DbAntigo = id, Antigas = OpcoesDoPreparador(itens, id), Novas = novas };
            novas.Db = novas.Db.Trim().ToUpperInvariant();
            plano.DbNovo = novas.Db;
            List<Partida> geradas = CriarPreparador(novas);
            plano.DescricaoPadraoAntiga = CriarPreparador(plano.Antigas);
            foreach (Partida n in geradas)
            {
                Partida atual = itens.FirstOrDefault(x => !string.IsNullOrEmpty(x.Papel) && x.Papel == n.Papel);
                if (atual != null) plano.Mantidas.Add(new KeyValuePair<Partida, Partida>(atual, n));
                else plano.Novos.Add(n);
            }
            plano.Removidas = itens.Where(x => !string.IsNullOrEmpty(x.Papel) && !geradas.Any(n => n.Papel == x.Papel)).ToList();
            return plano;
        }

        /// <summary>Aplica nas linhas que continuam o que veio da janela: tipo de acionamento e descricoes que ainda estao no padrao.</summary>
        public static void AplicarNasMantidas(PlanoPreparador plano)
        {
            foreach (var par in plano.Mantidas)
            {
                Partida a = par.Key, n = par.Value;
                Partida padrao = plano.DescricaoPadraoAntiga.FirstOrDefault(x => x.Papel == a.Papel);
                if (padrao != null && string.Equals(a.Descricao, padrao.Descricao, StringComparison.Ordinal)) a.Descricao = n.Descricao;
                if (a.Papel == PapelRosca || a.Papel == PapelAg1 || a.Papel == PapelAg2) a.Tipo = n.Tipo;
            }
        }

        /// <summary>
        /// Troca o DB da maquina em todas as linhas (DB, grupo dos sensores, maquina, intertravamentos, modulacoes, tags...).
        /// So troca a palavra inteira (PP1 ou PP1_..., nunca PP10). As tags da aba Tags que tinham o nome antigo
        /// seguem o novo nome, mantendo endereco e descricao. Devolve quantos campos mudaram.
        /// </summary>
        public static int RenomearDb(IEnumerable<Partida> todas, string antigo, string novo, IList<Io> ios)
        {
            var rx = new Regex("(?<![A-Za-z0-9])" + Regex.Escape(antigo) + "(?![A-Za-z0-9])", RegexOptions.IgnoreCase);
            string[] pular = { "Descricao", "Receita", "Papel", "Tipo", "Aba", "Categoria" };
            int n = 0;
            foreach (Partida p in todas)
            {
                foreach (PropertyInfo pi in typeof(Partida).GetProperties())
                {
                    if (pi.PropertyType != typeof(string) || !pi.CanRead || !pi.CanWrite || pi.GetIndexParameters().Length > 0) continue;
                    if (Array.IndexOf(pular, pi.Name) >= 0) continue;
                    string v = pi.GetValue(p, null) as string;
                    if (string.IsNullOrEmpty(v) || !rx.IsMatch(v)) continue;
                    string nv = rx.Replace(v, novo);
                    // a tag da aba Tags que tinha o nome antigo acompanha (endereco e descricao ficam)
                    if (ios != null && pi.Name.StartsWith("Tag", StringComparison.Ordinal))
                    {
                        Io io = ios.FirstOrDefault(x => string.Equals(x.Nome, v, StringComparison.OrdinalIgnoreCase));
                        if (io != null && !ios.Any(x => string.Equals(x.Nome, nv, StringComparison.OrdinalIgnoreCase))) io.Nome = nv;
                    }
                    pi.SetValue(p, nv, null);
                    n++;
                }
            }
            return n;
        }

        /// <summary>Linha de um preparador de polimero (projetos antigos: maquina sem receita = preparador).</summary>
        public static bool EhPreparador(Partida p)
        {
            // (item avulso colocado dentro de uma maquina nao tem papel: continua uma partida normal)
            return p != null && !string.IsNullOrEmpty(p.Maquina)
                && (p.Receita == Preparador || (string.IsNullOrEmpty(p.Receita) && !string.IsNullOrEmpty(p.Papel)));
        }

        /// <summary>Item de uma maquina pelo papel (null se nao houver).</summary>
        public static Partida Parte(IEnumerable<Partida> todas, string maquina, string papel)
        {
            return todas.FirstOrDefault(x => string.Equals(x.Maquina, maquina, StringComparison.OrdinalIgnoreCase) && x.Papel == papel);
        }

        /// <summary>Partida que faz parte de uma maquina e usa o gerador normal (rosca, agitadores).</summary>
        public static bool EhParte(Partida p)
        {
            if (!EhPreparador(p)) return false;
            if (p.Papel == PapelRosca || p.Papel == PapelAg1 || p.Papel == PapelAg2) return true;
            // valvula de agua como BL_VALVULA (o tipo antigo, com BL_PARTIDA_HIBRIDA, tem rede propria)
            return p.Papel == PapelAgua && p.Tipo != TipoAguaPreparador;
        }

        /// <summary>Saida do bloco da maquina que habilita cada parte (o intertravamento e a falta dela).</summary>
        public static string HabilitaDe(string papel)
        {
            switch (papel)
            {
                case PapelRosca: return "LEI_ROS";
                case PapelAg1: return "LEI_AG1";
                case PapelAg2: return "LEI_AG2";
                case PapelAgua: return "LEI_EAG";
                default: return null;
            }
        }

                public static List<string> MaquinasNoProjeto(IEnumerable<Partida> todas)
        {
            return todas.Where(x => !string.IsNullOrEmpty(x.Maquina) || !string.IsNullOrEmpty(x.Subsistema))
                        .Select(x => !string.IsNullOrEmpty(x.Maquina) ? x.Maquina : x.Subsistema)
                        .Distinct().ToList();
        }
    }

    // =======================================================================
    //  Geracao das redes especiais do preparador
    // =======================================================================
    internal partial class Gerador
    {
        // pinos do bloco da maquina -> membro do SIS.AUT (ou do sensor), como no PREPARADOR PPF 1
        private static readonly string[][] PinosPreparadorSaida =
        {
            new[] {"ROS_LEI_HAB","LEI_ROS"}, new[] {"AGT_LEI_HA1","LEI_AG1"}, new[] {"AGT_LEI_HA2","LEI_AG2"},
            new[] {"EAG_LEI_HAB","LEI_EAG"}, new[] {"BAT_LEI_FIM","LEI_FIM"}, new[] {"AGT_LEI_TP1","LEI_TP1"},
            new[] {"AGT_LEI_TP2","LEI_TP2"}, new[] {"ROS_LEI_TPO","LEI_TPO"}, new[] {"ROS_CAL_TPO","LEI_CTP"},
            new[] {"PPF_LEI_LTR","LEI_LTR"}
        };

        private static readonly string[][] PinosPreparadorSet =
        {
            new[] {"PPF_SET_CON","SET_CON"}, new[] {"PPF_SET_TAR","SET_TAR"}, new[] {"PPF_SET_LTR","SET_LTR"},
            new[] {"AGT_SET_TP1","SET_TP1"}, new[] {"AGT_SET_TP2","SET_TP2"}
        };

        private List<Partida> _todas = new List<Partida>();                 // todas as linhas (sensores de outro DB)
        private Dictionary<string, string> _templatesFb = new Dictionary<string, string>();   // modelos de instancia (pinos)

        /// <summary>Simbolo de um membro de uma parte da maquina (ex.: "PP1".PPF.ROS.CMD_LIG ou "SEN".PP1.VAZ.LEI_TOT).</summary>
        private string SimboloParte(Partida parte, string membro)
        {
            return Cfg.Prefixo + parte.Sub + "." + parte.Eq + "." + parte.Fn + "." + membro;
        }

        /// <summary>Partes que recebem comando do automatico da maquina (motores e a valvula), na ordem.</summary>
        private List<Partida> PartesComandadas(string maq)
        {
            return new[] { Maquinas.PapelAg1, Maquinas.PapelAg2, Maquinas.PapelAgua, Maquinas.PapelRosca }
                .Select(pp => Maquinas.Parte(_todas, maq, pp)).Where(x => x != null).ToList();
        }

        private static string CmdLigDe(Partida parte)
        {
            return parte.Papel == Maquinas.PapelAgua ? "CMD_ABR" : "CMD_LIG";
        }

        private static string LiberaDe(Partida parte)
        {
            return parte.Papel == Maquinas.PapelAgua ? "CMD_LOP" : "CMD_LO1";
        }

        /// <summary>
        /// Nucleo do preparador (SIS.AUT): rede 1 = bloco da maquina + visualizacao; rede 2 = automatico
        /// (desliga temporizado, set/reset das partes pelo intertravamento, defeito geral).
        /// </summary>
        private string XmlRedesPreparador(SubsistemaGerado sg, Partida sis, TipoPartida tipo, Func<string, bool> existe)
        {
            string maq = sis.Maquina;
            string a = sg.NomeDb + "." + sis.Eq + "." + sis.Fn + ".";   // "PP1".SIS.AUT.
            Partida vaz = Maquinas.Parte(_todas, maq, Maquinas.PapelVazao);
            Partida nBai = Maquinas.Parte(_todas, maq, Maquinas.PapelNivBaixo) ?? Maquinas.Parte(_todas, maq, Maquinas.PapelNivAna);
            Partida nAlt = Maquinas.Parte(_todas, maq, Maquinas.PapelNivAlto) ?? Maquinas.Parte(_todas, maq, Maquinas.PapelNivAna);
            Partida nPol = Maquinas.Parte(_todas, maq, Maquinas.PapelNivPolimero);
            List<Partida> partes = PartesComandadas(maq);

            var sb = new System.Text.StringBuilder();

            // ---- rede 1: bloco da maquina ----------------------------------------------
            string fb = FbReal(tipo.Fb);
            string modelo;
            string[][] pinos = _templatesFb.TryGetValue(fb, out modelo) ? PinosDoModelo(modelo) : new string[0][];
            Func<string, bool> tem = delegate (string pino) { return pinos.Any(x => x[0] == pino); };

            var r = new Rede();
            Func<string, string> sym = delegate (string s) { return Rede.Ident(r.Acesso("SYM|" + s)); };
            int call = r.Chamada(fb, sis.Instancia, pinos);
            r.ParaTrilho(call, "en");
            if (tem("SIS_CMD_AUT")) r.Entrada(call, "SIS_CMD_AUT", sym(a + "CMD_SEL"));
            if (vaz != null && tem("SEN_LEI_FLU"))
            {
                int cf = r.Contato(SimboloParte(vaz, "LEI_BAI"), true);   // fluxo = NAO vazao baixa
                r.ParaTrilho(cf, "in");
                r.Entrada(call, "SEN_LEI_FLU", Rede.Pino(cf, "out"));
            }
            if (nBai != null && tem("TAN_LEI_BAI")) r.Entrada(call, "TAN_LEI_BAI", sym(SimboloParte(nBai, "LEI_BAI")));
            if (nAlt != null && tem("TAN_LEI_ALT")) r.Entrada(call, "TAN_LEI_ALT", sym(SimboloParte(nAlt, "LEI_ALT")));
            if (vaz != null && tem("PPF_LEI_TOT")) r.Entrada(call, "PPF_LEI_TOT", sym(SimboloParte(vaz, "LEI_TOT")));
            foreach (string[] ps in PinosPreparadorSet)
                if (tem(ps[0])) r.Entrada(call, ps[0], sym(a + ps[1]));
            if (nPol != null && tem("NPO_LEI_BAI")) r.Entrada(call, "NPO_LEI_BAI", sym(SimboloParte(nPol, "LEI_BAI")));
            foreach (string[] ps in PinosPreparadorSaida)
                if (tem(ps[0])) r.Saida(call, ps[0], sym(a + ps[1]));
            r.FecharPinos(call, pinos);

            // visualizacao da maquina: (REM/LOC OU CMD_SEL) -> <grupo das partes>.SIS.VIS.OFF
            Partida umaParte = partes.FirstOrDefault();
            if (umaParte != null)
            {
                int vSel = r.Contato(a + "CMD_SEL", false);
                r.ParaTrilho(vSel, "in");
                int vBob = r.Bobina("Coil", sg.NomeDb + "." + umaParte.Eq + ".SIS.VIS.OFF");
                if (_remLoc != null)
                {
                    int vRem = r.Contato(_remLoc, false);
                    r.ParaTrilho(vRem, "in");
                    int vOu = r.Ou(2);
                    r.Fio(Rede.Pino(vRem, "out"), Rede.Pino(vOu, "in1"));
                    r.Fio(Rede.Pino(vSel, "out"), Rede.Pino(vOu, "in2"));
                    r.Fio(Rede.Pino(vOu, "out"), Rede.Pino(vBob, "in"));
                }
                else r.Fio(Rede.Pino(vSel, "out"), Rede.Pino(vBob, "in"));
            }
            sb.Append(Embrulhar(r, string.IsNullOrWhiteSpace(sis.Descricao) ? "PREPARADOR AUTOMÁTICO DE POLÍMERO" : sis.Descricao));

            // ---- rede 2: automatico ---------------------------------------------------
            r = new Rede();
            sym = delegate (string s) { return Rede.Ident(r.Acesso("SYM|" + s)); };

            // a) saiu do automatico (e nao e o 1o ciclo): por 1 s desliga todas as partes
            int cNaoSel = r.Contato(a + "CMD_SEL", true);
            if (existe(Cfg.TagPrimeiroCiclo))
            {
                int cFs = r.Contato(Cfg.TagPrimeiroCiclo, true);
                r.ParaTrilho(cFs, "in");
                r.Fio(Rede.Pino(cFs, "out"), Rede.Pino(cNaoSel, "in"));
            }
            else r.ParaTrilho(cNaoSel, "in");
            string tmr = sg.NomeDb + "." + sis.Eq + "." + NomeTimerPreparador;
            int ton = r.Temporizador("TON", tmr);
            r.Entrada(ton, "IN", Rede.Pino(cNaoSel, "out"));
            r.Entrada(ton, "PT", Rede.Ident(r.Acesso("TYP|T#1S")));
            r.FecharPinos(ton, new[] { new[] { "IN", "Input", "Bool" }, new[] { "PT", "Input", "Time" },
                new[] { "Q", "Output", "Bool" }, new[] { "ET", "Output", "Time" } });
            int cNaoQ = r.Contato(tmr + ".Q", true);
            r.Fio(Rede.Pino(cNaoSel, "out"), Rede.Pino(cNaoQ, "in"));
            foreach (Partida pt in partes)
            {
                int rb = r.Bobina("RCoil", SimboloParte(pt, CmdLigDe(pt)));
                r.Fio(Rede.Pino(cNaoQ, "out"), Rede.Pino(rb, "in"));
            }

            // b) automatico sem defeito: cada parte liga pelo bloco (CMD_INT) e fica em automatico
            int cSel = r.Contato(a + "CMD_SEL", false);
            r.ParaTrilho(cSel, "in");
            int cNaoDef = r.Contato(a + "CMD_DEF", true);
            r.Fio(Rede.Pino(cSel, "out"), Rede.Pino(cNaoDef, "in"));
            string auto = Rede.Pino(cNaoDef, "out");
            foreach (Partida pt in partes)
            {
                int nInt = r.Contato(SimboloParte(pt, "CMD_INT"), true);
                r.Fio(auto, Rede.Pino(nInt, "in"));
                int sb1 = r.Bobina("SCoil", SimboloParte(pt, CmdLigDe(pt)));
                r.Fio(Rede.Pino(nInt, "out"), Rede.Pino(sb1, "in"));

                int pInt = r.Contato(SimboloParte(pt, "CMD_INT"), false);
                r.Fio(auto, Rede.Pino(pInt, "in"));
                int rb1 = r.Bobina("RCoil", SimboloParte(pt, CmdLigDe(pt)));
                r.Fio(Rede.Pino(pInt, "out"), Rede.Pino(rb1, "in"));
            }
            foreach (Partida pt in partes)
            {
                int ca = r.Bobina("Coil", SimboloParte(pt, "CMD_AUT"));
                r.Fio(auto, Rede.Pino(ca, "in"));
            }

            // c) defeito ou alguma parte liberada em manual: sai do automatico
            var fontes = new List<string>();
            int cDef = r.Contato(a + "CMD_DEF", false);
            r.ParaTrilho(cDef, "in");
            fontes.Add(Rede.Pino(cDef, "out"));
            foreach (Partida pt in partes)
            {
                int cl = r.Contato(SimboloParte(pt, LiberaDe(pt)), false);
                r.ParaTrilho(cl, "in");
                fontes.Add(Rede.Pino(cl, "out"));
            }
            int ouR = r.Ou(fontes.Count);
            for (int k = 0; k < fontes.Count; k++) r.Fio(fontes[k], Rede.Pino(ouR, "in" + (k + 1)));
            int rSel = r.Bobina("RCoil", a + "CMD_SEL");
            r.Fio(Rede.Pino(ouR, "out"), Rede.Pino(rSel, "in"));

            // d) defeito de qualquer parte = defeito da maquina
            if (partes.Count > 0)
            {
                var defs = new List<string>();
                foreach (Partida pt in partes)
                {
                    int cd = r.Contato(SimboloParte(pt, "LEI_DEF"), false);
                    r.ParaTrilho(cd, "in");
                    defs.Add(Rede.Pino(cd, "out"));
                }
                int bDef = r.Bobina("Coil", a + "CMD_DEF");
                if (defs.Count == 1) r.Fio(defs[0], Rede.Pino(bDef, "in"));
                else
                {
                    int ouD = r.Ou(defs.Count);
                    for (int k = 0; k < defs.Count; k++) r.Fio(defs[k], Rede.Pino(ouD, "in" + (k + 1)));
                    r.Fio(Rede.Pino(ouD, "out"), Rede.Pino(bDef, "in"));
                }
            }
            sb.Append(Embrulhar(r, "AUTOMÁTICO " + maq));
            return sb.ToString();
        }

        internal const string NomeTimerPreparador = "DES";   // IEC_TIMER no grupo SIS do DB da maquina

        /// <summary>
        /// Rosca, agitadores e valvula de alimentacao: LEI_DEF reseta o comando (igual as outras partidas);
        /// intertravamento = automatico E NAO habilitado pelo bloco.
        /// </summary>
        private void LogicaParteMaquina(Rede r, SubsistemaGerado sg, Partida p, string b)
        {
            Partida sis = Maquinas.Parte(_todas, p.Maquina, Maquinas.PapelSis);
            // O contato LEI_DEF e compartilhado com a logica geral da partida:
            // ele ja aciona o reset de CMD_LIG e a bobina de espelho TagDef.
            // Aqui ficam somente os intertravamentos especificos do preparador.
            string hab = Maquinas.HabilitaDe(p.Papel);
            if (sis == null || hab == null) return;
            int cs = r.Contato(SimboloParte(sis, "CMD_SEL"), false);
            r.ParaTrilho(cs, "in");
            int ch = r.Contato(SimboloParte(sis, hab), true);
            r.Fio(Rede.Pino(cs, "out"), Rede.Pino(ch, "in"));
            int bi = r.Bobina("Coil", b + "CMD_INT");
            r.Fio(Rede.Pino(ch, "out"), Rede.Pino(bi, "in"));
        }

        /// <summary>
        /// Valvula de entrada de agua do preparador (BL_PARTIDA_HIBRIDA com o UDT de valvula), como no PREPARADOR PPF 1:
        /// confirmacao = fluxo (NAO vazao baixa) OU (aberta e fora do automatico); tempo de falha = SIS.AUT.SET_FLU.
        /// </summary>
        private string XmlRedeAguaPreparador(SubsistemaGerado sg, Partida p, TipoPartida tipo, Func<string, bool> existe)
        {
            string b = sg.NomeDb + "." + p.Eq + "." + p.Fn + ".";
            Partida sis = Maquinas.Parte(_todas, p.Maquina, Maquinas.PapelSis);
            Partida vaz = Maquinas.Parte(_todas, p.Maquina, Maquinas.PapelVazao);
            var r = new Rede();
            Func<string, string> sym = delegate (string s) { return Rede.Ident(r.Acesso("SYM|" + s)); };

            int call = r.Chamada(FbReal(FbPartida), p.Instancia, PinosPartida);
            r.ParaTrilho(call, "en");
            r.Entrada(call, "SIS_CMD_RST", existe(Cfg.TagReset) ? sym(Cfg.TagReset) : Rede.Ident(r.Acesso("LIT|Bool|false")));
            if (existe(Cfg.TagEmergencia))
            {
                int ce = r.Contato(Cfg.TagEmergencia, true);
                r.ParaTrilho(ce, "in");
                r.Entrada(call, "SIS_LEI_EME", Rede.Pino(ce, "out"));
            }
            r.Entrada(call, "MOT_CMD_LIG", sym(b + "CMD_ABR"));

            int c1 = r.Contato(b + "CMD_INT", false);
            r.ParaTrilho(c1, "in");
            int c2 = r.Contato(b + "CMD_LOP", false);
            r.ParaTrilho(c2, "in");
            int ouI = r.Ou(2);
            r.Fio(Rede.Pino(c1, "out"), Rede.Pino(ouI, "in1"));
            r.Fio(Rede.Pino(c2, "out"), Rede.Pino(ouI, "in2"));
            r.Entrada(call, "MOT_CMD_INT", Rede.Pino(ouI, "out"));

            // confirmacao: fluxo OU (aberta E fora do automatico)
            if (vaz != null && sis != null)
            {
                int cFlu = r.Contato(SimboloParte(vaz, "LEI_BAI"), true);
                r.ParaTrilho(cFlu, "in");
                int cAbr = r.Contato(b + "LEI_ABR", false);
                r.ParaTrilho(cAbr, "in");
                int cMan = r.Contato(SimboloParte(sis, "CMD_SEL"), true);
                r.Fio(Rede.Pino(cAbr, "out"), Rede.Pino(cMan, "in"));
                int ouC = r.Ou(2);
                r.Fio(Rede.Pino(cFlu, "out"), Rede.Pino(ouC, "in1"));
                r.Fio(Rede.Pino(cMan, "out"), Rede.Pino(ouC, "in2"));
                r.Entrada(call, "MOT_LEI_CN1", Rede.Pino(ouC, "out"));
            }
            if (sis != null) r.Entrada(call, "MOT_SET_TPD", sym(SimboloParte(sis, "SET_FLU")));
            r.Saida(call, "MOT_LEI_HAB", sym(b + "LEI_ABR"));
            r.Saida(call, "MOT_LEI_DEF", sym(b + "LEI_DEF"));
            r.Saida(call, "MOT_LEI_ST1", sym(b + "LEI_STS"));
            r.FecharPinos(call, PinosPartida);

            // defeito fecha a valvula (igual as outras partidas)
            int cd = r.Contato(b + "LEI_DEF", false);
            r.ParaTrilho(cd, "in");
            string origem = Rede.Pino(cd, "out");
            int rAbr = r.Bobina("RCoil", b + "CMD_ABR");
            r.Fio(origem, Rede.Pino(rAbr, "in"));

            // intertravamento: automatico E NAO habilitada pelo bloco
            if (sis != null)
            {
                int cs = r.Contato(SimboloParte(sis, "CMD_SEL"), false);
                r.ParaTrilho(cs, "in");
                int ch = r.Contato(SimboloParte(sis, "LEI_EAG"), true);
                r.Fio(Rede.Pino(cs, "out"), Rede.Pino(ch, "in"));
                int bi = r.Bobina("Coil", b + "CMD_INT");
                r.Fio(Rede.Pino(ch, "out"), Rede.Pino(bi, "in"));
            }

            // espelho de defeito: reutiliza a mesma leitura LEI_DEF que reseta CMD_ABR.
            int be = r.Bobina("Coil", p.TagDefEfetiva);
            r.Fio(origem, Rede.Pino(be, "in"));
            if (existe(p.TagHab))
            {
                int ca = r.Contato(b + "LEI_ABR", false);
                r.ParaTrilho(ca, "in");
                int bh = r.Bobina("Coil", p.TagHab.Trim());
                r.Fio(Rede.Pino(ca, "out"), Rede.Pino(bh, "in"));
            }

            // severidade
            int sv = r.Chamada(FbReal(FbSeveridade), p.InstanciaSev, PinosSeveridade);
            r.ParaTrilho(sv, "en");
            r.Entrada(sv, "MOT_LEI_SV2", sym(b + "LEI_DEF"));
            r.Entrada(sv, "MOT_LEI_SV4", sym(b + "CMD_LOP"));
            r.Saida(sv, "MOT_LEI_SVD", sym(b + "LEI_SVD"));
            r.FecharPinos(sv, PinosSeveridade);

            return Embrulhar(r, string.IsNullOrWhiteSpace(p.Descricao) ? "VÁLVULA PREPARADOR" : p.Descricao);
        }
    }
}