# Primeira versão real — 0.1.0

Implementação iniciada em 08/10/2026, após o usuário solicitar o aplicativo real e reafirmar a preferência por Flutter. A prévia interativa deixou de ser o caminho de validação.

**Entrega de 09/10/2026:** pacote Windows x64 Release compilado em `app/dist/Mini32 Stream Deck`. Abra `Iniciar Mini32.cmd` na raiz do projeto. Mantenha as pastas `engine`, `data` e a biblioteca Flutter junto do executável.

## Estrutura implementada

- `app/flutter`: interface Flutter com tema escuro/laranja, bancos, teclado de 32 teclas, editor, biblioteca e conexões.
- `app/src`: motor C# com entrada MIDI WinMM, áudio NAudio 1.10.0 e cliente obs-websocket reutilizados/evoluídos a partir das bancadas.
- `app/Build-Engine.ps1`: compila o motor com o compilador .NET Framework disponível no Windows.
- `app/Build.ps1`: analisa/testa Flutter, compila Windows e empacota interface, motor e dependências.
- `Iniciar Mini32.cmd`: abre o pacote após a primeira compilação concluída.

A interface inicia o motor como processo filho e troca mensagens JSON por entrada/saída padrão redirecionadas. Não há servidor HTTP, porta de rede local ou comandos de shell enviados pela interface. Cada comando possui identificador e confirmação. Encerrar a interface fecha o canal e encerra o motor. O motor não depende da renderização Flutter para receber MIDI e produzir áudio.

O motor hospeda o ícone de bandeja; a janela Flutter trata Fechar como ocultar somente após a inicialização bem-sucedida do motor. Sair encerra ambos. Se o motor falhar, a interface exibe o erro e permite fechar normalmente.

## Funcionalidades desta entrega

- Até 100 bancos, cada um com 32 vínculos independentes. Mapa inicial: canal 1, notas 53–84. Aprendizagem individual impede duplicatas dentro do banco.
- Modo Editar seleciona teclas; modo Usar executa. A edição da tecla é confirmada em **Salvar tecla**. A interface sinaliza alterações não salvas.
- Ações compartilhadas por identidade; edição pode atualizar todos os vínculos ou criar uma cópia independente.
- Biblioteca própria: WAV/MP3 mono/estéreo, até dois minutos; conversão prévia para estéreo/48 kHz. Arquivos originais não são alterados.
- Até oito vozes, reinício/segurar/alternar, volume por ação e geral, loop, recorte, fade de entrada e ao parar, grupos exclusivos e redução de música interna.
- Pitch de ±12 semitons preservando duração, aplicado à última vinheta disparada com pitch habilitado. Novo áudio usa a posição atual.
- OBS: cena ao vivo, mute de entrada, cena da prévia, transição do Modo Estúdio, início/fim de gravação e transmissão. Transmissão e parada de gravação exigem manter a tecla por um segundo.
- Parar tudo encerra sons e cancela confirmações/disparos ainda aguardando carregamento.
- Reconexão MIDI/OBS quando habilitada, sem repetir comandos antigos. Perder o MIDI cancela confirmações e sons enquanto-segura; sons disparados normalmente continuam.
- Configuração versionada com escrita atômica e cópia de segurança. Credencial OBS lembrada protegida por DPAPI do usuário Windows. Nenhuma reprodução é retomada ao reiniciar.

## Limites explícitos

O aplicativo ainda não inclui macros, atalhos para programas externos, driver HID virtual, pacotes de exportação/importação, início com Windows, exclusão de bancos e personalização de cores por tecla. Atribuição de áudio usa os botões da biblioteca/editor; arrastar e soltar ainda não está implementado. Esses itens não foram removidos do planejamento geral.

A biblioteca aceita arquivos curtos; não é um reprodutor de faixas longas. Cache: 128 MB, além de amostras mantidas por vozes ativas e carregamentos em andamento. Uma vinheta fora do cache pode demorar mais no primeiro disparo. Oito vozes é um limite operacional inicial, não uma garantia de desempenho no i3-2120.

A redução automática diminui apenas sons marcados como música dentro deste aplicativo. As ações de OBS são diretas, sem emular o teclado comum. O processo de áudio para captura no OBS é **Mini32.Engine.exe**.

Botões de oitava continuam transpondo notas: não há inferência automática de bancos. A ambiguidade das teclas 29/30 do diagnóstico pode ser corrigida por aprendizagem, respeitando vínculos únicos.

