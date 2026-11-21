@echo off
chcp 65001 >nul
setlocal
rem Actualiza el juego dentro de tu proyecto de Unity: copia la version nueva de Assets\Football y
rem Assets\Plugins\Core y BORRA los archivos viejos que ya no existen (evita mezclar versiones).
rem Solo toca esas dos carpetas; el resto del proyecto no se modifica.

set "SRC=%~dp0Game\Assets"
if not exist "%SRC%\Football\Runtime" goto nosource
rem Si el ZIP se descomprimio encima de otro, Windows deja copias "(1)" que despues no compilan.
dir /s /b "%SRC%\*(1).*" >nul 2>&1 && goto dirtysource

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

rem Copias "(1)", "(2)"... que quedaron de actualizaciones manuales: dentro de las carpetas del juego se borran.
for /f "delims=" %%F in ('dir /s /b "%PROJ%\Assets\Football\*(1).*" "%PROJ%\Assets\Football\*(2).*" "%PROJ%\Assets\Plugins\Core\*(1).*" 2^>nul') do (
    echo   Borrando copia duplicada: %%F
    del /f /q "%%F"
)

rem Otras copias de los scripts del juego fuera de Assets\Football (por ejemplo una carpeta Football vieja en otro
rem lugar): no se borran solas, solo se avisa.
set "DUP="
set "OWN1=%PROJ%\Assets\Football\Editor\BuildAndroid.cs"
set "OWN2=%PROJ%\Assets\Football\Runtime\GameRoot.cs"
set "OWN3=%PROJ%\Assets\Plugins\Core\Football.Core.dll"
for /f "delims=" %%F in ('dir /s /b "%PROJ%\Assets\BuildAndroid*.cs" "%PROJ%\Assets\GameRoot*.cs" "%PROJ%\Assets\Football.Core*.dll" 2^>nul') do (
    if /i not "%%F"=="%OWN1%" if /i not "%%F"=="%OWN2%" if /i not "%%F"=="%OWN3%" (
        echo   ATENCION: hay otra copia del juego en %%F
        set "DUP=1"
    )
)
if defined DUP (
    echo.
    echo   Borra a mano esas copias ^(y su carpeta si es una copia entera^) antes de abrir Unity.
)

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

:dirtysource
echo La carpeta del ZIP tiene archivos duplicados con "(1)" en el nombre.
echo Borra esta carpeta, descomprime el ZIP en una carpeta NUEVA y vacia y vuelve a ejecutar este archivo.
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
