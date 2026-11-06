# Bancada de áudio e OBS

Este é o segundo protótipo, separado do aplicativo completo. Pense nele como uma bancada: uma tecla toca um som, a roda muda o tom e outra tecla troca uma cena no OBS.

## Abrir

Abra **Iniciar.cmd**, nesta pasta, ou **bin/Mini32Lab.exe**. Não precisa instalar o aplicativo. Mantenha `NAudio.dll` junto do executável se mover a pasta `bin`.

Feche o monitor MIDI anterior antes de conectar esta bancada. Fechar a janela deste protótipo encerra o programa e libera a controladora; ainda não há bandeja do Windows.

## Primeiro teste: só o som

1. Deixe a Mini 32 conectada e escolha **AKM320**. Clique **Conectar MIDI**.
2. Clique **Escolher WAV / MP3** e escolha uma vinheta curta. Se ainda não tiver uma, use **Carregar som de exemplo**.
3. Clique **Tocar / reiniciar** para conferir a saída. Se não ouvir, confira o volume, os fones e a lista **Saída**. A mudança de saída vale no próximo disparo.
4. Por padrão, a nota 53 (primeira tecla no estado testado) toca a vinheta. Se preferir, clique **Aprender tecla do som** e pressione a tecla desejada. Esse toque apenas configura; solte e pressione novamente para tocar.
5. Mova a roda de pitch enquanto o som toca. O tom deve mudar, sem acelerar ou desacelerar a reprodução. **Pitch no centro** restaura o tom original até a próxima mensagem da roda.
6. **PARAR SOM** interrompe a reprodução. **Parar ao soltar** muda para o comportamento de tocar enquanto segura.

É como um controle remoto com um botão configurável. Os números são notas MIDI, não posições físicas; usar Aprender evita precisar decorá-los. Não altere oitava/Transpose durante este teste.

## Segundo teste: OBS

1. Abra o OBS. Faça este teste fora de uma transmissão pública.
2. No menu **Ferramentas**, abra as configurações do servidor **WebSocket** (o nome pode variar com o idioma).
3. Habilite o servidor, mantenha a autenticação e obtenha a porta e a senha pelas informações de conexão do OBS.
4. Na bancada, preencha porta e senha e clique **Conectar OBS**. A conexão é somente com o OBS no mesmo computador. A senha fica em memória e não é gravada nos logs.
5. Escolha uma cena de teste. Por padrão, a nota 54 (segunda tecla no estado testado) será usada; **Aprender tecla da cena** permite escolher outra.
6. Marque **Habilitar troca de cena**. Pressione a tecla escolhida ou use **Testar troca de cena**.

A troca altera a cena de programa, isto é, a cena que apareceria na transmissão. Não é uma troca somente de prévia. A bancada nunca inicia/paralisa transmissão nem gravação. Um comando ocupado é descartado, não guardado para executar mais tarde.

## Terceiro teste: gravar o resultado

1. Com a bancada reproduzindo um som, adicione no OBS uma fonte de **Captura de áudio de aplicativo**, se disponível, e selecione **Mini32Lab.exe** / a janela da bancada.
2. Para o ensaio, evite capturar o mesmo som também pelo Áudio do desktop; desative temporariamente essa captura duplicada ou use uma configuração de teste.
3. Faça uma gravação local curta no OBS: dispare o som, mova o pitch e pare.
4. Ouça a gravação e observe se há som duplicado, cortes, distorção ou demora incômoda.
5. Ao terminar, informe no chat como foi. Se a captura por aplicativo não estiver disponível, avise antes de mudar o roteamento de áudio; não é preciso instalar um cabo virtual agora.

## O que esta bancada já faz

- WAV/MP3 mono ou estéreo, até 2 minutos, com limite de memória.
- Uma vinheta por vez, reinício por novo toque, parada por botão ou liberação opcional.
- Pitch de -12 a +12 semitons; volume começa em 25%.
- Seleção de saída do Windows, MIDI aprendido com canal e nota.
- Conexão autenticada com obs-websocket v5, lista e troca de cenas.
- Logs locais em `logs/bancada-*.jsonl`; botão **Abrir logs**.

## Limites desta etapa

- Ainda não é a versão completa: não há biblioteca permanente, bancos, macros, múltiplos sons, salvamento das escolhas nem reconexão automática.
- Configurações desta bancada são feitas novamente ao reabrir. Após retirar o USB, desconecte na interface, atualize a lista e conecte novamente.
- A saída trabalha com buffer solicitado de 100 ms, além do processamento FFT. Isso não é uma medição da latência final. O filtro acrescenta uma cauda fixa de 2048 amostras por canal (aproximadamente 43 ms em 48 kHz) para não cortar o final.
- Volume é aplicado depois do pitch, com limite de amplitude. A qualidade de voz/música e a resposta ao movimento da roda ainda exigem avaliação auditiva.
- O nome de algumas saídas pode aparecer abreviado pelo driver WinMM.
- Os indicadores de CPU/RAM são aproximados. O log não mede o instante físico em que você aperta a tecla.
- Não existe pré-escuta privada: se o OBS estiver capturando a bancada, poderá captar qualquer som tocado nela.

## Verificações realizadas

Testes automatizados de frequência/duração em -12/0/+12, alteração dinâmica, separação estéreo, decodificação real WAV e MP3, autenticação OBS, respostas fragmentadas e erro de cena usando servidor local simulado. Saída real inicializada com silêncio. Janela renderizada e inspecionada. Resultados locais em `logs/test-result.txt`, `device-test.txt` e `ui-test.png`.

Esses testes não substituem o teste com suas vinhetas, com o MIDI físico nesta janela e com o OBS real.

## Para desenvolvimento

`Build.ps1` compila com o C#/.NET Framework do Windows e restaura NAudio 1.10.0 se ausente, usando URL oficial do NuGet e hash fixado. A versão foi escolhida para a prova técnica no ambiente existente, não como decisão definitiva do produto. `THIRD-PARTY-NOTICES.txt` contém atribuições e licença. O pitch usa uma instância de `SmbPitchShifter` por canal, inclusive no tom original, para manter o histórico do processamento.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Build.ps1 -Test
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Build.ps1 -UiTest
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Build.ps1 -DeviceTest
```

`-DeviceTest` abre a saída padrão e reproduz apenas silêncio por 300 ms. Nenhum teste automatizado troca cenas no OBS real. A política de execução é alterada apenas para o processo do comando, não permanentemente.
