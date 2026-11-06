# Direção de interface e integração Flutter

08/10/2026 — planejamento da próxima entrega.

## Confirmado

- Usuário aprovou o resultado auditivo e a gravação local da bancada.
- Deseja interface bonita, intuitiva e fácil de operar, preferencialmente em Flutter.
- Continuam válidos: Windows inicial, português do Brasil, tema escuro, janela ajustável, representação das 32 teclas, nomes/cores e editor lateral.
- Requisitos funcionais já respondidos não serão repetidos no formulário visual.

## Proposta de arquitetura

Flutter apresenta a interface e envia configurações/comandos. Um motor C# local recebe MIDI, executa ações, processa áudio e controla o OBS. O caminho de uma tecla física até o som deve continuar dentro do motor, sem depender da renderização da interface.

O protótipo atual mistura interface e parte da coordenação: será necessário separar responsabilidades e ampliar o motor. Não basta trocar a aparência da janela ou supor que Flutter chama diretamente qualquer biblioteca C#.

Comunicação local entre processos é a primeira alternativa a avaliar, com mensagens versionadas, confirmação, estados e cancelamento. Não transmitir as amostras de áudio por essa comunicação. Definir identificação da sessão, acesso apenas local, ciclo de vida e recuperação de falhas antes de integrar.

Se o áudio passar a ser produzido por processo auxiliar, validar novamente qual executável o OBS deve capturar. O pacote deve esconder essa complexidade operacional com nomes estáveis, instruções claras e encerramento coordenado. Fechar à bandeja não pode matar o motor; Sair deve encerrar ambos e liberar MIDI.

## Direção visual consolidada após o formulário

- Estilo de estúdio moderno: elegante, discreto e organizado.
- Fundo escuro, contraste legível e laranja como cor principal para seleção, foco e ações de destaque.
- Espaçamento equilibrado, com prioridade para leitura e facilidade de operação.
- Teclado de 32 teclas no centro, com identificação visível e ação atribuída.
- Editor lateral para a tecla selecionada, com apenas os campos relevantes à ação escolhida.
- Bancos na lateral esquerda, visíveis nas janelas que comportem a composição completa.
- Biblioteca de áudio com busca; atribuição tanto por arrastar sobre a tecla quanto por seleção no editor.
- Indicadores claros de MIDI, OBS, som em reprodução e estado salvo.
- Parar tudo sempre visível.
- Modo Editar: clicar numa tecla abre sua configuração. Modo Usar: clicar executa a ação.
- Assistente curto na primeira abertura e dicas opcionais, sem números MIDI ou detalhes técnicos como requisito para usar.
- Fluxo principal: configurar antes da live e deixar o aplicativo minimizado durante a operação.
- Animações discretas, sem efeitos contínuos que disputem recursos com o OBS.

O usuário delegou as demais escolhas estéticas. Flutter continua sendo a preferência para implementação; a legibilidade e a facilidade de uso serão verificadas na prévia.

## Adaptação ao tamanho da janela

O usuário solicitou adaptação a qualquer tamanho. A implementação deve reorganizar a composição conforme o espaço disponível, sem prometer legibilidade em dimensões arbitrariamente pequenas. O tamanho mínimo utilizável e os pontos de reorganização serão definidos após testar a prévia com diferentes larguras e escalas de texto do Windows.

- Janela ampla: bancos à esquerda, teclado e biblioteca no centro, editor à direita.
- Janela intermediária: editor abre como painel recolhível ou sobreposto; banco atual continua identificado.
- Janela estreita: bancos acessíveis por botão/painel, conteúdo em coluna e editor em vista própria. Todas as funções continuam acessíveis, mesmo que não estejam simultaneamente visíveis.
- Teclado compacto: manter proporções e identificação das teclas; usar rolagem horizontal dentro da área do teclado quando necessário, sem quebrar a ordem física nem reduzir indefinidamente os alvos.
- Parar tudo e indicação do modo permanecem acessíveis; não depender apenas de cores para comunicar estado.
- Quando o editor abrir ou fechar, preservar a tecla selecionada e os dados em edição.
- Arrastar é um atalho opcional: o editor sempre oferece uma alternativa por clique/teclado.

Essa estratégia é uma proposta técnica para atender à preferência de adaptação, não uma mudança na preferência por bancos à esquerda.

## Roteiro da prévia navegável

