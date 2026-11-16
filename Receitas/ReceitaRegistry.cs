using System;
using System.Collections.Generic;
using System.Linq;

namespace Genesis.Receitas
{
    internal static class ReceitaRegistry
    {
        public static readonly IReadOnlyList<IReceitaMaquina> Maquinas = new IReceitaMaquina[]
        {
            new ReceitaPreparador(),
            new ReceitaAdensador(),
        };

        public static readonly IReadOnlyList<IReceitaSubsistema> Subsistemas = new IReceitaSubsistema[]
        {
            new ReceitaSubsistemaFlotador(),
            new ReceitaSubsistemaAdensador(),
        };

        public static IEnumerable<IReceitaMaquina> Todas
        {
            get { return Maquinas.Concat(Subsistemas.Cast<IReceitaMaquina>()); }
        }

        public static IReceitaMaquina PorId(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            return Maquinas.FirstOrDefault(r => string.Equals(r.Id, id, StringComparison.OrdinalIgnoreCase))
                ?? Subsistemas.FirstOrDefault(r => string.Equals(r.Id, id, StringComparison.OrdinalIgnoreCase));
        }

        public static IReceitaMaquina PorNome(string nome)
        {
            if (string.IsNullOrEmpty(nome)) return null;
            return Maquinas.FirstOrDefault(r => string.Equals(r.NomeExibicao, nome, StringComparison.OrdinalIgnoreCase))
                ?? Subsistemas.FirstOrDefault(r => string.Equals(r.NomeExibicao, nome, StringComparison.OrdinalIgnoreCase));
        }

        public static IReceitaSubsistema SubsistemaPorId(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            return Subsistemas.FirstOrDefault(r => string.Equals(r.Id, id, StringComparison.OrdinalIgnoreCase));
        }
    }
}
