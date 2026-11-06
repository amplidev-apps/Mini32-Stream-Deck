# Requisitos e comportamentos

Versão 0.1 — 08/10/2026. C = confirmado; P = proposto; V = a validar.

## Catálogo de requisitos

| ID | Estado | Requisito |
|---|---|---|
| MIDI-01 | C/V | Selecionar a Mini 32 e aprender mensagens reais de teclas e controles |
| MIDI-02 | C | Mapear cada tecla independentemente em cada banco |
| MIDI-03 | C | Liberar o dispositivo ao encerrar o aplicativo |
| MIDI-04 | C/V | Reconectar após desconexão, sem executar comandos antigos |
| BANK-01 | C | Criar, renomear e alternar bancos pela interface ou tecla configurada |
| BANK-02 | V | Usar botões de oitava como seleção de banco, caso seja identificável |
| ACT-01 | C | Permitir ações compartilhadas entre diferentes teclas |
| ACT-02 | C | Configurar comportamento de acionamento por tecla |
| ACT-03 | C | Executar macros e atalhos para outros programas |
| OBS-01 | C | Trocar cena e silenciar/reativar áudio |
| OBS-02 | C | Iniciar/parar gravação e iniciar/encerrar transmissão |
| OBS-03 | C | Controlar prévia e transição no Modo Estúdio |
| OBS-04 | P | Mostrar/ocultar fontes e ajustar volume |
| OBS-05 | P | Listar alvos disponíveis e refletir alterações realizadas no próprio OBS |
| AUD-01 | C | Importar áudio copiando para biblioteca própria |
| AUD-02 | P/V | Aceitar inicialmente WAV e MP3 |
| AUD-03 | C | Reiniciar, manter enquanto segura ou alternar reprodução conforme configuração |
| AUD-04 | C | Misturar sons simultâneos e suportar grupos exclusivos |
| AUD-05 | C | Volume fixo por vinheta, sem influência da força da tecla |
| AUD-06 | C | Volume geral, fades de entrada/saída e ao parar |
| AUD-07 | C | Recorte de início/fim não destrutivo e loop |
| AUD-08 | C/P | Redução automática de música; alvo interno proposto para v1 |
| PIT-01 | C/V | Pitch em tempo real, preservando duração, de -12 a +12 semitons |
| PIT-02 | C | Última vinheta disparada habilitada recebe o pitch |
| PIT-03 | C | Novo disparo habilitado assume a posição atual da roda |
| OUT-01 | C/V | Selecionar saída disponível, inclusive interface USB reconhecida pelo Windows |
| OUT-02 | C/V | Ouvir no dispositivo habitual e capturar o áudio do app no OBS |
| UI-01 | C | Português, tema escuro, janela ajustável, teclado visual e editor lateral |
| LIFE-01 | C | Fechar janela recolhe à bandeja; Sair encerra e libera recursos |
| LIFE-02 | C | Início com Windows opcional, inicialmente desligado |
| SAVE-01 | P | Salvamento automático, exportação/importação e recuperação de configuração |
| SAFE-01 | C | Manter pressionado para confirmar ações sensíveis |
| SAFE-02 | C | Parar tudo encerra sons e cancela macros |
| SAFE-03 | C | Perda de MIDI não interrompe sons já iniciados |

## Semântica das teclas

| Modo | Pressionar | Soltar | Novo toque |
|---|---|---|---|
| Disparar | Inicia o áudio | Continua | Reinicia do começo |
| Enquanto segura | Inicia o áudio | Para com fade configurado | Inicia novamente |
| Alternar | Inicia se parado | Não altera | Para se estiver tocando |
| Manter para confirmar | Inicia contagem visual | Cancela se antes do limite | Nova contagem |

Proposta: confirmação por 1 segundo para início/encerramento de transmissão e parada de gravação, com indicação visual e apenas uma execução por pressão. O tempo ainda poderá ser ajustado.

Loop é uma propriedade do áudio, independente do modo de tecla. Toque duplo e combinações MIDI não são requisitos confirmados da v1.

## Bancos e ações compartilhadas

- Ação tem identidade própria. Duas teclas podem apontar para a mesma ação, sem encadeamentos circulares de teclas.
- Proposta: mudar de banco mantém os sons em execução.
- Uma liberação de tecla deve encerrar a ação iniciada pela pressão original, mesmo se o banco mudar enquanto a tecla estiver segurada.
- Uma ação compartilhada disparada em modo reiniciar controla sua reprodução existente, sem criar duplicações acidentais.
- Mudança de banco não executa automaticamente as ações do novo banco.

## Áudio e pitch

- Vinhetas diferentes podem se sobrepor; iniciar uma em grupo exclusivo encerra as demais desse grupo com o fade definido.
- Proposta: cada reprodução conserva o pitch atual quando deixa de ser o alvo da roda. Quando o alvo termina, não reassumir automaticamente um áudio antigo; aguardar novo disparo habilitado.
- Sons desabilitados para pitch permanecem no tom original.
- Pitch deve acompanhar o centro real calibrado do controle, com suavização a validar.
- Proposta para redução automática: vinhetas marcadas reduzem o grupo Música por valor configurável e restauram o nível quando a última terminar, usando tempos de ataque/retorno. Não sobrescrever ajustes manuais de volume feitos durante a redução.
- Arquivos ausentes, corrompidos ou não suportados geram erro legível sem encerrar o app.
- Proposta: ao perder a saída de áudio, sinalizar e parar a reprodução; não redirecionar silenciosamente para caixas de som.

## Macros e atalhos

Proposta de v1: sequência ordenada de ações OBS, reprodução/parada de vinheta, espera por duração, espera por fim do áudio e envio de atalho. Cada etapa apresenta falha; por padrão, interromper a sequência em erro. Esperas por áudio devem ter cancelamento e limite configurado.

Parar tudo cancela etapas ainda não executadas e libera teclas simuladas mantidas pelo app. Não desfaz ações já concluídas, não para gravação/transmissão e não restaura automaticamente uma cena anterior.

Atalhos são opcionais e podem conflitar com outros atalhos quando configurados. O app deve explicitar se serão enviados à janela em foco; direcionamento específico a programas não é garantido. Não incluir execução de comandos arbitrários na primeira versão.

## Telas propostas

1. **Painel:** banco, teclado de 32 teclas com rótulos/cores, estado MIDI/OBS, áudios ativos e Parar tudo.
2. **Editor lateral:** tecla, nome, ação, gesto e parâmetros; aprendizagem MIDI e teste explícito.
3. **Biblioteca:** importar, localizar e atribuir arquivos; reprodução de teste identificada como audível/capturável.
4. **Som:** saída, volume geral, grupos, redução de música, pitch e diagnóstico.
5. **Conexões e preferências:** MIDI, OBS, bandeja, início com Windows e backup.

Prévia não é privada: se o OBS estiver capturando o app, poderá capturá-la também.
