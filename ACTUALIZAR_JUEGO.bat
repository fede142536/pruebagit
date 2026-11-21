@echo off
chcp 65001 >nul
setlocal
rem Actualiza el juego dentro de tu proyecto de Unity: copia la version nueva de Assets\Football y
rem Assets\Plugins\Core y BORRA los archivos viejos que ya no existen (evita mezclar versiones).
rem Solo toca esas dos carpetas; el resto del proyecto no se modifica.

set "SRC=%~dp0Game\Assets"
if not exist "%SRC%\Football\Runtime" goto nosource

set "PROJ=%USERPROFILE%\Juego de futbol"
set "ANS="
echo.
echo ==================================================================
echo   Actualizar el juego de futbol en tu proyecto de Unity
echo ==================================================================
echo   IMPORTANTE: cierra Unity antes de continuar.
echo.
echo   Carpeta del proyecto: "%PROJ%"
set /p "ANS=  Pulsa Enter para usarla o escribe otra ruta: "
if defined ANS set "PROJ=%ANS%"
set "PROJ=%PROJ:"=%"

if not exist "%PROJ%\Assets\" goto noproject
if not exist "%PROJ%\ProjectSettings\" goto noproject

echo.
echo   Copiando Football...
robocopy "%SRC%\Football" "%PROJ%\Assets\Football" /MIR /XF *.meta FootballUnlit.mat /NFL /NDL /NJH /NJS /NP
if errorlevel 8 goto fail
echo   Copiando Plugins\Core...
robocopy "%SRC%\Plugins\Core" "%PROJ%\Assets\Plugins\Core" /MIR /XF *.meta /NFL /NDL /NJH /NJS /NP
if errorlevel 8 goto fail

echo.
echo   Listo. Abre Unity y espera a que termine de compilar.
echo   Si pregunta por la Library, elige "Rebuild Library".
echo.
pause
exit /b 0

:nosource
echo No encuentro la carpeta Game\Assets\Football junto a este archivo.
echo Ejecuta ACTUALIZAR_JUEGO.bat desde la carpeta del ZIP descomprimido.
pause
exit /b 1

:noproject
echo No encuentro un proyecto de Unity en "%PROJ%".
echo Debe tener dentro las carpetas Assets y ProjectSettings.
pause
exit /b 1

:fail
echo Hubo un error al copiar. Comprueba que Unity este cerrado y vuelve a intentarlo.
pause
exit /b 1
