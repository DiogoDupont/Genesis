using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Siemens.Engineering;
using Siemens.Engineering.Compiler;
using Siemens.Engineering.SW;
using Siemens.Engineering.SW.Blocks;

namespace Genesis
{
    // =======================================================================
    //  Mapa Modbus TCP (CLP servidor): DB COM (DB6) com os holding registers
    //
    //  Padrao (90% dos programas):
    //    40001-40004  comandos binarios (BL_ESCRITA_COMANDO: 2 words = 32 comandos por bloco)
    //    40005-40032  words de escrita   (SET_VEL)
    //    40033-40082  words de leitura   (status, velocidades, niveis, defeitos, status words)
    //    40083-40110  DWords de leitura  (analiticos, vazao, totalizador)
    //  Se uma faixa nao cabe, ela cresce e as seguintes andam (como nos programas maiores).
    // =======================================================================
    internal class ItemModbus
    {
        public int Registro;          // 40001...
        public string Faixa;          // Comandos, Escrita, Leitura, DWord
        public string Membro;         // "COM".ESC.ESCRITA_40001 / "COM".LEI.LEITURA_40033
        public string Tipo;           // Word / DWord / Bool (bit de comando)
        public int Bit = -1;          // comandos e status words: bit dentro da word
        public string Tag;            // tag do programa ligada ao registro (vazio = reserva)
        public string Descricao;
    }

    internal class MapaModbus
    {
        public const int Base = 40001;
        public const int PadraoCmd = 4, PadraoEsc = 28, PadraoLei = 50, PadraoDw = 28;   // em registros

        public int IniCmd, QtdCmd, IniEsc, QtdEsc, IniLei, QtdLei, IniDw, QtdDw;
        public readonly List<KeyValuePair<string, string>> Comandos = new List<KeyValuePair<string, string>>();
        public readonly List<KeyValuePair<string, string>> Escritas = new List<KeyValuePair<string, string>>();
        public readonly List<KeyValuePair<string, string>> Leituras = new List<KeyValuePair<string, string>>();
        public readonly List<KeyValuePair<string, string>> DWords = new List<KeyValuePair<string, string>>();
        public readonly List<KeyValuePair<string, string>> BitsStatus = new List<KeyValuePair<string, string>>();
        public int NumDefeitos, NumStatusWords;

        /// <summary>
        /// Escritas de SET_CON de preparador: tag -> variavel Temp (Int) da FC de comunicacao.
        /// A word do Modbus vem x100 (ex.: 2,5 = 250): MOVE word -> Temp e DIV Temp / 100.0 -> SET_CON.
        /// </summary>
        public readonly Dictionary<string, string> AuxCon = new Dictionary<string, string>();

        public int TotalRegistros { get { return IniDw + QtdDw - Base; } }
        public int TotalBytes { get { return TotalRegistros * 2; } }
        public int BlocosComando { get { return QtdCmd / 2; } }
        public bool ForaDoPadrao
        {
            get { return QtdCmd > PadraoCmd || QtdEsc > PadraoEsc || QtdLei > PadraoLei || QtdDw > PadraoDw; }
        }

        public static string NomeStatusWord(int n) { return NomeWordStatus() + n; }
        public static string NomeWordStatus()
        {
            return string.IsNullOrWhiteSpace(Cfg.NomeStatusWord) ? "STATUS_WORD_" : Cfg.NomeStatusWord.Trim();
        }

        public static string NomeDefeitos(int n)
        {
            return (string.IsNullOrWhiteSpace(Cfg.NomeWordDefeitos) ? "DEFEITOS_" : Cfg.NomeWordDefeitos.Trim()) + n;
        }

        /// <summary>
        /// Monta o mapa a partir de todas as partidas, sensores e PIDs (na ordem das listas).
        /// numDefeitos = quantas words DEFEITOS_n existem (ou vao existir).
        /// </summary>
        public static MapaModbus Montar(IList<Partida> todas, int numDefeitos)
        {
            var m = new MapaModbus { NumDefeitos = numDefeitos };
            Func<Partida, string> db = delegate (Partida p) { return Cfg.Prefixo + p.Sub + "." + p.Eq + "." + p.Fn + "."; };
            Func<Partida, string> desc = delegate (Partida p)
            {
                return string.IsNullOrWhiteSpace(p.Descricao) ? (p.Sub + " " + p.Eq + " " + p.Fn) : p.Descricao.Trim();
            };

            var valvulas = new List<Partida>();
            foreach (Partida p in todas)
            {
                Gerador.TipoPartida t = Gerador.BuscarTipo(p.Tipo);
                if (t == null || t.Sensor || t.Pid) continue;
                string b = db(p), d = desc(p);

                // maquinas: nucleo = automatico da maquina; valvula de agua do preparador
                if (t.MaquinaSis)
                {
                    // CMD_SEL e o membro interno ligado ao pino SIS_CMD_AUT do bloco.
                    // No mapa Modbus ele representa o CMD_AUT geral do preparador.
                    m.Comandos.Add(Par(b + "CMD_SEL", "CMD_AUT PREPARADOR " + d));
                    m.Escritas.Add(Par(b + "SET_CON", "SET CONCENTRAÇÃO " + d));
                    m.AuxCon[b + "SET_CON"] = NomeAuxCon(p);
                    continue;
                }
                if (t.MaquinaAgua)
                {
                    // No preparador, a valvula e comandada pelo automatico da maquina;
                    // nao expor um comando individual de abertura no mapa.
                    if (!Maquinas.EhPreparador(p))
                        m.Comandos.Add(Par(b + "CMD_ABR", "COMANDO ABRE " + d));
                    m.Leituras.Add(Par(b + "LEI_STS", "STATUS " + d));
                    continue;
                }

                // As partes do preparador recebem comando do bloco SIS.AUT.
                // Nao criar CMD_AUT/CMD_LIG nem SET_VEL individuais no mapa Modbus.
                if (Maquinas.EhPreparador(p))
                {
                    m.Leituras.Add(Par(b + "LEI_ST1", "STATUS " + d));
                    if (t.Bypass) m.Leituras.Add(Par(b + "LEI_ST2", "STATUS MOTOR 2 " + d));
                    if (t.Velocidade || t.Rede) m.Leituras.Add(Par(b + "LEI_VEL", "LEITURA VEL " + d));
                    if (Gerador.TemExaustor(p))
                        m.Leituras.Add(Par(Cfg.Prefixo + p.Sub + "." + p.Eq + "." + Gerador.NomeExa(todas, p) + ".LEI_ST1",
                            "STATUS EXAUSTOR " + d));
                    continue;
                }

                // comandos binarios
                if (t.Valvula)
                {
                    if (p.Auto) m.Comandos.Add(Par(b + "CMD_AUT", "SELEÇÃO AUTOMÁTICO " + d));
                    m.Comandos.Add(Par(b + "CMD_ABR", "COMANDO ABRE " + d));
                    valvulas.Add(p);
                    continue;
                }
                if (p.Auto) m.Comandos.Add(Par(b + "CMD_AUT", "MODO AUTOMÁTICO " + d));
                m.Comandos.Add(Par(b + "CMD_LIG", "COMANDO LIGA " + d));

                // escrita: set de velocidade
                if (t.Velocidade || t.Rede) m.Escritas.Add(Par(b + "SET_VEL", "SET VEL " + d));

                // leituras: status (+ motor 2 no bypass), velocidade, exaustor
                m.Leituras.Add(Par(b + "LEI_ST1", "STATUS " + d));
                if (t.Bypass) m.Leituras.Add(Par(b + "LEI_ST2", "STATUS MOTOR 2 " + d));
                if (t.Velocidade || t.Rede) m.Leituras.Add(Par(b + "LEI_VEL", "LEITURA VEL " + d));
                if (Gerador.TemExaustor(p))
                    m.Leituras.Add(Par(Cfg.Prefixo + p.Sub + "." + p.Eq + "." + Gerador.NomeExa(todas, p) + ".LEI_ST1",
                        "STATUS EXAUSTOR " + d));
            }

            // valvulas no fim das leituras de status
            foreach (Partida p in valvulas)
            {
                Gerador.TipoPartida t = Gerador.BuscarTipo(p.Tipo);
                m.Leituras.Add(Par(db(p) + "LEI_STS", "STATUS " + desc(p)));
                if (t.Analogica) m.Leituras.Add(Par(db(p) + "LEI_POS", "LEITURA POSIÇÃO " + desc(p)));
            }

            // sensores: niveis analogicos (word, %), analiticos e vazao (DWord), bits de alarme (status words)
            foreach (Partida p in todas)
            {
                Gerador.TipoPartida t = Gerador.BuscarTipo(p.Tipo);
                if (t == null || !t.Sensor) continue;
                string b = db(p), d = desc(p);
                if (t.SenAna && !t.SenAnalitico)
                {
                    m.Leituras.Add(Par(b + "LEI_POR", "NÍVEL " + d));
                }
                if (t.SenAnalitico) m.DWords.Add(Par(b + "LEI_NIV", d));
                if (t.SenVazao)
                {
                    m.DWords.Add(Par(b + "LEI_VAZ", "VAZÃO " + d));
                    // vazao do preparador de polimero: o totalizador e so calculo interno (zera apos a batelada),
                    // nao e totalizador de verdade; vai so a leitura da vazao no mapa
                    if (!(p.Papel == Maquinas.PapelVazao && Maquinas.EhPreparador(p)))
                        m.DWords.Add(Par(b + "LEI_TOT", "TOTALIZADOR " + d));
                }

                if (t.SenAna || t.SenVazao)
                {
                    m.BitsStatus.Add(Par(b + "LEI_BAI", "BAIXO " + d));
                    m.BitsStatus.Add(Par(b + "LEI_ALT", "ALTO " + d));
                }
                else if (!t.SenRaspador)
                {
                    m.BitsStatus.Add(Par(b + (t.SenAlto ? "LEI_ALT" : "LEI_BAI"), (t.SenAlto ? "ALTO " : "BAIXO ") + d));
                }
            }

            for (int i = 1; i <= numDefeitos; i++) m.Leituras.Add(Par(NomeDefeitos(i), "DEFEITOS " + i));
            m.NumStatusWords = (m.BitsStatus.Count + 15) / 16;
            for (int i = 1; i <= m.NumStatusWords; i++) m.Leituras.Add(Par(NomeStatusWord(i), "STATUS WORD " + i));

            // faixas: o padrao, ou o que precisar (comandos em blocos de 2 words)
            int wordsCmd = (m.Comandos.Count + 15) / 16;
            m.QtdCmd = Math.Max(PadraoCmd, ((wordsCmd + 1) / 2) * 2);
            m.QtdEsc = Math.Max(PadraoEsc, m.Escritas.Count);
            m.QtdLei = Math.Max(PadraoLei, m.Leituras.Count);
            m.QtdDw = Math.Max(PadraoDw, m.DWords.Count * 2);
            m.IniCmd = Base;
            m.IniEsc = m.IniCmd + m.QtdCmd;
            m.IniLei = m.IniEsc + m.QtdEsc;
            m.IniDw = m.IniLei + m.QtdLei;
            return m;
        }

