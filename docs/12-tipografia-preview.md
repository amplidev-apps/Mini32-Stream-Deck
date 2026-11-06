# TT Interphases Pro no aplicativo

A interface usa a família TT Interphases Pro. O titular do projeto informou ter comprado a fonte e recebido autorização direta do autor para usar os arquivos presentes em `tt_interphases_pro`, embora os nomes dos arquivos incluam **Trial**. Os nomes originais são preservados. Esta documentação registra a informação fornecida pelo titular; o texto da autorização não foi examinado aqui.

O script `app/Build.ps1` exige oito arquivos da pasta `tt_interphases_pro`: Regular, Italic, Medium, Medium Italic, DemiBold, DemiBold Italic, Bold e Bold Italic. Na compilação, copia esses arquivos para `fonts/tt-interphases-pro` no pacote portátil. O aplicativo carrega automaticamente essa família ao iniciar, inclusive quando `mini32_stream_deck.exe` é aberto diretamente. Se a pasta da fonte não acompanhar o executável, a interface volta a usar Roboto. O atalho `Iniciar Mini32.cmd` abre esse mesmo pacote sem argumento especial.

O diretório original da fonte está no `.gitignore`; os arquivos não são inseridos no repositório de código. O pacote local em `app/dist` também é ignorado. Para compilar de outra pasta, use `app/Build.ps1 -FontSourceDir <pasta>` com os mesmos nomes de arquivo.

Antes de publicar o aplicativo ou enviar o pacote a terceiros, o titular deve conservar a autorização escrita do autor e conferir que ela cobre a distribuição dos arquivos da fonte dentro de um aplicativo Windows. Essa checagem não impede a compilação e os testes locais.
