# Upgrade 0.2.0-dev.2 — etapas de personalização

Esta é uma entrega de desenvolvimento do upgrade planejado. Ela mantém a Mini 32 e as ações da versão 0.1.0, acrescentando a primeira camada visual e de personalização. Não representa ainda a versão Pro completa.

## Entregue

- Nova direção visual glassmorphism: fundo baseado em `#1B1919`, iluminação ambiente quente, painéis translúcidos, desfoque limitado aos painéis principais e bordas claras. A grade de 32 teclas usa camadas leves, sem 32 filtros de desfoque.
- Forma de onda no espaço entre a logo e os controles da janela. Ela usa a amplitude da mistura real enviada à saída de áudio; sem reprodução, fica parada. Com vários sons, mostra a mistura e informa o nome do áudio mais recente ao passar o mouse. A atualização do medidor não reconstrói a tela inteira.
- `final-app-icon.png` é a fonte definitiva dos ícones do executável Flutter, do motor e da bandeja. O script de compilação regenera os arquivos ICO a partir dele e conserva o PNG original.
- Barra de menus no topo: Arquivo, Editar, Bancos, Exibir, Conexões e Ajuda. Menus de clique direito nas teclas, bancos e itens da biblioteca de áudio.
- Imagem PNG/JPEG independente por tecla. O arquivo selecionado é copiado para `%LOCALAPPDATA%\Mini32 Stream Deck\icons`; a grade o apresenta com 50% de opacidade atrás do nome e uma camada escura para manter a leitura. É possível trocar/remover a imagem.
- Volume de áudio independente por tecla, entre 0% e 100%. Ações compartilhadas podem ter volumes diferentes em cada vínculo; o volume geral permanece separado. O editor salva nome/ação e volume da tecla juntos. No modo Usar, um seletor permite ajustar o volume da tecla enquanto a música está tocando.
- Faixas WAV/MP3 com mais de dois minutos (até quatro horas) usam decodificação progressiva em uma fila limitada a dois segundos de áudio. As vinhetas curtas continuam pré-carregadas para reduzir a latência de disparo. Ao atribuir uma faixa longa, o app sugere comportamento de música de fundo: volume inicial de 30%, repetição, alternar tocar/parar, fade de 500 ms e pitch desligado. O usuário pode mudar todos esses ajustes.
- Configuração versão 2. A leitura migra a versão 1 em memória, preservando bancos, ações e biblioteca. A próxima gravação usa escrita atômica e mantém a cópia de segurança anterior. Antes da atualização, uma cópia extra de `settings.json` foi preservada na pasta de dados do usuário.
- Clique direito no modo Usar abre o menu da tecla sem dispará-la.

## Como experimentar

1. Abra `Iniciar Mini32.cmd`. A primeira abertura lê as configurações existentes.
2. No modo Editar, selecione uma tecla e clique em **Escolher imagem da tecla**. A imagem aparecerá na representação da tecla dentro do app. A Mini 32 física não tem tela; a imagem não aparece nela.
3. Para uma tecla de áudio, ajuste **Volume desta tecla** e clique em **Salvar tecla**. Se duas teclas usarem a mesma ação, os volumes continuam separados.
4. Importe uma música longa para ver os ajustes iniciais de música de fundo; revise volume e repetição no editor da tecla.
5. No modo **Usar**, escolha a tecla no painel lateral e mova o controle de volume; a mudança alcança a música que já está tocando.
6. Clique com o botão direito em uma tecla ou use a nova barra de menus para conferir os atalhos.

## Verificação

- Testes do motor: migração v1→v2, persistência e validação de volume/imagem, volume efetivo de duas teclas que compartilham ação.
- Teste do motor com faixa de 121 segundos: caminho progressivo, primeiras amostras audíveis, parada e liberação da voz.
- Teste do motor: 48 amostras do medidor reagem ao sinal real e são limpas em **Parar tudo**. O protocolo envia quadros de medição independentes do estado geral; o widget do cabeçalho atualiza somente sua área.
- Testes Flutter: layout, modos, edição, volume por tecla, opacidade da imagem, menu de clique direito sem disparo e controles da janela.
- Teste de protocolo com OBS simulado: ações compartilhadas, volumes distintos, reinício e comandos OBS.
- Compilação Windows e teste de inicialização isolada do pacote, sem acessar a controladora real ou o OBS real.
- O pacote `0.2.0-dev.2` fica em uma pasta própria para preservar o executável anterior enquanto ele estiver aberto. Testes com `--data-dir` usam uma instância isolada do motor, sem tomar o lugar do aplicativo em uso.

## Próximas etapas do upgrade

1. Controles adicionais de música: pausa/retomada e redução automática configurável. Repetição, fades e volume por tecla já estão disponíveis; prévia dedicada de volume ainda falta.
2. Expansão das ações do OBS e Windows; sequências de ações.
3. Perfis, páginas e pastas; importação/exportação; testes completos com a Mini 32 e o OBS reais.

Não existe driver HID virtual nesta etapa. A comunicação direta com o OBS continua evitando conflito com o teclado comum.
