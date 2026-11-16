using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace Genesis.Receitas
{
    /// <summary>
    /// Receita do SUBSISTEMA Flotador. É wrapper em volta de Maquinas.cs (mesmo motor do antigo
    /// ReceitaFlotador), com uma diferença: marca Subsistema = id em cada linha criada, para a UI
    /// e futuras features (árvore de projeto, alarmes) distinguirem "veio do subsistema X".
    ///
    /// As linhas continuam tendo Maquina = id (compatibilidade com o AtualizarMaquinas atual).
    /// </summary>
    internal class ReceitaSubsistemaFlotador : IReceitaSubsistema
    {
        public string Id { get { return "Flotador"; } }
        public string NomeExibicao { get { return "Flotador"; } }
        public string Descricao { get { return "Subsistema completo: equalização, alimentação, microbolhas, raspador (com pás), floculador, lodo e químicos com níveis, vazão e PID de pH."; } }
        public string Icone { get { return "flotador"; } }

        public object OpcoesPadrao { get { return new OpcoesFlotador(); } }

        public string SugerirId(ContextoReceita ctx)
        {
            int n = 1;
            while (ctx.TodasAsLinhas.Any(x =>
                       string.Equals(x.Subsistema, "FL" + n, StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(x.Maquina, "FL" + n, StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(x.Sub, "FL" + n, StringComparison.OrdinalIgnoreCase)))
                n++;
            return "FL" + n;
        }

        public Form CriarForm(string sugestaoId, ContextoReceita ctx, Func<string, bool> dbEmUso, object editar)
        {
            var preparadores = ctx.TodasAsLinhas
                .Where(x => Maquinas.EhPreparador(x))
                .Select(x => x.Maquina)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            return new FlotadorForm(sugestaoId, preparadores, dbEmUso, editar as OpcoesFlotador);
        }

        public List<Partida> Criar(ContextoReceita ctx, object opcoes)
        {
            List<Partida> linhas = Maquinas.CriarFlotador((OpcoesFlotador)opcoes, ctx.TodasAsLinhas);
            string id = ((OpcoesFlotador)opcoes).Sub.Trim().ToUpperInvariant();
            foreach (Partida p in linhas)
            {
                p.Subsistema = id;
                if (string.IsNullOrEmpty(p.Maquina)) p.Maquina = id;   // compatibilidade com AtualizarMaquinas atual
            }
            return linhas;
        }

        public object OpcoesDe(ContextoReceita ctx, IEnumerable<Partida> itens, string id)
        {
            return Maquinas.OpcoesDoFlotador(id, ctx.TodasAsLinhas);
        }

        public PlanoReconfiguracao Planejar(ContextoReceita ctx, string id, List<Partida> itens, object novasOpcoes)
        {
            PlanoFlotador plano = Maquinas.PlanejarFlotador(id, ctx.TodasAsLinhas, (OpcoesFlotador)novasOpcoes);
            var generico = new PlanoReconfiguracao
            {
                IdAntigo = plano.DbAntigo,
                IdNovo = plano.DbNovo,
                PlanoEspecifico = plano
            };
            foreach (FlotMantida m in plano.Mantidas)
                generico.Mantidas.Add(new KeyValuePair<Partida, Partida>(m.Atual, m.Nova));
            generico.Novas.AddRange(plano.Novos);
            generico.Removidas.AddRange(plano.Removidas);
            return generico;
        }

        public void AplicarNasMantidas(PlanoReconfiguracao plano)
        {
            var original = plano.PlanoEspecifico as PlanoFlotador;
            if (original == null)
                throw new InvalidOperationException("AplicarNasMantidas chamado sem Planejar antes.");
            Maquinas.AplicarNasMantidasFlotador(original);
        }
    }
}