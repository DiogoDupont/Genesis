using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace Genesis.Receitas
{
    internal class ReceitaSubsistemaAdensador : IReceitaSubsistema
    {
        public string Id { get { return "SubsistemaAdensador"; } }
        public string NomeExibicao { get { return Maquinas.SubsistemaAdensador; } }
        public string Descricao { get { return "Máquina Adensador + periféricos opcionais (agitadores, floculador, bombas de lodo e floculante)."; } }
        public string Icone { get { return "flotador"; } }
        public object OpcoesPadrao { get { return new OpcoesSubsistemaAdensador(); } }

        public string SugerirId(ContextoReceita ctx) { return ctx.ProximoIdLivre("ADN"); }

        public Form CriarForm(string sugestaoId, ContextoReceita ctx, Func<string, bool> dbEmUso, object editar)
        {
            return new SubsistemaAdensadorForm(sugestaoId, dbEmUso, editar as OpcoesSubsistemaAdensador);
        }

        public List<Partida> Criar(ContextoReceita ctx, object opcoes) { return Maquinas.CriarSubsistemaAdensador((OpcoesSubsistemaAdensador)opcoes, ctx.TodasAsLinhas); }
        public object OpcoesDe(ContextoReceita ctx, IEnumerable<Partida> itens, string id) { return Maquinas.OpcoesDoSubsistemaAdensador(ctx.TodasAsLinhas, id); }

        public PlanoReconfiguracao Planejar(ContextoReceita ctx, string id, List<Partida> itens, object novasOpcoes)
        {
            PlanoAdensador plano = Maquinas.PlanejarSubsistemaAdensador(id, ctx.TodasAsLinhas.ToList(), (OpcoesSubsistemaAdensador)novasOpcoes);
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
