using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Siemens.Engineering;
using Siemens.Engineering.SW;
using Siemens.Engineering.SW.Blocks;

namespace Genesis
{
    internal partial class Gerador
    {
        /// <summary>Se true, gera apenas o Main e o DB GER (ignora FCs e instâncias).</summary>
        public bool SoMain;

        private const string NomeObMain = "Main";
        private const string NumObMain = "123";
        private const string NomeDbGer = "GER";

        public void GerarMain(PlcSoftware plc, PlcBlockUserGroup grupo, List<Partida> partidas, List<Io> ios, string saida)
        {
            Log("--- MAIN (OB123) e DB GER ---");

            var utf8 = new UTF8Encoding(false);
            PlcBlockGroup ondeBloco;

            // 1) Apaga o OB Main antigo, se existir
            PlcBlock obAntigo = AcharBloco(plc.BlockGroup, NomeObMain, out ondeBloco);
            if (obAntigo != null)
            {
                Log("Apagando OB " + NomeObMain + " antigo (pasta: " + ondeBloco.Name + ")...");
                obAntigo.Delete();
            }

            // 2) Apaga o DB GER antigo, se existir
            PlcBlock dbGerAntigo = AcharBloco(plc.BlockGroup, NomeDbGer, out ondeBloco);
            if (dbGerAntigo != null)
            {
                Log("Apagando DB " + NomeDbGer + " antigo (pasta: " + ondeBloco.Name + ")...");
                dbGerAntigo.Delete();
            }

            // 3) Cria o DB GER novo
            string arqGer = Path.Combine(saida, NomeDbGer + ".xml");
            File.WriteAllText(arqGer, XmlDbGer(), utf8);
            ImportarArquivo(grupo, arqGer);

            // 4) Cria o OB123 novo
            string arqOb = Path.Combine(saida, "OB" + NumObMain + ".xml");
            File.WriteAllText(arqOb, XmlObMain(partidas, ios, grupo), utf8);
            ImportarArquivo(grupo, arqOb);

            var compilar = new List<string> { NomeDbGer, NomeObMain };
            CompilarPorNome(plc, compilar);
        }

                private string XmlDbGer()
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
            sb.AppendLine("          <Member Name=\"SIS\" Datatype=\"Struct\">");
            // Telas
            for (int i = 1; i <= 5; i++)
            {
                sb.AppendLine("            <Member Name=\"TL" + i + "\" Datatype=\"Struct\">");
                sb.AppendLine("              <Member Name=\"CMD\" Datatype=\"Struct\">");
                sb.AppendLine("                <Member Name=\"DES\" Datatype=\"Bool\" />");
                sb.AppendLine("              </Member>");
                sb.AppendLine("            </Member>");
            }
            // Vis / Def
            sb.AppendLine("            <Member Name=\"VIS\" Datatype=\"Struct\">");
            sb.AppendLine("              <Member Name=\"LEI\" Datatype=\"Struct\">");
            sb.AppendLine("                <Member Name=\"DEF\" Datatype=\"Bool\" />");
            sb.AppendLine("              </Member>");
            sb.AppendLine("            </Member>");
            // SKL
            sb.AppendLine("            <Member Name=\"SKL\" Datatype=\"Struct\">");
            sb.AppendLine("              <Member Name=\"FIM_EXPEDIENTE\" Datatype=\"Bool\" />");
            sb.AppendLine("              <Member Name=\"HAB_GRAFICOS\" Datatype=\"Bool\" />");
            sb.AppendLine("              <Member Name=\"GESTOS_BOTOES\" Datatype=\"Bool\" />");
            sb.AppendLine("            </Member>");
            // LNG
            sb.AppendLine("            <Member Name=\"LNG\" Datatype=\"Struct\">");
            sb.AppendLine("              <Member Name=\"NUM_IDIOMA\" Datatype=\"Int\" />");
            sb.AppendLine("            </Member>");
            // Diversos
            sb.AppendLine("            <Member Name=\"REM_LOC_CMD_SEL\" Datatype=\"Bool\" />");
            sb.AppendLine("          </Member>");
            sb.AppendLine("        </Section>");
            sb.AppendLine("      </Sections></Interface>");
            sb.AppendLine("      <MemoryLayout>Optimized</MemoryLayout>");
            sb.AppendLine("      <Name>" + NomeDbGer + "</Name>");
            sb.AppendLine("      <Namespace />");
            sb.AppendLine("      <ProgrammingLanguage>DB</ProgrammingLanguage>");
            sb.AppendLine("    </AttributeList>");
            sb.AppendLine("  </SW.Blocks.GlobalDB>");
            sb.AppendLine("</Document>");
            return sb.ToString();
        }

