# Registro inicial do diagnóstico MIDI

08/10/2026 — protótipo isolado em `prototype/midi-diagnostic`.

## Observações verificadas

- O sistema informou Windows 10, build 19045.
- Windows PowerShell 5.1/.NET Framework compilou o protótipo localmente; não foi necessário instalar dependências.
- WinMM enumerou uma entrada: índice 0, nome **AKM320**, fabricante/produto numéricos 65535/65535.
- A única entrada foi aberta, permaneceu em recepção durante aproximadamente um segundo e foi fechada sem erro. Isso não comprova o mapeamento físico da Mini 32 nem a recuperação após retirar o USB.
- A janela abriu e fechou em teste automatizado; uma imagem renderizada da janela foi inspecionada.
- Foram testadas interpretação de Note On, Note Off, Note On com velocidade zero, canal 16, CC, extremos/centro de pitch, comprimentos de mensagens curtas, ordenação dos marcadores e indicação de perda por fila cheia.

## Evidências locais

- `prototype/midi-diagnostic/logs/smoke-*.jsonl`: ensaio técnico de abertura/fechamento, separado dos logs de mapeamento.
- `prototype/midi-diagnostic/logs/ui-smoke.png`: imagem da janela no ensaio visual.
- `-SelfTest`: comando reproduzível descrito no README do protótipo.

Logs de diagnóstico não são versionados. O nome AKM320 é o que o driver informou; não foi usada uma suposição sobre o nome comercial para inferir mensagens.

## Pendências na verificação inicial

- Operação das 32 teclas, identificação de cada posição e liberação.
- Centro/extremos reais de pitch e modulação.
- Eventos próprios dos botões de oitava e relação com transposição.
- Outros botões, mensagens SysEx reais e operação simultânea de teclas.
- Retirada/reconexão física do USB e reabertura da captura.
- Captura de áudio, qualidade de pitch, OBS e desempenho, fora deste protótipo.

## Roteiro disponibilizado

Abrir `Iniciar.cmd`, selecionar AKM320 e clicar **Conectar e gravar**. Seguir cada etapa e, ao concluir, clicar **Encerrar captura**. Depois avisar no chat para que os logs locais sejam analisados. Não é preciso copiar manualmente as mensagens.

## Resultado da análise dos testes físicos — 08/10/2026

Foram analisados o arquivo enviado `mini32-20261008-193327-0ae983.jsonl` e a sessão anterior explicitamente referenciada por ele, `mini32-20261008-192636-6d6d12.jsonl`. Os arquivos originais foram preservados.

A sessão anterior contém o roteiro físico principal e 529 mensagens MIDI. A enviada contém a nova abertura, navegação pelas etapas e mensagens de verificação/livre. Na última leitura havia 85 mensagens MIDI e ainda não existia `session_end`; a janela permanecia aberta. Portanto, a segunda contagem é uma fotografia de um arquivo ainda em gravação.

### Teclas e canal

Todas as mensagens MIDI analisadas usam o canal 1. O mapa abaixo corresponde ao estado do instrumento durante o teste; não deve ser tratado como configuração imutável de fábrica. A observação inicial do usuário registrou LEDs de oitava apagados.

| Tecla física | Nota MIDI | Tecla física | Nota MIDI |
|---|---:|---|---:|
| 01 | 53 | 17 | 69 |
| 02 | 54 | 18 | 70 |
| 03 | 55 | 19 | 71 |
| 04 | 56 | 20 | 72 |
| 05 | 57 | 21 | 73 |
| 06 | 58 | 22 | 74 |
| 07 | 59 | 23 | 75 |
| 08 | 60 | 24 | 76 |
| 09 | 61 | 25 | 77 |
| 10 | 62 | 26 | 78 |
| 11 | 63 | 27 | 79 |
| 12 | 64 | 28 | 80 |
| 13 | 65 | 29 | 81, provisório |
| 14 | 66 | 30 | 82 |
| 15 | 67 | 31 | 83 |
| 16 | 68 | 32 | 84 |

Na etapa `key_29`, houve pressionamento/liberação de 81, depois 82, depois 81. A nota 82 também aparece na etapa própria da tecla 30. Isso é compatível com um toque adicional na tecla vizinha, mas o log não prova a causa. Repetir somente 29 e 30 isoladamente antes de fechar o mapa.

