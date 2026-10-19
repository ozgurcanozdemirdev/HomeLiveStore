@echo off
setlocal
cd /d "%~dp0"
echo Home ^& Living Store: http://localhost:5100
echo Yonetim: http://localhost:5100/Admin
dotnet run --project "HomeLiveStore.Web\HomeLiveStore.Web.csproj" --launch-profile HomeLiveStore
if errorlevel 1 pause
endlocal
