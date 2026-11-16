using System;
using System.Collections.Generic;
using System.Linq;

namespace Genesis
{
    public class OpcoesAdensador
    {
        public string Db { get; set; } = "ADN1";
        public string Descricao { get; set; } = "ADENSADOR 1";
        public string TipoMotor { get; set; } = "Partida inversor por rede";

        public bool TemBombaAlim { get; set; } = true;
        public string TipoBombaAlim { get; set; } = "Partida direta";
        
        public bool TemNivelAlim { get; set; } = true;
        public string TipoNivelAlim { get; set; } = "Sensor nível baixo digital";

        public bool TemBombaHig { get; set; } = true;
        public string TipoBombaHig { get; set; } = "Partida direta";
        public bool TemValvulaHig { get; set; } = true;
        public string TipoValvulaHig { get; set; } = "Válvula 0DI e 1DO";

        public bool TemIntermitenteHig { get; set; } = true;
        public string TempoLigadoHig { get; set; } = "Minutos";
        public string TempoDesligadoHig { get; set; } = "Minutos";
    }

    public class OpcoesSubsistemaAdensador
    {
        public string Db { get; set; } = "ADN1";
        public string Descricao { get; set; } = "ADENSADOR 1";

        public string TipoMotor { get; set; } = "Partida inversor por rede";
        public bool TemBombaAlim { get; set; } = true;
        public string TipoBombaAlim { get; set; } = "Partida direta";
        
        public bool TemNivelAlim { get; set; } = true;
        public string TipoNivelAlim { get; set; } = "Sensor nível baixo digital";

        public bool TemBombaHig { get; set; } = true;
        public string TipoBombaHig { get; set; } = "Partida direta";
        public bool TemValvulaHig { get; set; } = true;
        public string TipoValvulaHig { get; set; } = "Válvula 0DI e 1DO";

        public bool TemIntermitenteHig { get; set; } = true;
        public string TempoLigadoHig { get; set; } = "Minutos";
        public string TempoDesligadoHig { get; set; } = "Minutos";

        public bool IncluirAgitLodoAdensado { get; set; } = true;
        public string TipoAgitLodo { get; set; } = "Partida direta";
        
        public bool TemNivelLad { get; set; } = true;
        public string TipoNivelLad { get; set; } = "Sensor nível baixo digital";

        public bool IncluirAgitTanqueEqualizacao { get; set; } = true;
        public string TipoAgitEqual { get; set; } = "Partida direta";
        
        public bool IncluirFloculador { get; set; } = true;
        public string TipoFloculador { get; set; } = "Partida direta";
        
        public bool IncluirBombaLodoAdensado { get; set; } = true;
        public string TipoBombaLodo { get; set; } = "Partida inversor";
        
        public bool IncluirBombaFloculante { get; set; } = true;
        public string TipoBombaFloc { get; set; } = "Partida diafragma";
        public bool TemNivelFlc { get; set; } = true;
        
        public bool IncluirAgitFloculante { get; set; } = true;
        public string TipoAgitFloc { get; set; } = "Partida direta";
    }

    internal class PlanoAdensador
    {
        public string DbAntigo, DbNovo;
        public object Antigas;
        public List<KeyValuePair<Partida, Partida>> Mantidas = new List<KeyValuePair<Partida, Partida>>();
        public List<Partida> Novos = new List<Partida>();
        public List<Partida> Removidas = new List<Partida>();
        public List<Partida> DescricaoPadraoAntiga = new List<Partida>();
    }

    internal static partial class Maquinas
    {
        public const string Adensador = "Adensador";
        public const string SubsistemaAdensador = "Subsistema Adensador";
        
        public const string PapelAdMotor = "MOT";
        public const string PapelAdAlm = "ALM_BBA";
        public const string PapelAdNivAlim = "ALM_NIV";
        public const string PapelAdHigBba = "HIG_BBA";
        public const string PapelAdHigVal = "HIG_VAL";

