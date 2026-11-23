@echo off
chcp 65001 >nul
setlocal
rem Genera el APK sin abrir Unity y, si hay un celular conectado por USB con la depuracion activada, lo instala.
rem Antes: corre ACTUALIZAR_JUEGO.bat. IMPORTANTE: cierra Unity (no puede haber dos copias abiertas del mismo proyecto).

set "PROJ=%USERPROFILE%\Juego de futbol"
set "ANS="
echo.
echo ==================================================================
echo   Construir el APK del juego
echo ==================================================================
echo   Carpeta del proyecto: "%PROJ%"
set /p "ANS=  Pulsa Enter para usarla o escribe otra ruta: "
if defined ANS set "PROJ=%ANS%"
set "PROJ=%PROJ:"=%"
if not exist "%PROJ%\Assets\" goto noproject

rem Busca el Unity instalado por Unity Hub (la version mas nueva).
set "UNITY="
for /f "delims=" %%D in ('dir /b /ad /o:n "%ProgramFiles%\Unity\Hub\Editor" 2^>nul') do (
  if exist "%ProgramFiles%\Unity\Hub\Editor\%%D\Editor\Unity.exe" set "UNITY=%ProgramFiles%\Unity\Hub\Editor\%%D\Editor\Unity.exe"
)
if not defined UNITY goto nounity

echo.
echo   Unity: "%UNITY%"
echo   Construyendo (la primera vez tarda varios minutos)...
if not exist "%PROJ%\Builds" mkdir "%PROJ%\Builds"
"%UNITY%" -batchmode -nographics -quit -projectPath "%PROJ%" -buildTarget Android -executeMethod Football.Game.Editor.BuildAndroid.BuildBatch -logFile "%PROJ%\Builds\build.log"
set "CODE=%ERRORLEVEL%"
if not "%CODE%"=="0" goto failed

set "APK=%PROJ%\Builds\FootballGame.apk"
if not exist "%APK%" goto failed
echo.
echo   LISTO: "%APK%"

where adb >nul 2>&1
if errorlevel 1 goto noadb
adb devices | findstr /r /c:"device$" >nul
if errorlevel 1 goto nophone
echo   Instalando en el celular...
adb install -r "%APK%"
echo.
echo   Hecho. Abre POTRERO en el celular.
goto end

:noadb
echo   (adb no esta instalado: copia el APK al celular a mano, o instala "Android platform-tools".)
goto end
:nophone
echo   (No hay un celular conectado con la depuracion USB activada: copia el APK a mano.)
goto end
:failed
echo.
echo   FALLO la construccion (codigo %CODE%). Abre este archivo y mandame las ultimas lineas:
echo   "%PROJ%\Builds\build.log"
goto end
:nounity
echo.
echo   No encontre Unity en "%ProgramFiles%\Unity\Hub\Editor". Abre Unity y usa el menu Football ^> Construir APK para Android.
goto end
:noproject
echo.
echo   No encuentro un proyecto de Unity en "%PROJ%".
:end
echo.
pause
