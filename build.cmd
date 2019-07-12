@echo off
echo dotnet SDK version
dotnet --version

REM The reason we don't use dotnet build is that dotnet build doesn't support COM references yet https://github.com/microsoft/msbuild/issues/3986
REM dotnet build -c Release -f net48
REM dotnet publish -c Release -f netcoreapp3.0 -r win-x86 --self-contained
REM dotnet publish -c Release -f netcoreapp3.0 -r win-x64 --self-contained

cd shadowsocks-csharp

echo Building .NET Core
msbuild -v:m -t:Restore -p:Configuration=Release -p:TargetFramework=netcoreapp3.0 || goto :error
msbuild -v:m -t:Publish -p:Configuration=Release -p:TargetFramework=netcoreapp3.0 || goto :error

echo Building .NET Framework x86 and x64
msbuild -v:m -t:Restore -p:Configuration=Release -p:TargetFramework=net48 || goto :error
msbuild -v:m -t:Build -p:Configuration=Release -p:TargetFramework=net48 || goto :error

echo Building .NET Core SelfContained x86
msbuild -v:m -t:Restore -p:Configuration=Release -p:TargetFramework=netcoreapp3.0 -p:RuntimeIdentifier=win-x86 -p:SelfContained=True -p:PublishSingleFile=true|| goto :error
msbuild -v:m -t:Publish -p:Configuration=Release -p:TargetFramework=netcoreapp3.0 -p:RuntimeIdentifier=win-x86 -p:SelfContained=True -p:PublishSingleFile=true|| goto :error

echo Building .NET Core SelfContained x64
msbuild -v:m -t:Restore -p:Configuration=Release -p:TargetFramework=netcoreapp3.0 -p:RuntimeIdentifier=win-x64 -p:SelfContained=True -p:PublishSingleFile=true|| goto :error
msbuild -v:m -t:Publish -p:Configuration=Release -p:TargetFramework=netcoreapp3.0 -p:RuntimeIdentifier=win-x64 -p:SelfContained=True -p:PublishSingleFile=true|| goto :error

cd..
goto :EOF

:error
cd..
exit /b %errorlevel%
