using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace Genesis.Receitas
{
    /// <summary>
    /// Receita do Preparador de Polímero. Wrapper em volta de Maquinas.cs:
    ///   CriarPreparador              → Criar
    ///   OpcoesDoPreparador           → OpcoesDe
    ///   PlanejarPreparador           → Planejar
    ///   AplicarNasMantidas           → AplicarNasMantidas
    ///   MaquinaForm                  → CriarForm
    ///
    /// O "núcleo" da máquina (SIS.AUT) e as redes do preparador ficam no Gerador.cs
    /// (partial Maquinas). A receita só monta as linhas.
    /// </summary>
    internal class ReceitaPreparador : IReceitaMaquina
    {
        public string Id { get { return "Preparador"; } }
        public string NomeExibicao { get { return Maquinas.Preparador; } }   // "Preparador de polímero"
        public string Descricao { get { return "Rosca, 1 ou 2 agitadores, válvula de alimentação, medidor de vazão e nível (digital ou analógico). Bloco BL_POLIMERO_PREPARO_FLOCULANTE."; } }
        public string Icone { get { return "preparador"; } }

        public string SugerirId(ContextoReceita ctx)
        {
            // o Preparador usa Maquina == "PP1" para todas as linhas (partidas e sensores)
            return ctx.ProximoIdLivre("PP");
        }

        public Form CriarForm(string sugestaoId, ContextoReceita ctx, Func<string, bool> dbEmUso, object editar)
        {
            return new MaquinaForm(sugestaoId, dbEmUso, editar as OpcoesPreparador);
        }

        public List<Partida> Criar(ContextoReceita ctx, object opcoes)
        {
            return Maquinas.CriarPreparador((OpcoesPreparador)opcoes);
        }

        public object OpcoesDe(ContextoReceita ctx, IEnumerable<Partida> itens, string id)
        {
            return Maquinas.OpcoesDoPreparador(ctx.TodasAsLinhas, id);
        }

        public object OpcoesPadrao { get { return new OpcoesPreparador(); } }

        public PlanoReconfiguracao Planejar(ContextoReceita ctx, string id, List<Partida> itens, object novasOpcoes)
        {
            PlanoPreparador plano = Maquinas.PlanejarPreparador(id, ctx.TodasAsLinhas.ToList(), (OpcoesPreparador)novasOpcoes);

            var generico = new PlanoReconfiguracao
            {
                IdAntigo = plano.DbAntigo,
                IdNovo = plano.DbNovo,
                PlanoEspecifico = plano
            };
            foreach (var par in plano.Mantidas)
                generico.Mantidas.Add(new KeyValuePair<Partida, Partida>(par.Key, par.Value));
            generico.Novas.AddRange(plano.Novos);
            generico.Removidas.AddRange(plano.Removidas);
            return generico;
        }

        public void AplicarNasMantidas(PlanoReconfiguracao plano)
        {
            var original = plano.PlanoEspecifico as PlanoPreparador;
            if (original == null)
                throw new InvalidOperationException("AplicarNasMantidas chamado sem Planejar antes (PlanoEspecifico vazio).");
            Maquinas.AplicarNasMantidas(original);
        }
    }
}