        internal static string GerarTagNivel(string tipo, string descricaoEq, IEnumerable<Partida> todasLinhas)
        {
            bool ana = tipo.Contains("analógico");
            bool alto = tipo.Contains("alto");
            string letraBase = ana ? "TAN" : (alto ? "TAL" : "TBA");
            
            int qtd = todasLinhas.Count(x => 
                x.Sub == "SEN" && 
                x.Eq.Equals(descricaoEq, StringComparison.OrdinalIgnoreCase) && 
                x.Tipo.Contains("Sensor nível") &&
                (ana ? x.Tipo.Contains("analógico") : (alto ? x.Tipo.Contains("alto") : x.Tipo.Contains("baixo"))));

            string sufixo = qtd == 0 ? letraBase : letraBase.Substring(0, 2) + qtd.ToString();
            return $"SEN_{descricaoEq}_{sufixo}";
        }

        public static List<Partida> CriarAdensador(OpcoesAdensador o, IEnumerable<Partida> todas = null)
        {
            var linhasExistentes = todas?.ToList() ?? new List<Partida>();
            string db = o.Db.Trim().ToUpperInvariant();
            string d = (o.Descricao ?? "").Trim();
            string sufixo = d.Replace("ADENSADOR", "").Trim();
            if (!string.IsNullOrEmpty(sufixo)) sufixo = "_" + sufixo;
            string nomeBase = "ADENSADOR" + sufixo;

            var l = new List<Partida>();
            
            Func<string, string, string, string, string, string, Partida> nova = delegate (string sub, string eq, string fn, string tipo, string papel, string desc)
            {
                var p = new Partida { Sub = sub, Eq = eq, Fn = fn, Tipo = tipo, Maquina = db, Papel = papel, Receita = Adensador, Descricao = desc.Trim() };
                l.Add(p);
                return p;
            };

            var mot = nova(db, db, "MOT", o.TipoMotor, PapelAdMotor, "MOTOR " + nomeBase);
            mot.Auto = true;

            if (o.TemNivelAlim)
            {
                bool ana = o.TipoNivelAlim.Contains("analógico");
                bool alto = o.TipoNivelAlim.Contains("alto");
                string fnNiv = ana ? "TAN" : (alto ? "TAL" : "TBA");
                var niv = nova("SEN", "ALM", fnNiv, o.TipoNivelAlim, PapelAdNivAlim, "NÍVEL BAIXO ALIMENTAÇÃO " + nomeBase);
                niv.TagSensor = "SEN_ALM_" + fnNiv;
                niv.Auto = false;
            }

            if (o.TemBombaAlim)
            {
                var bombaAlim = nova(db, "ALM", "BBA", o.TipoBombaAlim, PapelAdAlm, "BOMBA ALIMENTACAO " + nomeBase);
                bombaAlim.Auto = true;
                bombaAlim.Intertravamento = true;
                
                var conds = new List<string>();
                
                var nivLadExistente = linhasExistentes.FirstOrDefault(x => x.Papel == "LAD_NIV");
                if (nivLadExistente != null) 
                    conds.Add("SEN.LAD." + nivLadExistente.Fn + ".LEI_ALT");
                
                if (o.TemNivelAlim) 
                    conds.Add("SEN.ALM.TBA.LEI_BAI");
                
                string tagMot = o.TipoMotor.Contains("rede") ? db + "." + db + ".MOT.LEI_RUN" : db + "." + db + ".MOT.LEI_HAB";
                conds.Add("NÃO " + tagMot);
                
                bombaAlim.CondicoesInt = string.Join("\n", conds);
            }

            if (o.TemBombaHig)
            {
                var bombaHig = nova(db, "HIG", "BBA", o.TipoBombaHig, PapelAdHigBba, "BOMBA HIGIENIZACAO " + nomeBase);
                bombaHig.Auto = true;
                if (o.TemIntermitenteHig)
                {
                    bombaHig.Intermitente = o.TempoLigadoHig;
                    bombaHig.IntermitenteDes = o.TempoDesligadoHig;
                }
            }

            if (o.TemValvulaHig)
            {
                var valvulaHig = nova(db, "HIG", "VAL", o.TipoValvulaHig, PapelAdHigVal, "VALVULA HIGIENIZACAO " + nomeBase);
                valvulaHig.Auto = true;
                if (o.TemBombaHig)
                {
                    valvulaHig.Intertravamento = true;
                    valvulaHig.CondicoesInt = "NÃO " + db + ".HIG.BBA.LEI_HAB";
                }
            }

            return l;
        }