        /// <summary>Nome da variavel Temp do SET_CON do preparador (ex.: AUX_PP1_CON).</summary>
        public static string NomeAuxCon(Partida p)
        {
            var sb = new System.Text.StringBuilder("AUX_");
            foreach (char c in (string.IsNullOrWhiteSpace(p.Maquina) ? p.Sub : p.Maquina).Trim())
                sb.Append(char.IsLetterOrDigit(c) ? char.ToUpperInvariant(c) : '_');
            return sb.Append("_CON").ToString();
        }

        private static KeyValuePair<string, string> Par(string a, string b) { return new KeyValuePair<string, string>(a, b); }

        public static string Escrita(int reg) { return "ESCRITA_" + reg; }
        public static string Leitura(int reg) { return "LEITURA_" + reg; }

        /// <summary>Lista completa (com reservas) para a tela e para o CSV.</summary>
        public List<ItemModbus> Itens()
        {
            string com = string.IsNullOrWhiteSpace(Cfg.ComDb) ? "COM" : Cfg.ComDb.Trim();
            var l = new List<ItemModbus>();
            for (int w = 0; w < QtdCmd; w++)
            {
                int reg = IniCmd + w;
                for (int bit = 0; bit < 16; bit++)
                {
                    int i = w * 16 + bit;
                    bool tem = i < Comandos.Count;
                    if (!tem && bit > 0) continue;   // reserva: uma linha por word
                    l.Add(new ItemModbus
                    {
                        Registro = reg, Faixa = "Comandos", Membro = com + ".ESC." + Escrita(reg), Tipo = tem ? "Bool" : "Word",
                        Bit = tem ? bit : -1, Tag = tem ? Comandos[i].Key : "", Descricao = tem ? Comandos[i].Value : "RESERVA"
                    });
                }
            }
            for (int i = 0; i < QtdEsc; i++)
            {
                int reg = IniEsc + i;
                bool tem = i < Escritas.Count;
                l.Add(new ItemModbus
                {
                    Registro = reg, Faixa = "Escrita", Membro = com + ".ESC." + Escrita(reg), Tipo = "Word",
                    Tag = tem ? Escritas[i].Key : "", Descricao = tem ? Escritas[i].Value : "RESERVA"
                });
            }
            for (int i = 0; i < QtdLei; i++)
            {
                int reg = IniLei + i;
                bool tem = i < Leituras.Count;
                l.Add(new ItemModbus
                {
                    Registro = reg, Faixa = "Leitura", Membro = com + ".LEI." + Leitura(reg), Tipo = "Word",
                    Tag = tem ? Leituras[i].Key : "", Descricao = tem ? Leituras[i].Value : "RESERVA"
                });
                // bits das status words
                if (tem && Leituras[i].Key.StartsWith(NomeWordStatus()))
                {
                    int n = int.Parse(Leituras[i].Key.Substring(NomeWordStatus().Length));
                    for (int bit = 0; bit < 16; bit++)
                    {
                        int k = (n - 1) * 16 + bit;
                        if (k >= BitsStatus.Count) break;
                        l.Add(new ItemModbus
                        {
                            Registro = reg, Faixa = "Leitura", Membro = Leituras[i].Key + ".%X" + bit, Tipo = "Bool", Bit = bit,
                            Tag = BitsStatus[k].Key, Descricao = BitsStatus[k].Value
                        });
                    }
                }
            }
            for (int i = 0; i < QtdDw / 2; i++)
            {
                int reg = IniDw + i * 2;
                bool tem = i < DWords.Count;
                l.Add(new ItemModbus
                {
                    Registro = reg, Faixa = "DWord", Membro = com + ".LEI." + Leitura(reg), Tipo = "DWord",
                    Tag = tem ? DWords[i].Key : "", Descricao = tem ? DWords[i].Value : "RESERVA"
                });
            }
            return l;
        }

        public string Csv()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Registro;Faixa;Bit;Membro no DB;Tipo;Tag do programa;Descricao");
            foreach (ItemModbus i in Itens())
                sb.AppendLine(i.Registro + ";" + i.Faixa + ";" + (i.Bit >= 0 ? i.Bit.ToString() : "") + ";" + i.Membro + ";"
                    + i.Tipo + ";" + i.Tag + ";" + (i.Descricao ?? "").Replace(";", ","));
            return sb.ToString();
        }

