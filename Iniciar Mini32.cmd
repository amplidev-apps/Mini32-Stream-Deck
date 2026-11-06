@echo off
setlocal
set "MINI32_APP=%~dp0app\dist\Mini32 Stream Deck 0.3.0-dev.2\mini32_stream_deck.exe"
if exist "%MINI32_APP%" (
  start "" "%MINI32_APP%"
  exit /b
)
echo A primeira compilacao do aplicativo ainda precisa ser concluida.
echo Consulte app\LEIA-ME.txt ou execute app\Build.ps1 apos preparar o Flutter.
pause
