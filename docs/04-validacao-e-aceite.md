# Validações e critérios de aceite

Versão 0.1 — 08/10/2026. Os testes de aceite do aplicativo ainda não foram executados. Para a verificação inicial do protótipo MIDI, veja [o registro de diagnóstico](06-diagnostico-midi.md).

## Provas técnicas antes da implementação completa

| ID | Procedimento | Evidência esperada | Decisão |
|---|---|---|---|
| V-01 | Identificar build Windows, versão OBS e dispositivos | Registro de ambiente, sem segredos | Fixar compatibilidade e dependências |
| V-02 | Pressionar/soltar as 32 teclas, mover pitch/modulação | Eventos com dispositivo, canal, nota/CC, extremos e centro | Definir mapeamento e calibração |
| V-03 | Acionar octave +/- e repetir teclas | Verificar evento independente e sobreposição de notas | Viabilidade de decks por oitava |
| V-04 | Abrir/fechar app de teste e reconectar USB | Liberação e recuperação do dispositivo | Estratégia MIDI |
| V-05 | Conectar ao OBS, listar alvos e trocar cena de teste | Respostas e eventos observados | Validar adaptador |
| V-06 | Tocar WAV/MP3 e variar pitch até ±12 | Gravação, avaliação auditiva, CPU e atraso | Selecionar DSP e buffers |
| V-07 | Capturar o app em gravação local OBS | Som presente uma única vez, sem eco | Roteamento inicial |
| V-08 | Sobrepor sons com OBS ativo | Vozes testadas, consumo e falhas | Definir limite inicial |

Os testes de teclas/rodas exigem participação do usuário. O fato de a controladora estar conectada não comprova mensagens, disponibilidade exclusiva ou funcionamento.

Não iniciar transmissão real para testar. Utilizar ambiente de teste ou ação explicitamente coordenada com o usuário. Registrar alterações de cenas/fontes necessárias aos ensaios.

## Matriz de aceite da primeira versão

| Teste | Requisitos | Critério |
|---|---|---|
| T-01 | MIDI-01/02, BANK-01 | Todas as teclas aprendidas; dois bancos executam configurações diferentes |
| T-02 | ACT-01/02, AUD-03 | Modos disparar/segurar/alternar funcionam; não há duplicação por Note Off |
| T-03 | BANK-01 | Soltar tecla após mudança de banco encerra a ação originalmente iniciada |
| T-04 | OBS-01/02/03/04/05 | Ações selecionadas funcionam e interface acompanha mudanças externas |
| T-05 | ACT-03 | Macro respeita ordem, espera e falha; atalho funciona em programa de teste |
| T-06 | AUD-01/02/07 | Cópia do áudio continua válida após mover original; recorte e loop não alteram original |
| T-07 | AUD-04/05/06/08 | Mistura, exclusividade, volumes, fades e redução/restauração de música funcionam |
| T-08 | PIT-01/02/03 | Centro/extremos, alvo e novo disparo seguem configuração; qualidade aprovada pelo usuário |
| T-09 | OUT-01/02 | Gravação local contém vinhetas uma vez; perda de dispositivo é indicada |
| T-10 | SAVE-01 | Reinício restaura bancos/configurações, sem iniciar sons; exportação/importação funciona |
| T-11 | LIFE-01/02, MIDI-03 | Minimizar mantém execução; Sair libera MIDI e encerra áudio |
| T-12 | MIDI-04, SAFE-03 | MIDI/OBS reconectam sem reproduzir fila antiga; áudio sobrevive à perda de MIDI |
| T-13 | SAFE-01/02 | Pressão curta não dispara ação protegida; Parar tudo cancela sons/macros e libera atalhos |
| T-14 | UI-01 | Rótulos legíveis, configuração acessível e janela sem controles essenciais cortados |
| T-15 | Desempenho | Sessão de 30 minutos com OBS ativo sem travamento ou interrupção audível recorrente |

## Metas a fechar com a prova técnica

- Latência medida de entrada até início audível e variação durante uso.
- Quantidade de sons simultâneos sustentada, com e sem pitch.
- CPU e memória do app em repouso e com carga, preservando a operação do OBS.
- Qualidade aceitável de voz, música e efeitos nos extremos de pitch.

Não atribuir números de desempenho antes de medir. A prova técnica deverá transformar estas metas em limites concretos antes de declarar a v1 concluída.

## Registro de execução

Para cada teste, registrar data, build do app, ambiente, arquivos utilizados, configuração, resultado esperado/observado, evidência e pendências. O estado inicial de todos os testes é **não executado**.

Testes automatizados devem focar regras com risco real: liberação após mudança de banco, cancelamento de macros, redução de música concorrente, mapeamento e persistência. Qualidade de áudio e compatibilidade do hardware exigem testes reais.
