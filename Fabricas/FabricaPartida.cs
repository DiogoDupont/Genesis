using System;
using System.Collections.Generic;
using System.Linq;

namespace Genesis.Fabricas
{
    internal static class FabricaPartida
    {
        // ===================================================================
        //  CÁLCULO DE FN ÚNICO
        // ===================================================================
        public static string CalcularFnNivel(string baseFn, string eq, IEnumerable<Partida> todas)
        {
            bool existe = todas.Any(x => x.Sub == "SEN" && x.Eq.Equals(eq, StringComparison.OrdinalIgnoreCase) && x.Fn == baseFn);
            if (!existe) return baseFn;

            string prefixo;
            if (baseFn == "TBA") prefixo = "TB";
            else if (baseFn == "TAL") prefixo = "TA";
            else if (baseFn == "TAN") prefixo = "TN";
            else prefixo = baseFn.Substring(0, 2);

            int n = 1;
            while (todas.Any(x => x.Sub == "SEN" && x.Eq.Equals(eq, StringComparison.OrdinalIgnoreCase) && x.Fn == prefixo + n))
                n++;
            return prefixo + n;
        }

        public static string GerarDescNivel(string baseDesc, string fnCalculado, string baseFn, string sufixo)
        {
            if (fnCalculado == baseFn) return baseDesc;
            return baseDesc + " " + sufixo;
        }

        // ===================================================================
        //  CRIAÇÃO DE PARTIDAS
        // ===================================================================
        public static Partida CriarNivel(string tipo, string eq, string baseDescricao, IEnumerable<Partida> todas,
                                         string maquina = "", string subsistema = "", string papel = "", string fnManual = null)
        {
            bool ana = tipo.Contains("analógico");
            bool alto = tipo.Contains("alto");
            string baseFn = ana ? "TAN" : (alto ? "TAL" : "TBA");
            string fn;

            if (!string.IsNullOrWhiteSpace(fnManual))
                fn = fnManual.Trim().ToUpperInvariant();
            else
                fn = CalcularFnNivel(baseFn, eq, todas);

            string sufixo = string.IsNullOrEmpty(subsistema) ? maquina : subsistema;
            string desc = GerarDescNivel(baseDescricao, fn, baseFn, sufixo);

            return new Partida
            {
                Sub = "SEN",
                Eq = eq,
                Fn = fn,
                Tipo = tipo,
                Descricao = desc,
                Maquina = maquina,
                Subsistema = subsistema,
                Papel = papel,
                FnNivel = fnManual ?? "",
                Auto = false
            };
        }

        public static Partida CriarAcionamento(string sub, string eq, string fn, string tipo, string descricao,
                                               string maquina = "", string subsistema = "", string papel = "")
        {
            return new Partida
            {
                Sub = sub,
                Eq = eq,
                Fn = fn,
                Tipo = tipo,
                Descricao = descricao,
                Maquina = maquina,
                Subsistema = subsistema,
                Papel = papel,
                Auto = true
            };
        }

        // ===================================================================
        //  NORMALIZAÇÃO DE DUPLICADOS
        // ===================================================================
        public static List<string> NormalizarNiveisDuplicados(IList<Partida> todas, out List<Partida> ajustadas)
        {
            ajustadas = new List<Partida>();
            var avisos = new List<string>();
            var renomeacoes = new Dictionary<string, string>();

            var grupos = todas
                .Where(x => x.Sub == "SEN" && !string.IsNullOrEmpty(x.Fn))
                .Where(x => x.Tipo != null && x.Tipo.Contains("Sensor nível"))
                .Where(x => string.IsNullOrEmpty(x.FnNivel))   // PULA os que têm Fn manual
                .GroupBy(x =>
                {
                    string familia;
                    if (x.Tipo.Contains("analógico")) familia = "TN";
                    else if (x.Tipo.Contains("alto")) familia = "TA";
                    else familia = "TB";
                    return x.Eq.ToUpperInvariant() + "|" + familia;
                })
                .ToList();

            foreach (var grupo in grupos)
            {
                var lista = grupo.OrderBy(x => x.Linha).ThenBy(x => x.Descricao).ToList();
                if (lista.Count == 0) continue;

                string familia = grupo.Key.Substring(grupo.Key.IndexOf('|') + 1);

                string fnUnico, prefixoNumerado;
                if (familia == "TB") { fnUnico = "TBA"; prefixoNumerado = "TB"; }
                else if (familia == "TA") { fnUnico = "TAL"; prefixoNumerado = "TA"; }
                else { fnUnico = "TAN"; prefixoNumerado = "TN"; }

                for (int i = 0; i < lista.Count; i++)
                {
                    string fnCorreto = lista.Count == 1
                        ? fnUnico
                        : prefixoNumerado + (i + 1).ToString();

                    if (lista[i].Fn == fnCorreto) continue;

                    string chave = lista[i].Eq.ToUpperInvariant() + "|" + lista[i].Fn;
                    if (!renomeacoes.ContainsKey(chave))
                        renomeacoes[chave] = fnCorreto;

                    string descAntiga = lista[i].Descricao ?? "";
                    string fnAntigo = lista[i].Fn;
                    lista[i].Fn = fnCorreto;

                    string sufixo = !string.IsNullOrEmpty(lista[i].Subsistema) ? lista[i].Subsistema : lista[i].Maquina;
                    bool ehPrincipal = lista.Count == 1 || i == 0;

                    if (ehPrincipal)
                    {
                        if (!string.IsNullOrEmpty(sufixo) && descAntiga.EndsWith(" " + sufixo))
                            lista[i].Descricao = descAntiga.Substring(0, descAntiga.Length - sufixo.Length - 1).Trim();
                    }
                    else
                    {
                        if (!string.IsNullOrEmpty(sufixo) && !descAntiga.EndsWith(sufixo))
                            lista[i].Descricao = (descAntiga + " " + sufixo).Trim();
                    }

                    ajustadas.Add(lista[i]);
                    avisos.Add("SEN." + lista[i].Eq + "." + fnAntigo + "  →  SEN." + lista[i].Eq + "." + fnCorreto);
                }
            }

            if (renomeacoes.Count == 0) return avisos;

            foreach (var p in todas)
            {
                if (string.IsNullOrEmpty(p.CondicoesInt)) continue;
                string novo = p.CondicoesInt;
                foreach (var ren in renomeacoes)
                {
                    string[] partes = ren.Key.Split('|');
                    novo = novo.Replace("SEN." + partes[0] + "." + partes[1] + ".", "SEN." + partes[0] + "." + ren.Value + ".");
                }
                if (novo != p.CondicoesInt) p.CondicoesInt = novo;
            }

            return avisos;
        }
    }
}