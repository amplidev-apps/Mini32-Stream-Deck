@echo off
if exist "%~dp0bin\Mini32Lab.exe" (
  start "" "%~dp0bin\Mini32Lab.exe"
) else (
  "%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -ExecutionPolicy Bypass -File "%~dp0Build.ps1" -Run
  if errorlevel 1 pause
)
