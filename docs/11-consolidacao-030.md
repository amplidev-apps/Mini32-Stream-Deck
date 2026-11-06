# Consolidação 0.3.0-dev.1 — primeira etapa

Esta é uma versão portátil de desenvolvimento. O sistema de seriais e o instalador serão feitos depois que o aplicativo estiver consolidado e testado com a Mini 32 e o OBS reais.

## Entregue nesta etapa

- Edição Free com um banco e 16 posições ativas, escolhidas entre as 32 teclas físicas. A pessoa pode trocar uma posição ativa por outra. A ação, o volume, a imagem e os demais dados da posição substituída continuam salvos.
- As regras de teclas e bancos são verificadas no motor, inclusive quando a ordem chega diretamente pelo protocolo. Os bancos anteriores permanecem nos dados, mas só o banco Free é acessível nesta edição.
- Biblioteca de áudio sem limite comercial: a edição Free não impõe uma cota de três arquivos. Continuam existindo apenas limites técnicos de proteção do formato de configurações e da duração máxima por arquivo.
- Wallpaper PNG/JPEG selecionado pelo menu **Exibir**, copiado para a pasta de dados e exibido atrás da interface a 50% de opacidade. É possível removê-lo pelo mesmo menu.
- Os dados existentes continuam na versão 2 do formato. A escolha das 16 posições, o banco Free e o wallpaper são campos opcionais acrescentados sem apagar bancos ou vínculos anteriores.

## Verificação

- Testes do motor: seleção e troca das 16 posições, proteção de bancos adicionais, preservação de vínculos e persistência após reiniciar; caminho seguro do wallpaper.
- Testes da interface: troca de posição Free, biblioteca com mais de três áudios, imagem de fundo a 50%, além dos testes anteriores de áudio, teclado e layout.
- Análise estática do Dart, compilação Windows Release, protocolo com OBS simulado e teste de inicialização isolada do pacote portátil.

## Próximas etapas

1. Internacionalização e revisão dos idiomas priorizados, incluindo as mensagens produzidas pelo motor.
2. Polimento visual e validação com Mini 32/OBS reais. Corrigir problemas encontrados sem criar instalador.
3. Desenvolver e validar isoladamente o sistema de seriais; depois integrá-lo ao app.
4. Criar o instalador somente após a validação da ativação.

Os recursos Pro completos não estão disponíveis nesta entrega e não há venda de licenças nesta fase.