A interface reorganiza o editor abaixo do teclado em larguras menores. A janela Windows tem tamanho mínimo utilizável; não se promete funcionamento legível em dimensões arbitrariamente pequenas.

## Verificação realizada durante a construção

- Motor compilado no Windows local.
- Testes de áudio: medição de frequência de 220/440/880 Hz para o sinal original de 440 Hz; duração preservada; ausência de amostras inválidas; mixagem, reinício, loop, parada individual e grupos exclusivos.
- Persistência: ida/volta de configuração, recuperação de arquivo corrompido, rejeição de notas duplicadas, proteção/desproteção de credenciais e conversão de áudio mono/22,05 kHz.
- Protocolo completo: importação de exemplo, bancos e ações compartilhadas, reinício sem executar ações e autenticação OBS contra servidor de teste local.
- Ações sensíveis: toque curto não executa, pressão longa executa uma vez, Parar tudo cancela a confirmação pendente. Nenhum desses testes iniciou uma transmissão no OBS real.

- Flutter: análise estática sem problemas; quatro testes de interface aprovados, cobrindo janelas de 1280×900, 900×700 e 760×640, alternância Editar/Usar, Parar tudo e salvar a edição de uma tecla. Capturas com fontes reais do Windows foram inspecionadas.
- Corrigido o fundo dos bancos para que o destaque de seleção seja pintado corretamente.
- Corrigida a leitura do canal de comandos no .NET Framework: o motor lê diretamente o identificador de entrada herdado, pois a leitura via Console não respondia aos pipes criados pelo Dart no Windows. Testes do protocolo foram repetidos com sucesso.
- Executável final iniciado em teste isolado: inicialização nativa, criação do processo do motor, comandos/respostas JSON, criação de banco com 32 teclas e atualização da configuração confirmadas. Encerramento com código 0 e sem processo do motor restante. Relatório gerado em `app/flutter/build/smoke-report.json`.

O teste integrado pode ser repetido executando o pacote com `--smoke-test --smoke-report=<caminho-absoluto>`. Ele usa uma pasta temporária, não conecta ao MIDI/OBS e não modifica a biblioteca do usuário.

Não confundir essas verificações com o aceite do aplicativo integrado na Mini 32 física. O usuário já aprovou o som das bancadas anteriores; a primeira versão integrada ainda requer teste manual com seu equipamento e OBS.

## Identidade visual

O cabeçalho usa a arte fornecida em `final-logo.png`, substituindo o nome e o símbolo genéricos. `final-icon.png` fornece os ícones do executável, da janela e da bandeja. Os originais permanecem intactos na raiz; `app/Prepare-Branding.ps1` gera os derivados de distribuição, removendo apenas margens transparentes e criando o ICO em tamanhos de 16 a 256 pixels.

A interface Flutter usa a família Roboto, com arquivos locais de pesos e itálicos em `app/flutter/assets/fonts`, incluindo a licença Apache 2.0. Não depende de download de fontes durante o uso. Diálogos nativos do Windows, como o seletor de arquivos, seguem a aparência do sistema.

O cabeçalho foi ajustado para uma logo menor e substitui a barra de título do Windows. Seus botões controlam minimizar, ampliar/restaurar e fechar para a bandeja. A área livre da logo move a janela; duplo clique amplia/restaura. A janela mantém a borda nativa de redimensionamento e calcula o tamanho maximizado conforme a área de trabalho do monitor, respeitando a barra de tarefas.

Verificação no pacote Windows: cinco testes Flutter aprovados; a janela nativa apresentou estilo `0x840f0000`, sem `WS_CAPTION` e com `WS_THICKFRAME`. O teste integrado de inicialização Flutter/C# também passou. O gesto de arrastar e os controles físicos da janela devem ser conferidos visualmente no uso normal.

## Ferramentas

Flutter 3.47.7 / Dart 3.13.5 preparado em `.tools/flutter`. Visual Studio Community 2026 com C++, CMake e Windows SDK instalado pelo usuário e reconhecido pela compilação. O SDK local precisou de correções de bootstrap e atualização do parâmetro de cache dos downloads, mantendo a origem oficial e a validação de integridade. `app/Build.ps1` usa o hash de conteúdo do motor registrado no SDK.

Referências: [preparação Flutter para Windows](https://docs.flutter.dev/platform-integration/windows/setup), [compiladores Microsoft C++](https://learn.microsoft.com/en-us/cpp/overview/acquire-msvc), [protocolo obs-websocket](https://github.com/obsproject/obs-websocket/blob/master/docs/generated/protocol.md).
