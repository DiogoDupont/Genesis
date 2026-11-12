# Genesis — catálogo configurável de máquinas e componentes

## O que foi adicionado

- A página **Máquinas** ganhou o botão **Configurar padrões**.
- O catálogo permite editar categoria, código, nome, tipo padrão, descrição padrão, perfil de geração, inclusão padrão, agitador, PID, modulação, nível e estado ativo.
- Há ações para cadastrar químicos, equipamentos e componentes genéricos.
- Os dados são persistidos em `%AppData%\Genesis\catalogo-maquinas.xml`, separados dos projetos `.genesis`.
- O código padrão do desinfetante foi corrigido de `DES` para `DSN` para novos flotadores. Um catálogo antigo com `DES` é migrado em memória quando não existe `DSN`.
- Químicos cadastrados na categoria `Químico` e marcados como ativos passam a aparecer na configuração de novos flotadores.
- Equipamentos cadastrados na categoria `Equipamento` aparecem no menu **Novo item** dentro de uma máquina e criam uma partida com os padrões cadastrados.

## Limite atual da opção B

O campo **Perfil de geração = Personalizada** permite registrar e persistir a definição, mas não cria automaticamente lógica PLC. Para isso, ainda será necessário implementar um gerador específico que defina estrutura de DB/FB/FC, sinais, intertravamentos, comportamento e XML compatível com a versão do TIA Portal. O perfil está explícito na interface para não sugerir que essa lógica já existe.

## Compatibilidade e teste

- Projetos existentes não são reescritos pelo catálogo.
- Novos flotadores usam o catálogo salvo no momento da criação.
- A alteração foi feita no código-fonte. Não foi possível compilar/testar o executável neste ambiente, pois ele não dispõe do .NET Framework 4.8/Siemens TIA Openness para Windows.
