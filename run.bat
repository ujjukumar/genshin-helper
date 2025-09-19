@echo off
pushd "%~dp0"

REM Create a temporary PowerShell script that will pick the venv python (venv or .venv) and run the autoskip script.
set "PSFILE=%TEMP%\run_autoskip_%RANDOM%.ps1"
> "%PSFILE%" echo $ErrorActionPreference = 'Stop'
>> "%PSFILE%" echo $repo = '%~dp0'
>> "%PSFILE%" echo $venv = Join-Path $repo 'venv\Scripts\python.exe'
>> "%PSFILE%" echo $dotvenv = Join-Path $repo '.venv\Scripts\python.exe'
>> "%PSFILE%" echo if (Test-Path $venv) { $py = $venv } elseif (Test-Path $dotvenv) { $py = $dotvenv } else { $py = 'python' }
>> "%PSFILE%" echo Write-Host "Using Python: $py"
>> "%PSFILE%" echo $script = Join-Path $repo 'src\autoskip_dialogue.py'
>> "%PSFILE%" echo if (-not (Test-Path $script)) { Write-Host 'ERROR: script not found:' $script; Pause; exit 1 }
>> "%PSFILE%" echo ^& $py $script
>> "%PSFILE%" echo Pause

REM Launch the temp PowerShell script elevated (will open an elevated PowerShell window to run the script).
powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-Process -FilePath 'powershell' -ArgumentList '-NoProfile -ExecutionPolicy Bypass -File \"%PSFILE%\"' -Verb RunAs"

REM Cleanup briefly; elevated process will already have a copy of the file.
timeout /t 2 >nul
if exist "%PSFILE%" del "%PSFILE%"

popd
exit /b