1. Tela principal com banco de exemplo, 32 teclas, estado de conexão ilustrativo e Parar tudo.
2. Modo Editar: selecionar tecla, dar nome, escolher ação de áudio ou cena, ajustar parâmetros e aplicar a configuração na demonstração.
3. Atribuir vinheta da biblioteca pelo editor e por arrastar, com indicação do destino.
4. Trocar de banco e alternar para Usar; cliques simulam a execução visualmente.
5. Demonstrar janela ampla e compacta, assistente inicial e mensagens de vazio/desconexão.

A prévia deverá identificar claramente os estados simulados. Não conectará ao OBS, não disparará áudio real nem será apresentada como aplicativo Flutter já implementado. A aparência serve para validar navegação e composição antes de construir e integrar.

Critério de avaliação: o usuário consegue reconhecer banco/modo, configurar uma tecla e localizar Parar tudo sem precisar de explicação técnica adicional.

## Sequência proposta

1. Formulário curto para preferências visuais e operação — respondido e consolidado.
2. Prévia navegável da tela principal e editor; ainda não é o aplicativo Flutter final.
3. Verificação completa do ambiente e preparação das ferramentas Flutter/Windows.
4. Separação do motor e prova de comunicação com uma janela Flutter mínima.
5. Primeira entrega vertical: escolher uma tecla, atribuir som/cena, executar e restaurar ao reabrir.
6. Expandir para 32 teclas, bancos, biblioteca e estados do OBS.
7. Adicionar demais recursos confirmados, testar carga e preparar distribuição.

## Ambiente observado

Na inspeção inicial, `flutter`, `dart` e `cmake` não foram encontrados no PATH, e o instalador Visual Studio não foi localizado no caminho padrão verificado. Isso não comprova ausência em qualquer diretório. Nenhuma ferramenta foi instalada nesta etapa.

Para compilar Flutter Windows, a documentação oficial exige Flutter SDK e ferramentas Visual Studio com a carga de desenvolvimento desktop C++. Preparar esse ambiente é parte do desenvolvimento, não uma exigência de instalar Flutter no computador de quem apenas usar o aplicativo distribuído.

## Critérios para a primeira entrega visual funcional

- Usuário configura uma tecla sem editar arquivo ou conhecer números MIDI.
- Aprendizagem da tecla não dispara acidentalmente som/cena.
- Reinício restaura configurações, sem iniciar áudio ou transmissão.
- Interface representa estado confirmado e mostra erros de conexão compreensíveis.
- MIDI/áudio continuam respondendo com a interface minimizada.
- Aparência legível na janela/tela escolhida, com teclado, editor e Parar tudo acessíveis.
- Comparar consumo e resposta com a bancada, usando build de produção para avaliação de desempenho.

## Referências

- [Flutter: suporte desktop](https://docs.flutter.dev/platform-integration/desktop)
- [Preparar desenvolvimento Windows](https://docs.flutter.dev/platform-integration/windows/setup)
- [Código específico de plataforma](https://docs.flutter.dev/platform-integration/platform-channels)

## Prévia navegável entregue

Foi criada uma maquete interativa com tema escuro e laranja, bancos à esquerda, teclado físico de 32 teclas, biblioteca de exemplo e editor. Permite renomear teclas, escolher áudio ou cena, ajustar volume/comportamento/pitch/loop, criar bancos de demonstração e alternar entre Editar e Usar. A execução é apenas visual; não produz áudio nem conecta ao MIDI ou OBS.

A prévia reorganiza os painéis em coluna conforme a largura. Em espaços estreitos, divide o teclado em páginas 01–16 e 17–32, preservando acesso a todas as teclas. Essa solução concretiza uma alternativa à rolagem horizontal proposta acima; a composição final em Flutter será definida com o retorno do usuário.

Verificações automatizadas no Edge: seleção e edição, retenção da configuração ao trocar bancos, atribuição por botão, criação de banco, simulação de áudio/cena, segurar/soltar, Parar tudo e acesso à última tecla na apresentação compacta. Os manipuladores de arrastar/soltar foram exercitados com eventos sintéticos; o gesto real ainda depende de avaliação manual. Não foram detectados erros JavaScript nem transbordamento horizontal nas larguras de janela 768, 375 e 320 pixels. A composição ampla e as apresentações menores também foram inspecionadas visualmente.

Esses testes validam somente a maquete. Não comprovam desempenho do aplicativo final, escalas de texto do Windows ou integração com o equipamento.

Atualização de 09/10/2026: a prévia foi substituída pela implementação real em `app/flutter`, conectada ao motor C#. Consulte [a primeira versão](09-primeira-versao.md) para o estado da entrega, verificações e limites. A maquete acima permanece apenas como registro histórico.
