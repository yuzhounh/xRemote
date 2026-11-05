# 原生组件与来源

## AVNC / LibVNCClient

- 固定采用 AVNC 3.3.1 官方发布包中的未修改 arm64 `libnative-vnc.so`，在本应用内部显示和控制 VNC 桌面。
- 发布包：https://github.com/gujjwal00/avnc/releases/tag/v3.3.1
- JNI 及构建源码：https://github.com/gujjwal00/avnc/tree/v3.3.1
- LibVNC 子模块源码：https://github.com/gujjwal00/libvncserver/tree/3517d6337bb07012618c591bb73aae8f10a868d0
- AVNC 为 GPL-3.0-or-later；LibVNCClient 为 GPL-2.0-or-later，与本项目 GPL-3.0 使用方式兼容。保留 AVNC-GPL-3.0.txt、AVNC-BSD-libjpeg-turbo.txt、AVNC-X11.txt 及上游构建所使用的 LibreSSL/libjpeg-turbo 来源。CMake 将协议与构建依赖静态链接入该 JNI 库。
- 本项目仅编写最小 Java JNI ABI 适配层并使用自己的设备列表、加密凭据、会话界面及生命周期，没有合并 AVNC 的数据库或整个应用。准备脚本为 scripts/prepare-vnc.py。

应用代码遵循项目根目录的 GPL-3.0 许可证。以下上游组件保留各自许可证；本目录不改变其授权。既有 FreeRDP／EasyTier 原生库的来源包及历史校验记录见 native-manifest.json；AVNC 的固定发布包和许可证记录见上方。

## EasyTier

- 使用 EasyTier 2.6.4 系列 Android JNI，来自官方文档列出的第三方客户端 EasyTier Compose 1.0.9 arm64 发布包。
- 发布包：https://github.com/ECSDevs/EasyTierCompose/releases/tag/v1.0.9
- JNI 来源仓库：https://github.com/EasyTier/EasyTier
- 上游项目许可证：Apache-2.0；EasyTier Compose 应用自身为 GPL-3.0。本项目只复用其中的 EasyTier JNI 原生库，并自行实现界面与生命周期。
- 包 SHA-256：7d38361c30b85ede60e309b67f5cc4969a4ff4512fddc0d138eb2d49276b80f2。

## FreeRDP

- 使用 FreeRDP 3.32.1 Android JNI 与其构建依赖，来自 F-Droid 构建、签名的 aFreeRDP arm64 包。
- APK：https://f-droid.org/repo/com.freerdp.afreerdp_3320103.apk
- 对应源码包：https://f-droid.org/repo/com.freerdp.afreerdp_3320103_src.tar.gz
- 原始源码：https://github.com/FreeRDP/FreeRDP/tree/3.32.1
- F-Droid 构建元数据：https://gitlab.com/fdroid/fdroiddata/-/blob/master/metadata/com.freerdp.afreerdp.yml
- 上游主项目 Apache-2.0；Android JNI 源文件另有 MPL-2.0 声明。根许可证副本为 FreeRDP-LICENSE。本项目使用单独编写的最小 Java ABI 适配层，没有复用上游应用的后台服务、数据库或界面。
- APK SHA-256：520403bf41dec2e4599ddd22ab2bd7597f64867ec422c5e2fa46da259cac668e。
- 包中的 OpenSSL、FFmpeg、OpenH264、Opus、WebP、libjpeg、libpng、cJSON、uriparser、SQLCipher 等依赖保留其原许可证；对应版本和构建方法见上面的源码包与构建元数据。后续公开分发应随软件保留这些来源及适用的许可证、源码获取说明。
