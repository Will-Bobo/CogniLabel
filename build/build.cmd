@echo off
setlocal EnableExtensions
REM CogniLabel build: SDK-style .NET (WPF net8.0-windows)
REM Usage: build\build.cmd [project_or_solution] [Configuration] [Target] [RID]
REM   build\build.cmd
REM   build\build.cmd CogniLabel\CogniLabel.csproj Debug
REM   build\build.cmd CogniLabel\CogniLabel.csproj Release Rebuild
REM   build\build.cmd CogniLabel\CogniLabel.csproj Release Publish win-x64

set "ROOT=%~dp0.."
pushd "%ROOT%" || exit /b 1

set "PROJ=%~1"
if "%PROJ%"=="" set "PROJ=CogniLabel\CogniLabel.csproj"

set "CFG=%~2"
if "%CFG%"=="" set "CFG=Debug"

set "TARGET=%~3"
if "%TARGET%"=="" set "TARGET=Rebuild"

set "RID=%~4"
if "%RID%"=="" set "RID=win-x64"

where dotnet >nul 2>&1
if %ERRORLEVEL% neq 0 (
  echo ERROR: dotnet not found. Install .NET SDK.
  popd
  exit /b 1
)

echo RESTORE: "%PROJ%"
dotnet restore "%PROJ%"
if %ERRORLEVEL% neq 0 (
  set "ERR=%ERRORLEVEL%"
  popd
  exit /b %ERR%
)

if /I "%TARGET%"=="Publish" (
  echo PUBLISH: "%PROJ%" Configuration=%CFG% RID=%RID%
  echo RESTORE_RID: "%PROJ%" RID=%RID%
  dotnet restore "%PROJ%" -r "%RID%"
  if %ERRORLEVEL% neq 0 (
    set "ERR=%ERRORLEVEL%"
    popd
    exit /b %ERR%
  )
  dotnet publish "%PROJ%" -c "%CFG%" -r "%RID%" --no-restore
) else (
  echo BUILD: "%PROJ%" Configuration=%CFG% Target=%TARGET%
  dotnet build "%PROJ%" -c "%CFG%" -t:%TARGET%
)
set "ERR=%ERRORLEVEL%"
popd
exit /b %ERR%
