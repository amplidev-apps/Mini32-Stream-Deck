# Diagnostico MIDI isolado

Prototipo de leitura da AMW Mini 32 para Windows 10/11. Nao implementa o aplicativo principal, nao toca audio e nao envia MIDI, atalhos ou comandos ao OBS.

## Executar

1. Feche programas musicais que possam estar usando a controladora.
2. Abra **Iniciar.cmd** nesta pasta.
3. Selecione a entrada MIDI e clique **Conectar e gravar**.
4. Siga as instrucoes e avance manualmente, soltando as teclas antes de mudar de etapa.
5. Ao final, clique **Encerrar captura**. Use **Abrir pasta dos logs** para localizar os resultados.

O roteiro numera todas as 32 teclas, brancas e pretas, da esquerda para a direita. Os botoes de oitava sao testados separadamente das teclas. O monitor nao altera a configuracao do aparelho, mas operar os botoes fisicos de oitava altera o estado dele: siga as etapas de retorno e registre os LEDs.

Na reconexao USB, o prototipo exige encerrar a captura, atualizar a lista e conectar novamente. Se a etapa atual era `usb_reconnect`, a nova captura continua na verificacao de reconexao. As duas sessoes ficam em arquivos diferentes. Reconexao automatica pertence ao app futuro.

## Logs

`logs/mini32-AAAAmmdd-HHmmss-ID.jsonl`: registro detalhado em UTF-8, uma entrada JSON por linha, contendo identificacao do dispositivo, horario UTC, sequencia, etapa, dados MIDI brutos, canal, notas, CC, pitch, mensagens SysEx em blocos, observacoes e mudancas na lista de dispositivos.

`logs/*-resumo.txt`: contagem por etapa, mensagens e perdas detectadas. O resumo e criado ao encerrar normalmente. O JSONL e descarregado para o sistema a cada ciclo da interface; uma interrupcao abrupta pode perder o ultimo lote, mas nao exige finalizar para ler as linhas ja salvas.

- A tela mostra apenas as 300 linhas mais recentes; o arquivo registra a captura completa, salvo perdas explicitamente sinalizadas.
- Etapa sem mensagem nao prova que o controle nao envia MIDI: conferir observacao, conexao e driver.
- Dados recebidos recebem a etapa ativa naquele instante. O usuario precisa seguir o roteiro; o dispositivo nao informa a posicao fisica da tecla.
- Horarios nao medem latencia fisica da tecla. `driver_ms` e o tempo informado pelo driver desde o inicio da captura.
- Presenca na lista Windows nao garante que uma conexao antiga voltou a receber mensagens.
- Logs sao locais e nao sao enviados automaticamente. Nao registrar senhas nas observacoes.
- Logs com prefixo `smoke` sao testes tecnicos de abrir/fechar, nao mapeamentos fisicos.

## Tecnologia e verificacao

Windows PowerShell 5.1 compila o codigo C# em memoria usando .NET Framework e WinForms ja presentes no Windows. WinMM recebe as mensagens. Nao precisa de Python, Node, SDK .NET nem acesso a rede.

O iniciador aplica `ExecutionPolicy Bypass` somente ao processo que executa este script; nao altera a politica permanente do Windows. Em maquinas com politica corporativa, o bloqueio pode permanecer. Falhas de inicio sao registradas em `startup-error.txt`.

Comandos de verificacao (executar nesta pasta):

```powershell
powershell.exe -NoProfile -STA -ExecutionPolicy Bypass -File .\Start-MidiDiagnostic.ps1 -SelfTest
powershell.exe -NoProfile -STA -ExecutionPolicy Bypass -File .\Start-MidiDiagnostic.ps1 -ListDevices
```

`-SmokeTest` abre a unica entrada MIDI por um segundo e a fecha; deve ser usado somente com a controladora pretendida e sem outro programa usando-a. `-UiSmokeTest` abre a janela e fecha automaticamente, sem capturar.

Referencias: [midiInOpen](https://learn.microsoft.com/en-us/windows/win32/api/mmeapi/nf-mmeapi-midiinopen), [callback MIDI](https://learn.microsoft.com/en-us/previous-versions/dd798460(v=vs.85)).
