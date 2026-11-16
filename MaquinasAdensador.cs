using System;
using System.Collections.Generic;
using System.Linq;
using Genesis.Fabricas;

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
        public string EqNivelAlim { get; set; } = "ALM";
        public string FnNivelAlim { get; set; } = "";

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
        public string EqNivelAlim { get; set; } = "ALM";
        public string FnNivelAlim { get; set; } = "";

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
        public string EqNivelLad { get; set; } = "LAD";
        public string FnNivelLad { get; set; } = "";

        public bool IncluirAgitTanqueEqualizacao { get; set; } = true;
        public string TipoAgitEqual { get; set; } = "Partida direta";

        public bool IncluirFloculador { get; set; } = true;
        public string TipoFloculador { get; set; } = "Partida direta";

        public bool IncluirBombaLodoAdensado { get; set; } = true;
        public string TipoBombaLodo { get; set; } = "Partida inversor";

        public bool IncluirBombaFloculante { get; set; } = true;
        public string TipoBombaFloc { get; set; } = "Partida diafragma";
        public bool TemNivelFlc { get; set; } = true;
        public string EqNivelFlc { get; set; } = "FLC";
        public string FnNivelFlc { get; set; } = "";

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

        // ===================================================================
        //  MÁQUINA ADENSADOR (isolada)
        // ===================================================================
        public static List<Partida> CriarAdensador(OpcoesAdensador o, IEnumerable<Partida> todas = null)
        {
            var linhasExistentes = todas?.ToList() ?? new List<Partida>();
            string db = o.Db.Trim().ToUpperInvariant();
            string d = (o.Descricao ?? "").Trim();
            string sufixo = d.Replace("ADENSADOR", "").Trim();
            if (!string.IsNullOrEmpty(sufixo)) sufixo = "_" + sufixo;
            string nomeBase = "ADENSADOR" + sufixo;

            var l = new List<Partida>();

            var mot = FabricaPartida.CriarAcionamento(db, db, "MOT", o.TipoMotor, "MOTOR " + nomeBase, maquina: db, subsistema: "", papel: PapelAdMotor);
            mot.Auto = true;
            l.Add(mot);

            string eqNiv = string.IsNullOrWhiteSpace(o.EqNivelAlim) ? "ALM" : o.EqNivelAlim.Trim().ToUpperInvariant();
            string fnNiv = "";
            if (o.TemNivelAlim)
            {
                bool ana = o.TipoNivelAlim.Contains("analógico");
                string baseDesc = ana ? "NÍVEL TANQUE ALIMENTAÇÃO" : "NÍVEL BAIXO TANQUE ALIMENTAÇÃO";
                var niv = FabricaPartida.CriarNivel(o.TipoNivelAlim, eqNiv, baseDesc, linhasExistentes,
                    maquina: db, subsistema: "", papel: PapelAdNivAlim, fnManual: o.FnNivelAlim);
                fnNiv = niv.Fn;
                l.Add(niv);
            }

            if (o.TemBombaAlim)
            {
                var bombaAlim = FabricaPartida.CriarAcionamento(db, "ALM", "BBA", o.TipoBombaAlim,
                    "BOMBA ALIMENTACAO " + nomeBase, maquina: db, subsistema: "", papel: PapelAdAlm);
                bombaAlim.Auto = true;
                bombaAlim.Intertravamento = true;

                var conds = new List<string>();
                var nivLadExistente = linhasExistentes.FirstOrDefault(x => x.Papel == "LAD_NIV");
                if (nivLadExistente != null)
                    conds.Add("SEN." + nivLadExistente.Eq + "." + nivLadExistente.Fn + ".LEI_ALT");

                if (o.TemNivelAlim && !string.IsNullOrEmpty(fnNiv))
                    conds.Add("SEN." + eqNiv + "." + fnNiv + ".LEI_BAI");

                string tagMot = o.TipoMotor.Contains("rede") ? db + "." + db + ".MOT.LEI_RUN" : db + "." + db + ".MOT.LEI_HAB";
                conds.Add("NÃO " + tagMot);

                bombaAlim.CondicoesInt = string.Join("\n", conds);
                l.Add(bombaAlim);
            }

            if (o.TemBombaHig)
            {
                var bombaHig = FabricaPartida.CriarAcionamento(db, "HIG", "BBA", o.TipoBombaHig,
                    "BOMBA HIGIENIZACAO " + nomeBase, maquina: db, subsistema: "", papel: PapelAdHigBba);
                bombaHig.Auto = true;
                if (o.TemIntermitenteHig)
                {
                    bombaHig.Intermitente = o.TempoLigadoHig;
                    bombaHig.IntermitenteDes = o.TempoDesligadoHig;
                }
                l.Add(bombaHig);
            }

            if (o.TemValvulaHig)
            {
                var valvulaHig = FabricaPartida.CriarAcionamento(db, "HIG", "VAL", o.TipoValvulaHig,
                    "VALVULA HIGIENIZACAO " + nomeBase, maquina: db, subsistema: "", papel: PapelAdHigVal);
                valvulaHig.Auto = true;
                if (o.TemBombaHig)
                {
                    valvulaHig.Intertravamento = true;
                    valvulaHig.CondicoesInt = "NÃO " + db + ".HIG.BBA.LEI_HAB";
                }
                l.Add(valvulaHig);
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
            if (nivAlim != null) { o.TipoNivelAlim = nivAlim.Tipo; o.EqNivelAlim = nivAlim.Eq; o.FnNivelAlim = nivAlim.FnNivel; }

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

        // ===================================================================
        //  SUBSISTEMA ADENSADOR
        // ===================================================================
        public static List<Partida> CriarSubsistemaAdensador(OpcoesSubsistemaAdensador o, IEnumerable<Partida> existentes)
        {
            var linhasExistentes = existentes?.ToList() ?? new List<Partida>();
            string db = o.Db.Trim().ToUpperInvariant();
            string d = (o.Descricao ?? "").Trim();
            string sufixoDesc = d.Replace("ADENSADOR", "").Trim();
            if (!string.IsNullOrEmpty(sufixoDesc)) sufixoDesc = "_" + sufixoDesc;
            string nomeBase = "ADENSADOR" + sufixoDesc;

            var l = new List<Partida>();

            // 1) Nível LAD
            if (o.TemNivelLad)
            {
                bool ana = o.TipoNivelLad.Contains("analógico");
                string baseDesc = ana ? "NÍVEL TANQUE LODO ADENSADO" : "NÍVEL BAIXO TANQUE LODO ADENSADO";
                string eqNiv = string.IsNullOrWhiteSpace(o.EqNivelLad) ? "LAD" : o.EqNivelLad.Trim().ToUpperInvariant();
                var nivLad = FabricaPartida.CriarNivel(o.TipoNivelLad, eqNiv, baseDesc, linhasExistentes,
                    maquina: "", subsistema: db, papel: "LAD_NIV", fnManual: o.FnNivelLad);
                l.Add(nivLad);
            }

            // 2) Nível FLC
            if (o.TemNivelFlc)
            {
                string eqNiv = string.IsNullOrWhiteSpace(o.EqNivelFlc) ? "FLC" : o.EqNivelFlc.Trim().ToUpperInvariant();
                var nivFlc = FabricaPartida.CriarNivel("Sensor nível baixo digital", eqNiv, "NÍVEL BAIXO TANQUE FLOCULANTE",
                    linhasExistentes.Concat(l), maquina: "", subsistema: db, papel: "FLC_NIV", fnManual: o.FnNivelFlc);
                l.Add(nivFlc);
            }

            // 3) Máquina Adensador (motor + bombas + nível alim)
            var opMaq = new OpcoesAdensador
            {
                Db = db, Descricao = o.Descricao,
                TipoMotor = o.TipoMotor,
                TemBombaAlim = o.TemBombaAlim, TipoBombaAlim = o.TipoBombaAlim,
                TemNivelAlim = o.TemNivelAlim, TipoNivelAlim = o.TipoNivelAlim, EqNivelAlim = o.EqNivelAlim, FnNivelAlim = o.FnNivelAlim,
                TemBombaHig = o.TemBombaHig, TipoBombaHig = o.TipoBombaHig,
                TemValvulaHig = o.TemValvulaHig, TipoValvulaHig = o.TipoValvulaHig,
                TemIntermitenteHig = o.TemIntermitenteHig, TempoLigadoHig = o.TempoLigadoHig, TempoDesligadoHig = o.TempoDesligadoHig
            };

            var maqLinhas = CriarAdensador(opMaq, linhasExistentes.Concat(l));
            foreach (var p in maqLinhas)
            {
                p.Subsistema = db;
                p.Receita = SubsistemaAdensador;
                l.Add(p);
            }

            // 4) Periféricos opcionais
            if (o.IncluirAgitLodoAdensado)
                l.Add(FabricaPartida.CriarAcionamento(db, "LAD", "AGT", o.TipoAgitLodo, "AGITADOR LODO ADENSADO " + nomeBase, subsistema: db, papel: "LAD_AGT"));
            if (o.IncluirAgitTanqueEqualizacao)
                l.Add(FabricaPartida.CriarAcionamento(db, "EQU", "AGT", o.TipoAgitEqual, "AGITADOR TANQUE EQUALIZACAO " + nomeBase, subsistema: db, papel: "EQU_AGT"));
            if (o.IncluirFloculador)
                l.Add(FabricaPartida.CriarAcionamento(db, "FME", "AGT", o.TipoFloculador, "FLOCULADOR MECANICO " + nomeBase, subsistema: db, papel: "FME_AGT"));

            // 5) Bomba de Lodo Adensado
            if (o.IncluirBombaLodoAdensado)
            {
                var pLad = FabricaPartida.CriarAcionamento(db, "LAD", "BBA", o.TipoBombaLodo, "BOMBA LODO ADENSADO " + nomeBase, subsistema: db, papel: "LAD_BBA");
                var nivLad = l.FirstOrDefault(x => x.Papel == "LAD_NIV");
                if (nivLad != null)
                {
                    pLad.Intertravamento = true;
                    pLad.CondicoesInt = "SEN." + nivLad.Eq + "." + nivLad.Fn + ".LEI_BAI";
                }
                l.Add(pLad);
            }

            // 6) Bomba de Floculante
            if (o.IncluirBombaFloculante)
            {
                var flc = FabricaPartida.CriarAcionamento(db, "FLC", "BBA", o.TipoBombaFloc, "BOMBA FLOCULANTE " + nomeBase, subsistema: db, papel: "FLC_BBA");
                var conds = new List<string>();

                var nivFlc = l.FirstOrDefault(x => x.Papel == "FLC_NIV");
                if (nivFlc != null)
                    conds.Add("SEN." + nivFlc.Eq + "." + nivFlc.Fn + ".LEI_BAI");

                string tagAlim = o.TipoBombaAlim.Contains("rede") ? db + ".ALM.BBA.LEI_RUN" : db + ".ALM.BBA.LEI_HAB";
                conds.Add("NÃO " + tagAlim);

                flc.Intertravamento = true;
                flc.CondicoesInt = string.Join("\n", conds);
                l.Add(flc);
            }

            if (o.IncluirAgitFloculante)
                l.Add(FabricaPartida.CriarAcionamento(db, "FLC", "AGT", o.TipoAgitFloc, "AGITADOR FLOCULANTE " + nomeBase, subsistema: db, papel: "FLC_AGT"));

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
            o.TemNivelAlim = opMaq.TemNivelAlim; o.TipoNivelAlim = opMaq.TipoNivelAlim; o.EqNivelAlim = opMaq.EqNivelAlim; o.FnNivelAlim = opMaq.FnNivelAlim;
            o.TemBombaHig = opMaq.TemBombaHig; o.TipoBombaHig = opMaq.TipoBombaHig;
            o.TemValvulaHig = opMaq.TemValvulaHig; o.TipoValvulaHig = opMaq.TipoValvulaHig;
            o.TemIntermitenteHig = opMaq.TemIntermitenteHig; o.TempoLigadoHig = opMaq.TempoLigadoHig; o.TempoDesligadoHig = opMaq.TempoDesligadoHig;

            Func<string, Partida> por = papel => l.FirstOrDefault(x => x.Papel == papel);

            Partida pLadAgt = por("LAD_AGT"); o.IncluirAgitLodoAdensado = pLadAgt != null; if (pLadAgt != null) o.TipoAgitLodo = pLadAgt.Tipo;
            Partida pLadNiv = por("LAD_NIV"); o.TemNivelLad = pLadNiv != null; if (pLadNiv != null) { o.TipoNivelLad = pLadNiv.Tipo; o.EqNivelLad = pLadNiv.Eq; o.FnNivelLad = pLadNiv.FnNivel; }

            Partida pEquAgt = por("EQU_AGT"); o.IncluirAgitTanqueEqualizacao = pEquAgt != null; if (pEquAgt != null) o.TipoAgitEqual = pEquAgt.Tipo;
            Partida pFmeAgt = por("FME_AGT"); o.IncluirFloculador = pFmeAgt != null; if (pFmeAgt != null) o.TipoFloculador = pFmeAgt.Tipo;
            Partida pLadBba = por("LAD_BBA"); o.IncluirBombaLodoAdensado = pLadBba != null; if (pLadBba != null) o.TipoBombaLodo = pLadBba.Tipo;
            Partida pFlcBba = por("FLC_BBA"); o.IncluirBombaFloculante = pFlcBba != null; if (pFlcBba != null) o.TipoBombaFloc = pFlcBba.Tipo;

            Partida pFlcNiv = por("FLC_NIV"); o.TemNivelFlc = pFlcNiv != null; if (pFlcNiv != null) { o.EqNivelFlc = pFlcNiv.Eq; o.FnNivelFlc = pFlcNiv.FnNivel; }

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
                a.Eq = n.Eq;
                a.Fn = n.Fn;
                a.FnNivel = n.FnNivel;
                a.Intertravamento = n.Intertravamento;
                a.CondicoesInt = n.CondicoesInt;
                a.Intermitente = n.Intermitente;
                a.IntermitenteDes = n.IntermitenteDes;
                if (string.IsNullOrEmpty(a.TagSensor) || (padrao != null && a.TagSensor == padrao.TagSensor))
                    a.TagSensor = n.TagSensor;
            }
        }
    }
}