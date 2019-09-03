ShadowsocksR for Windows
=======================

[![Build Status]][Appveyor]

#### Download

* [latest release]
* [nightly build]

#### Artifacts
* `ShadowsocksR-netcore.zip` 
    * 需要安装 [.NET Core 3.0 Runtime](https://dotnet.microsoft.com/download/dotnet-core/3.0)
    * 文件小

* `ShadowsocksR-net48.zip` 
    * 需要安装 [.NET Framework 4.8](https://dotnet.microsoft.com/download) （最新版 Win10 自带）
    * 文件小

**以下版本首次运行会在 `%temp%\.net\ShadowsocksR\` 释放运行库，若有各种清理软件、Windows 更新等误删则会造成运行出错，删除该目录下的所有文件即可重新释放**

* `ShadowsocksR-netcore-win32.zip`
    * 无需额外安装任何运行时
    * 文件大

* `ShadowsocksR-netcore-win64.zip`
    * 无需额外安装任何运行时
    * 文件大

#### Donate
[Donate](./pic/wechat.jpg)

#### Develop

Visual Studio Community 2019 is recommended.

#### License

GPLv3

Copyright © HMBSbige 2019. Forked from ShadowsocksR by BreakWa11

[Appveyor]:       https://ci.appveyor.com/project/HMBSbige/shadowsocksr-windows
[Build Status]:   https://ci.appveyor.com/api/projects/status/b9jgwdfvn20ithj1/branch/master?svg=true
[latest release]: https://github.com/HMBSbige/ShadowsocksR-Windows/releases
[nightly build]: https://ci.appveyor.com/project/HMBSbige/shadowsocksr-windows/branch/master/artifacts