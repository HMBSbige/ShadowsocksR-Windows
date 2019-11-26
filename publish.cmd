@echo off

for /f "tokens=*" %%s in ('findstr /C:"public const string Version = @" %~dp0\shadowsocks-csharp\Controller\HttpRequest\UpdateChecker.cs') do (
    set version=%%s
)
set version=%version:~32,-2%

echo Version: %version%

echo package .NET Framework
7z a -mx9 ShadowsocksR-net48-%version%.7z %~dp0\shadowsocks-csharp\bin\Release\net48\ShadowsocksR.exe %~dp0\shadowsocks-csharp\bin\Release\net48\ShadowsocksR.exe.config

echo package .NET Core
7z a -mx9 ShadowsocksR-netcore-%version%.7z %~dp0\shadowsocks-csharp\bin\Release\netcoreapp3.0\publish\
7z rn ShadowsocksR-netcore-%version%.7z publish ShadowsocksR

echo package .NET Core SelfContained x86
7z a -mx9 ShadowsocksR-Portable-Win32-%version%.7z %~dp0\shadowsocks-csharp\bin\Release\netcoreapp3.0\win-x86\publish\
7z rn ShadowsocksR-Portable-Win32-%version%.7z publish ShadowsocksR

echo package .NET Core SelfContained x64
7z a -mx9 ShadowsocksR-Portable-Win64-%version%.7z %~dp0\shadowsocks-csharp\bin\Release\netcoreapp3.0\win-x64\publish\
7z rn ShadowsocksR-Portable-Win64-%version%.7z publish ShadowsocksR
