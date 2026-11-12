using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace Genesis
{
    /// <summary>
    /// Atualiza o DB do subsistema JA EXISTENTE a partir do XML exportado pelo TIA:
    /// acrescenta so os membros que faltam (equipamento, partida e SI&lt;n&gt;). Nada e removido,
    /// e o que o usuario editou (valores iniciais, comentarios) continua la.
    /// </summary>
    internal static class AtualizadorXml
    {
        // ===================================================================
        //  DB global
        // ===================================================================

        /// <summary>
        /// Garante que cada partida tenha seu membro (UDT) e o SI&lt;n&gt; no DB. Se nada faltar,
        /// nao grava arquivo e devolve adicionadas = 0.
        /// </summary>
        public static void AtualizarDb(
            string arquivoEntrada, string arquivoSaida, IList<Partida> partidas,
            Func<Partida, string> udtDe, out int adicionadas, Func<Partida, bool> comSi = null,
            Func<Partida, string> itmDe = null, string udtItm = null,
            Func<Partida, string> exaDe = null, string udtExa = null,
            Func<Partida, XNamespace, IEnumerable<XElement>> extrasDe = null)
        {
            XDocument doc = XDocument.Load(arquivoEntrada);
            XElement secao = SecaoStatic(doc);
            XNamespace ns = secao.Name.Namespace;
            adicionadas = 0;

            foreach (Partida p in partidas)
            {
                XElement eq = Membros(secao).FirstOrDefault(m => (string)m.Attribute("Name") == p.Eq);
                if (eq == null)
                {
                    eq = new XElement(ns + "Member",
                        new XAttribute("Name", p.Eq),
                        new XAttribute("Datatype", "Struct"),
                        new XAttribute("Remanence", "Retain"),
                        new XAttribute("Accessibility", "Public"));
                    secao.Add(eq);
                }

                if (!Membros(eq).Any(m => (string)m.Attribute("Name") == p.Fn))
                {
                    int proximoSi = MaiorSi(eq) + 1;
                    eq.Add(MembroUdt(ns, p, udtDe(p)));
                    if (comSi == null || comSi(p)) eq.Add(MembroSi(ns, proximoSi));   // sensor nao tem SI
                    adicionadas++;
                }

                // intermitente da partida (struct ITM / IT<n> com a UDT_INTERMITENTE), tambem em partida que ja existia
                string itm = itmDe != null ? itmDe(p) : null;
                if (itm != null && !Membros(eq).Any(m => (string)m.Attribute("Name") == itm))
                {
                    var mi = new XElement(ns + "Member",
                        new XAttribute("Name", itm),
                        new XAttribute("Datatype", "\"" + udtItm + "\""));
                    mi.Add(Comentario(ns, ("INTERMITENTE " + (p.Descricao ?? "")).Trim()));
                    eq.Add(mi);
                    adicionadas++;
                }

                // extras (ex.: maquinas: temporizador do automatico, SIS de visualizacao), se ainda nao existem
                if (extrasDe != null)
                {
                    foreach (XElement extra in extrasDe(p, ns))
                    {
                        string nomeExtra = (string)extra.Attribute("Name");
                        if (Membros(eq).Any(m => (string)m.Attribute("Name") == nomeExtra)) continue;
                        eq.Add(extra);
                        adicionadas++;
                    }
                }

                // exaustor da cabine (struct EXA / EX<n> com a UDT_PARTIDAS), tambem em partida que ja existia
                string exa = exaDe != null ? exaDe(p) : null;
                if (exa != null && !Membros(eq).Any(m => (string)m.Attribute("Name") == exa))
                {
                    var me = new XElement(ns + "Member",
                        new XAttribute("Name", exa),
                        new XAttribute("Datatype", "\"" + udtExa + "\""));
                    me.Add(Comentario(ns, ("EXAUSTOR " + (p.Descricao ?? "")).Trim()));
                    eq.Add(me);
                    adicionadas++;
                }
            }

            if (adicionadas > 0) Salvar(doc, arquivoSaida);
        }

        /// <summary>
        /// Numero do SI&lt;n&gt; de cada partida ("EQ.FN" -> n): e o SI que vem logo depois do membro da partida,
        /// como o gerador grava e como estao os DBs reais (BB1, SI1, BB2, SI2...).
        /// </summary>
        public static Dictionary<string, int> MapaSi(string arquivoDb, ICollection<string> udts)
        {
            var mapa = new Dictionary<string, int>();
            XDocument doc = XDocument.Load(arquivoDb);
            XElement secao = SecaoStatic(doc);

            foreach (XElement eq in Membros(secao).Where(m => (string)m.Attribute("Datatype") == "Struct"))
            {
                string nomeEq = (string)eq.Attribute("Name");
                List<XElement> filhos = Membros(eq).ToList();

                for (int i = 0; i + 1 < filhos.Count; i++)
                {
                    string tipo = ((string)filhos[i].Attribute("Datatype") ?? "").Trim('"');
                    if (!udts.Contains(tipo)) continue;

                    Match m = Regex.Match((string)filhos[i + 1].Attribute("Name") ?? "", @"^SI(\d+)$");
                    if (m.Success)
                        mapa[nomeEq + "." + (string)filhos[i].Attribute("Name")] = int.Parse(m.Groups[1].Value);
                }
            }
            return mapa;
        }

        private static XElement SecaoStatic(XDocument doc)
        {
            return doc.Descendants().First(e =>
                e.Name.LocalName == "Section" && (string)e.Attribute("Name") == "Static");
        }

        private static IEnumerable<XElement> Membros(XElement pai)
        {
            return pai.Elements().Where(e => e.Name.LocalName == "Member");
        }

        private static int MaiorSi(XElement eq)
        {
            int max = 0;
            foreach (XElement m in Membros(eq))
            {
                Match r = Regex.Match((string)m.Attribute("Name") ?? "", @"^SI(\d+)$");
                if (r.Success) max = Math.Max(max, int.Parse(r.Groups[1].Value));
            }
            return max;
        }

        private static XElement MembroUdt(XNamespace ns, Partida p, string nomeUdt)
        {
            var m = new XElement(ns + "Member",
                new XAttribute("Name", p.Fn),
                new XAttribute("Datatype", "\"" + nomeUdt + "\""));

            if (!string.IsNullOrEmpty(p.Descricao))
                m.Add(Comentario(ns, p.Descricao));
            return m;
        }

        private static XElement MembroSi(XNamespace ns, int n)
        {
            return new XElement(ns + "Member",
                new XAttribute("Name", "SI" + n),
                new XAttribute("Datatype", "Struct"),
                Comentario(ns, "VISIBILIDADE"),
                new XElement(ns + "Member",
                    new XAttribute("Name", "VIS"),
                    new XAttribute("Datatype", "Struct"),
                    new XElement(ns + "Member",
                        new XAttribute("Name", "OFF"),
                        new XAttribute("Datatype", "Bool"))));
        }

        private static XElement Comentario(XNamespace ns, string texto)
        {
            return new XElement(ns + "Comment",
                new XElement(ns + "MultiLanguageText", new XAttribute("Lang", "pt-BR"), Textos.Padrao(texto)));
        }

        // ===================================================================
        private static void Salvar(XDocument doc, string arquivo)
        {
            var cfg = new XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = true };
            using (XmlWriter w = XmlWriter.Create(arquivo, cfg))
                doc.Save(w);
        }
    }
}