@echo off
:: Check if running as admin
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo Requesting administrative privileges...
    powershell -Command "Start-Process '%~f0' -Verb RunAs"
    exit /b
)

:: Check if PowerShell Core (pwsh) exists
where pwsh >nul 2>&1
if %errorlevel%==0 (
    set "SHELL=pwsh"
) else (
    set "SHELL=powershell"
)

:: Launch Windows Terminal with the right shell, set folder, bypass policy, and activate venv
start wt.exe -d "D:\Coding\Python\genshin-dialogue-autoskip\python" %SHELL% -NoExit -ExecutionPolicy Bypass -Command "poetry run python src/autoskip_dialogue.py"
