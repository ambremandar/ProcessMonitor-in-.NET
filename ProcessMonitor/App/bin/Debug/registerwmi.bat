@echo off

echo Registering WMI provider
@echo off
:: // Run Registration code
start cmd /c %~dp0ProcessMonitor.exe -i
if %errorlevel% == 2 GOTO UNABLE_TO_REGISTER_WMIPROVIDER
echo Done
echo.


pause
GOTO:EXIT

:: // Show an error and exit
:UNABLE_TO_REGISTER_WMIPROVIDER
echo.
echo ERROR: Unable to register WMIProvider.  Try running this script as Administrator.
echo.
pause

:EXIT