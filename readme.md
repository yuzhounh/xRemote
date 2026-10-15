# xRemote

Personal fork of **1Remote**. Current version: **xRemote 1.3.0 Rev.8**.

The full upstream baseline and xRemote revision are defined separately in `Ui/AppVersion.cs`.
Change the upstream version only when integrating that upstream release. Increment `ForkRevision`
only for a published xRemote release, including after upstream upgrades; never reset it.
Repeated builds and ordinary edits do not increment it. Tags use `v1.3.0-r8` and release packages
use `xRemote-v1.3.0-r8-x64.zip`. Windows numeric file versions use `1.3.0.8`;
the interface and product version show `1.3.0 Rev.8`.

Updates are checked only against [yuzhounh/xRemote releases](https://github.com/yuzhounh/xRemote/releases).
The enabled-by-default setting checks stable tags of the form `v1.3.0-r8`, comparing both the
upstream baseline and xRemote revision. It does not check or install upstream 1Remote releases.
Update notifications open the matching xRemote release page; installation remains manual.
If GitHub limits anonymous API requests, the checker falls back to the same repository's latest
stable release redirect, without using or storing GitHub credentials.

### Build xRemote on Windows

Install the .NET 9 SDK, initialize the repository's submodules, and apply the maintained dependency patch:

```powershell
git submodule update --init --recursive
.\scripts\Prepare-Dependencies.ps1
dotnet publish Ui/Ui.csproj -c Release -r win-x64 --self-contained true -o artifacts/publish
.\scripts\Package-WindowsRelease.ps1 -PublishDirectory artifacts/publish
.\scripts\Build-WindowsInstaller.ps1 -PackageDirectory artifacts/package-1.3.0-r8
```

The published app host is `1Remote.exe`; release packaging renames it to `xRemote.exe`.
Internal DLL and user-data filenames remain unchanged. Distribution packages contain only program
files and a portable-mode marker, never local profiles, databases, connection settings or credentials.
The optional Inno Setup 6 installer uses the same program files, creates shortcuts and uninstall
registration, and stores profiles in the existing Windows AppData locations. It preserves existing
portable storage when deliberately installed over a portable profile; uninstall does not delete profiles.
Both the portable ZIP and installer EXE are published in each Windows release.

Existing `1Remote` profile filenames, assembly identity and credential storage keys are retained
for compatibility. Upstream developer build hooks are disabled by default. The original upstream
description follows; its update links refer to 1Remote releases.

## Upstream 1Remote

English | [中文](https://github.com/1Remote/1Remote/wiki/Intro%E2%80%90ZhCn)

[![version](https://img.shields.io/github/v/release/1Remote/1Remote?color=Green&include_prereleases)](https://github.com/1Remote/1Remote/releases)
[![issues](https://img.shields.io/github/issues/1Remote/1Remote)](https://github.com/1Remote/1Remote/issues)
[![license](https://img.shields.io/github/license/1Remote/1Remote?color=blue)](https://github.com/1Remote/1Remote/blob/dev/LICENSE)
![](https://github.com/1Remote/1Remote/actions/workflows/build-on-dev-push.yml/badge.svg)

1Remote is a modern personal remote session manager and launcher. It is a single place to manage all your remote sessions supporting number of different protocols.

## Features

- Supports RDP, SSH, VNC, Telnet, (S)FTP, [RemoteApp](https://1remote.github.io/usage/protocol/especial/remoteapp/), [NoMachine and other app](https://1remote.github.io/usage/protocol/especial/app/)
- Quick and convenient remote session launcher (Alt + M)
- Multi-screen and HiDPI RDP connection (Test on **Win10 + 4k monitor *2** RDP TO **Win2016**)
- Detailed connection configuration: tags, icons, colors, connection scripts etc.
- Multiple languages, themes and tabbed interface
- [Import connections from mRemoteNG](https://1remote.github.io/usage/overview/#importing-from-mremoteng)
- Customizable runners, in SFTP \ FTP \ VNC \ etc. protocols, you can replace the internal runner with your favourite tools.[wiki](https://1remote.github.io/usage/protocol/runner/)
- Portable - just unpack and run

## 🚩Installation

Latest Version: 1.3

### 🗺[Quick start](https://1remote.github.io/usage/quick-start/)

### 🔻[Download](https://1remote.github.io/download/)

## 👓Overview

<img src="https://1remote.github.io/img/home_override/hero1.png" width="800" />

<p align="center">
    <img src="https://1remote.github.io/img/home_override/protocols.png" width="400" />
</p>
<p align="center">
    <img src="https://1remote.github.io/img/home_override/hero2.gif" width="400"/>
</p>

<p align="center">
    ↑ Launcher(Alt + M) open RDP connection & resizing
</p>

<p align="center">
    <img src="https://raw.githubusercontent.com/1Remote/PRemoteM/Doc/DocPic/multi-screen.jpg" width="500"/>
</p>

<p align="center">
    ↑ RDP with Multi-monitors
</p>

<p align="center">
    <img src="https://raw.githubusercontent.com/1Remote/PRemoteM/Doc/DocPic/RemoteApp/demo.jpg" width="800"/>
</p>

<p align="center">
    ↑ RemoteApp via RDP
</p>

<p align="center">
    <img src="https://raw.githubusercontent.com/1Remote/PRemoteM/Doc/DocPic/Runner/vnc_runners.jpg" width="800"/>
</p>

<p align="center">
    ↑ Customizable runners
</p>

## Make 1Remote Stronger

If you like **1Remote**, help us make it stronger by doing any of the following:

1. Simply star this repository
2. [Help translation](https://1remote.github.io/usage/misc/help-translation/)
3. [Buy a coffee](https://ko-fi.com/VShawn)
4. [Join DEV](DEVELOP.md)

## Special thanks

<a href="http://www.jetbrains.com/resharper/"><img src="http://www.tom-englert.de/Images/icon_ReSharper.png" alt="ReSharper" width="64" height="64" /></a>
