using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace Genesis.Receitas
{
    internal class ReceitaAdensador : IReceitaMaquina
    {
        public string Id { get { return "Adensador"; } }
        public string NomeExibicao { get { return Maquinas.Adensador; } }
        public string Descricao { get { return "Motor do adensador, bomba de alimentação, bomba e válvula de higienização. Tanque com sensor de nível opcional."; } }
        public string Icone { get { return "maquinas"; } }
        public object OpcoesPadrao { get { return new OpcoesAdensador(); } }

        public string SugerirId(ContextoReceita ctx) { return ctx.ProximoIdLivre("ADN"); }

        public Form CriarForm(string sugestaoId, ContextoReceita ctx, Func<string, bool> dbEmUso, object editar)
        {
            return new AdensadorForm(sugestaoId, dbEmUso, editar as OpcoesAdensador);
        }

        public List<Partida> Criar(ContextoReceita ctx, object opcoes) { return Maquinas.CriarAdensador((OpcoesAdensador)opcoes, ctx.TodasAsLinhas); }
        public object OpcoesDe(ContextoReceita ctx, IEnumerable<Partida> itens, string id) { return Maquinas.OpcoesDoAdensador(ctx.TodasAsLinhas, id); }

        public PlanoReconfiguracao Planejar(ContextoReceita ctx, string id, List<Partida> itens, object novasOpcoes)
        {
            PlanoAdensador plano = Maquinas.PlanejarAdensador(id, ctx.TodasAsLinhas.ToList(), (OpcoesAdensador)novasOpcoes);
            var generico = new PlanoReconfiguracao { IdAntigo = plano.DbAntigo, IdNovo = plano.DbNovo, PlanoEspecifico = plano };
            foreach (var m in plano.Mantidas) generico.Mantidas.Add(m);
            generico.Novas.AddRange(plano.Novos);
            generico.Removidas.AddRange(plano.Removidas);
            return generico;
        }

        public void AplicarNasMantidas(PlanoReconfiguracao plano)
        {
            var original = plano.PlanoEspecifico as PlanoAdensador;
            if (original != null) Maquinas.AplicarNasMantidasAdensador(original);
        }
    }
}