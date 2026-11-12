# Genesis

**Onde a lógica nasce.** Gerador de programas de CLP para TIA Portal (Openness).
É o antigo FAST Logic Generator com nome, marca e interface novos; o motor de geração é o mesmo (v8.0).

## Como abrir e compilar

1. Abra `Genesis.csproj` no Visual Studio (ou rode `dotnet build`). O projeto antigo `FASTOpennessTest.csproj` foi substituído por ele.
2. O executável sai como `Genesis.exe`, já com o ícone.
3. `Genesis.exe projeto.genesis` abre o projeto direto; `--pagina=gerar` (ou tags, maquinas, partidas, sensores, pids, modbus, config) abre numa página.

## O que continua igual (de propósito)

- Grupo de blocos e tabela de tags no TIA: `FAST_GERADO`. Trocar criaria blocos duplicados nos projetos que já existem.
- Pasta de modelos: `Templates\FAST_Export_*`.
- Formato do arquivo de projeto. Os `.fastproj` abrem normalmente; os novos são salvos como `.genesis`.

## O que muda no computador

- Configuração e última sessão passam para `%AppData%\Genesis`. Na primeira execução elas são copiadas de `%AppData%\FASTGenerator`.

## Arquivos

| Arquivo | Conteúdo |
|---|---|
| `Tema.cs` | cores, fontes, ícones em vetor, botões, cartões, caixas de mensagem |
| `MainForm.cs` | janela principal: barra do topo, páginas, editor, projeto |
| `PaginaGerar.cs` | página Gerar: o que gerar, conferência e disparo |
| `Recursos\` | ícone e logos embutidos no executável |
| `Marca\` | ícone e logos em SVG/PNG/ICO para usar fora do programa |
| `Gerador.cs`, `Comunicacao.cs`, `Maquinas.cs`, `AtualizadorXml.cs` | motor de geração (sem mudança de lógica) |

## Atalhos

Ctrl+S salvar · Ctrl+Shift+S salvar como · Ctrl+O abrir · F5 página Gerar · Ctrl+V colar tags (na página Tags)
Ctrl+1…6 etapas (Tags, Máquinas, Partidas, Sensores, PIDs, Modbus) · Ctrl+7 Configuração · Ctrl+0 início
