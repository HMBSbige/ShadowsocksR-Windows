@echo off

echo package .NET Core
7z a ShadowsocksR-netcore.zip %~dp0\shadowsocks-csharp\bin\Release\netcoreapp3.0\publish\ShadowsocksR.exe %~dp0\shadowsocks-csharp\bin\Release\netcoreapp3.0\publish\ShadowsocksR.dll %~dp0\shadowsocks-csharp\bin\Release\netcoreapp3.0\publish\ShadowsocksR.runtimeconfig.json

echo package .NET Framework
7z a ShadowsocksR-net48.zip %~dp0\shadowsocks-csharp\bin\Release\net48\ShadowsocksR.exe %~dp0\shadowsocks-csharp\bin\Release\net48\ShadowsocksR.exe.config

echo package .NET Core SelfContained x86
7z a ShadowsocksR-netcore-win32.zip %~dp0\shadowsocks-csharp\bin\Release\netcoreapp3.0\win-x86\publish\ShadowsocksR.exe %~dp0\clean.cmd

echo package .NET Core SelfContained x64
7z a ShadowsocksR-netcore-win64.zip %~dp0\shadowsocks-csharp\bin\Release\netcoreapp3.0\win-x64\publish\ShadowsocksR.exe %~dp0\clean.cmd