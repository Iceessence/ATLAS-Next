@echo off
setlocal
set "DOTNET_ROOT=%~dp0runtime"
set "DOTNET_ROOT_X64=%~dp0runtime"
start "" "%~dp0app\AtlasNext.exe"
