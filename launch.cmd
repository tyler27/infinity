@echo off
setlocal enabledelayedexpansion

:: Find Godot executable
:: Priority: GODOT_HOME env var > PATH > common locations

if defined GODOT_HOME (
    if exist "%GODOT_HOME%\godot.exe" (
        set "GODOT_EXE=%GODOT_HOME%\godot.exe"
        goto :run
    )
    for %%f in ("%GODOT_HOME%\Godot_v*.exe") do (
        set "GODOT_EXE=%%~f"
        goto :run
    )
)

where godot >nul 2>nul
if %errorlevel%==0 (
    set "GODOT_EXE=godot"
    goto :run
)

:: Scan common locations
for %%d in (
    "%USERPROFILE%\Tools\Godot"
    "%LOCALAPPDATA%\Godot"
    "%ProgramFiles%\Godot"
    "%ProgramFiles(x86)%\Godot"
) do (
    if exist %%d (
        for /r %%d %%f in (Godot_v*.exe) do (
            echo %%~nf | findstr /i "console" >nul || (
                set "GODOT_EXE=%%~f"
                goto :run
            )
        )
    )
)

echo Error: Could not find Godot executable.
echo Set GODOT_HOME to the directory containing the Godot executable, or add it to PATH.
exit /b 1

:run
set "PROJECT_DIR=%~dp0"
if "%PROJECT_DIR:~-1%"=="\" set "PROJECT_DIR=%PROJECT_DIR:~0,-1%"
start "" "%GODOT_EXE%" --path "%PROJECT_DIR%" -e
