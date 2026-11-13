using System;
using System.Collections.Generic;
using System.Linq;

namespace Genesis.Receitas
{
    /// <summary>
    /// Executa um PlanoReconfiguracao genérico: aplica nas mantidas, renomeia, remove antigas,
    /// adiciona novas. Deixa a UI e o log para o chamador (MainForm), mas centraliza a parte
    /// que é sempre igual — e que hoje está duplicada em ReconfigurarPreparador / ReconfigurarFlotador.
    ///
    /// Fluxo:
    ///   1) (opcional) pede confirmação das removidas ao usuário via callback
    ///   2) renomeia o DB (se IdAntigo != IdNovo) em todas as linhas e tags
    ///   3) aplica nas mantidas via receita.AplicarNasMantidas(plano)
    ///   4) remove as antigas das listas
    ///   5) adiciona as novas nas listas
    ///   6) preenche campos automáticos das mantidas e novas
    /// </summary>
    internal static class AplicadorPlano
    {
        /// <summary>
        /// Aplica o plano. Devolve um resumo do que aconteceu para o chamador logar.
        /// </summary>
        /// <param name="receita">A receita que gerou o plano (para chamar AplicarNasMantidas).</param>
        /// <param name="plano">O plano calculado por receita.Planejar(...).</param>
        /// <param name="confirmar">Callback que pede confirmação das removidas. Recebe a lista, devolve true/false. Se null, não pede (assume true).</param>
        /// <param name="listas">As três listas onde as linhas vivem: partidas, sensores, pids.</param>
        /// <param name="ios">A lista de IOs (para RenomearDb atualizar tags).</param>
        /// <param name="preencherAutomaticos">Callback para preencher campos automáticos de uma linha (o MainForm já tem esse método privado; passa como delegate).</param>
        public static ResultadoAplicacao Aplicar(
            IReceitaMaquina receita,
            PlanoReconfiguracao plano,
            Func<List<Partida>, bool> confirmar,
            ListasDoProjeto listas,
            IList<Io> ios,
            Action<Partida> preencherAutomaticos)
        {
            if (receita == null) throw new ArgumentNullException("receita");
            if (plano == null) throw new ArgumentNullException("plano");
            if (listas == null) throw new ArgumentNullException("listas");

            var resultado = new ResultadoAplicacao();

            // 1) confirmação das removidas
            if (plano.Removidas.Count > 0)
            {
                if (confirmar != null && !confirmar(plano.Removidas))
                {
                    resultado.CanceladoPeloUsuario = true;
                    return resultado;
                }
            }

            // 2) renomeia DB (se mudou)
            if (plano.Renomeia)
            {
                var todas = listas.Partidas.Concat(listas.Sensores).Concat(listas.Pids).ToList();
                resultado.CamposRenomeados = Maquinas.RenomearDb(todas, plano.IdAntigo, plano.IdNovo, ios);
            }

            // 3) aplica nas mantidas (delega para a receita)
            receita.AplicarNasMantidas(plano);

            // 4) remove as antigas
            foreach (Partida p in plano.Removidas)
            {
                listas.Partidas.Remove(p);
                listas.Sensores.Remove(p);
                listas.Pids.Remove(p);
            }
            resultado.Removidas = plano.Removidas.Count;

            // 5) adiciona as novas (decide a lista pela categoria)
            foreach (Partida p in plano.Novas)
            {
                if (preencherAutomaticos != null) preencherAutomaticos(p);
                if (Gerador.EhPid(p)) listas.Pids.Add(p);
                else if (Gerador.EhSensor(p)) listas.Sensores.Add(p);
                else listas.Partidas.Add(p);
            }
            resultado.Novas = plano.Novas.Count;

            // 6) preenche automáticos das mantidas também (pode ter mudado tipo/descrição)
            if (preencherAutomaticos != null)
            {
                foreach (var par in plano.Mantidas) preencherAutomaticos(par.Key);
            }
            resultado.Mantidas = plano.Mantidas.Count;

            return resultado;
        }
    }

    /// <summary>
    /// Referência às três listas onde as linhas vivem no MainForm.
    /// Passado por referência para o AplicadorPlano poder remover/adicionar direto.
    /// </summary>
    internal class ListasDoProjeto
    {
        public System.ComponentModel.BindingList<Partida> Partidas { get; set; }
        public System.ComponentModel.BindingList<Partida> Sensores { get; set; }
        public System.ComponentModel.BindingList<Partida> Pids { get; set; }
    }

    /// <summary>
    /// Resumo do que o AplicadorPlano fez, para o chamador logar.
    /// </summary>
    internal class ResultadoAplicacao
    {
        public bool CanceladoPeloUsuario { get; set; }
        public int Mantidas { get; set; }
        public int Novas { get; set; }
        public int Removidas { get; set; }
        public int CamposRenomeados { get; set; }

        public string Resumo()
        {
            if (CanceladoPeloUsuario) return "Cancelado pelo usuário.";
            return Mantidas + " mantidos, " + Novas + " novos, " + Removidas + " removidos"
                + (CamposRenomeados > 0 ? " (" + CamposRenomeados + " campo(s) renomeado(s))" : "") + ".";
        }
    }
}