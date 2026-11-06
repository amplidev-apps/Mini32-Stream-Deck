# Plano de desenvolvimento

Versão 0.1 — 08/10/2026. Sem prazo assumido; esforço será estimado após as provas técnicas.

## Etapas e condições de avanço

| Etapa | Trabalho | Entrega verificável | Condição para avançar |
|---|---|---|---|
| 0. Documentação | Consolidar respostas, requisitos, riscos e testes | Este conjunto v0.1 | Corrigir divergências de escopo se identificadas |
| 1. Provas técnicas | MIDI, oitavas, OBS, reprodução/pitch e captura | Resultados V-01 a V-08 | Confirmar viabilidade ou escolher alternativas explícitas |
| 2. Especificação técnica | Fixar bibliotecas/versões, telas, dados e limites | Documentação v0.2 e backlog detalhado | Comportamentos críticos sem ambiguidade |
| 3. Base executável | App Windows, persistência, MIDI, bancos e conexão OBS | Configurar uma tecla, trocar cena e restaurar configuração | Fluxo básico estável |
| 4. Soundboard | Biblioteca, saída, modos, grupos, fades, recortes, loops e pitch | Áudio processado e capturado no OBS | Testes funcionais e de carga básicos aprovados |
| 5. Escopo completo | Macros, atalhos, redução de música, Modo Estúdio, ações protegidas | Todos os requisitos confirmados integrados | Recursos da v1 presentes e testáveis |
| 6. Estabilidade | Reconexão, falhas, bandeja, backup, desempenho e testes de sessão | Matriz de aceite preenchida | Nenhum defeito bloqueador conhecido |
| 7. Pacote de teste | Empacotar, instruções de execução e configurações de exemplo | Pacote executável no PC do usuário | Demonstração de aceite aprovada |

As entregas intermediárias servem para testar cedo; não substituem o escopo completo pedido. Pitch deve aparecer já na prova técnica, por ser central ao produto e ao risco de desempenho.

## Backlog inicial ordenado

1. Diagnóstico MIDI com registro de eventos e calibração.
2. Experimento específico com octave +/- e identificação de decks.
3. Conexão OBS autenticada, consulta de capacidades e ação de cena.
4. Experimento de áudio/pitch/captura no computador de referência.
5. Escolha final de dependências, revisão de licenças e limites medidos.
6. Teclado visual, editor lateral, ações compartilhadas e bancos.
7. Persistência versionada e recuperação de configurações.
8. Biblioteca e motor de áudio completo.
9. Macros e atalhos com cancelamento.
10. Ações OBS completas, estado sincronizado e proteções por pressão longa.
11. Preferências, bandeja, reconexão e diagnóstico de falhas.
12. Aceite e pacote de teste.

## Pontos ainda abertos

| Ponto | Como resolver | Alternativa |
|---|---|---|
| Oitavas como bancos | Capturar mensagens do aparelho | Interface ou teclas dedicadas |
| Modulação como volume | Testar mensagem e alcance; manter atribuição configurável | Ajuste na interface |
| WAV/MP3 e arquivos longos | Testar decodificador com amostras reais | Informar formatos suportados e erro claro |
| Música a reduzir | Padrão proposto: grupo interno Música | Documentar alvo externo antes de ampliar integração |
| Captura por aplicativo | Testar build do Windows, OBS e saída escolhida | Avaliar roteamento alternativo explicitamente |
| Pitch no i3-2120 | Medir áudio real com OBS ativo | Ajustar DSP, buffer e limite de vozes |
| Limite de sons simultâneos | Teste de carga | Limite configurável com comportamento previsível |
| Meta de latência | Medir e avaliar com usuário | Otimizar antes de ampliar efeitos |

## Participação do usuário

- Operar teclas e controles durante o diagnóstico.
- Disponibilizar algumas vinhetas representativas, incluindo voz, efeito curto e arquivo longo, quando começarem os testes.
- Conferir sensação de resposta e qualidade de pitch.
- Validar o fluxo de configuração e a demonstração final.

Não é necessário especificar todos os packs ou nomes de cenas antecipadamente: o aplicativo deve permitir configurar isso durante o uso.

## Próximo passo concreto

Etapa 1 iniciada com o [protótipo isolado de diagnóstico MIDI](../prototype/midi-diagnostic/README.md). O roteiro físico já gerou logs analisados e encerrados; resultados e ressalvas estão no [registro de diagnóstico](06-diagnostico-midi.md). A [bancada de áudio/OBS](../prototype/audio-obs/README.md) já foi implementada e verificada com testes de áudio, protocolo simulado e saída silenciosa. Próxima ação: testar uma vinheta real, conectar ao OBS e fazer gravação local, conforme o roteiro. A confirmação das teclas 29/30 e o teste de reconexão física permanecem pendentes, sem impedir essa prova técnica.

Atualização após teste do usuário: duas vinhetas MP3 foram carregadas, a conexão com OBS real funcionou e houve confirmação de comandos de cena. O usuário avaliou positivamente a bancada. O [registro de resultados](07-prototipo-audio-obs.md) documenta evidências e limitações. A próxima entrega planejada é a base configurável de 32 teclas, editor, bancos e persistência; confirmar também a captura numa gravação local. Ainda não declarar a v1 pronta ou os testes prolongados concluídos.
