@echo off
echo Cleaning all build artifacts...

rd /s /q ASPSBackend\bin 2>nul
rd /s /q ASPSBackend\obj 2>nul
rd /s /q Business\bin 2>nul
rd /s /q Business\obj 2>nul
rd /s /q Common\bin 2>nul
rd /s /q Common\obj 2>nul
rd /s /q Interface\bin 2>nul
rd /s /q Interface\obj 2>nul
rd /s /q WebApi\bin 2>nul
rd /s /q WebApi\obj 2>nul

echo Clean complete!
echo.
echo Now run: dotnet build
pause