        private string XmlObMain(List<Partida> partidas, List<Io> ios, PlcBlockUserGroup grupo)
        {
            _id = 1;
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
            sb.AppendLine("          <Member Name=\"Remanence\" Datatype=\"Bool\" Informative=\"true\" />");
            sb.AppendLine("        </Section>");
            sb.AppendLine("        <Section Name=\"Temp\" />");
            sb.AppendLine("        <Section Name=\"Constant\" />");
            sb.AppendLine("      </Sections></Interface>");
            sb.AppendLine("      <MemoryLayout>Optimized</MemoryLayout>");
            sb.AppendLine("      <Name>" + NomeObMain + "</Name>");
            sb.AppendLine("      <Namespace />");
            sb.AppendLine("      <Number>" + NumObMain + "</Number>");
            sb.AppendLine("      <ProgrammingLanguage>LAD</ProgrammingLanguage>");
            sb.AppendLine("      <SecondaryType>ProgramCycle</SecondaryType>");
            sb.AppendLine("      <SetENOAutomatically>false</SetENOAutomatically>");
            sb.AppendLine("    </AttributeList>");
            sb.AppendLine("    <ObjectList>");

            // REDE 1: SUBROTINAS
            var r1 = new Rede();
            var fcs = new List<string>();
            var subs = partidas.Where(x => !string.IsNullOrEmpty(x.Sub)).Select(x => x.Sub).Distinct().ToList();
            foreach (var sub in subs)
            {
                var doSub = partidas.Where(x => x.Sub == sub).ToList();
                fcs.Add(NomeFcDe(sub, doSub));
            }
            if (Cfg.ComAtivo) fcs.Add(Cfg.Prefixo + "COMANDO_REMOTO");
            
            foreach (var fc in fcs.Distinct())
            {
                int call = r1.ChamadaFc(fc);
                r1.ParaTrilho(call, "en");
            }
            sb.Append(Embrulhar(r1, "SUBROTINAS"));

            // REDE 2: EMERGENCIA
            var r2 = new Rede();
            string tagEme = string.IsNullOrWhiteSpace(Cfg.TagEmergencia) ? "SIS_EME_GER_ACL_CNF" : Cfg.TagEmergencia.Trim();
            
            // A tag de emergência sempre será setada pelo contato de emergência
            int cEme = r2.Contato(tagEme, true); // Negado
            r2.ParaTrilho(cEme, "in");
            int bEme = r2.Bobina("SCoil", tagEme); 
            r2.Fio(Rede.Pino(cEme, "out"), Rede.Pino(bEme, "in"));
            
            if (!string.IsNullOrWhiteSpace(Cfg.TagReset))
            {
                int cRst = r2.Contato(Cfg.TagReset, false);
                r2.ParaTrilho(cRst, "in");
                int bRst = r2.Bobina("RCoil", tagEme);
                r2.Fio(Rede.Pino(cRst, "out"), Rede.Pino(bRst, "in"));
            }
            sb.Append(Embrulhar(r2, "EMERGENCIA"));

            // REDE 3: DEFEITOS (Pisca no IHM)
            var r3 = new Rede();
            if (!string.IsNullOrWhiteSpace(Cfg.TagClock1Hz))
            {
                int cClk = r3.Contato(Cfg.TagClock1Hz, false);
                r3.ParaTrilho(cClk, "in");
                string tagDef = "DEFEITOS_1";
                var tagDefIo = ios.FirstOrDefault(x => x.Nome == tagDef);
                if (tagDefIo != null)
                {
                    int cDef = r3.Contato(tagDef, false);
                    r3.Fio(Rede.Pino(cClk, "out"), Rede.Pino(cDef, "in"));
                    int bVis = r3.Bobina("Coil", NomeDbGer + ".SIS.VIS.LEI.DEF");
                    r3.Fio(Rede.Pino(cDef, "out"), Rede.Pino(bVis, "in"));
                    sb.Append(Embrulhar(r3, "FALHA (PISCA IHM)"));
                }
            }

            // REDE 4..8: FIM DE EXPEDIENTE POR TELA
            for (int i = 1; i <= 5; i++)
            {
                string tela = "TL" + i;
                var daTela = partidas.Where(x => (string.IsNullOrEmpty(x.Tela) ? "TL1" : x.Tela) == tela).ToList();
                if (daTela.Count == 0) continue;

                var r = new Rede();
                // Condição: GER.SIS.TLx.CMD_DES OU TAG_EMERGENCIA
                int cDes = r.Contato(NomeDbGer + ".SIS." + tela + ".CMD.DES", false);
                r.ParaTrilho(cDes, "in");
                
                int cEmeTela = r.Contato(tagEme, false);
                r.ParaTrilho(cEmeTela, "in");

                int ou = r.Ou(2);
                r.Fio(Rede.Pino(cDes, "out"), Rede.Pino(ou, "in1"));
                r.Fio(Rede.Pino(cEmeTela, "out"), Rede.Pino(ou, "in2"));

                foreach (var p in daTela)
                {
                    // Ignora sensores e PIDs
                    if (Gerador.EhSensor(p) || Gerador.EhPid(p)) continue;

                    string b = Cfg.Prefixo + p.Sub + "." + p.Eq + "." + p.Fn + ".";
                    
                    if (p.Papel == Maquinas.PapelSis)
                    {
                        // Núcleo do preparador: reseta o CMD_SEL
                        int rSel = r.Bobina("RCoil", b + "CMD_SEL");
                        r.Fio(Rede.Pino(ou, "out"), Rede.Pino(rSel, "in"));
                    }
                    else if (Maquinas.EhPreparador(p))
                    {
                        // Partes do preparador: reseta apenas o CMD_LIG
                        int rLig = r.Bobina("RCoil", b + "CMD_LIG");
                        r.Fio(Rede.Pino(ou, "out"), Rede.Pino(rLig, "in"));
                    }
                    else
                    {
                        // Partidas normais e outras máquinas: reseta CMD_LIG e CMD_AUT
                        int rLig = r.Bobina("RCoil", b + "CMD_LIG");
                        int rAut = r.Bobina("RCoil", b + "CMD_AUT");
                        r.Fio(Rede.Pino(ou, "out"), Rede.Pino(rLig, "in"));
                        r.Fio(Rede.Pino(ou, "out"), Rede.Pino(rAut, "in"));
                    }
                }
                sb.Append(Embrulhar(r, "DESLIGA PARTIDAS " + tela + " (ENCERRAR EXPEDIENTE)"));
            }

            sb.AppendLine("    </ObjectList>");
            sb.AppendLine("  </SW.Blocks.OB>");
            sb.AppendLine("</Document>");
            return sb.ToString();
        }
    }
}