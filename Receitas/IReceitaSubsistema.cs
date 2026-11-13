using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace Genesis.Receitas
{
    /// <summary>
    /// Contrato de um SUBSISTEMA (etapa do processo, composta de máquinas + periféricos).
    /// Ex.: Subsistema Adensador = Máquina Adensador + periféricos.
    /// Ex.: Subsistema Flotador = máquinas opcionais (Preparador) + periféricos próprios.
    ///
    /// A diferença em relação a IReceitaMaquina:
    ///  - um subsistema pode INCLUIR uma ou mais máquinas (via MaquinasIncluidas)
    ///  - um subsistema cria PERIFÉRICOS que não são de nenhuma máquina
    ///  - o resultado final são linhas com Subsistema = id, algumas com Maquina preenchida
    ///    (as que vieram da máquina), outras não (periféricos).
    /// </summary>
    internal interface IReceitaSubsistema
    {
        /// <summary>Identificador estável. Ex.: "SubsistemaAdensador".</summary>
        string Id { get; }

        /// <summary>Nome mostrado no catálogo. Ex.: "Adensador (subsistema)".</summary>
        string NomeExibicao { get; }

        /// <summary>Descrição curta no cartão.</summary>
        string Descricao { get; }

        /// <summary>Ícone (Tema.Icones).</summary>
        string Icone { get; }

        /// <summary>Opções do "padrão de fábrica". Nunca null.</summary>
        object OpcoesPadrao { get; }

        /// <summary>Sugere o próximo id livre. Ex.: "AD1", "AD2".</summary>
        string SugerirId(ContextoReceita ctx);

        /// <summary>Abre o form. editar = null → criar; com valor → reconfigurar.</summary>
        Form CriarForm(string sugestaoId, ContextoReceita ctx, Func<string, bool> dbEmUso, object editar);

        /// <summary>
        /// Cria TODAS as linhas do subsistema: primeiro as máquinas incluídas (via contexto),
        /// depois os periféricos próprios. O resultado já tem Subsistema/Maquina preenchidos.
        /// </summary>
        List<Partida> Criar(ContextoReceita ctx, object opcoes);

        /// <summary>Lê as opções de volta a partir das linhas existentes.</summary>
        object OpcoesDe(ContextoReceita ctx, IEnumerable<Partida> itens, string id);

        /// <summary>Compara linhas atuais com opções novas.</summary>
        PlanoReconfiguracao Planejar(ContextoReceita ctx, string id, List<Partida> itens, object novasOpcoes);

        /// <summary>Aplica nas linhas que continuam.</summary>
        void AplicarNasMantidas(PlanoReconfiguracao plano);
    }
}