using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Siemens.Engineering;
using Siemens.Engineering.SW;
using Siemens.Engineering.SW.Blocks;

namespace Genesis
{
    // =======================================================================
    //  PIDs direto no OB de interrupcao ciclica (OB30)
    //  Em vez de uma FC G_PID_<DB> por subsistema (para chamar no OB), as redes de TODOS os PIDs
    //  do projeto sao escritas nas redes do proprio OB. O OB existente e exportado, so as redes sao
    //  trocadas (ficam o tempo de ciclo e as outras propriedades) e ele e importado por cima.
    // =======================================================================
    internal partial class Gerador
    {
        private static int NumeroObPid
        {
            get { int n; return int.TryParse((Cfg.NumeroObPid ?? "").Trim(), out n) && n > 0 ? n : 30; }
        }

        private static PlcBlock AcharObPorNumero(PlcBlockGroup grupo, int numero, out PlcBlockGroup onde)
        {
            foreach (PlcBlock b in grupo.Blocks)
            {
                if (b is OB && b.Number == numero) { onde = grupo; return b; }
            }
            foreach (PlcBlockGroup sub in grupo.Groups)
            {
                PlcBlock achado = AcharObPorNumero(sub, numero, out onde);
                if (achado != null) return achado;
            }
            onde = null;
            return null;
        }

        /// <summary>
        /// Escreve as redes de todos os PIDs do projeto no OB ciclico. garantirTo = PIDs cujo objeto
        /// tecnologico ainda pode faltar (Gerar marcadas); null = ja foram garantidos.
        /// </summary>
        private bool GerarPidsNoOb(PlcSoftware plc, PlcBlockUserGroup grupo, List<Partida> todas, string saida,
            IEnumerable<Partida> garantirTo)
        {
            List<Partida> pids = todas.Where(EhPid).ToList();
            if (pids.Count == 0) return true;
            int numero = NumeroObPid;
            Log("--- PIDs no OB" + numero + " (" + pids.Count + " PID(s)) ---");

            if (garantirTo != null && !GarantirPids(plc, garantirTo)) return false;

            PlcBlockGroup ondeOb;
            PlcBlock ob = AcharObPorNumero(plc.BlockGroup, numero, out ondeOb);
            string nomeOb = "Cyclic interrupt";
            string xmlBase = null;

            if (ob != null)
            {
                nomeOb = ob.Name;
                string arqAtual = Path.Combine(saida, "OB" + numero + "_atual.xml");
                string erro = ExportarBloco(ob, arqAtual);
                if (erro == null && File.Exists(arqAtual) && new FileInfo(arqAtual).Length > 0)
                {
                    xmlBase = File.ReadAllText(arqAtual, Encoding.UTF8);
                    try
                    {
                        string pasta = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                            "Genesis", "Backup", DateTime.Now.ToString("yyyyMMdd_HHmmss"));
                        Directory.CreateDirectory(pasta);
                        File.Copy(arqAtual, Path.Combine(pasta, "OB" + numero + "_antigo.xml"), true);
                        Log("Copia de seguranca do " + nomeOb + " (OB" + numero + "): " + pasta);
                    }
                    catch { /* so a copia */ }
                }
                else
                {
                    // OB inconsistente (o TIA nao exporta): como so tem os PIDs, apaga e refaz
                    Log("AVISO: nao consegui exportar o OB" + numero + " (" + (erro ?? "XML vazio") + "); apagando e criando de novo.");
                    Log("      O tempo de ciclo do OB volta ao padrao: confira em Propriedades > Cyclic interrupt.");
                    ob.Delete();
                    ob = null;
                    ondeOb = null;
                }
            }
            else
            {
                Log("O OB" + numero + " nao existe: criando (confira o tempo de ciclo em Propriedades > Cyclic interrupt).");
            }

            var versoes = new List<string>();
            foreach (string v in new[] { _versaoPid, Cfg.VersaoPid }.Concat(VersoesPid))
                if (!string.IsNullOrWhiteSpace(v) && !versoes.Contains(v.Trim())) versoes.Add(v.Trim());

            string arqNovo = Path.Combine(saida, "OB" + numero + "_novo.xml");
            PlcBlockGroup destino = ondeOb ?? plc.BlockGroup;
            Exception ultimo = null;
            bool entrou = false;
            foreach (string v in versoes)
            {
                _versaoPid = v;
                File.WriteAllText(arqNovo, XmlObPid(pids, xmlBase, numero), new UTF8Encoding(false));
                try
                {
                    ImportarArquivo(destino, arqNovo, ImportOptions.Override);
                    entrou = true;
                    break;
                }
                catch (Exception ex) { ultimo = ex; }
            }
            if (!entrou)
            {
                Log("ERRO ao importar o OB" + numero + ": " + (ultimo == null ? "?" : DetalheErro(ultimo)));
                Log("      O OB antigo (se existia) ficou como estava. Mande esta mensagem se nao souber o que e.");
                return false;
            }
            Log("OB" + numero + " (" + nomeOb + "): redes dos " + pids.Count + " PID(s) geradas.");

