@echo off
echo Instalando PyInstaller...
pip install pyinstaller

echo.
echo Compilando executavel...
pyinstaller ^
  --onefile ^
  --noconsole ^
  --name "MonitorPonto" ^
  --add-data "config.json;." ^
  main.py

echo.
echo Pronto! O executavel esta em: dist\MonitorPonto.exe
echo.
echo Para colocar na inicializacao do Windows:
echo   1. Copie dist\MonitorPonto.exe para uma pasta definitiva
echo   2. Copie tambem o config.json para a mesma pasta
echo   3. Pressione Win+R, digite: shell:startup
echo   4. Crie um atalho para o MonitorPonto.exe nessa pasta
pause
