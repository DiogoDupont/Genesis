using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Genesis
{
    /// <summary>Uma linha do flotador que continua depois de reconfigurar: a atual, a que as opcoes novas gerariam e a que as opcoes antigas geravam.</summary>
    internal class FlotMantida
    {
        public Partida Atual, Nova, PadraoAntigo;
    }

    /// <summary>O que muda num flotador quando ele e reconfigurado (nada e alterado ate aplicar).</summary>
    internal class PlanoFlotador
    {
        public string DbAntigo, DbNovo;
        public OpcoesFlotador Antigas, Novas;
        public List<FlotMantida> Mantidas = new List<FlotMantida>();
        public List<Partida> Novos = new List<Partida>();       // linhas que passam a existir
        public List<Partida> Removidas = new List<Partida>();   // linhas atuais que deixam de existir
    }

    internal static partial class Maquinas
    {
        // --- reconfigurar um flotador ja criado -------------------------------------

        /// <summary>Linha que pertence so a este flotador (os sensores de tanque compartilhados, SEN.EQU, SEN.LFL e SEN.&lt;quimico&gt;, nao).</summary>
        private static bool PropriaDoFlotador(Partida x, string id)
        {
            return string.Equals(x.Maquina, id, StringComparison.OrdinalIgnoreCase)
                && (string.Equals(x.Sub, id, StringComparison.OrdinalIgnoreCase)
                    || (x.Sub == "SEN" && string.Equals(x.Eq, id, StringComparison.OrdinalIgnoreCase)));
        }

        private static string TrocarPalavra(string texto, string de, string para)
        {
            if (string.IsNullOrEmpty(texto) || string.IsNullOrEmpty(de)) return texto;
            return Regex.Replace(texto, "(?<![A-Za-z0-9])" + Regex.Escape(de) + "(?![A-Za-z0-9])", para, RegexOptions.IgnoreCase);
        }

        /// <summary>Le as opcoes atuais de um flotador a partir das linhas dele (a janela abre com elas).</summary>
        public static OpcoesFlotador OpcoesDoFlotador(string id, IEnumerable<Partida> todas)
        {
            List<Partida> tudo = todas.ToList();
            List<Partida> meus = tudo.Where(x => PropriaDoFlotador(x, id)).ToList();
            Func<string, string, string, Partida> um = (sub, eq, fn) => tudo.FirstOrDefault(x =>
                string.Equals(x.Sub, sub, StringComparison.OrdinalIgnoreCase) && string.Equals(x.Eq, eq, StringComparison.OrdinalIgnoreCase)
                && string.Equals(x.Fn, fn, StringComparison.OrdinalIgnoreCase) && (sub == "SEN" || string.Equals(x.Maquina, id, StringComparison.OrdinalIgnoreCase)));
            Func<string, List<Partida>> grupo = eq => meus.Where(x => x.Sub == id && string.Equals(x.Eq, eq, StringComparison.OrdinalIgnoreCase)
                && (x.Fn == "BBA" || Regex.IsMatch(x.Fn ?? "", "^BB[1-3]$"))).OrderBy(x => x.Fn).ToList();

            var o = new OpcoesFlotador { Sub = id };

            // descricao: sai das descricoes padrao ("MEDIDOR VAZÃO <desc>", "RASPADOR <desc>"...)
            string desc = null;
            Func<string, string, string> pega = delegate (string padrao, string texto)
            {
                Match m = Regex.Match(texto ?? "", padrao);
                return m.Success ? m.Groups[1].Value.Trim() : null;
            };
            Partida pVaz = meus.FirstOrDefault(x => x.Sub == "SEN" && x.Fn == "VAZ"), pRas = meus.FirstOrDefault(x => x.Eq == "RAS" && x.Fn == "MOT"),
                pPh = meus.FirstOrDefault(x => x.Sub == "SEN" && x.Fn == "PHI");
            if (pVaz != null) desc = pega("^MEDIDOR VAZ[ÃA]O (.+)$", pVaz.Descricao);
            if (desc == null && pRas != null) desc = pega("^RASPADOR (.+)$", pRas.Descricao);
            if (desc == null && pPh != null) desc = pega("^MEDIDOR PH (.+)$", pPh.Descricao);
            if (desc == null)
            {
                Partida a1 = grupo("ALM").FirstOrDefault();
                if (a1 != null) desc = pega("^BOMBA ALIMENTA[ÇC][ÃA]O (.+?)(?: \\d+)?$", a1.Descricao);
            }
            o.Descricao = desc ?? ("FLOTADOR " + new string(id.Where(char.IsDigit).ToArray())).Trim();

            // equalizacao / lodo (os tanques sao compartilhados: vale o que existe no projeto)
            Partida agtEqu = um(id, "EQU", "AGT");
            o.AgitEqualizacao = agtEqu != null; if (agtEqu != null) o.TipoAgitEqu = agtEqu.Tipo;
            o.NivelEqualizacao = um("SEN", "EQU", "TAN") != null ? QuimicoFlotador.NivelAnalogico
                : um("SEN", "EQU", "TBA") != null ? QuimicoFlotador.NivelDigital : QuimicoFlotador.NivelNenhum;
            o.NivelLodo = um("SEN", "LFL", "TAN") != null ? QuimicoFlotador.NivelAnalogico : QuimicoFlotador.NivelNenhum;

            // floculador
            List<Partida> fme = meus.Where(x => x.Sub == id && x.Eq == "FME" && Regex.IsMatch(x.Fn ?? "", "^AG[12]$")).OrderBy(x => x.Fn).ToList();
            o.Floculadores = fme.Count;
            if (fme.Count > 0)
            {
                o.TipoFloculador = fme[0].Tipo;
                o.FloculadorIntermitente = fme[0].Intermitente ?? ""; o.FloculadorIntermitenteDes = fme[0].IntermitenteDes ?? "";
            }
            else { o.FloculadorIntermitente = ""; o.FloculadorIntermitenteDes = ""; }

            // bombas e agitadores
            Action<string, Action<bool>, Action<string>, Action<int>, Action<bool>> bombas = delegate (string eq, Action<bool> tem, Action<string> tipo, Action<int> qtd, Action<bool> alt)
            {
                List<Partida> g = grupo(eq);
                tem(g.Count > 0);
                if (g.Count == 0) return;
                tipo(g[0].Tipo); qtd(g.Count); alt(g.Any(x => x.Alternancia));
            };
            bombas("ALM", v => o.Alimentacao = v, v => o.TipoAlim = v, v => o.QtdAlim = v, v => o.AltAlim = v);
            bombas("MIC", v => o.Microbolhas = v, v => o.TipoMic = v, v => o.QtdMic = v, v => o.AltMic = v);
            bombas("LFL", v => o.BombaLodo = v, v => o.TipoBombaLodo = v, v => o.QtdLodo = v, v => o.AltLodo = v);
            bombas("CLA", v => o.Clarificado = v, v => o.TipoClarificado = v, v => o.QtdCla = v, v => o.AltCla = v);
            Partida agtLodo = um(id, "LFL", "AGT");
            o.AgitLodo = agtLodo != null; if (agtLodo != null) o.TipoAgitLodo = agtLodo.Tipo;

            // raspadores
            o.Raspador = pRas != null;
            if (pRas != null)
            {
                o.TipoRas = pRas.Tipo;
                o.RaspadorIntermitente = pRas.Intermitente ?? ""; o.RaspadorIntermitenteDes = pRas.IntermitenteDes ?? "";
            }
            else { o.RaspadorIntermitente = ""; o.RaspadorIntermitenteDes = ""; }
            o.SensoresPas = meus.Any(x => x.Sub == "SEN" && x.Fn == "RAS");
            Partida rac = um(id, "RAC", "MOT");
            o.RaspadorCircular = rac != null; if (rac != null) o.TipoRasCircular = rac.Tipo;
            o.MedidorVazao = pVaz != null;

            // quimicos
            foreach (QuimicoFlotador q in o.Quimicos)
            {
                List<Partida> bb = grupo(q.Codigo);
                Partida agt = um(id, q.Codigo, "AGT");
                Partida pid = um(id, q.Codigo, "PID");
                q.Incluir = bb.Count > 0 || agt != null || pid != null;
                if (!q.Incluir) continue;
                q.Bombas = Math.Max(1, bb.Count);
                if (bb.Count > 0)
                {
                    q.TipoBomba = bb[0].Tipo;
                    q.Alternancia = bb.Any(x => x.Alternancia);
                    q.Modulacao = bb.Any(x => !string.IsNullOrEmpty(x.ModPid)) || pid != null ? QuimicoFlotador.ModPid
                        : bb.Any(x => !string.IsNullOrEmpty(x.ModVazao)) ? QuimicoFlotador.ModVazao : QuimicoFlotador.ModNenhuma;
                    // nivel do tanque: sai da condicao de intertravamento (SEN.<tanque>.TAN|TBA.LEI_BAI)
                    q.Nivel = QuimicoFlotador.NivelNenhum;
                    foreach (string linha in (bb[0].CondicoesInt ?? "").Split('\n'))
                    {
                        Match m = Regex.Match(linha.Trim(), @"^SEN\.([A-Za-z0-9_]+)\.(TAN|TBA)\.LEI_BAI$", RegexOptions.IgnoreCase);
                        if (!m.Success || string.Equals(m.Groups[1].Value, id, StringComparison.OrdinalIgnoreCase)) continue;
                        bool ana = string.Equals(m.Groups[2].Value, "TAN", StringComparison.OrdinalIgnoreCase);
                        if (string.Equals(m.Groups[1].Value, q.Codigo, StringComparison.OrdinalIgnoreCase))
                            q.Nivel = ana ? QuimicoFlotador.NivelAnalogico : QuimicoFlotador.NivelDigital;
                        else
                            q.Nivel = QuimicoFlotador.NivelDoPreparador + m.Groups[1].Value.ToUpperInvariant();
                        break;
                    }
                }
                q.Agitador = agt != null; if (agt != null) q.TipoAgitador = agt.Tipo;
            }
            return o;
        }

        /// <summary>
        /// Compara o flotador atual com as opcoes novas: o que continua (casando por DB/grupo/nome, com BBA = BB1), o que e novo
        /// e o que sai. So entram como "sai" as linhas que as opcoes antigas geravam: itens colocados a mao ficam como estao.
        /// Sensores de tanque compartilhados nunca saem.
        /// </summary>
        public static PlanoFlotador PlanejarFlotador(string id, IEnumerable<Partida> todas, OpcoesFlotador novas)
        {
            List<Partida> tudo = todas.ToList();
            List<Partida> proprias = tudo.Where(x => PropriaDoFlotador(x, id)).ToList();
            List<Partida> fora = tudo.Where(x => !proprias.Contains(x)).ToList();
            novas.Sub = (novas.Sub ?? "").Trim().ToUpperInvariant();
            var plano = new PlanoFlotador { DbAntigo = id, DbNovo = novas.Sub, Antigas = OpcoesDoFlotador(id, tudo), Novas = novas };

            List<Partida> padraoAntigo = CriarFlotador(plano.Antigas, fora);
            List<Partida> geradas = CriarFlotador(novas, fora);
            Func<string, string> mapa = s => string.Equals(s, plano.DbNovo, StringComparison.OrdinalIgnoreCase) ? id : s;   // espaco do DB antigo
            Func<string, string, string, string> chave = (s, e, f) => (s + "|" + e + "|" + f).ToUpperInvariant();

            var padraoPor = new Dictionary<string, Partida>();
            foreach (Partida p in padraoAntigo) padraoPor[chave(p.Sub, p.Eq, p.Fn)] = p;
            var propriaPor = new Dictionary<string, Partida>();
            foreach (Partida p in proprias) propriaPor[chave(p.Sub, p.Eq, p.Fn)] = p;
            var usadas = new HashSet<Partida>();

            foreach (Partida g in geradas)
            {
                string k = chave(mapa(g.Sub), mapa(g.Eq), g.Fn);
                Partida atual;
                if (!propriaPor.TryGetValue(k, out atual) || usadas.Contains(atual))
                {
                    // 1 bomba = BBA; varias = BB1.. : a BBA vira BB1 (e o contrario)
                    string alt = g.Fn == "BBA" ? "BB1" : g.Fn == "BB1" ? "BBA" : null;
                    atual = null;
                    string ka = alt == null ? null : chave(mapa(g.Sub), mapa(g.Eq), alt);
                    Partida candidata;
                    if (ka != null && propriaPor.TryGetValue(ka, out candidata) && !usadas.Contains(candidata) && !geradas.Any(x => chave(mapa(x.Sub), mapa(x.Eq), x.Fn) == ka))
                    {
                        atual = candidata; k = ka;
                    }
                }
                if (atual == null) { plano.Novos.Add(g); continue; }
                usadas.Add(atual);
                Partida padrao;
                padraoPor.TryGetValue(k, out padrao);
                plano.Mantidas.Add(new FlotMantida { Atual = atual, Nova = g, PadraoAntigo = padrao });
            }
            foreach (var par in padraoPor)
            {
                Partida p;
                if (propriaPor.TryGetValue(par.Key, out p) && !usadas.Contains(p)) plano.Removidas.Add(p);
            }
            return plano;
        }

        /// <summary>Uniao das linhas de dois textos de condicoes: tira o que as opcoes antigas tinham e as novas nao, poe o que so as novas tem.</summary>
        private static string MesclarLinhas(string atual, string padraoAntigo, string padraoNovo)
        {
            Func<string, List<string>> lin = s => (s ?? "").Split('\n').Select(x => x.Trim()).Where(x => x.Length > 0).ToList();
            List<string> a = lin(atual), velho = lin(padraoAntigo), novo = lin(padraoNovo);
            Func<List<string>, string, bool> tem = (l, x) => l.Any(y => string.Equals(y, x, StringComparison.OrdinalIgnoreCase));
            a = a.Where(x => !(tem(velho, x) && !tem(novo, x))).ToList();
            foreach (string x in novo) if (!tem(a, x)) a.Add(x);
            return string.Join("\n", a.ToArray());
        }

        /// <summary>
        /// Aplica nas linhas que continuam so o que as opcoes mudaram (tipo, intermitente, alternancia, modulacao, intertravamento...),
        /// preservando o que voce editou a mao. Tudo e escrito com o DB antigo: a troca de nome vem depois (RenomearDb).
        /// </summary>
        public static void AplicarNasMantidasFlotador(PlanoFlotador plano)
        {
            bool renomeia = !string.Equals(plano.DbAntigo, plano.DbNovo, StringComparison.OrdinalIgnoreCase);
            Func<string, string> velho = s => renomeia ? TrocarPalavra(s, plano.DbNovo, plano.DbAntigo) : s;
            Func<string, string, bool> igual = (a, b) => string.Equals(a ?? "", b ?? "", StringComparison.Ordinal);

            foreach (FlotMantida m in plano.Mantidas)
            {
                Partida a = m.Atual, n = m.Nova, p = m.PadraoAntigo;
                if (p == null) continue;   // linha que as opcoes antigas nao geravam: nao mexe

                if (!igual(p.Tipo, n.Tipo)) a.Tipo = n.Tipo;
                if (!igual(p.Descricao, n.Descricao) && igual(a.Descricao, p.Descricao)) a.Descricao = n.Descricao;   // descricao editada a mao fica
                if (!igual(p.Intermitente, n.Intermitente)) a.Intermitente = n.Intermitente;
                if (!igual(p.IntermitenteDes, n.IntermitenteDes)) a.IntermitenteDes = n.IntermitenteDes;
                if (p.Alternancia != n.Alternancia) a.Alternancia = n.Alternancia;
                if (!igual(p.ModVazao, velho(n.ModVazao))) a.ModVazao = velho(n.ModVazao);
                if (!igual(p.ModPid, velho(n.ModPid))) a.ModPid = velho(n.ModPid);
                if (!igual(p.TagSensor, velho(n.TagSensor)) && igual(a.TagSensor, p.TagSensor)) a.TagSensor = velho(n.TagSensor);
                if (p.PidInverso != n.PidInverso) a.PidInverso = n.PidInverso;

                if (!igual(p.CondicoesInt, velho(n.CondicoesInt)))
                    a.CondicoesInt = MesclarLinhas(a.CondicoesInt, p.CondicoesInt, velho(n.CondicoesInt));
                if (p.Intertravamento != n.Intertravamento) a.Intertravamento = n.Intertravamento;
                if (!igual(p.DefeitosExtras, velho(n.DefeitosExtras)))
                    a.DefeitosExtras = MesclarLinhas(a.DefeitosExtras, p.DefeitosExtras, velho(n.DefeitosExtras));
            }
        }
    }
}