        public static OpcoesAdensador OpcoesDoAdensador(IEnumerable<Partida> itens, string id)
        {
            var l = itens.Where(x => string.Equals(x.Maquina, id, StringComparison.OrdinalIgnoreCase)).ToList();
            Func<string, Partida> por = papel => l.FirstOrDefault(x => x.Papel == papel);
            var o = new OpcoesAdensador { Db = id };
            Partida sis = l.FirstOrDefault();
            if (sis != null && !string.IsNullOrWhiteSpace(sis.Descricao))
                o.Descricao = System.Text.RegularExpressions.Regex.Replace(sis.Descricao, @"\s*-\s*.*$", "").Trim();
            
            Partida mot = por(PapelAdMotor); if (mot != null) o.TipoMotor = mot.Tipo;
            
            Partida ali = por(PapelAdAlm); 
            o.TemBombaAlim = ali != null; 
            if (ali != null) o.TipoBombaAlim = ali.Tipo;

            Partida nivAlim = por(PapelAdNivAlim);
            o.TemNivelAlim = nivAlim != null;
            if (nivAlim != null) o.TipoNivelAlim = nivAlim.Tipo;

            Partida hig = por(PapelAdHigBba); 
            o.TemBombaHig = hig != null; 
            if (hig != null) { o.TipoBombaHig = hig.Tipo; o.TemIntermitenteHig = !string.IsNullOrEmpty(hig.Intermitente); o.TempoLigadoHig = hig.Intermitente; o.TempoDesligadoHig = hig.IntermitenteDes; }
            
            Partida vhi = por(PapelAdHigVal); 
            o.TemValvulaHig = vhi != null; 
            if (vhi != null) o.TipoValvulaHig = vhi.Tipo;
            
            return o;
        }

        public static PlanoAdensador PlanejarAdensador(string id, List<Partida> itens, OpcoesAdensador novas)
        {
            var plano = new PlanoAdensador { DbAntigo = id, Antigas = OpcoesDoAdensador(itens, id) };
            novas.Db = novas.Db.Trim().ToUpperInvariant();
            plano.DbNovo = novas.Db;
            List<Partida> geradas = CriarAdensador(novas, itens);
            plano.DescricaoPadraoAntiga = CriarAdensador((OpcoesAdensador)plano.Antigas, itens);
            
            foreach (Partida n in geradas)
            {
                Partida atual = itens.FirstOrDefault(x => !string.IsNullOrEmpty(x.Papel) && x.Papel == n.Papel);
                if (atual != null) plano.Mantidas.Add(new KeyValuePair<Partida, Partida>(atual, n));
                else plano.Novos.Add(n);
            }
            plano.Removidas = itens.Where(x => !string.IsNullOrEmpty(x.Papel) && !geradas.Any(n => n.Papel == x.Papel)).ToList();
            return plano;
        }

