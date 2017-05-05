@ECHO OFF

setlocal

set EnableNuGetPackageRestore=true

set logOptions=/flp:Summary;Verbosity=detailed;LogFile=msbuild.log /flp1:warningsonly;logfile=msbuild.wrn /flp2:errorsonly;logfile=msbuild.err

REM Find the most recent 32bit MSBuild.exe on the system. Require v14.0 (installed with VS2015) or later. Always quote the %MSBuild% value when setting the variable and never quote %MSBuild% references.
set MSBuild="%ProgramFiles(x86)%\Microsoft Visual Studio\2017\Enterprise\MSBuild\15.0\Bin\MSBuild.exe"
if not exist %MSBuild% @set MSBuild="%ProgramFiles(x86)%\MSBuild\14.0\Bin\MSBuild.exe"
if not exist %MSBuild% (
  echo Could not find msbuild.exe. Please run this from a Visual Studio developer prompt
  goto BuildFail
)

%MSBuild% "%~dp0\Microsoft.AspNet.TelemetryCorrelation.msbuild" %logOptions% /v:minimal /maxcpucount /nodeReuse:false /p:Configuration=Release /p:Platform=AnyCPU %*
if %ERRORLEVEL% neq 0 goto BuildFail
goto BuildSuccess

:BuildFail
echo.
echo *** BUILD FAILED ***
exit /B 999

:BuildSuccess
echo.
echo **** BUILD SUCCESSFUL ***
exit /B 0