        public string Resumo()
        {
            return "Comandos " + IniCmd + "–" + (IniEsc - 1) + " (" + Comandos.Count + "/" + QtdCmd * 16 + " bits) · "
                + "Escrita " + IniEsc + "–" + (IniLei - 1) + " (" + Escritas.Count + "/" + QtdEsc + ") · "
                + "Leitura " + IniLei + "–" + (IniDw - 1) + " (" + Leituras.Count + "/" + QtdLei + ") · "
                + "DWords " + IniDw + "–" + (IniDw + QtdDw - 1) + " (" + DWords.Count + "/" + QtdDw / 2 + ")";
        }
    }

    // =======================================================================
    //  Geracao no TIA: DB COM + FC de mapeamento
    // =======================================================================
    internal partial class Gerador
    {
        private const string FbEscritaComando = "BL_ESCRITA_COMANDO";

        /// <summary>So gera a comunicacao (botao da pagina Modbus).</summary>
        public bool SoComunicacao;

        /// <summary>Tags das status words que faltam (STATUS_WORD_n em %MW600, 602...): entram na aba Tags.</summary>
        private void PrepararTagsComunicacao(PlcSoftware plc, List<Partida> todas, List<Io> ios)
        {
            if (!Cfg.ComAtivo) return;
            MapaModbus m = MapaModbus.Montar(todas, 0);
            if (m.NumStatusWords == 0) return;

            int ini;
            if (!int.TryParse((Cfg.InicioStatusWord ?? "").Trim(), out ini)) ini = 600;
            var noPlc = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            ColetarEnderecos(plc.TagTableGroup, noPlc);

            var novas = new List<Io>();
            for (int n = 1; n <= m.NumStatusWords; n++)
            {
                string nome = MapaModbus.NomeStatusWord(n);
                if (noPlc.ContainsKey(nome) || ios.Any(x => string.Equals(x.Nome, nome, StringComparison.OrdinalIgnoreCase))) continue;
                var io = new Io { Nome = nome, Area = "MEM", Numero = (ini + 2 * (n - 1)).ToString(), Tipo = "Word", Descricao = "STATUS WORD " + n };
                ios.Add(io);
                novas.Add(io);
                Log("Comunicacao: " + nome + " em %MW" + io.Numero + ".");
            }
            if (novas.Count > 0 && AoCriarTags != null) AoCriarTags(novas);
        }

        /// <summary>Quantas words de defeito existem (DEFEITOS_1, 2... seguidas), no PLC ou criadas agora.</summary>
        private static int ContarDefeitos(Func<string, bool> existe)
        {
            int n = 0;
            while (existe(MapaModbus.NomeDefeitos(n + 1))) n++;
            return n;
        }

        /// <summary>
        /// Gera o DB COM (numero fixo, layout Standard) e a FC de mapeamento no grupo do gerador.
        /// O MB_SERVER entra na FC se ja houver uma chamada dele no projeto (copia versao, instancia e CONNECT).
        /// </summary>
        private bool GerarComunicacao(PlcSoftware plc, PlcBlockUserGroup grupo, List<Partida> todas,
            Func<string, bool> existe, string pastaExport, string saida)
        {
            if (!Cfg.ComAtivo) return true;
            Log("--- Comunicacao Modbus (DB " + NomeCom + ") ---");

            MapaModbus m = MapaModbus.Montar(todas, ContarDefeitos(existe));
            Log(m.Resumo());
            if (m.ForaDoPadrao)
                Log("AVISO: o mapa nao coube no padrao (40001-40110): faixas aumentadas, total " + m.TotalRegistros
                    + " registros (40001-" + (MapaModbus.Base + m.TotalRegistros - 1) + "). Avise a equipe do supervisorio.");

            // BL_ESCRITA_COMANDO: pinos e modelo de instancia vem do proprio projeto
            var fbs = new List<string>();
            ColetarFbs(plc.BlockGroup, fbs);
            string fbEsc = fbs.FirstOrDefault(f => f == FbEscritaComando)
                ?? fbs.Where(f => f.StartsWith(FbEscritaComando + "_")).OrderBy(f => f).LastOrDefault();
            if (fbEsc == null)
            {
                Log("ERRO: o FB " + FbEscritaComando + " nao esta no projeto; comunicacao nao gerada.");
                return false;
            }
            Dictionary<string, string> tpl = CarregarTemplates(plc, new List<string> { fbEsc }, pastaExport);
            if (tpl == null || !tpl.ContainsKey(fbEsc)) return false;
            string[][] pinosEsc = PinosDoModelo(tpl[fbEsc]);
            string[] pinosCmd = pinosEsc.Where(p => p[1] == "InOut" && p[2] == "Bool").Select(p => p[0]).ToArray();
            string[] pinosWord = pinosEsc.Where(p => p[1] == "Input" && p[0].StartsWith("ESC_") && p[0].EndsWith("_WRD")).Select(p => p[0]).OrderBy(x => x).ToArray();
            string pinoRemLoc = pinosEsc.Where(p => p[1] == "Input" && p[2] == "Bool").Select(p => p[0]).FirstOrDefault(x => x.Contains("REM"));
            if (pinosCmd.Length < 32 || pinosWord.Length < 2)
            {
                Log("ERRO: o " + fbEsc + " do projeto nao tem os pinos esperados (2 words ESC_..._WRD e 32 comandos InOut).");
                return false;
            }

            // MB_SERVER: copia de uma chamada que ja exista no projeto; se nao houver, cria conexao e instancia
            // O DB de conexao (CNT) precisa existir ANTES da busca: uma FC com MB_SERVER feita a mao
            // (ex.: TESTE_MB_SERVER) que aponta para "CNT".CONNECT fica inconsistente sem ele,
            // e o TIA nao exporta bloco inconsistente -> a chamada nao era encontrada.
            GarantirConexao(plc, grupo, saida);
            ChamadaMbServer mb = AcharMbServer(plc);
            bool mbNovo = false;   // o servidor novo e preparado depois que o DB COM existir (abaixo)

            // XMLs
            var utf8 = new UTF8Encoding(false);
            string arqDb = Path.Combine(saida, NomeCom + ".xml");
            File.WriteAllText(arqDb, XmlCom(m), utf8);

            var instancias = new List<string>();
            for (int i = 1; i <= m.BlocosComando; i++)
            {
                string inst = InstanciaEscrita(i);
                instancias.Add(inst);
                File.WriteAllText(Path.Combine(saida, inst + ".xml"), XmlInstancia(tpl[fbEsc], inst), utf8);
            }

            string nomeFc = NomeFcCom;
            string arqFc = Path.Combine(saida, nomeFc + ".xml");
            File.WriteAllText(arqFc, XmlFcCom(nomeFc, m, fbEsc, pinosEsc, pinosWord, pinosCmd, pinoRemLoc, mb), utf8);

            // DB COM, FC e instancias que ja existam: com "recriar" (padrao) tudo e apagado, onde estiver e em
            // qualquer formato, depois de guardar uma copia XML; sem "recriar", so substitui o que o Genesis gerou.
            bool recriar = Cfg.ComRecriar;
            PlcBlockGroup ondeCom;
            PlcBlock comAtual = AcharBloco(plc.BlockGroup, NomeCom, out ondeCom);
            string backupCom = null;
            string pastaBackup = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Genesis", "Backup", DateTime.Now.ToString("yyyyMMdd_HHmmss"));

            // 1) conferir TUDO antes de apagar qualquer coisa
            if (comAtual != null && !recriar && !ComNoFormatoNovo(comAtual))
            {
                Log("ERRO: o DB " + NomeCom + " do projeto esta no formato antigo (sem ESC/LEI); nao vou substituir.");
                Log("      Marque \"apagar e gerar tudo de novo\" na Configuracao (Comunicacao Modbus), ou renomeie/apague o " + NomeCom + " antigo.");
                return false;
            }
            var nomesRecriar = new[] { nomeFc }.Concat(instancias).ToList();
            if (!recriar)
            {
                foreach (string n in nomesRecriar)
                {
                    PlcBlockGroup onde;
                    PlcBlock b = AcharBloco(plc.BlockGroup, n, out onde);
                    if (b != null && !Comunicacao_GrupoEstaDentroDe(onde, grupo) && !n.StartsWith("iESC_"))
                    {
                        Log("ERRO: ja existe um bloco " + n + " fora do grupo " + NomeGrupo + "; comunicacao nao gerada.");
                        Log("      Marque \"apagar e gerar tudo de novo\" na Configuracao (Comunicacao Modbus) para recriar.");
                        return false;
                    }
                }
            }

            // 2) copia de seguranca e remocao
            try
            {
                if (comAtual != null)
                {
                    Directory.CreateDirectory(pastaBackup);
                    backupCom = Path.Combine(pastaBackup, NomeCom + "_antigo.xml");
                    string erroBk = ExportarBloco(comAtual, backupCom);
                    if (erroBk != null) { Log("AVISO: sem copia do DB " + NomeCom + " antigo (" + erroBk + ")."); backupCom = null; }
                    Log("DB " + NomeCom + ": " + (recriar ? "apagando o que existe e gerando de novo" : "substituindo pelo mapa novo")
                        + " (numero " + NumeroCom + ").");
                    comAtual.Delete();
                }
                foreach (string n in nomesRecriar)
                {
                    PlcBlockGroup onde;
                    PlcBlock b = AcharBloco(plc.BlockGroup, n, out onde);
                    if (b == null) continue;
                    Directory.CreateDirectory(pastaBackup);
                    string erroBk = ExportarBloco(b, Path.Combine(pastaBackup, n + "_antigo.xml"));
                    if (erroBk != null) Log("AVISO: sem copia de " + n + " antigo (" + erroBk + ").");
                    Log("Apagando " + n + " antigo" + (recriar ? " (copia em " + pastaBackup + ")" : "") + ".");
                    b.Delete();
                }
            }
            catch (Exception ex)
            {
                Log("ERRO ao apagar o que ja existia da comunicacao: " + DetalheErro(ex));
                PlcBlockGroup ondeTmp;
                if (backupCom != null && AcharBloco(plc.BlockGroup, NomeCom, out ondeTmp) == null)
                {
                    try { ImportarArquivo(ondeCom ?? grupo, backupCom); Log("      O " + NomeCom + " antigo foi restaurado."); }
                    catch (Exception ex2) { Log("      E nao consegui restaurar o antigo (" + DetalheErro(ex2) + "). Copia em: " + backupCom); }
                }
                return false;
            }
            if (recriar && Directory.Exists(pastaBackup)) Log("Copias de seguranca dos blocos apagados: " + pastaBackup);

            Log("Importando " + NomeCom + " (DB" + NumeroCom + ", " + m.TotalBytes + " bytes)...");
            try
            {
                ImportarArquivo(grupo, arqDb);
            }
            catch (Exception ex)
            {
                Log("ERRO ao importar o DB " + NomeCom + ": " + DetalheErro(ex));
                if (backupCom != null)
                {
                    try
                    {
                        ImportarArquivo(ondeCom ?? grupo, backupCom);
                        Log("      O " + NomeCom + " antigo foi restaurado; nada mudou na comunicacao.");
                    }
                    catch (Exception ex2)
                    {
                        Log("      E nao consegui restaurar o antigo (" + DetalheErro(ex2) + "). Copia em: " + backupCom);
                    }
                }
                Log("      Mande esta mensagem se nao souber o que e.");
                return false;
            }
            foreach (string inst in instancias)
            {
                Log("Importando " + inst + "...");
                ImportarArquivo(grupo, Path.Combine(saida, inst + ".xml"));
            }

            // MB_SERVER novo: so agora, com o DB COM ja no projeto (MB_HOLD_REG aponta para ele)
            if (mb == null)
            {
                mb = PrepararMbServer(plc, grupo, saida, m);
                mbNovo = mb != null;
            }

            Log("Importando " + nomeFc + "...");
            {
                // versao do MB_SERVER: a copiada ou, se foi criado agora, a que o TIA aceitar
                var versoes = new List<string>();
                if (mb != null && mb.Embrulhado) versoes.Add(mb.Versao);   // o MB_SERVER ja esta no FB; a FC so chama o FB
                else if (mb != null) versoes.AddRange(mb.Versao != null ? new[] { mb.Versao } : VersoesMbServer);
                bool entrou = false;
                var errosVistos = new HashSet<string>();
                foreach (string v in versoes)
                {
                    mb.Versao = v;
                    File.WriteAllText(arqFc, XmlFcCom(nomeFc, m, fbEsc, pinosEsc, pinosWord, pinosCmd, pinoRemLoc, mb), utf8);
                    try
                    {
                        ImportarArquivo(grupo, arqFc);
                        entrou = true;
                        if (mbNovo && !mb.Embrulhado) Log("MB_SERVER: chamada criada com a versao " + v + ".");
                        break;
                    }
                    catch (Exception ex)
                    {
                        // mostra o motivo (uma vez por mensagem diferente) para dar para diagnosticar
                        string motivo = DetalheErro(ex);
                        if (errosVistos.Add(motivo))
                            Log((mb.Embrulhado ? "FC " + nomeFc + " (chamando " + mb.Fb + ")" : "MB_SERVER V" + v)
                                + " recusado na importacao: " + motivo);
                    }
                }
                if (!entrou)
                {
                    if (mb != null && mb.Embrulhado)
                    {
                        Log("AVISO: a FC com a chamada do " + mb.Fb + " nao importou; a FC vai sem o servidor Modbus.");
                        Log(">>> Chame o " + mb.Fb + " (instancia " + mb.Instancia + ") na rede 1 da FC " + nomeFc + ".");
                    }
                    else if (mb != null)
                    {
                        Log("AVISO: nenhuma versao do MB_SERVER foi aceita; a FC vai sem ele.");
                        Log(">>> Arraste o MB_SERVER uma vez para a rede 1 da FC " + nomeFc + " (instancia " + mb.Instancia
                            + "); nas proximas geracoes o gerador copia ele.");
                    }
                    mb = null;
                    File.WriteAllText(arqFc, XmlFcCom(nomeFc, m, fbEsc, pinosEsc, pinosWord, pinosCmd, pinoRemLoc, null), utf8);
                    ImportarArquivo(grupo, arqFc);
                }
            }

            var compilar = new List<string> { NomeCom };
            if (mb != null && mb.Embrulhado) compilar.AddRange(new[] { mb.Fb, mb.Instancia });
            compilar.AddRange(instancias);
            compilar.Add(nomeFc);
            bool ok = CompilarPorNome(plc, compilar);

            if (mb == null)
            {
                Log(">>> Nao achei uma chamada do MB_SERVER no projeto: a FC " + nomeFc + " foi gerada sem ela.");
                Log(">>> Inclua o MB_SERVER (MB_HOLD_REG = P#DB" + NumeroCom + ".DBX0.0 BYTE " + m.TotalBytes + ").");
            }
            else if (mb.Embrulhado)
            {
                Log(">>> MB_SERVER criado dentro do FB " + mb.Fb + " (V" + mb.Versao + ", DB de instancia " + mb.Instancia
                    + "), CONNECT = " + mb.Conexao + ", MB_HOLD_REG = P#DB" + NumeroCom + ".DBX0.0 BYTE " + m.TotalBytes + ".");
                Log(">>> Confira no " + mb.Conexao.Split('.')[0] + " o InterfaceId (HW ID da porta PROFINET do CLP, em Propriedades > Identificadores de hardware).");
                Log(">>> Diagnostico online: " + mb.Instancia + ".ERROR e .STATUS (o STATUS so indica falha quando ERROR = 1).");
            }
            else if (mbNovo)
            {
                Log(">>> MB_SERVER criado: instancia " + mb.Instancia + ", CONNECT = " + mb.Conexao + " (interface " + Cfg.InterfaceModbus
                    + ", porta " + Cfg.PortaModbus + "), MB_HOLD_REG = P#DB" + NumeroCom + ".DBX0.0 BYTE " + m.TotalBytes + ".");
                Log(">>> Confira no " + mb.Conexao.Split('.')[0] + " o InterfaceId (HW ID da porta PROFINET do CLP, em Propriedades > Identificadores de hardware).");
            }
            else
            {
                Log(">>> MB_SERVER copiado de " + mb.Bloco + " (V" + mb.Versao + ", instancia " + mb.Instancia
                    + "), agora com MB_HOLD_REG = P#DB" + NumeroCom + ".DBX0.0 BYTE " + m.TotalBytes + ".");
                if (mb.Bloco != nomeFc)
                    Log(">>> Retire a chamada antiga do MB_SERVER em " + mb.Bloco + " (a mesma instancia nao pode rodar duas vezes).");
            }
            Log(">>> Chame a FC " + nomeFc + " no Main (OB1), no lugar da FC de comunicacao antiga, se houver.");
            Log("");
            return ok;
        }

        private static string NomeCom
        {
            get { return string.IsNullOrWhiteSpace(Cfg.ComDb) ? "COM" : Cfg.ComDb.Trim(); }
        }

        private static int NumeroCom
        {
            get { int n; return int.TryParse((Cfg.ComNumero ?? "").Trim(), out n) && n > 0 ? n : 6; }
        }

        private static string NomeFcCom
        {
            get { return Cfg.Prefixo + "COMANDO_REMOTO"; }
        }

        private static string InstanciaEscrita(int n)
        {
            return "iESC_" + Cfg.Prefixo + "COMANDO_" + n;
        }

        /// <summary>DB COM ja existente: e do formato novo (structs ESC e LEI)?</summary>
        private bool ComNoFormatoNovo(PlcBlock com)
        {
            string arq = Path.Combine(Path.GetTempPath(), "Genesis", "com_atual.xml");
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(arq));
                if (File.Exists(arq)) File.Delete(arq);
                com.Export(new FileInfo(arq), ExportOptions.None);
                string s = File.ReadAllText(arq, Encoding.UTF8);
                File.Delete(arq);
                return s.Contains("Name=\"ESC\"") && s.Contains("Name=\"LEI\"");
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Pinos (Input/Output/InOut de primeiro nivel) a partir do XML de uma instancia do FB.</summary>
        internal static string[][] PinosDoModelo(string xmlInstancia)
        {
            var lista = new List<string[]>();
            XDocument doc = XDocument.Parse(xmlInstancia);
            foreach (XElement sec in doc.Descendants().Where(e => e.Name.LocalName == "Section"))
            {
                string nome = (string)sec.Attribute("Name");
                if (nome != "Input" && nome != "Output" && nome != "InOut") continue;
                foreach (XElement mem in sec.Elements().Where(e => e.Name.LocalName == "Member"))
                    lista.Add(new[] { (string)mem.Attribute("Name"), nome, ((string)mem.Attribute("Datatype") ?? "").Trim('"') });
            }
            return lista.ToArray();
        }

        // --- MB_SERVER ---------------------------------------------------------------
        private class ChamadaMbServer
        {
            public string Bloco, Versao, Instancia, Conexao;
            /// <summary>
            /// true = o MB_SERVER roda como multi-instancia dentro do FB gerado (Fb), e a FC chama esse FB
            /// com o DB de instancia Instancia. false = MB_SERVER chamado direto na FC (instancia global).
            /// </summary>
            public bool Embrulhado;
            public string Fb;
        }

        /// <summary>
        /// Compila somente o grupo de PLC data types (UDTs), evitando a
        /// compilacao geral do software, que pode incluir erros de outros blocos.
        /// </summary>
        private bool CompilarTiposParaExportacao(PlcSoftware plc)
        {
            try
            {
                if (plc == null || plc.TypeGroup == null)
                {
                    Log("MB_SERVER: PlcSoftware ou TypeGroup nao esta disponivel.");
                    return false;
                }

                ICompilable compilavel = plc.TypeGroup.GetService<ICompilable>();
                if (compilavel == null)
                {
                    Log("MB_SERVER: o grupo de PLC data types nao disponibilizou ICompilable.");
                    return false;
                }

                CompilerResult resultado = compilavel.Compile();
                if (resultado == null)
                {
                    Log("MB_SERVER: compilacao dos PLC data types nao retornou resultado.");
                    return false;
                }

                Log("MB_SERVER: compilacao dos PLC data types concluida. Estado="
                    + resultado.State + ", erros=" + resultado.ErrorCount
                    + ", avisos=" + resultado.WarningCount + ".");

                foreach (CompilerResultMessage mensagem in resultado.Messages)
                    LogMensagem(mensagem, 2);

                return resultado.ErrorCount == 0;
            }
            catch (Exception ex)
            {
                Log("MB_SERVER: falha ao compilar PLC data types: " + DetalheErro(ex));
                return false;
            }
        }

        /// <summary>
        /// Compila um bloco candidato individualmente antes da exportacao.
        /// </summary>
        private bool CompilarBlocoParaExportacao(PlcBlock bloco)
        {
            try
            {
                if (bloco == null)
                    return false;

                ICompilable compilavel = bloco.GetService<ICompilable>();
                if (compilavel == null)
                {
                    Log("MB_SERVER: bloco " + bloco.Name + " nao disponibilizou ICompilable.");
                    return false;
                }

                CompilerResult resultado = compilavel.Compile();
                if (resultado == null)
                {
                    Log("MB_SERVER: compilacao de " + bloco.Name + " nao retornou resultado.");
                    return false;
                }

                Log("MB_SERVER: compilacao individual de " + bloco.Name
                    + " concluida. Estado=" + resultado.State
                    + ", erros=" + resultado.ErrorCount
                    + ", avisos=" + resultado.WarningCount + ".");

                foreach (CompilerResultMessage mensagem in resultado.Messages)
                    LogMensagem(mensagem, 2);

                return resultado.ErrorCount == 0;
            }
            catch (Exception ex)
            {
                Log("MB_SERVER: falha ao compilar " + (bloco == null ? "bloco" : bloco.Name)
                    + ": " + DetalheErro(ex));
                return false;
            }
        }

        /// <summary>
        /// Exporta um bloco para analise. A compilacao geral do PLC e executada
        /// uma unica vez antes da varredura em AcharMbServer().
        /// </summary>
        private string ExportarBlocoParaAnalise(PlcBlock bloco, string arquivo)
        {
            string erro = ExportarBloco(bloco, arquivo);

            if (erro != null)
                return erro;

            if (!File.Exists(arquivo) || new FileInfo(arquivo).Length == 0)
                return "O TIA nao gerou um XML valido para o bloco.";

            return null;
        }

        /// <summary>Procura uma chamada do MB_SERVER nas FCs/OBs do projeto (versao, instancia e o CONNECT usado).</summary>
        private ChamadaMbServer AcharMbServer(PlcSoftware plc)
        {
            string pasta = Path.Combine(Path.GetTempPath(), "Genesis", "Diagnostico_MB_SERVER");
            Directory.CreateDirectory(pasta);

            // Primeiro atualiza a consistencia dos UDTs, sem compilar o projeto inteiro.
            bool tiposOk = CompilarTiposParaExportacao(plc);
            if (!tiposOk)
                Log("MB_SERVER: a compilacao dos PLC data types apresentou pendencias; "
                    + "vou continuar tentando compilar e exportar os blocos candidatos.");

            foreach (PlcBlock b in BlocosDe(plc.BlockGroup))
            {
                if (!(b is FC) && !(b is OB)) continue;

                // Usa um nome exclusivo por bloco para nao sobrescrever o XML de outro candidato.
                string nomeSeguro = Regex.Replace(b.Name ?? "bloco", @"[^A-Za-z0-9_-]", "_");
                string arq = Path.Combine(pasta, nomeSeguro + "_MB_SERVER.xml");
                string s;

                // Compila o candidato individualmente. Mesmo se houver erro de
                // compilacao, ainda tenta exportar para preservar o diagnostico.
                bool blocoCompilado = CompilarBlocoParaExportacao(b);
                if (!blocoCompilado)
                    Log("MB_SERVER: a compilacao de " + b.Name
                        + " apresentou pendencias; tentando exportar mesmo assim.");

                string erroExportacao = ExportarBlocoParaAnalise(b, arq);
                if (erroExportacao != null)
                {
                    Log("MB_SERVER: nao consegui exportar o bloco " + b.Name
                        + " para analisar: " + erroExportacao);
                    continue;
                }

                try
                {
                    s = File.ReadAllText(arq, Encoding.UTF8);
                }
                catch (Exception ex)
                {
                    Log("MB_SERVER: XML exportado, mas nao consegui ler o arquivo de "
                        + b.Name + ": " + DetalheErro(ex));
                    continue;
                }

                // Primeiro filtro barato. Se o texto nao contem o nome, nao ha chamada para analisar.
                if (s.IndexOf("MB_SERVER", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                // Mantem o XML para diagnostico caso o formato do TIA seja diferente do esperado.
                XDocument doc;
                try
                {
                    doc = XDocument.Parse(s, LoadOptions.PreserveWhitespace);
                }
                catch (Exception ex)
                {
                    Log("MB_SERVER: o XML de " + b.Name + " contem MB_SERVER, mas nao pode ser interpretado: " + PrimeiraLinha(ex));
                    Log("      XML preservado em: " + arq);
                    continue;
                }

                XElement parte = doc.Descendants()
                    .FirstOrDefault(e => e.Name.LocalName == "Part" &&
                        string.Equals((string)e.Attribute("Name"), "MB_SERVER", StringComparison.OrdinalIgnoreCase));

                if (parte == null)
                {
                    Log("MB_SERVER: encontrei o texto em " + b.Name + ", mas nao um elemento Part Name=MB_SERVER.");
                    Log("      XML preservado em: " + arq);
                    continue;
                }

                string versao = (string)parte.Attribute("Version");
                string uid = (string)parte.Attribute("UId");
                XElement instancia = parte.Descendants().FirstOrDefault(e => e.Name.LocalName == "Instance");
                XElement componenteInstancia = instancia == null ? null :
                    instancia.Descendants().FirstOrDefault(e => e.Name.LocalName == "Component" && e.Attribute("Name") != null);
                string nomeInstancia = componenteInstancia == null ? null : (string)componenteInstancia.Attribute("Name");

                if (string.IsNullOrWhiteSpace(versao) || string.IsNullOrWhiteSpace(uid) || string.IsNullOrWhiteSpace(nomeInstancia))
                {
                    Log("MB_SERVER: chamada encontrada em " + b.Name + ", mas faltam dados para reutiliza-la (Version="
                        + (versao ?? "<ausente>") + ", UId=" + (uid ?? "<ausente>") + ", instancia="
                        + (nomeInstancia ?? "<ausente>") + ").");
                    Log("      XML preservado em: " + arq);
                    continue;
                }

                var resultado = new ChamadaMbServer
                {
                    Bloco = b.Name,
                    Versao = versao,
                    Instancia = nomeInstancia
                };

                // Descobre o DB ligado ao pino CONNECT sem depender da ordem dos atributos XML.
                XElement flgNet = parte.Ancestors().FirstOrDefault(e => e.Name.LocalName == "FlgNet");
                if (flgNet != null)
                {
                    XElement wire = flgNet.Descendants().FirstOrDefault(e => e.Name.LocalName == "Wire" &&
                        e.Descendants().Any(n => n.Name.LocalName == "NameCon" &&
                            string.Equals((string)n.Attribute("UId"), uid, StringComparison.Ordinal) &&
                            string.Equals((string)n.Attribute("Name"), "CONNECT", StringComparison.OrdinalIgnoreCase)));

                    if (wire != null)
                    {
                        XElement ident = wire.Descendants().FirstOrDefault(e => e.Name.LocalName == "IdentCon");
                        string accessUid = ident == null ? null : (string)ident.Attribute("UId");
                        if (!string.IsNullOrWhiteSpace(accessUid))
                        {
                            XElement acesso = flgNet.Descendants().FirstOrDefault(e => e.Name.LocalName == "Access" &&
                                string.Equals((string)e.Attribute("UId"), accessUid, StringComparison.Ordinal));
                            XElement simbolo = acesso == null ? null : acesso.Descendants().FirstOrDefault(e => e.Name.LocalName == "Symbol");
                            if (simbolo != null)
                            {
                                string[] componentes = simbolo.Descendants()
                                    .Where(e => e.Name.LocalName == "Component" && e.Attribute("Name") != null)
                                    .Select(e => (string)e.Attribute("Name")).ToArray();
                                if (componentes.Length > 0)
                                    resultado.Conexao = string.Join(".", componentes);
                            }
                        }
                    }
                }

                Log("MB_SERVER: chamada encontrada em " + resultado.Bloco + " (V" + resultado.Versao
                    + ", instancia " + resultado.Instancia + ", CONNECT="
                    + (resultado.Conexao ?? "nao identificado") + ").");
                return resultado;
            }

            return null;
        }

        private static readonly string[] VersoesMbServer = { "5.3", "5.2", "5.1", "5.0", "4.3", "4.2", "4.1", "4.0" };

        /// <summary>Cria o DB de conexao (TCON_IP_v4) configurado, se ainda nao existir.</summary>
        private bool GarantirConexao(PlcSoftware plc, PlcBlockUserGroup grupo, string saida)
        {
            string con = string.IsNullOrWhiteSpace(Cfg.ConexaoModbus) ? "CNT.CONNECT" : Cfg.ConexaoModbus.Trim();
            if (!con.Contains(".")) con += ".CONNECT";
            string dbCon = con.Substring(0, con.IndexOf('.'));
            string membro = con.Substring(con.IndexOf('.') + 1);

            // 1) conexao (TCON_IP_v4): mantem o DB CNT junto dos demais DBs de tags.
            // Versoes anteriores podiam deixa-lo na raiz de FAST_GERADO. Se estiver dentro
            // do grupo do gerador, exporta e reimporta na pasta correta sem recriar seu conteudo.
            PlcBlockGroup ondeCon;
            PlcBlock blocoCon = AcharBloco(plc.BlockGroup, dbCon, out ondeCon);
            if (blocoCon != null && Comunicacao_GrupoEstaDentroDe(ondeCon, grupo))
            {
                PlcBlockGroup pastaTags = Comunicacao_ObterSubgrupo(grupo, "DBs DE TAGS");
                if (!string.Equals(ondeCon.Name, pastaTags.Name, StringComparison.OrdinalIgnoreCase))
                {
                    string arqMover = Path.Combine(saida, dbCon + "_reorganizar.xml");
                    string erroExportacao = ExportarBloco(blocoCon, arqMover);
                    if (erroExportacao == null)
                    {
                        try
                        {
                            blocoCon.Delete();
                            ImportarArquivo(grupo, arqMover);
                            Log("DB " + dbCon + " movido para a pasta DBs DE TAGS.");
                        }
                        catch (Exception exMover)
                        {
                            Log("AVISO: nao consegui reorganizar o DB " + dbCon + " (" + DetalheErro(exMover) + ").");
                            try
                            {
                                if (!ExisteBloco(plc.BlockGroup, dbCon))
                                {
                                    PlcBlockGroup restaurarEm = ondeCon ?? grupo;
                                    if (string.Equals(restaurarEm.Name, NomeGrupo, StringComparison.OrdinalIgnoreCase))
                                        restaurarEm.Blocks.Import(new FileInfo(arqMover), ImportOptions.None);
                                    else
                                        ImportarArquivo(restaurarEm, arqMover);
                                }
                            }
                            catch (Exception exRestaurar)
                            {
                                Log("ERRO: nao consegui restaurar o DB " + dbCon + " (" + DetalheErro(exRestaurar) + ").");
                            }
                        }
                    }
                    else
                        Log("AVISO: nao consegui exportar o DB " + dbCon + " para reorganiza-lo (" + erroExportacao + ").");
                }
            }

            // Se ainda nao existe, cria a conexao (TCON_IP_v4).
            if (!ExisteBloco(plc.BlockGroup, dbCon))
            {
                bool ok = false;
                foreach (int variante in new[] { 0, 1, 2 })
                {
                    string arq = Path.Combine(saida, dbCon + "_" + variante + ".xml");
                    File.WriteAllText(arq, XmlConexao(dbCon, membro, variante), new UTF8Encoding(false));
                    try
                    {
                        ImportarArquivo(grupo, arq);
                        ok = true;
                        Log("MB_SERVER: DB de conexao " + dbCon + " criado (" + membro + ": TCON_IP_v4"
                            + (variante == 0 ? ", interface " + Cfg.InterfaceModbus + ", porta " + Cfg.PortaModbus + ")." : ")."));
                        if (variante > 0)
                            Log("AVISO: preencha no " + dbCon + "." + membro + ": InterfaceId = HW ID da porta PROFINET, ID = 1, ConnectionType = 16#0B, LocalPort = " + Cfg.PortaModbus + ".");
                        break;
                    }
                    catch (Exception ex)
                    {
                        if (variante == 2) Log("AVISO: nao consegui criar o DB " + dbCon + " (" + DetalheErro(ex) + ").");
                    }
                }
                if (!ok)
                {
                    Log(">>> Crie no TIA um DB " + dbCon + " com " + membro + " do tipo TCON_IP_v4 e gere de novo.");
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Sem chamada do MB_SERVER no projeto: cria o DB de conexao (TCON_IP_v4) e o DB de instancia do MB_SERVER.
        /// Devolve null (com a explicacao no log) se o TIA nao deixar.
        /// </summary>
        private ChamadaMbServer PrepararMbServer(PlcSoftware plc, PlcBlockUserGroup grupo, string saida, MapaModbus m)
        {
            string inst = string.IsNullOrWhiteSpace(Cfg.MbServerDb) ? "MB_SERVER_DB" : Cfg.MbServerDb.Trim();
            string con = string.IsNullOrWhiteSpace(Cfg.ConexaoModbus) ? "CNT.CONNECT" : Cfg.ConexaoModbus.Trim();
            if (!con.Contains(".")) con += ".CONNECT";

            if (!GarantirConexao(plc, grupo, saida)) return null;

            // 2) DB de instancia que ja seja do MB_SERVER (alguem arrastou a instrucao antes): usa direto na FC
            PlcBlockGroup ondeInst;
            PlcBlock blocoInst = AcharBloco(plc.BlockGroup, inst, out ondeInst);
            InstanceDB idb = blocoInst as InstanceDB;
            if (idb != null && string.Equals(idb.InstanceOfName, "MB_SERVER", StringComparison.OrdinalIgnoreCase))
            {
                Log("MB_SERVER: o DB " + inst + " ja e instancia do MB_SERVER; a chamada vai direto na FC.");
                return new ChamadaMbServer { Bloco = NomeFcCom, Versao = null, Instancia = inst, Conexao = con };
            }

            // 3) Caso normal: o Openness NAO cria DB de instancia de instrucao de sistema (CreateInstanceDB com
            //    "MB_SERVER" falha, e importar a FC com uma instancia inexistente deixa a FC sem compilar).
            //    Solucao: um FB do gerador com o MB_SERVER como multi-instancia (Static); o DB de instancia
            //    desse FB o Openness cria sem problema.
            string fb = NomeFbServidor;
            PlcBlockGroup ondeFb;
            PlcBlock fbAtual = AcharBloco(plc.BlockGroup, fb, out ondeFb);
            if (fbAtual != null && ondeFb != grupo)
            {
                Log("ERRO: ja existe um bloco " + fb + " fora do grupo " + NomeGrupo + "; MB_SERVER nao gerado.");
                return null;
            }
            if (blocoInst != null && !(idb != null && idb.InstanceOfName == fb))
            {
                Log("ERRO: ja existe um bloco " + inst + " que nao e instancia do MB_SERVER nem do " + fb + ".");
                Log(">>> Mude o nome do DB de instancia do MB_SERVER na pagina Modbus ou apague o " + inst + " e gere de novo.");
                return null;
            }

            string arqFb = Path.Combine(saida, fb + ".xml");
            string versaoOk = null, primeiraImportada = null;
            var errosVistos = new HashSet<string>();
            // MB_HOLD_REG: o TIA recusou o ponteiro como "TypedConstant" ('ConstantValue' invalido).
            // Tenta as outras formas de escrever o mesmo endereco; a primeira que importar E compilar fica.
            string ponteiro = "P#DB" + NumeroCom + ".DBX0.0 BYTE " + m.TotalBytes;
            // Ponteiro absoluto (P#DB6...) so e permitido em bloco Standard ("Absolute access to data in
            // blocks with optimized access is not permitted"), por isso o FB vai Standard nessas formas.
            string[] formasHold =
            {
                "SYM|" + NomeCom,            // o DB COM inteiro, simbolico (FB otimizado)
                "LIT0|" + ponteiro,          // ponteiro absoluto, constante sem tipo (FB Standard)
                "LIT|Any|" + ponteiro        // ponteiro absoluto tipado como ANY (FB Standard)
            };
            string formaOk = null, primeiraForma = null;
            foreach (string forma in formasHold)
            {
                string descForma = forma.StartsWith("SYM|") ? "\"" + NomeCom + "\" simbolico" : forma.Substring(0, forma.LastIndexOf('|'));
                foreach (string v in VersoesMbServer)
                {
                    File.WriteAllText(arqFb, XmlFbServidor(fb, v, con, forma), new UTF8Encoding(false));
                    try
                    {
                        ImportarArquivo(grupo, arqFb, ImportOptions.Override);
                    }
                    catch (Exception ex)
                    {
                        string motivo = DetalheErro(ex);
                        if (errosVistos.Add(motivo)) Log("MB_SERVER V" + v + " / MB_HOLD_REG " + descForma + ": recusado na importacao: " + motivo);
                        else Log("MB_SERVER V" + v + " / MB_HOLD_REG " + descForma + ": recusado (mesmo motivo).");
                        // erro na constante nao depende da versao: passa para a proxima forma
                        if (motivo.IndexOf("ConstantValue", StringComparison.OrdinalIgnoreCase) >= 0
                            || motivo.IndexOf("ConstantType", StringComparison.OrdinalIgnoreCase) >= 0) break;
                        continue;
                    }
                    if (primeiraImportada == null) { primeiraImportada = v; primeiraForma = forma; }

                    // importou: so vale se o FB compilar (versao existente para a CPU e operando aceito)
                    PlcBlockGroup g;
                    PlcBlock novoFb = AcharBloco(plc.BlockGroup, fb, out g);
                    int erros = 0;
                    try
                    {
                        CompilerResult r = novoFb.GetService<ICompilable>().Compile();
                        erros = r.ErrorCount;
                        if (erros > 0)
                        {
                            Log("MB_SERVER V" + v + " / MB_HOLD_REG " + descForma + ": importou mas nao compilou (" + erros + " erro(s)):");
                            foreach (CompilerResultMessage msg in r.Messages) LogMensagem(msg, 2);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log("MB_SERVER V" + v + ": falha ao compilar o " + fb + ": " + PrimeiraLinha(ex));
                        erros = 1;
                    }
                    if (erros == 0) { versaoOk = v; formaOk = forma; break; }
                    break;   // a versao importou; o erro e do operando -> proxima forma
                }
                if (versaoOk != null)
                {
                    _formaHoldOk = formaOk;
                    Log("MB_SERVER: MB_HOLD_REG aceito na forma " + descForma + ".");
                    break;
                }
            }

            if (versaoOk == null)
            {
                if (primeiraImportada == null)
                {
                    Log("AVISO: nenhuma versao do MB_SERVER foi aceita no " + fb + ".");
                    Log("      XML em: " + arqFb + " (mande junto com este log se precisar de ajuda).");
                    return null;
                }
                // nenhuma compilou limpo: fica com a primeira que importou, para o erro aparecer no TIA
                File.WriteAllText(arqFb, XmlFbServidor(fb, primeiraImportada, con, primeiraForma), new UTF8Encoding(false));
                ImportarArquivo(grupo, arqFb, ImportOptions.Override);
                versaoOk = primeiraImportada;
                Log("AVISO: o " + fb + " ficou com o MB_SERVER V" + versaoOk + ", mas com erro de compilacao (veja acima).");
            }
            Log("MB_SERVER: FB " + fb + " gerado com o MB_SERVER V" + versaoOk + " (multi-instancia).");

            // 4) DB de instancia do FB: recria sempre (o layout/versao do FB pode ter mudado)
            if (blocoInst != null)
            {
                try { blocoInst.Delete(); blocoInst = null; }
                catch (Exception ex) { Log("AVISO: nao consegui recriar o " + inst + " (" + PrimeiraLinha(ex) + "); compile-o no TIA."); }
            }
            if (blocoInst == null)
            {
                try
                {
                    Comunicacao_ObterSubgrupo(grupo, "FCs\\DBs DE INSTANCIA").Blocks.CreateInstanceDB(inst, true, 1, fb);
                    Log("MB_SERVER: DB de instancia " + inst + " (do " + fb + ") criado.");
                }
                catch (Exception ex)
                {
                    Log("ERRO: nao consegui criar o DB de instancia " + inst + " do " + fb + ": " + DetalheErro(ex));
                    return null;
                }
            }

            return new ChamadaMbServer
            {
                Bloco = NomeFcCom, Versao = versaoOk, Instancia = inst, Conexao = con,
                Embrulhado = true, Fb = fb
            };
        }

        /// <summary>Forma do MB_HOLD_REG que o TIA aceitou nesta execucao (null = ainda nao testada).</summary>
        private static string _formaHoldOk;

        private static string NomeFbServidor
        {
            get { return Cfg.Prefixo + "MB_SERVIDOR"; }
        }

        private static readonly string[][] PinosFbServidor =
        {
            new[] {"NDR","Output","Bool"}, new[] {"DR","Output","Bool"},
            new[] {"ERROR","Output","Bool"}, new[] {"STATUS","Output","Word"}
        };

        /// <summary>
        /// FB com o MB_SERVER como multi-instancia (Static SERVIDOR). MB_HOLD_REG aponta para o DB COM inteiro
        /// e CONNECT para o TCON_IP_v4; as saidas do MB_SERVER saem nas saidas do FB (para diagnostico).
        /// </summary>
        private string XmlFbServidor(string nomeFb, string versao, string conexao, string formaHold)
        {
            _id = 1;
            var sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            sb.AppendLine("<Document>");
            sb.AppendLine("  <Engineering version=\"V20\" />");
            sb.AppendLine("  <SW.Blocks.FB ID=\"0\">");
            sb.AppendLine("    <AttributeList>");
            sb.AppendLine("      <AutoNumber>true</AutoNumber>");
            sb.AppendLine("      <Interface><Sections xmlns=\"" + NsIf + "\">");
            sb.AppendLine("        <Section Name=\"Input\" />");
            sb.AppendLine("        <Section Name=\"Output\">");
            sb.AppendLine("          <Member Name=\"NDR\" Datatype=\"Bool\" />");
            sb.AppendLine("          <Member Name=\"DR\" Datatype=\"Bool\" />");
            sb.AppendLine("          <Member Name=\"ERROR\" Datatype=\"Bool\" />");
            sb.AppendLine("          <Member Name=\"STATUS\" Datatype=\"Word\" />");
            sb.AppendLine("        </Section>");
            sb.AppendLine("        <Section Name=\"InOut\" />");
            sb.AppendLine("        <Section Name=\"Static\">");
            sb.AppendLine("          <Member Name=\"SERVIDOR\" Datatype=\"MB_SERVER\" Version=\"" + versao + "\" />");
            bool standard = !formaHold.StartsWith("SYM|");
            sb.AppendLine("        </Section>");
            sb.AppendLine("        <Section Name=\"Temp\" />");
            sb.AppendLine("        <Section Name=\"Constant\" />");
            sb.AppendLine("      </Sections></Interface>");
            sb.AppendLine("      <MemoryLayout>" + (standard ? "Standard" : "Optimized") + "</MemoryLayout>");
            sb.AppendLine("      <Name>" + SecurityElement.Escape(nomeFb) + "</Name>");
            sb.AppendLine("      <Namespace />");
            sb.AppendLine("      <ProgrammingLanguage>LAD</ProgrammingLanguage>");
            sb.AppendLine("      <SetENOAutomatically>false</SetENOAutomatically>");
            sb.AppendLine("    </AttributeList>");
            sb.AppendLine("    <ObjectList>");

            var r = new Rede();
            int c = r.Parte("MB_SERVER", versao, "SERVIDOR", "LocalVariable");
            r.ParaTrilho(c, "en");
            r.Entrada(c, "DISCONNECT", Rede.Ident(r.Acesso("LIT|Bool|false")));
            r.Entrada(c, "MB_HOLD_REG", Rede.Ident(r.Acesso(formaHold)));
            r.Entrada(c, "CONNECT", Rede.Ident(r.Acesso("SYM|" + conexao)));
            r.Saida(c, "NDR", Rede.Ident(r.Acesso("LOC|NDR")));
            r.Saida(c, "DR", Rede.Ident(r.Acesso("LOC|DR")));
            r.Saida(c, "ERROR", Rede.Ident(r.Acesso("LOC|ERROR")));
            r.Saida(c, "STATUS", Rede.Ident(r.Acesso("LOC|STATUS")));
            r.FecharPinos(c, PinosMbServer);
            sb.Append(Embrulhar(r, "SERVIDOR MODBUS TCP"));

            sb.AppendLine("    </ObjectList>");
            sb.AppendLine("  </SW.Blocks.FB>");
            sb.AppendLine("</Document>");
            return sb.ToString();
        }

        /// <summary>DB com o TCON_IP_v4 do servidor. variante 0 = com valores iniciais; 1 = sem; 2 = sem versao.</summary>
        private static string XmlConexao(string nomeDb, string membro, int variante)
        {
            int iface, porta;
            if (!int.TryParse((Cfg.InterfaceModbus ?? "").Trim(), out iface)) iface = 64;
            if (!int.TryParse((Cfg.PortaModbus ?? "").Trim(), out porta)) porta = 502;
            var sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            sb.AppendLine("<Document>");
            sb.AppendLine("  <Engineering version=\"V20\" />");
            sb.AppendLine("  <SW.Blocks.GlobalDB ID=\"0\">");
            sb.AppendLine("    <AttributeList>");
            sb.AppendLine("      <AutoNumber>true</AutoNumber>");
            sb.AppendLine("      <Interface><Sections xmlns=\"" + NsIf + "\">");
            sb.AppendLine("        <Section Name=\"Static\">");
            string versao = variante == 2 ? "" : " Version=\"1.0\"";
            if (variante == 0)
            {
                sb.AppendLine("          <Member Name=\"" + membro + "\" Datatype=\"TCON_IP_v4\"" + versao + ">");
                sb.AppendLine("            <Comment><MultiLanguageText Lang=\"pt-BR\">CONEXÃO DO SERVIDOR MODBUS TCP</MultiLanguageText></Comment>");
                sb.AppendLine("            <Sections><Section Name=\"None\">");
                sb.AppendLine("              <Member Name=\"InterfaceId\" Datatype=\"HW_ANY\"><StartValue>" + iface + "</StartValue></Member>");
                sb.AppendLine("              <Member Name=\"ID\" Datatype=\"CONN_OUC\"><StartValue>1</StartValue></Member>");
                sb.AppendLine("              <Member Name=\"ConnectionType\" Datatype=\"Byte\"><StartValue>16#0B</StartValue></Member>");
                sb.AppendLine("              <Member Name=\"ActiveEstablished\" Datatype=\"Bool\"><StartValue>false</StartValue></Member>");
                sb.AppendLine("              <Member Name=\"RemoteAddress\" Datatype=\"IP_V4\"" + versao + ">");
                sb.AppendLine("                <Sections><Section Name=\"None\"><Member Name=\"ADDR\" Datatype=\"Array[1..4] of Byte\" /></Section></Sections>");
                sb.AppendLine("              </Member>");
                sb.AppendLine("              <Member Name=\"RemotePort\" Datatype=\"UInt\"><StartValue>0</StartValue></Member>");
                sb.AppendLine("              <Member Name=\"LocalPort\" Datatype=\"UInt\"><StartValue>" + porta + "</StartValue></Member>");
                sb.AppendLine("            </Section></Sections>");
                sb.AppendLine("          </Member>");
            }
            else
            {
                sb.AppendLine("          <Member Name=\"" + membro + "\" Datatype=\"TCON_IP_v4\"" + versao + " />");
            }
            sb.AppendLine("        </Section>");
            sb.AppendLine("      </Sections></Interface>");
            sb.AppendLine("      <MemoryLayout>Optimized</MemoryLayout>");
            sb.AppendLine("      <Name>" + SecurityElement.Escape(nomeDb) + "</Name>");
            sb.AppendLine("      <Namespace />");
            sb.AppendLine("      <ProgrammingLanguage>DB</ProgrammingLanguage>");
            sb.AppendLine("    </AttributeList>");
            sb.AppendLine("  </SW.Blocks.GlobalDB>");
            sb.AppendLine("</Document>");
            return sb.ToString();
        }

        // --- XML do DB COM ---------------------------------------------------------
        private string XmlCom(MapaModbus m)
        {
            var itens = m.Itens();
            Func<int, string> descDe = delegate (int reg)
            {
                var doReg = itens.Where(i => i.Registro == reg && i.Bit < 0 || i.Registro == reg && i.Faixa == "Comandos").ToList();
                if (doReg.Count == 0) return "RESERVA";
                if (doReg[0].Faixa == "Comandos")
                    return doReg.All(i => i.Descricao == "RESERVA") ? "RESERVA" : "COMANDOS BOOLEANOS " + (reg - m.IniCmd + 1);
                return doReg[0].Descricao;
            };

            var sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            sb.AppendLine("<Document>");
            sb.AppendLine("  <Engineering version=\"V20\" />");
            sb.AppendLine("  <SW.Blocks.GlobalDB ID=\"0\">");
            sb.AppendLine("    <AttributeList>");
            sb.AppendLine("      <AutoNumber>false</AutoNumber>");
            sb.AppendLine("      <Interface><Sections xmlns=\"" + NsIf + "\">");
            sb.AppendLine("        <Section Name=\"Static\">");

            Action<string, int, string> membro = delegate (string nome, int reg, string tipo)
            {
                sb.AppendLine("            <Member Name=\"" + nome + "\" Datatype=\"" + tipo + "\" Remanence=\"NonRetain\" Accessibility=\"Public\">");
                sb.AppendLine("              <Comment><MultiLanguageText Lang=\"pt-BR\">" + SecurityElement.Escape(Textos.Padrao(descDe(reg))) + "</MultiLanguageText></Comment>");
                sb.AppendLine("            </Member>");
            };

            sb.AppendLine("          <Member Name=\"ESC\" Datatype=\"Struct\" Remanence=\"NonRetain\" Accessibility=\"Public\">");
            sb.AppendLine("            <Comment><MultiLanguageText Lang=\"pt-BR\">ESCRITA (SUPERVISÓRIO -> CLP)</MultiLanguageText></Comment>");
            for (int r = m.IniCmd; r < m.IniLei; r++) membro(MapaModbus.Escrita(r), r, "Word");
            sb.AppendLine("          </Member>");
            sb.AppendLine("          <Member Name=\"LEI\" Datatype=\"Struct\" Remanence=\"NonRetain\" Accessibility=\"Public\">");
            sb.AppendLine("            <Comment><MultiLanguageText Lang=\"pt-BR\">LEITURA (CLP -> SUPERVISÓRIO)</MultiLanguageText></Comment>");
            for (int r = m.IniLei; r < m.IniDw; r++) membro(MapaModbus.Leitura(r), r, "Word");
            for (int r = m.IniDw; r < m.IniDw + m.QtdDw; r += 2) membro(MapaModbus.Leitura(r), r, "DWord");
            sb.AppendLine("          </Member>");

            sb.AppendLine("        </Section>");
            sb.AppendLine("      </Sections></Interface>");
            sb.AppendLine("      <MemoryLayout>Standard</MemoryLayout>");
            sb.AppendLine("      <Name>" + SecurityElement.Escape(NomeCom) + "</Name>");
            sb.AppendLine("      <Namespace />");
            sb.AppendLine("      <Number>" + NumeroCom + "</Number>");
            sb.AppendLine("      <ProgrammingLanguage>DB</ProgrammingLanguage>");
            sb.AppendLine("    </AttributeList>");
            sb.AppendLine("  </SW.Blocks.GlobalDB>");
            sb.AppendLine("</Document>");
            return sb.ToString();
        }

        // --- XML da FC de mapeamento ---------------------------------------------------
        private static readonly string[][] PinosMbServer =
        {
            new[] {"DISCONNECT","Input","Bool"}, new[] {"MB_HOLD_REG","Input","Variant"},
            new[] {"CONNECT","Input","Variant"},
            new[] {"NDR","Output","Bool"}, new[] {"DR","Output","Bool"},
            new[] {"ERROR","Output","Bool"}, new[] {"STATUS","Output","Word"}
        };

        private string XmlFcCom(string nomeFc, MapaModbus m, string fbEsc, string[][] pinosEsc, string[] pinosWord,
            string[] pinosCmd, string pinoRemLoc, ChamadaMbServer mb)
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
            sb.AppendLine("        <Section Name=\"Temp\">");
            sb.AppendLine("          <Member Name=\"server_ndr\" Datatype=\"Bool\" />");
            sb.AppendLine("          <Member Name=\"server_dr\" Datatype=\"Bool\" />");
            sb.AppendLine("          <Member Name=\"server_error\" Datatype=\"Bool\" />");
            sb.AppendLine("          <Member Name=\"server_status\" Datatype=\"Word\" />");
            foreach (string aux in m.AuxCon.Values.Distinct())
                sb.AppendLine("          <Member Name=\"" + aux + "\" Datatype=\"Int\" />");
            sb.AppendLine("        </Section>");
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

            string com = NomeCom;
            // remoto/local: sempre o contato configurado (no REM_LOC_CMD e no set de velocidade)
            string remLoc = string.IsNullOrWhiteSpace(Cfg.ContatoRemLoc) ? null : Cfg.ContatoRemLoc.Trim();
            Func<int, string> esc = delegate (int reg) { return com + ".ESC." + MapaModbus.Escrita(reg); };
            Func<int, string> lei = delegate (int reg) { return com + ".LEI." + MapaModbus.Leitura(reg); };

            // 1) MB_SERVER (dentro do FB do gerador, ou direto se ja havia uma instancia global dele)
            if (mb != null && mb.Embrulhado)
            {
                var r = new Rede();
                int c = r.Chamada(mb.Fb, mb.Instancia, PinosFbServidor);
                r.ParaTrilho(c, "en");
                r.Saida(c, "NDR", Rede.Ident(r.Acesso("LOC|server_ndr")));
                r.Saida(c, "DR", Rede.Ident(r.Acesso("LOC|server_dr")));
                r.Saida(c, "ERROR", Rede.Ident(r.Acesso("LOC|server_error")));
                r.Saida(c, "STATUS", Rede.Ident(r.Acesso("LOC|server_status")));
                r.FecharPinos(c, PinosFbServidor);
                sb.Append(Embrulhar(r, "BLOCO DE COMUNICAÇÃO"));
            }
            else if (mb != null)
            {
                var r = new Rede();
                int c = r.Parte("MB_SERVER", mb.Versao, mb.Instancia);
                r.ParaTrilho(c, "en");
                r.Entrada(c, "DISCONNECT", Rede.Ident(r.Acesso("LIT|Bool|false")));
                // mesma forma que o TIA aceitou no FB do servidor (ou, sem ela, a constante sem tipo)
                string hold = _formaHoldOk ?? ("LIT0|P#DB" + NumeroCom + ".DBX0.0 BYTE " + m.TotalBytes);
                r.Entrada(c, "MB_HOLD_REG", Rede.Ident(r.Acesso(hold)));
                if (!string.IsNullOrEmpty(mb.Conexao)) r.Entrada(c, "CONNECT", Rede.Ident(r.Acesso("SYM|" + mb.Conexao)));
                r.Saida(c, "NDR", Rede.Ident(r.Acesso("LOC|server_ndr")));
                r.Saida(c, "DR", Rede.Ident(r.Acesso("LOC|server_dr")));
                r.Saida(c, "ERROR", Rede.Ident(r.Acesso("LOC|server_error")));
                r.Saida(c, "STATUS", Rede.Ident(r.Acesso("LOC|server_status")));
                r.FecharPinos(c, PinosMbServer);
                sb.Append(Embrulhar(r, "BLOCO DE COMUNICAÇÃO"));
            }

            // 2) comandos: um BL_ESCRITA_COMANDO por 2 words (32 comandos)
            for (int bl = 0; bl < m.BlocosComando; bl++)
            {
                var r = new Rede();
                int call = r.Chamada(FbReal(fbEsc), InstanciaEscrita(bl + 1), pinosEsc);
                r.ParaTrilho(call, "en");
                r.Entrada(call, pinosWord[0], Rede.Ident(r.Acesso("SYM|" + esc(m.IniCmd + bl * 2))));
                r.Entrada(call, pinosWord[1], Rede.Ident(r.Acesso("SYM|" + esc(m.IniCmd + bl * 2 + 1))));
                if (pinoRemLoc != null)
                    r.Entrada(call, pinoRemLoc, remLoc != null
                        ? Rede.Ident(r.Acesso("SYM|" + remLoc))
                        : Rede.Ident(r.Acesso("LIT|Bool|true")));
                for (int k = 0; k < 32 && k < pinosCmd.Length; k++)
                {
                    int i = bl * 32 + k;
                    if (i >= m.Comandos.Count) break;
                    r.Entrada(call, pinosCmd[k], Rede.Ident(r.Acesso("SYM|" + m.Comandos[i].Key)));
                }
                r.FecharPinos(call, pinosEsc);
                sb.Append(Embrulhar(r, "COMANDOS " + (m.IniCmd + bl * 2) + "-" + (m.IniCmd + bl * 2 + 1)));
            }

            // 3) set de velocidade (so em remoto)
            if (m.Escritas.Count > 0)
            {
                var r = new Rede();
                string en = null;
                if (remLoc != null)
                {
                    int c = r.Contato(remLoc, false);
                    r.ParaTrilho(c, "in");
                    en = Rede.Pino(c, "out");
                }
                for (int i = 0; i < m.Escritas.Count; i++)
                {
                    string aux;
                    if (m.AuxCon.TryGetValue(m.Escritas[i].Key, out aux))
                    {
                        // SET_CON do preparador: MOVE word -> #AUX_xxx_CON e DIV #AUX_xxx_CON / 100.0 -> SET_CON
                        r.Mover(en, Rede.Ident(r.Acesso("SYM|" + esc(m.IniEsc + i))), Rede.Ident(r.Acesso("LOC|" + aux)));
                        r.Dividir(en, Rede.Ident(r.Acesso("LOC|" + aux)), Rede.Ident(r.Acesso("LIT|Real|100.0")),
                            Rede.Ident(r.Acesso("SYM|" + m.Escritas[i].Key)));
                    }
                    else
                        r.Mover(en, Rede.Ident(r.Acesso("SYM|" + esc(m.IniEsc + i))), Rede.Ident(r.Acesso("SYM|" + m.Escritas[i].Key)));
                }
                sb.Append(Embrulhar(r, "SET VELOCIDADE"));
            }

            // 4) leituras: status, velocidades, niveis, defeitos e status words
            if (m.Leituras.Count > 0)
            {
                var r = new Rede();
                for (int i = 0; i < m.Leituras.Count; i++)
                    r.Mover(null, Rede.Ident(r.Acesso("SYM|" + m.Leituras[i].Key)), Rede.Ident(r.Acesso("SYM|" + lei(m.IniLei + i))));
                sb.Append(Embrulhar(r, "STATUS, LEITURAS, DEFEITOS E STATUS WORDS"));
            }

            // 5) bits das status words (nivel baixo/alto dos sensores)
            if (m.BitsStatus.Count > 0)
            {
                var r = new Rede();
                for (int k = 0; k < m.BitsStatus.Count; k++)
                {
                    int ct = r.Contato(m.BitsStatus[k].Key, false);
                    r.ParaTrilho(ct, "in");
                    int bob = r.Bobina("Coil", MapaModbus.NomeStatusWord(k / 16 + 1) + ".%X" + (k % 16));
                    r.Fio(Rede.Pino(ct, "out"), Rede.Pino(bob, "in"));
                }
                sb.Append(Embrulhar(r, "STATUS WORD"));
            }

            // 6) DWords: analiticos, vazao e totalizadores
            if (m.DWords.Count > 0)
            {
                var r = new Rede();
                for (int i = 0; i < m.DWords.Count; i++)
                    r.Mover(null, Rede.Ident(r.Acesso("SYM|" + m.DWords[i].Key)), Rede.Ident(r.Acesso("SYM|" + lei(m.IniDw + i * 2))));
                sb.Append(Embrulhar(r, "ANALÍTICAS E VAZÕES"));
            }

            sb.AppendLine("    </ObjectList>");
            sb.AppendLine("  </SW.Blocks.FC>");
            sb.AppendLine("</Document>");
            return sb.ToString();
        }
        // Helpers locais da rotina de comunicacao. Mantidos com nomes proprios
        // para nao depender de metodos auxiliares de outras partes parciais.
        private static PlcBlockGroup Comunicacao_ObterSubgrupo(PlcBlockGroup grupo, string caminho)
        {
            PlcBlockGroup atual = grupo;
            foreach (string nome in caminho.Split('\\'))
            {
                PlcBlockUserGroup sub = atual.Groups.Find(nome) ?? atual.Groups.Create(nome);
                atual = sub;
            }
            return atual;
        }

        private static bool Comunicacao_GrupoEstaDentroDe(PlcBlockGroup candidato, PlcBlockGroup raiz)
        {
            if (candidato == null || raiz == null) return false;
            if (ReferenceEquals(candidato, raiz)) return true;
            foreach (PlcBlockGroup sub in raiz.Groups)
                if (Comunicacao_GrupoEstaDentroDe(candidato, sub)) return true;
            return false;
        }

    }
}