            // as FCs G_PID_<DB> do modo antigo deixam de ser chamadas: apaga as que o Genesis tinha gerado
            foreach (string sub in pids.Select(p => p.Sub).Distinct())
            {
                string fc = NomeFcPidDe(sub);
                PlcBlockGroup onde;
                PlcBlock b = AcharBloco(plc.BlockGroup, fc, out onde);
                if (b != null && GrupoEstaDentroDe(onde, grupo))
                {
                    Log("Apagando a FC " + fc + " do modo antigo (os PIDs agora estao no OB" + numero + ").");
                    b.Delete();
                }
            }

            bool ok = CompilarPorNome(plc, new List<string> { nomeOb });
            Log(">>> Confira o tempo de ciclo do OB" + numero + ": o PID_Compact precisa rodar em tempo fixo.");
            Log("");
            return ok;
        }

        /// <summary>XML do OB: do exportado (so as redes sao trocadas) ou, se nao existir, de um OB novo.</summary>
        private string XmlObPid(List<Partida> pids, string xmlBase, int numero)
        {
            _id = 4096;   // acima dos IDs de texto do OB exportado
            var redes = new StringBuilder();
            foreach (var g in pids.GroupBy(p => p.Sub))
            {
                var sg = new SubsistemaGerado { Sub = g.Key, NomeDb = Cfg.Prefixo + g.Key, Partidas = g.ToList() };
                foreach (Partida pid in g) redes.Append(XmlRedePid(sg, pid));
            }

            if (xmlBase == null)
            {
                var sb = new StringBuilder();
                sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
                sb.AppendLine("<Document>");
                sb.AppendLine("  <Engineering version=\"V20\" />");
                sb.AppendLine("  <SW.Blocks.OB ID=\"0\">");
                sb.AppendLine("    <AttributeList>");
                sb.AppendLine("      <AutoNumber>false</AutoNumber>");
                sb.AppendLine("      <Interface><Sections xmlns=\"" + NsIf + "\">");
                sb.AppendLine("        <Section Name=\"Input\">");
                sb.AppendLine("          <Member Name=\"Initial_Call\" Datatype=\"Bool\" Informative=\"true\" />");
                sb.AppendLine("          <Member Name=\"Event_Count\" Datatype=\"Int\" Informative=\"true\" />");
                sb.AppendLine("        </Section>");
                sb.AppendLine("        <Section Name=\"Temp\" />");
                sb.AppendLine("        <Section Name=\"Constant\" />");
                sb.AppendLine("      </Sections></Interface>");
                sb.AppendLine("      <MemoryLayout>Optimized</MemoryLayout>");
                sb.AppendLine("      <Name>Cyclic interrupt</Name>");
                sb.AppendLine("      <Namespace />");
                sb.AppendLine("      <Number>" + numero + "</Number>");
                sb.AppendLine("      <ProgrammingLanguage>LAD</ProgrammingLanguage>");
                sb.AppendLine("      <SecondaryType>CyclicInterrupt</SecondaryType>");
                sb.AppendLine("      <SetENOAutomatically>false</SetENOAutomatically>");
                sb.AppendLine("    </AttributeList>");
                sb.AppendLine("    <ObjectList>");
                sb.Append(redes);
                sb.AppendLine("    </ObjectList>");
                sb.AppendLine("  </SW.Blocks.OB>");
                sb.AppendLine("</Document>");
                return sb.ToString();
            }

            XDocument doc = XDocument.Parse(xmlBase);
            XElement obEl = doc.Descendants("SW.Blocks.OB").FirstOrDefault();
            if (obEl == null) throw new InvalidOperationException("o XML exportado nao e de um OB");
            XElement lista = obEl.Element("ObjectList");
            if (lista == null) { lista = new XElement("ObjectList"); obEl.Add(lista); }
            lista.Elements("SW.Blocks.CompileUnit").Remove();
            XElement novas = XElement.Parse("<raiz>" + redes + "</raiz>");
            foreach (XElement e in novas.Elements().ToList()) lista.Add(e);
            XElement attrs = obEl.Element("AttributeList");
            XElement lang = attrs == null ? null : attrs.Element("ProgrammingLanguage");
            if (lang != null) lang.Value = "LAD";
            return "<?xml version=\"1.0\" encoding=\"utf-8\"?>" + Environment.NewLine + doc.Root.ToString();
        }
    }
}
