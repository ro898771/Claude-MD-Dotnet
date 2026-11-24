---
name: Dino-Dev-Python_Init_Project
description: Initialize a Python project for Dinosaur List.
---

Help me create a boilerplate for project Init. And follow the sequence below.

1) Execute this command if "not yet installed" command = powershell -ExecutionPolicy ByPass -c "irm https://astral.sh/uv/install.ps1 | iex"

2) Please ask what python version you are requesting ? Then Execute the command uv venv .venv --python {python version user mentioned}

3) Execute command for uv init , and then move main.py into src folder.

4) Append information below into the pyproject.toml file

```toml
[tool.uv]
# Broadcom internal mirror as primary; PyPI as fallback for packages not cached there
index-url = "https://usw2p-edge.packages.broadcom.net/artifactory/api/pypi/platform-pypi/simple"
allow-insecure-host = ["usw2p-edge.packages.broadcom.net"]

```

5) Create run.cmd file that is able to execute command below:
@echo off
setlocal

REM ─── Get the directory of this batch file ────────────────────────────────────
set "BASE_DIR=%~dp0"

REM ─── Define environment paths ────────────────────────────────────────────────
set "ENV_DIR=%BASE_DIR%.venv"
set "ENV_PYTHON=%ENV_DIR%\Scripts\python.exe"

REM ─── Check if virtual environment exists ─────────────────────────────────────
if not exist "%ENV_PYTHON%" (
    echo Virtual environment not found or Python executable missing.
    pause
    exit /b 1
)

REM ─── Run Python Script ───────────────────────────────────────────────────────
echo Activated .venv. Running main.py...
set PYTHONDONTWRITEBYTECODE=1
"%ENV_PYTHON%" "%BASE_DIR%src\main.py"

echo.
echo Script complete. Press Enter to exit.
pause >nul
endlocal


6) Create install.cmd with the following content:

```cmd
@echo off
setlocal

REM ─── Define Environment Paths Relative to Script Location ────────────────────
set "BASE_DIR=%~dp0"
set "ENV_DIR=%BASE_DIR%.venv"
set "ENV_PATH=%ENV_DIR%\Scripts\activate"
set "ENV_PYTHON=%ENV_DIR%\Scripts\python.exe"

REM ─── Check if UV Is Installed ────────────────────────────────────────────
    where uv >nul 2>nul
    if errorlevel 1 (
        echo UV not found. Installing UV...
        powershell -ExecutionPolicy ByPass -c "irm https://astral.sh/uv/install.ps1 | iex"
        
        REM ─── Refresh Environment Variables After UV Installation ─────────────────
        echo Refreshing environment variables...
        call refreshenv >nul 2>nul
        
        REM ─── Add UV to PATH for current session ──────────────────────────────────
        set "PATH=%USERPROFILE%\.local\bin;%USERPROFILE%\.cargo\bin;%PATH%"
        
        REM ─── Verify UV Installation ──────────────────────────────────────────────
        where uv >nul 2>nul
        if errorlevel 1 (
            echo ERROR: UV installation failed or UV is not in PATH.
            echo Please restart your terminal and run this script again.
            pause
            exit /b 1
        ) else (
            echo UV installed successfully and is now available.
        )
    ) else (
        echo UV is already installed.
    )

REM ─── Check if Virtual Environment Exists ─────────────────────────────────────
if not exist "%ENV_DIR%" (
    echo Virtual environment not found. First run setup initiated.
    pause

    echo Creating virtual environment...
    pause
    uv venv "%ENV_DIR%" --python {python version user mentioned} --index-url https://usw2r-edge.packages.broadcom.net/artifactory/api/pypi/platform-pypi/simple
)

REM ─── Verify Creation ─────────────────────────────────────────────────────────
if exist "%ENV_PYTHON%" (
    echo Virtual environment created successfully.

    REM ─── Install Requirements Using uv pip ────────────────────────────────────
    echo Installing requirements...
    pushd "%ENV_DIR%"
	uv sync
	popd

) else (
    echo Failed to create virtual environment. Please check for errors.
)

REM ─── Final Message ───────────────────────────────────────────────────────────
echo.
echo Setup complete. Press Enter to exit.
pause >nul
endlocal
```
