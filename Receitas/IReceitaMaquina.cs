using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace Genesis.Receitas
{
    /// <summary>
    /// Contrato de uma "receita" de máquina/subsistema: o que o Genesis sabe criar
    /// (Preparador, Flotador, Lodos Ativados, Elevatória...).
    ///
    /// A receita é responsável por:
    ///  - saber quais opções pedir ao usuário (via CriarForm)
    ///  - transformar essas opções em linhas de Partida (via Criar)
    ///  - saber reler as opções a partir das linhas já existentes (OpcoesDe)
    ///  - saber o que muda quando as opções mudam (Planejar + AplicarNasMantidas)
    ///
    /// Ela NÃO é responsável por:
    ///  - geração de XML (isso é do Gerador)
    ///  - UI da lista/editor (isso é do MainForm)
    ///  - persistência (isso é do Modelo/Projeto)
    /// </summary>
    internal interface IReceitaMaquina
    {
        /// <summary>Identificador estável, sem espaços. Ex.: "Flotador", "Preparador". Não muda nunca (vai no .genesis).</summary>
        string Id { get; }

        /// <summary>Nome mostrado no catálogo de máquinas. Ex.: "Flotador".</summary>
        string NomeExibicao { get; }

        /// <summary>Descrição curta mostrada no cartão do catálogo.</summary>
        string Descricao { get; }

        /// <summary>Nome do ícone em Tema.Icones.Existe(nome). Ex.: "flotador", "preparador".</summary>
        string Icone { get; }

        /// <summary>Opções do "padrão de fábrica" (o mais comum). Nunca null.</summary>
        object OpcoesPadrao { get; }

        /// <summary>Sugestão de id/DB para uma nova instância (ex.: "FL1", "PP1"). O usuário pode trocar no form.</summary>
        string SugerirId(ContextoReceita ctx);

        /// <summary>
        /// Abre a janela de criação (nova máquina) ou de reconfiguração (edição).
        /// Deve retornar OK/Cancel. Em OK, preenche ResultadoReceita com as opções escolhidas.
        /// O form é responsável por validar (id válido, não em uso, etc.).
        /// </summary>
        /// <param name="editar">null = nova máquina; com valor = reconfigurar (a janela abre preenchida com essas opções).</param>
        /// <param name="dbEmUso">true se o id já está sendo usado no projeto (para validar unicidade).</param>
        Form CriarForm(string sugestaoId, ContextoReceita ctx, Func<string, bool> dbEmUso, object editar);

        /// <summary>
        /// A partir das opções (objeto específico da receita), gera as linhas de Partida/Sensor/PID.
        /// </summary>
        /// <param name="opcoes">Retorno de CriarForm.ResultadoReceita.Opcoes (ou de OpcoesDe).</param>
        List<Partida> Criar(ContextoReceita ctx, object opcoes);

        /// <summary>
        /// Relê as opções a partir das linhas atuais da máquina (para abrir o form em modo reconfigurar).
        /// </summary>
        /// <param name="itens">Linhas atuais da máquina (ctx.LinhasDaMaquina(id)).</param>
        object OpcoesDe(ContextoReceita ctx, IEnumerable<Partida> itens, string id);

        /// <summary>
        /// Compara as linhas atuais com as opções novas e devolve o plano do que muda.
        /// Não altera nada — só calcula.
        /// </summary>
        /// <param name="itens">Linhas atuais da máquina (ctx.LinhasDaMaquina(id)).</param>
        PlanoReconfiguracao Planejar(ContextoReceita ctx, string id, List<Partida> itens, object novasOpcoes);

        /// <summary>
        /// Aplica nas linhas que continuam o que as opções mudaram (tipo de acionamento,
        /// intertravamentos, modulações...), preservando o que o usuário editou à mão.
        /// O PlanoReconfiguracao já traz as listas Mantidas/Novas/Removidas — este método
        /// só cuida de atualizar as Mantidas no lugar.
        /// </summary>
        void AplicarNasMantidas(PlanoReconfiguracao plano);
    }

    /// <summary>
    /// O que o form de uma receita devolve quando o usuário confirma.
    /// </summary>
    internal class ResultadoReceita
    {
        /// <summary>Objeto de opções específico da receita (OpcoesFlotador, OpcoesPreparador...).</summary>
        public object Opcoes { get; set; }
    }
        /// <summary>
    /// Interface que os forms de receita implementam para devolver as opções escolhidas.
    /// Os forms existentes (FlotadorForm, MaquinaForm) já têm propriedades com o resultado;
    /// esta interface só dá um nome comum para o MainForm acessar de forma genérica.
    /// </summary>
    internal interface IRetornaResultadoReceita
    {
        ResultadoReceita Resultado { get; }
    }
}