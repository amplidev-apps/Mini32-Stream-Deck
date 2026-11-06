# Mini32 Stream Deck

Aplicativo de Windows para usar uma controladora MIDI AMW Mini 32 como painel de áudio e controle do OBS Studio. A interface é feita em Flutter; o motor de áudio, MIDI e integração com OBS é feito em C#.

**Estado:** versão portátil de desenvolvimento `0.3.0-dev.2` (Free). O repositório contém o código-fonte, os documentos e os scripts de compilação. Ele não contém um pacote executável pronto.

## Recursos desta versão

- Edição Free com um banco e 16 teclas configuráveis entre as 32 posições físicas. É possível trocar quais posições estão ativas sem apagar a configuração anterior.
- Importação e reprodução de arquivos WAV e MP3, sem cota comercial de quantidade de arquivos e com limite técnico de quatro horas por arquivo.
- Até oito sons simultâneos, com loop, recorte, fades, grupos exclusivos, volume por tecla, redução de música e pitch de -12 a +12.
- Controle de cenas, entradas de áudio, prévia/transição, gravação e transmissão pelo OBS WebSocket.
- Imagens por tecla e wallpaper com 50% de opacidade.
- Configurações locais preservadas entre atualizações.

O sistema de licenças/seriais Pro, o instalador e recursos como macros e atalhos do Windows ainda não fazem parte desta entrega. Oito vozes e pitch simultâneo precisam de validação de carga no computador de destino. Consulte [as limitações e o guia detalhado](app/LEIA-ME.txt).

## Executar

O pacote portátil é gerado em `app/dist/Mini32 Stream Deck 0.3.0-dev.2`. Depois de compilar, abra-o pelo arquivo `Iniciar Mini32.cmd` na raiz do projeto ou execute `mini32_stream_deck.exe` dentro da pasta do pacote. Mantenha os arquivos do pacote juntos, inclusive a pasta `engine` e, se disponível, `fonts/tt-interphases-pro`.

Na primeira utilização:

1. Feche outros programas que estejam usando a controladora MIDI.
2. No aplicativo, abra **Conexões**, selecione **AKM320** e conecte a Mini 32.
3. Importe um WAV/MP3 na Biblioteca, configure uma tecla e salve.
4. Mude para **Usar** e pressione a tecla física.
5. Para controlar o OBS, habilite o servidor WebSocket do OBS e configure a conexão no aplicativo.

O áudio é produzido pelo processo `Mini32.Engine.exe`. Para ouvi-lo na transmissão, capture esse processo no OBS. Fechar a janela recolhe o aplicativo para a bandeja; use **Sair** no menu da bandeja para encerrar o motor e liberar a controladora.

## Compilar no Windows

Requisitos:

- Windows 10/11 x64.
- Flutter SDK 3.47.7 (versão usada no projeto), instalado em `.tools/flutter`.
- Visual Studio ou Build Tools com **Desenvolvimento para desktop com C++**, CMake e Windows SDK.
- .NET Framework 4.x (compilador C# incluído no Windows).
- Os oito arquivos de fonte TT Interphases Pro Trial exigidos pelo script, numa pasta `tt_interphases_pro` na raiz. Também é possível informar outra pasta com `-FontSourceDir`.

Com os pré-requisitos disponíveis, execute no PowerShell:

```powershell
powershell -ExecutionPolicy Bypass -File app/Build.ps1
```

Para indicar a pasta da fonte:

```powershell
powershell -ExecutionPolicy Bypass -File app/Build.ps1 -FontSourceDir 'C:\caminho\para\tt_interphases_pro'
```

O script compila e testa o motor C#, analisa e compila a interface Flutter para Windows Release e monta o pacote em `app/dist`. Dependências do Flutter são obtidas em modo offline (`pub get --offline`), portanto o SDK/cache correspondente precisa estar preparado. Use `-SkipTests` para pular os testes automatizados durante a compilação ou `-Run` para abrir o pacote ao final.

### Fonte da interface

A pasta original `tt_interphases_pro` e o pacote compilado `app/dist` são excluídos do Git. O titular do projeto informou ter autorização direta do autor para usar os arquivos da fonte, cujos nomes incluem **Trial**; essa autorização não foi verificada neste repositório. Obtenha os arquivos e confirme que a autorização cobre sua inclusão em distribuições antes de compilar ou redistribuir o aplicativo com a fonte. Sem a pasta de fontes no pacote, o aplicativo usa Roboto como alternativa. Veja [as notas da tipografia](docs/12-tipografia-preview.md).

## Diagnóstico MIDI

O monitor local em `prototype/midi-diagnostic` ajuda a identificar a controladora e inspecionar mensagens MIDI. Execute `prototype/midi-diagnostic/Iniciar.cmd`. Ele apenas recebe MIDI; não controla o OBS nem reproduz áudio. Instruções e formato dos logs estão no [README do diagnóstico](prototype/midi-diagnostic/README.md).

## Dados do usuário

Biblioteca, configurações, backups e logs ficam em `%LOCALAPPDATA%\Mini32 Stream Deck`. A senha do OBS é armazenada protegida pelo Windows para o usuário atual. No aplicativo, **Menu > Abrir dados e logs** abre essa pasta. Desinstalar ou remover o repositório não apaga esses dados.

## Documentação

Os documentos de planejamento e histórico ficam em [`docs/`](docs/):

1. [Visão e escopo](docs/01-visao-e-escopo.md)
2. [Requisitos e comportamentos](docs/02-requisitos.md)
3. [Arquitetura e decisões técnicas](docs/03-arquitetura.md)
4. [Validações e critérios de aceite](docs/04-validacao-e-aceite.md)
5. [Plano de desenvolvimento](docs/05-plano-de-desenvolvimento.md)
6. [Diagnóstico MIDI](docs/06-diagnostico-midi.md)
7. [Protótipo de áudio e OBS](docs/07-prototipo-audio-obs.md)
8. [Direção de interface Flutter](docs/08-interface-flutter.md)
9. [Primeira versão e validações](docs/09-primeira-versao.md)
10. [Upgrade 0.2.0-dev.2](docs/10-upgrade-020.md)
11. [Consolidação 0.3.0-dev.1](docs/11-consolidacao-030.md)
12. [Tipografia](docs/12-tipografia-preview.md)

## Repositório

```text
app/        Aplicativo Flutter, motor C# e scripts de compilação
docs/       Requisitos, arquitetura, validações e histórico
prototype/  Ferramentas de diagnóstico MIDI e protótipo de áudio/OBS
```

O `.gitignore` exclui SDKs e dependências locais, saídas de compilação, logs e fontes comerciais. Use os documentos acima para consultar decisões e o estado planejado das próximas etapas.

## Licença

Este repositório não inclui um arquivo `LICENSE`; portanto, não declara uma licença aberta para o código ou para a identidade visual. Os avisos de terceiros incluídos junto aos componentes e fontes devem ser mantidos. A fonte TT Interphases Pro não está incluída no repositório.