Foram recebidos Note On e Note Off explícitos (status 90/80 em hexadecimal), com velocidade variável no pressionamento e 64 nas liberações observadas. Intensidade 1 também apareceu como pressionamento válido; não a descartar no app. As primeiras seis teclas tiveram repetições em etapas revisitadas.

No teste conjunto, as notas 55, 53 e 54 foram pressionadas antes das liberações individuais 53, 54 e 55. Isso confirma recepção simultânea das três teclas testadas, não um limite máximo de polifonia.

### Controles contínuos

| Controle | Mensagem observada | Faixa registrada | Conclusão |
|---|---|---|---|
| Pitch | Pitch Bend, canal 1 | 0 a 16383; retorno a 8192 | Extremos e centro observados |
| Modulação | CC 1, canal 1 | 0 a 127 | Alcance completo observado |
| Controle ainda não identificado | CC 7, canal 1 | 43 a 45 na segunda sessão | Não atribuir controle físico sem confirmação |

As três etapas de pitch da sessão principal terminaram em 8192. Isso permite implementar a calibração inicial, mas ainda não mede latência, precisão efetiva em bits ou qualidade de processamento de áudio. A modulação terminou em 74 na etapa específica, não em zero; não presumir retorno automático ao início.

### Oitavas e Transpose

| Estado | Notas das teclas testadas 1, 13 e 25 |
|---|---|
| Inicial | 53, 65, 77 |
| Após Oitava + | 65, 77, 89 |
| Após retorno | 53, 65, 77 |
| Após Oitava - | 41, 53, 65 |

Os botões isolados não produziram mensagem registrada, e o usuário marcou ausência de resposta nas três etapas correspondentes. As notas subsequentes confirmam transposição de +12/-12. O retorno final da tecla 1 foi 53.

Consequência: não usar esses botões como seletor automático confiável de bancos nesta implementação. Por exemplo, a nota 65 pode ser tecla 13 no estado inicial ou tecla 1 após Oitava +. O app não recebe informação suficiente para distinguir esses casos. Preferir bancos por interface ou teclas configuradas, mantendo oitava/transposição estáveis.

Em `other_controls`, o usuário anotou Transpose e ausência de resposta; não há teste subsequente associado suficiente para determinar seu efeito sobre as notas. Não concluir que o botão não tem função.

### Integridade e reconexão

- Não há eventos `midi_error`, `capture_loss` nem avisos `driver_overload` nos trechos analisados.
- A sessão principal informa zero perdas, mas sua sequência salta de 598 para 600. A entrada 599 não está no arquivo; a causa é indeterminada. Não afirmar captura integral sem ressalvas. O salto ocorre ao encerrar, depois das etapas de teclas/pitch/oitavas.
- A segunda sessão foi aberta e recebeu nota 53 e pitch. Isso comprova funcionamento após reabertura da entrada.
- Não há snapshot de lista vazia/remoção nesses arquivos. A retirada e recuperação física do USB não estão comprovadas somente pelos marcadores do roteiro.
- A segunda sessão deve ser encerrada pelo botão para produzir o resumo final. Não foi interrompida pela análise.

### Próximos passos ajustados

1. Encerrar normalmente a captura atual para consolidar o log.
2. Repetir apenas as teclas 29 e 30; identificar qual controle físico enviou CC 7, se desejado.
3. Adotar bancos por interface/teclas dedicadas como base, mantendo a preferência por oitavas documentada como inviável de forma automática com as mensagens observadas.
4. Validar separadamente a reconexão física e investigar a lacuna no encerramento do registro.
5. Avançar para as provas técnicas de OBS e reprodução/pitch; o mapeamento recebido já sustenta essa próxima etapa.

### Encerramento confirmado

Após o usuário encerrar a captura, o arquivo `mini32-20261008-193327-0ae983.jsonl` passou a conter `session_end` em 08/10/2026 às 19:36:38 (America/Sao_Paulo), com 87 mensagens MIDI e zero perdas declaradas. O arquivo agora está finalizado. Isso substitui a observação anterior de captura aberta; as demais ressalvas sobre mapeamento e reconexão permanecem.
