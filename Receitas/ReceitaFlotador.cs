using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace Genesis.Receitas
{
    /// <summary>
    /// Receita do Flotador. É um wrapper fino em volta do que já existe em Maquinas.cs:
    ///   CriarFlotador              → Criar
    ///   OpcoesDoFlotador           → OpcoesDe
    ///   PlanejarFlotador           → Planejar
    ///   AplicarNasMantidasFlotador → AplicarNasMantidas
    ///   FlotadorForm               → CriarForm
    ///
    /// Nada de lógica nova aqui. Só traduz o contrato genérico para as funções específicas.
    /// </summary>
    internal class ReceitaFlotador : IReceitaMaquina
    {
        public string Id { get { return "Flotador"; } }
        public string NomeExibicao { get { return "Flotador"; } }
        public string Descricao { get { return "Subsistema completo: equalização, alimentação, microbolhas, raspador (com pás), floculador, lodo e químicos com níveis, vazão e PID de pH."; } }
        public string Icone { get { return "flotador"; } }

        public string SugerirId(ContextoReceita ctx)
        {
            // o Flotador usa Sub == "FL1" para partidas próprias E Maquina == "FL1" para os sensores,
            // então o id tem que estar livre nos dois campos
            int n = 1;
            while (ctx.TodasAsLinhas.Any(x =>
                       string.Equals(x.Maquina, "FL" + n, StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(x.Sub, "FL" + n, StringComparison.OrdinalIgnoreCase)))
                n++;
            return "FL" + n;
        }

        public Form CriarForm(string sugestaoId, ContextoReceita ctx, Func<string, bool> dbEmUso, object editar)
        {
            // os preparadores existentes viram opção "Do preparador PPx" para o nível do floculante
            var preparadores = ctx.TodasAsLinhas
                .Where(x => Maquinas.EhPreparador(x))
                .Select(x => x.Maquina)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return new FlotadorForm(sugestaoId, preparadores, dbEmUso, editar as OpcoesFlotador);
        }

        public List<Partida> Criar(ContextoReceita ctx, object opcoes)
        {
            return Maquinas.CriarFlotador((OpcoesFlotador)opcoes, ctx.TodasAsLinhas);
        }

        public object OpcoesDe(ContextoReceita ctx, IEnumerable<Partida> itens, string id)
        {
            // OpcoesDoFlotador precisa de TODAS as linhas (para achar sensores de tanque compartilhados)
            return Maquinas.OpcoesDoFlotador(id, ctx.TodasAsLinhas);
        }

        public object OpcoesPadrao { get { return new OpcoesFlotador(); } }

        public PlanoReconfiguracao Planejar(ContextoReceita ctx, string id, List<Partida> itens, object novasOpcoes)
        {
            // PlanejarFlotador também precisa de TODAS as linhas (mesmo motivo)
            PlanoFlotador plano = Maquinas.PlanejarFlotador(id, ctx.TodasAsLinhas, (OpcoesFlotador)novasOpcoes);

            // traduz PlanoFlotador → PlanoReconfiguracao genérico
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
                throw new InvalidOperationException("AplicarNasMantidas chamado sem Planejar antes (PlanoEspecifico vazio).");
            Maquinas.AplicarNasMantidasFlotador(original);
        }
    }
}