using System.Collections.Generic;

namespace Genesis.Receitas
{
    /// <summary>
    /// O que muda numa máquina quando ela é reconfigurada. Nada é alterado até Aplicar.
    /// Unifica PlanoPreparador e PlanoFlotador que hoje têm a mesma estrutura.
    /// </summary>
    internal class PlanoReconfiguracao
    {
        /// <summary>Id/DB antigo da máquina (ex.: "FL1").</summary>
        public string IdAntigo { get; set; }

        /// <summary>Id/DB novo (pode ser igual a IdAntigo se o usuário não trocou).</summary>
        public string IdNovo { get; set; }

        /// <summary>
        /// Pares (linha atual, linha que a receita geraria com as opções novas).
        /// A "linha nova" é uma linha "virtual" — serve para saber o que deveria ser; o que o usuário
        /// editou à mão na linha atual é preservado por AplicarNasMantidas.
        /// </summary>
        public List<KeyValuePair<Partida, Partida>> Mantidas { get; set; }

        /// <summary>Linhas que passam a existir (não existiam antes).</summary>
        public List<Partida> Novas { get; set; }

        /// <summary>Linhas atuais que deixam de existir (com confirmação do usuário).</summary>
        public List<Partida> Removidas { get; set; }

        /// <summary>
        /// Plano específico da receita (PlanoFlotador, PlanoPreparador...). A receita guarda aqui
        /// o objeto original para usar no AplicarNasMantidas sem perder informação específica.
        /// </summary>
        public object PlanoEspecifico { get; set; }

        public PlanoReconfiguracao()
        {
            Mantidas = new List<KeyValuePair<Partida, Partida>>();
            Novas = new List<Partida>();
            Removidas = new List<Partida>();
        }

        public bool Renomeia { get { return IdAntigo != IdNovo; } }
    }
}