@echo off
setlocal
set PROJ=%~dp0Updater\Updater.csproj
set VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe
if exist "%VSWHERE%" (
  for /f "usebackq tokens=*" %%i in (`"%VSWHERE%" -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe`) do set MSBUILD=%%i
)
if "%MSBUILD%"=="" set MSBUILD=msbuild
"%MSBUILD%" "%PROJ%" /m /p:Configuration=Release /p:Platform=x86
pause
