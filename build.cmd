@echo off
echo dotnet SDK version
dotnet --version

REM The reason we don't use dotnet build is that dotnet build doesn't support COM references yet https://github.com/microsoft/msbuild/issues/3986
REM dotnet build -c Release -f net48
REM dotnet publish -c Release -f netcoreapp3.0 -r win-x86 --self-contained
REM dotnet publish -c Release -f netcoreapp3.0 -r win-x64 --self-contained

set netcore_tfm=netcoreapp3.0
set net_tfm=net48
set configuration=Release
set mainDir=%cd%
set net_baseoutput=%mainDir%\shadowsocks-csharp\bin\%configuration%
set apphostpatcherDir=%mainDir%\AppHostPatcher

cd %apphostpatcherDir%
call:Build-AppHostPatcher

cd %mainDir%\shadowsocks-csharp
call:Build-NetFramework
call:Build-NetCore
call:Build-NetCoreSelfContained x86
call:Build-NetCoreSelfContained x64

cd %mainDir%
goto :EOF

:error
cd %mainDir%
exit /b %errorlevel%

:Build-AppHostPatcher
echo Building AppHostPatcher

set outdir=%apphostpatcherDir%\bin\%configuration%\%netcore_tfm%
set publishDir=%outdir%\publish

rd /S /Q %publishDir%

msbuild -v:m -r -t:Publish -p:Configuration=%configuration% -p:TargetFramework=%netcore_tfm% || goto :error

goto :EOF

:Build-NetFramework
echo Building .NET Framework x86 and x64

set outdir=%net_baseoutput%\%net_tfm%

msbuild -v:m -r -t:Build -p:Configuration=%configuration% -p:TargetFramework=%net_tfm% || goto :error

goto :EOF

:Build-NetCore
echo Building .NET Core

set outdir=%net_baseoutput%\%netcore_tfm%
set publishDir=%outdir%\publish

rd /S /Q %publishDir%

msbuild -v:m -r -t:Publish -p:Configuration=%configuration% -p:TargetFramework=%netcore_tfm% || goto :error

set tmpbin=tmpbin
ren %publishDir% %tmpbin%
md %publishDir%
move %outdir%\%tmpbin% %publishDir%
ren %publishDir%\%tmpbin% bin
move %publishDir%\bin\ShadowsocksR.exe %publishDir%

echo Patching .NET Core
%apphostpatcherDir%\bin\%configuration%\%netcore_tfm%\AppHostPatcher.exe %publishDir%\ShadowsocksR.exe -d bin || goto :error
echo Build .NET Core completed

goto :EOF

:Build-NetCoreSelfContained
echo Building .NET Core SelfContained %1

set rid=win-%1
set outdir=%net_baseoutput%\%netcore_tfm%\%rid%
set publishDir=%outdir%\publish

rd /S /Q %publishDir%

msbuild -v:m -r -t:Publish -p:Configuration=%configuration% -p:TargetFramework=%netcore_tfm% -p:RuntimeIdentifier=%rid% -p:SelfContained=True -p:PublishReadyToRun=True || goto :error

set tmpbin=tmpbin
ren %publishDir% %tmpbin%
md %publishDir%
move %outdir%\%tmpbin% %publishDir%
ren %publishDir%\%tmpbin% bin
move %publishDir%\bin\ShadowsocksR.exe %publishDir%

echo Patching .NET Core SelfContained %1
%apphostpatcherDir%\bin\%configuration%\%netcore_tfm%\AppHostPatcher.exe %publishDir%\ShadowsocksR.exe -d bin || goto :error
echo Build .NET Core SelfContained %1 completed

goto :EOF