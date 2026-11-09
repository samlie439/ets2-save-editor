@echo off
rem Rebuild the editor from source (no Visual Studio required, uses .NET Framework csc).
setlocal
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
set "SRC=%~dp0"
set "OUT=%~dp0..\"
set "REF=/r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll"
set "FILES=%SRC%AssemblyInfo.cs %SRC%Scsc.cs %SRC%Bsii.cs %SRC%SiiEncoder.cs %SRC%SiiText.cs %SRC%Freight.cs %SRC%SaveLibrary.cs %SRC%SaveEditor.cs %SRC%UiAssets.cs %SRC%UiTheme.cs %SRC%CliMain.cs"
if not exist "%OUT%tools" mkdir "%OUT%tools"

"%CSC%" /nologo /target:exe /out:"%OUT%tools\命令行版.exe" /main:Ets2Save.CliMain /r:System.dll %FILES%
if errorlevel 1 goto :fail

"%CSC%" /nologo /target:winexe /out:"%OUT%ETS2存档修改器.exe" /main:Ets2Save.GuiMain %REF% %FILES% %SRC%GuiMain.cs
if errorlevel 1 goto :fail

echo.
echo Build OK.
pause
exit /b 0

:fail
echo.
echo Build FAILED.
pause
exit /b 1
