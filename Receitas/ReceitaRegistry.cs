using System;
using System.Collections.Generic;
using System.Linq;

namespace Genesis.Receitas
{
    /// <summary>
    /// Registro central de máquinas e subsistemas disponíveis no catálogo.
    /// Para adicionar uma nova máquina/subsistema, basta:
    ///   1) criar a classe que implementa IReceitaMaquina ou IReceitaSubsistema
    ///   2) adicionar uma linha na lista correspondente
    /// O MainForm não precisa ser tocado.
    /// </summary>
    internal static class ReceitaRegistry
    {
        /// <summary>Máquinas: equipamentos físicos reutilizáveis (Preparador, Adensador...).</summary>
        public static readonly IReadOnlyList<IReceitaMaquina> Maquinas = new IReceitaMaquina[]
        {
            new ReceitaPreparador(),
            new ReceitaFlotador(),   // temporário: migra para subsistema na Fase 3.2
        };

        /// <summary>Subsistemas: etapas do processo (Flotador, Adensador...).</summary>
        public static readonly IReadOnlyList<IReceitaSubsistema> Subsistemas = new IReceitaSubsistema[]
        {
            // vazios por enquanto: adicionados na Fase 3.2
        };

        /// <summary>Todas as receitas (para compatibilidade com código antigo que iterava só "Todas").</summary>
        public static IEnumerable<object> Todas
        {
            get { return Maquinas.Cast<object>().Concat(Subsistemas.Cast<object>()); }
        }

        public static IReceitaMaquina MaquinaPorId(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            return Maquinas.FirstOrDefault(r => string.Equals(r.Id, id, StringComparison.OrdinalIgnoreCase));
        }

        public static IReceitaMaquina MaquinaPorNome(string nome)
        {
            if (string.IsNullOrEmpty(nome)) return null;
            return Maquinas.FirstOrDefault(r => string.Equals(r.NomeExibicao, nome, StringComparison.OrdinalIgnoreCase));
        }

        public static IReceitaSubsistema SubsistemaPorId(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            return Subsistemas.FirstOrDefault(r => string.Equals(r.Id, id, StringComparison.OrdinalIgnoreCase));
        }

        public static IReceitaSubsistema SubsistemaPorNome(string nome)
        {
            if (string.IsNullOrEmpty(nome)) return null;
            return Subsistemas.FirstOrDefault(r => string.Equals(r.NomeExibicao, nome, StringComparison.OrdinalIgnoreCase));
        }

        // Compatibilidade com código antigo que chamava PorId/PorNome (busca em ambos)
        public static object PorId(string id) { return (object)MaquinaPorId(id) ?? SubsistemaPorId(id); }
        public static object PorNome(string nome) { return (object)MaquinaPorNome(nome) ?? SubsistemaPorNome(nome); }
    }
}