        public static List<Partida> CriarSubsistemaAdensador(OpcoesSubsistemaAdensador o, IEnumerable<Partida> existentes)
        {
            var linhasExistentes = existentes?.ToList() ?? new List<Partida>();
            string db = o.Db.Trim().ToUpperInvariant();
            string d = (o.Descricao ?? "").Trim();
            string sufixo = d.Replace("ADENSADOR", "").Trim();
            if (!string.IsNullOrEmpty(sufixo)) sufixo = "_" + sufixo;
            string nomeBase = "ADENSADOR" + sufixo;

            var l = new List<Partida>();
            
            Func<string, string, string, string, string, string, Partida> nova = delegate (string sub, string eq, string fn, string tipo, string papel, string desc)
            {
                var p = new Partida { Sub = sub, Eq = eq, Fn = fn, Tipo = tipo, Maquina = "", Subsistema = db, Papel = papel, Receita = SubsistemaAdensador, Descricao = desc.Trim(), Auto = true };
                l.Add(p);
                return p;
            };

            if (o.TemNivelLad)
            {
                bool ana = o.TipoNivelLad.Contains("analógico");
                bool alto = o.TipoNivelLad.Contains("alto");
                string fnNiv = ana ? "TAN" : (alto ? "TAL" : "TBA");
                var nivLad = nova("SEN", "LAD", fnNiv, o.TipoNivelLad, "LAD_NIV", "NÍVEL LODO ADENSADO " + nomeBase);
                nivLad.TagSensor = "SEN_LAD_" + fnNiv;
                nivLad.Auto = false;
            }

            if (o.TemNivelFlc)
            {
                var nivFlc = nova("SEN", "FLC", "TBA", "Sensor nível baixo digital", "FLC_NIV", "NÍVEL TANQUE FLOCULANTE " + nomeBase);
                nivFlc.TagSensor = "SEN_FLC_TBA";
                nivFlc.Auto = false;
            }

            var opMaq = new OpcoesAdensador
            {
                Db = db, Descricao = o.Descricao,
                TipoMotor = o.TipoMotor,
                TemBombaAlim = o.TemBombaAlim, TipoBombaAlim = o.TipoBombaAlim,
                TemNivelAlim = o.TemNivelAlim, TipoNivelAlim = o.TipoNivelAlim,
                TemBombaHig = o.TemBombaHig, TipoBombaHig = o.TipoBombaHig,
                TemValvulaHig = o.TemValvulaHig, TipoValvulaHig = o.TipoValvulaHig,
                TemIntermitenteHig = o.TemIntermitenteHig, TempoLigadoHig = o.TempoLigadoHig, TempoDesligadoHig = o.TempoDesligadoHig
            };
            
            var maqLinhas = CriarAdensador(opMaq, l.Concat(linhasExistentes));
            foreach (var p in maqLinhas) { p.Subsistema = db; l.Add(p); }

            if (o.IncluirAgitLodoAdensado) nova(db, "LAD", "AGT", o.TipoAgitLodo, "LAD_AGT", "AGITADOR LODO ADENSADO " + nomeBase);
            if (o.IncluirAgitTanqueEqualizacao) nova(db, "EQU", "AGT", o.TipoAgitEqual, "EQU_AGT", "AGITADOR TANQUE EQUALIZACAO " + nomeBase);
            if (o.IncluirFloculador) nova(db, "FME", "AGT", o.TipoFloculador, "FME_AGT", "FLOCULADOR MECANICO " + nomeBase);

            if (o.IncluirBombaLodoAdensado)
            {
                var pLad = nova(db, "LAD", "BBA", o.TipoBombaLodo, "LAD_BBA", "BOMBA LODO ADENSADO " + nomeBase);
                if (o.TemNivelLad) { pLad.Intertravamento = true; pLad.CondicoesInt = "SEN.LAD.TAN.LEI_BAI"; }
            }
            
            if (o.IncluirBombaFloculante)
            {
                var flc = nova(db, "FLC", "BBA", o.TipoBombaFloc, "FLC_BBA", "BOMBA FLOCULANTE " + nomeBase);
                flc.Intertravamento = true;
                var conds = new List<string>();
                if (o.TemNivelFlc) conds.Add("SEN.FLC.TBA.LEI_BAI");
                string tagAlim = o.TipoBombaAlim.Contains("rede") ? db + ".ALM.BBA.LEI_RUN" : db + ".ALM.BBA.LEI_HAB";
                conds.Add("NÃO " + tagAlim);
                flc.CondicoesInt = string.Join("\n", conds);
            }
            
            if (o.IncluirAgitFloculante) nova(db, "FLC", "AGT", o.TipoAgitFloc, "FLC_AGT", "AGITADOR FLOCULANTE " + nomeBase);

            return l;
        }

