using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

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
        /// <summary>
        /// Nome da maquina/subsistema para usar na descricao de um nivel repetido: FLO vira FLOTADOR, ADN1 vira ADENSADOR 1.
        /// Se nao reconhece a maquina, devolve o proprio codigo.
        /// </summary>
        public static string NomeAmigavelMaquina(IEnumerable<Partida> todas, string codigo)
        {
            if (string.IsNullOrWhiteSpace(codigo)) return codigo ?? "";
            string palavra = null;
            foreach (Partida x in todas)
            {
                if (!(string.Equals(x.Maquina, codigo, StringComparison.OrdinalIgnoreCase)
                      || string.Equals(x.Subsistema, codigo, StringComparison.OrdinalIgnoreCase))) continue;
                if (x.Receita == Maquinas.Flotador) { palavra = "FLOTADOR"; break; }
                if (x.Receita == Maquinas.SubsistemaAdensador || x.Receita == Maquinas.Adensador) { palavra = "ADENSADOR"; break; }
            }
            if (palavra == null) return codigo;
            string digitos = new string(codigo.Reverse().TakeWhile(char.IsDigit).Reverse().ToArray());
            return digitos.Length > 0 ? palavra + " " + digitos : palavra;
        }

        private static string TirarSufixo(string desc, params string[] sufixos)
        {
            foreach (string suf in sufixos)
                if (!string.IsNullOrEmpty(suf) && desc.EndsWith(" " + suf, StringComparison.Ordinal))
                    return desc.Substring(0, desc.Length - suf.Length - 1).Trim();
            return desc;
        }

        private static string ColocarSufixo(string desc, string sufixo, string codigo)
        {
            if (string.IsNullOrEmpty(sufixo)) return desc;
            if (desc == sufixo || desc.EndsWith(" " + sufixo, StringComparison.Ordinal)) return desc;
            if (!string.IsNullOrEmpty(codigo) && codigo != sufixo && desc.EndsWith(" " + codigo, StringComparison.Ordinal))
                return (desc.Substring(0, desc.Length - codigo.Length - 1).Trim() + " " + sufixo).Trim();
            return (desc + " " + sufixo).Trim();
        }

        private static string FamiliaDoSensor(Partida x)
        {
            if (x.Tipo != null && x.Tipo.Contains("analógico")) return "TN";
            if (x.Tipo != null && x.Tipo.Contains("alto")) return "TA";
            return "TB";
        }

        private static string FamiliaDoFn(string fn)
        {
            if (fn == "TAN" || fn.StartsWith("TN", StringComparison.Ordinal)) return "TN";
            if (fn == "TAL" || Regex.IsMatch(fn, @"^TA\d+$")) return "TA";
            if (fn.StartsWith("TB", StringComparison.Ordinal)) return "TB";
            return null;
        }

        public static List<string> NormalizarNiveisDuplicados(IList<Partida> todas, out List<Partida> ajustadas)
        {
            ajustadas = new List<Partida>();
            var avisos = new List<string>();
            var renomeacoes = new Dictionary<string, string>();

            var grupos = todas
                .Where(x => x.Sub == "SEN" && !string.IsNullOrEmpty(x.Fn))
                .Where(x => x.Tipo != null && x.Tipo.Contains("Sensor nível"))
                .Where(x => string.IsNullOrEmpty(x.FnNivel))   // PULA os que têm Fn manual
                .GroupBy(x => x.Eq.ToUpperInvariant() + "|" + FamiliaDoSensor(x))
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
                    Partida sen = lista[i];
                    string fnCorreto = lista.Count == 1 ? fnUnico : prefixoNumerado + (i + 1).ToString();
                    string fnAntigo = sen.Fn;
                    string descAntiga = sen.Descricao ?? "";

                    bool mudouFn = sen.Fn != fnCorreto;
                    if (mudouFn)
                    {
                        string chave = sen.Eq.ToUpperInvariant() + "|" + sen.Fn;
                        if (!renomeacoes.ContainsKey(chave)) renomeacoes[chave] = fnCorreto;
                        sen.Fn = fnCorreto;
                    }

                    // descricao: o principal fica sem o nome da maquina; os demais ganham (FLOTADOR, ADENSADOR 1...)
                    string codigo = !string.IsNullOrEmpty(sen.Subsistema) ? sen.Subsistema : sen.Maquina;
                    string sufixo = NomeAmigavelMaquina(todas, codigo);
                    bool ehPrincipal = lista.Count == 1 || i == 0;
                    string novaDesc = ehPrincipal ? TirarSufixo(descAntiga, sufixo, codigo) : ColocarSufixo(descAntiga, sufixo, codigo);
                    bool mudouDesc = !string.Equals(novaDesc, descAntiga, StringComparison.Ordinal);
                    if (mudouDesc) sen.Descricao = novaDesc;

                    if (mudouFn || mudouDesc)
                    {
                        ajustadas.Add(sen);
                        avisos.Add("SEN." + sen.Eq + "." + fnAntigo + "  →  SEN." + sen.Eq + "." + sen.Fn
                            + (mudouDesc ? "  (descrição: " + novaDesc + ")" : ""));
                    }
                }
            }

            // intertravamentos: troca todas as referencias de uma vez (uma troca nao pode ser refeita pela seguinte)
            if (renomeacoes.Count > 0)
            {
                var rxRef = new Regex(@"SEN\.([A-Za-z0-9_]+)\.([A-Za-z0-9_]+)\.", RegexOptions.CultureInvariant);
                foreach (var p in todas)
                {
                    if (string.IsNullOrEmpty(p.CondicoesInt)) continue;
                    string novo = rxRef.Replace(p.CondicoesInt, delegate (Match m)
                    {
                        string novoFn;
                        return renomeacoes.TryGetValue(m.Groups[1].Value.ToUpperInvariant() + "|" + m.Groups[2].Value, out novoFn)
                            ? "SEN." + m.Groups[1].Value + "." + novoFn + "."
                            : m.Value;
                    });
                    if (novo != p.CondicoesInt) p.CondicoesInt = novo;
                }
            }

            // intertravamentos que apontam para um nivel que nao existe (ou que ficou com o nome de outra maquina):
            // aponta para o nivel certo do mesmo equipamento (mesma familia e mesma maquina/subsistema da partida)
            var sensores = todas.Where(x => x.Sub == "SEN" && !string.IsNullOrEmpty(x.Fn)
                && x.Tipo != null && x.Tipo.Contains("Sensor nível")).ToList();
            var rxNivel = new Regex(@"SEN\.([A-Za-z0-9_]+)\.([A-Za-z0-9_]+)\.(LEI_BAI|LEI_ALT)", RegexOptions.CultureInvariant);
            foreach (var p in todas)
            {
                if (string.IsNullOrEmpty(p.CondicoesInt)) continue;
                string escopo = !string.IsNullOrEmpty(p.Maquina) ? p.Maquina : p.Subsistema;
                string antes = p.CondicoesInt;
                string novo = rxNivel.Replace(antes, delegate (Match m)
                {
                    string eq = m.Groups[1].Value, fn = m.Groups[2].Value;
                    string fam = FamiliaDoFn(fn);
                    if (fam == null) return m.Value;
                    var cands = sensores.Where(x => string.Equals(x.Eq, eq, StringComparison.OrdinalIgnoreCase) && FamiliaDoSensor(x) == fam).ToList();
                    if (cands.Count == 0) return m.Value;
                    Partida alvo = null;
                    if (cands.Count == 1) alvo = cands[0];
                    else if (!string.IsNullOrEmpty(escopo))
                    {
                        var doEscopo = cands.Where(x => string.Equals(x.Maquina, escopo, StringComparison.OrdinalIgnoreCase)
                            || string.Equals(x.Subsistema, escopo, StringComparison.OrdinalIgnoreCase)).ToList();
                        if (doEscopo.Count == 1) alvo = doEscopo[0];
                    }
                    if (alvo == null || alvo.Fn == fn) return m.Value;
                    return "SEN." + eq + "." + alvo.Fn + "." + m.Groups[3].Value;
                });
                if (novo != antes)
                {
                    p.CondicoesInt = novo;
                    avisos.Add("Intertravamento de " + p.Sub + "." + p.Eq + "." + p.Fn + " apontava para nível errado: ajustado");
                }
            }

            return avisos;
        }
    }
}