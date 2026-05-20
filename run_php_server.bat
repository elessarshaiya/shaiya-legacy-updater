@echo off
cd /d "%~dp0ServerSide"
echo Starting local PHP server on http://127.0.0.1:8000/Updater/
php -S 127.0.0.1:8000
pause