        public static OpcoesSubsistemaAdensador OpcoesDoSubsistemaAdensador(IEnumerable<Partida> todas, string id)
        {
            var l = todas.Where(x => string.Equals(x.Subsistema, id, StringComparison.OrdinalIgnoreCase) || string.Equals(x.Maquina, id, StringComparison.OrdinalIgnoreCase)).ToList();
            var o = new OpcoesSubsistemaAdensador { Db = id };
            
            var opMaq = OpcoesDoAdensador(l, id);
            o.Descricao = opMaq.Descricao;
            o.TipoMotor = opMaq.TipoMotor;
            o.TemBombaAlim = opMaq.TemBombaAlim; o.TipoBombaAlim = opMaq.TipoBombaAlim;
            o.TemNivelAlim = opMaq.TemNivelAlim; o.TipoNivelAlim = opMaq.TipoNivelAlim;
            o.TemBombaHig = opMaq.TemBombaHig; o.TipoBombaHig = opMaq.TipoBombaHig;
            o.TemValvulaHig = opMaq.TemValvulaHig; o.TipoValvulaHig = opMaq.TipoValvulaHig;
            o.TemIntermitenteHig = opMaq.TemIntermitenteHig; o.TempoLigadoHig = opMaq.TempoLigadoHig; o.TempoDesligadoHig = opMaq.TempoDesligadoHig;

            Func<string, Partida> por = papel => l.FirstOrDefault(x => x.Papel == papel);
            
            Partida pLadAgt = por("LAD_AGT"); o.IncluirAgitLodoAdensado = pLadAgt != null; if (pLadAgt != null) o.TipoAgitLodo = pLadAgt.Tipo;
            Partida pLadNiv = por("LAD_NIV"); o.TemNivelLad = pLadNiv != null; if (pLadNiv != null) { o.TipoNivelLad = pLadNiv.Tipo; }
            
            Partida pEquAgt = por("EQU_AGT"); o.IncluirAgitTanqueEqualizacao = pEquAgt != null; if (pEquAgt != null) o.TipoAgitEqual = pEquAgt.Tipo;
            Partida pFmeAgt = por("FME_AGT"); o.IncluirFloculador = pFmeAgt != null; if (pFmeAgt != null) o.TipoFloculador = pFmeAgt.Tipo;
            Partida pLadBba = por("LAD_BBA"); o.IncluirBombaLodoAdensado = pLadBba != null; if (pLadBba != null) o.TipoBombaLodo = pLadBba.Tipo;
            Partida pFlcBba = por("FLC_BBA"); o.IncluirBombaFloculante = pFlcBba != null; if (pFlcBba != null) o.TipoBombaFloc = pFlcBba.Tipo;
            Partida pFlcNiv = por("FLC_NIV"); o.TemNivelFlc = pFlcNiv != null;
            Partida pFlcAgt = por("FLC_AGT"); o.IncluirAgitFloculante = pFlcAgt != null; if (pFlcAgt != null) o.TipoAgitFloc = pFlcAgt.Tipo;

            return o;
        }

        public static PlanoAdensador PlanejarSubsistemaAdensador(string id, List<Partida> todas, OpcoesSubsistemaAdensador novas)
        {
            var plano = new PlanoAdensador { DbAntigo = id, Antigas = OpcoesDoSubsistemaAdensador(todas, id) };
            novas.Db = novas.Db.Trim().ToUpperInvariant();
            plano.DbNovo = novas.Db;
            List<Partida> geradas = CriarSubsistemaAdensador(novas, todas);
            plano.DescricaoPadraoAntiga = CriarSubsistemaAdensador((OpcoesSubsistemaAdensador)plano.Antigas, todas);
            
            var itens = todas.Where(x => string.Equals(x.Subsistema, id, StringComparison.OrdinalIgnoreCase) || string.Equals(x.Maquina, id, StringComparison.OrdinalIgnoreCase)).ToList();
            
            foreach (Partida n in geradas)
            {
                Partida atual = itens.FirstOrDefault(x => !string.IsNullOrEmpty(x.Papel) && x.Papel == n.Papel);
                if (atual != null) plano.Mantidas.Add(new KeyValuePair<Partida, Partida>(atual, n));
                else plano.Novos.Add(n);
            }
            plano.Removidas = itens.Where(x => !string.IsNullOrEmpty(x.Papel) && !geradas.Any(n => n.Papel == x.Papel)).ToList();
            return plano;
        }

        public static void AplicarNasMantidasAdensador(PlanoAdensador plano)
        {
            foreach (var par in plano.Mantidas)
            {
                Partida a = par.Key, n = par.Value;
                Partida padrao = plano.DescricaoPadraoAntiga.FirstOrDefault(x => x.Papel == a.Papel);
                if (padrao != null && string.Equals(a.Descricao, padrao.Descricao, StringComparison.Ordinal)) a.Descricao = n.Descricao;
                a.Tipo = n.Tipo;
            }
        }
    }
}