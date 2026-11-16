namespace Genesis.Receitas
{
    /// <summary>
    /// Contrato de um SUBSISTEMA (etapa do processo, composta de máquinas + periféricos).
    /// Ex.: Subsistema Adensador = Máquina Adensador + periféricos.
    /// Ex.: Subsistema Flotador = máquinas opcionais (Preparador) + periféricos próprios.
    ///
    /// Herda de IReceitaMaquina porque, do ponto de vista do motor (Criar, Planejar, Aplicar),
    /// um subsistema É uma receita. A diferença é conceitual: ele pode incluir máquinas no
    /// resultado (as linhas com Maquina preenchida) + periféricos (linhas sem Maquina).
    ///
    /// A distinção máquina vs subsistema aparece no ReceitaRegistry (listas separadas) e
    /// no catálogo da UI (grupos "Máquinas" e "Subsistemas"), não no contrato.
    /// </summary>
    internal interface IReceitaSubsistema : IReceitaMaquina
    {
        // Nenhum membro extra por enquanto.
        // Um subsistema é uma receita como qualquer outra.
    }
}