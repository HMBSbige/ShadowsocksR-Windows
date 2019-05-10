ShadowsocksR for Windows
=======================

[![Build Status]][Appveyor]

#### Download

Download the [latest release] for ShadowsocksR for Windows.

#### Usage

1. Find ShadowsocksR icon in the system tray.
2. You can add multiple servers in servers menu.
3. Select Enable System Proxy menu to enable system proxy. Please disable other
proxy addons in your browser, or set them to use system proxy.
4. You can also configure your browser proxy manually if you don't want to enable
system proxy. Set Socks5 or HTTP proxy to 127.0.0.1:1080. You can change this
port in Global settings.
5. You can change PAC rules by editing the PAC file. When you save the PAC file
with any editor, ShadowsocksR will notify browsers about the change automatically.
6. You can also update the PAC file from GFWList. Note your modifications to the PAC
file will be lost. However you can put your rules in the user rule file for GFWList.
Don't forget to update from GFWList again after you've edited the user rule.
7. For UDP, you need to use SocksCap or ProxyCap to force programs you want
to proxy to tunnel over ShadowsocksR.

### Develop

Visual Studio Community 2019 is recommended.

#### License

GPLv3

Copyright © HMBSbige 2019. Forked from ShadowsocksR by BreakWa11

[Appveyor]:       https://ci.appveyor.com/project/HMBSbige/shadowsocksr-windows
[Build Status]:   https://ci.appveyor.com/api/projects/status/b9jgwdfvn20ithj1/branch/master?svg=true
[latest release]: https://github.com/HMBSbige/ShadowsocksR-Windows/releases
