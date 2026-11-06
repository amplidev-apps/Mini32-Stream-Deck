# Prova técnica de áudio e OBS

08/10/2026 — protótipo local em `prototype/audio-obs`.

## Entrega

Executável Windows de teste com reprodução WAV/MP3, uma vinheta por vez, pitch controlado por MIDI, seleção de saída e troca de uma cena selecionada no OBS. A entrada MIDI reutiliza o receptor do primeiro diagnóstico; nenhum dado do diagnóstico original foi sobrescrito.

[Roteiro para o usuário](../prototype/audio-obs/README.md).

## Arquitetura experimental

- C#/.NET Framework + WinForms, compilado com o compilador já presente no Windows.
- NAudio 1.10.0 para leitura de áudio, saída WaveOut e DSP SmbPitchShifter.
- Arquivo de até 2 minutos preparado em memória antes do disparo; limite adicional de 12 milhões de amostras.
- Processamento por canal com FFT de 2048 amostras, sobreposição 4 e transição gradual do pitch. O processamento permanece ativo no tom original.
- Pitch MIDI assimétrico convertido exatamente para -12 (0), 0 (8192) e +12 (16383) semitons.
- Buffer de saída solicitado de 100 ms, dividido em três buffers. Latência final ainda não medida.
- OBS via ClientWebSocket, autenticação SHA-256, respostas correlacionadas, limite de tamanho e timeout. Somente listagem e troca de cena; sem início de transmissão/gravação.
- Senha não persistida nem registrada em log. Troca de cena exige habilitação na janela.

SoundTouch foi considerado na pesquisa, mas não é dependência de execução desta bancada. A comparação de qualidade com outro algoritmo continua possível após avaliação auditiva; nenhuma seleção definitiva de DSP para o produto foi feita.

## Verificação executada

| Verificação | Resultado |
|---|---|
| Pitch em tom sintético de 440 Hz | 220 / 440 / 880 Hz em -12 / 0 / +12 |
| Duração | Mesma quantidade de amostras mais cauda fixa de 2048 frames |
| Mudanças de pitch durante fluxo | Saída finita e contagem de amostras preservada |
| Estéreo | Canal silencioso permaneceu silencioso |
| WAV e MP3 | Arquivos gerados localmente decodificados |
| Autenticação OBS | Vetor SHA-256 conferido por implementação independente |
| Protocolo OBS simulado | Autenticação, lista, troca, fragmentação e rejeição de cena passaram |
| Saída Windows | Três dispositivos enumerados; padrão abriu/tocou silêncio/fechou |
| Interface | Abertura, renderização e fechamento verificados |

Relatórios de teste estão em `prototype/audio-obs/logs`. O tempo de processamento offline não deve ser apresentado como atraso entre tecla e som.

## Ainda não comprovado

- Qualidade auditiva com voz/música e alteração física do pitch.
- Disparo MIDI e troca de cena pelo usuário nesta nova bancada.
- Conexão e captura com o OBS real; o processo OBS não estava em execução na verificação inicial desta etapa.
- Operação prolongada com OBS gravando e latência ponta a ponta.
- Comportamento após perda de saída, USB ou conexão OBS no uso real.

## Próxima atividade

O usuário deverá seguir o roteiro em três partes: som, cena e gravação local. Usar os logs e o relato auditivo para escolher buffers/DSP e fechar os limites antes da interface e dos recursos completos. Este protótipo não reduz o escopo confirmado da v1.

## Resultado do teste do usuário — 08/10/2026

O usuário informou estar bastante satisfeito com o resultado. Isso registra uma avaliação positiva do uso da bancada; não substitui a verificação específica da gravação no OBS ou uma medição de latência.

Foram analisados os cinco logs enviados. Três são verificações de interface do desenvolvimento (19:53:57, 19:56:06 e 19:59:36), e o de 20:00:30 corresponde à instância inicial sem interação, encerrada pelo assistente para abrir a janela visível. A ausência de encerramento nesse arquivo não deve ser atribuída a falha durante o teste do usuário.

O teste funcional está em `bancada-20261008-200055-c343d2.jsonl`. No trecho analisado:

- A entrada AKM320 foi aberta.
- Dois MP3 estéreo de 44,1 kHz foram carregados: durações aproximadas de 15,65 e 10,97 segundos.
- Há 23 solicitações de reprodução e 17 comandos de parada pela liberação da tecla.
- Há 44 Note On e 44 Note Off, distribuídos entre as notas 53, 55, 83 e 84, canal 1. O log não registra a configuração aprendida ou a origem de cada disparo com detalhe suficiente para atribuir todos os comandos a posições físicas específicas.
- O pitch percorreu valores de 0 a 16383 nos estados amostrados.
- A conexão real com OBS foi confirmada na porta 4455.
- O OBS confirmou cinco solicitações de definição de cena: uma para Main e quatro para Cena 2. Confirmação repetida da mesma cena não significa cinco mudanças visuais distintas.
- Três erros anteriores à conexão bem-sucedida foram registrados: servidor remoto inacessível, ação sem conexão e encerramento da conexão pelo OBS. A causa exata não é comprovada pelo log; não concluir automaticamente senha incorreta. Houve conexão e comandos bem-sucedidos depois.
- Não há erro registrado de decodificação, processamento ou saída de áudio no trecho analisado. Isso não comprova ausência de pequenos cortes audíveis.
- O arquivo principal ainda não continha `end` no momento da análise; contagens referem-se ao trecho lido e podem crescer enquanto a janela permanece aberta.

### Situação após o teste

Integração MIDI/áudio e conexão/comandos de cena com o OBS real têm evidências de funcionamento. A avaliação do usuário sustenta seguir com esta solução como base inicial, sujeita a testes ampliados.

Antes do aceite completo ainda faltam: gravação local para confirmar áudio capturado sem duplicação, teste prolongado com OBS ativo, múltiplos sons simultâneos, reconexão e demais requisitos da v1. A próxima entrega de desenvolvimento deverá ser a base configurável: 32 teclas visuais, editor por tecla, bancos e salvamento/restauração. O ensaio curto de gravação pode ser realizado antes ou em paralelo a essa construção.

### Confirmação posterior do usuário

Ao ser perguntado especificamente se ouviu as vinhetas na gravação local do OBS sem duplicação, o usuário respondeu: “Ouvi tudo perfeitamente! Ficou ótimo!”. A captura e a avaliação auditiva do ensaio curto passam a ser registradas como aprovadas pelo usuário. Essa confirmação substitui a pendência de gravação curta acima; não constitui medição de latência nem aceite de sessão prolongada ou múltiplas vozes.
