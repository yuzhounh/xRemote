# xRemote 本地会话管理开发版

2026-10-16 用户已授权部署到登记的五台 Windows 设备；安装、客户端配对及尚待完成的实机验收状态见 `review/session_control_plan.md`。安装包经私网 SSH 传输，不公开上传。

候选的 Windows 文件夹为干净的独立便携版，不含当前连接数据库或密码，不会自动复制现有用户配置或替换日常运行版。

## 使用方式

被控电脑运行 `xRemote.ControlHost` Windows 服务。它通过 Windows WTS 接口查询真实会话并结束指定远程连接，使用 HTTPS、固定服务器证书和独立随机管理密钥；不使用或分发 SSH 私钥，不需要先登录 RDP，也不收集 Windows 密码。固定的私网端口为 24443。

Windows 客户端点击设备卡片或列表的状态区域打开“远程会话”，导入该设备的 `pairing.json`。Android 使用“设置 → 设备管理配对”导入，点击设备旁的“会话”查看和断开指定连接。手机经已有内置 EasyTier 的本地 24389–24393 转发访问服务；不新增 VPN 接口。

Windows 的配对配置使用当前用户 DPAPI；Android 使用既有 Keystore 存储。配对文件具有管理权限，只能保存在自己的设备，不随源码或发行包上传。远程使用情况未获取时单独说明；RDP 端口可达仍显示绿色“在线”，连通性尚在检测时为黄色。RDP 不可达时管理通道仍可独立使用。

## 本机隐私与收回控制

- `black`：新远程连接建立、确认实体控制台已锁定后，向实体控制台发送一次显示器息屏请求。默认值为 black；不安装遮屏驱动，不阻断键盘鼠标，不更改桌面或 Windows 登录组件。
- 本机键鼠可唤醒 Windows 登录界面；本机登录/解锁由 Windows 验证。服务仅接受 SCM 提供的登录/解锁通知，并重新核对实际实体控制台、解锁状态和账户，才结束该账户的远程连接。服务器其他账户会话保留。
- `lock`：保留系统锁屏，不请求息屏。服务不自动解锁 Windows；仅断开，不注销，不终止程序或计算任务。
- 息屏请求失败时保留系统锁屏并在状态中说明。接口只报告请求结果，不能证明每台实体显示器实际已黑。多显示器、唤醒和实际本机验证接管仍需现场验收。
- 这不是 UU 黑屏管理器，也不能保证唤醒被 UU 或其他软件的驱动屏蔽的显示器。

所有控制端应更新到本地候选并完成设备配对。受管 Windows RDP 连接禁用 ActiveX 和应用层自动重连；导出的 .rdp / RemoteApp 也强制关闭自动重连，附加配置不能覆盖。Android 显式禁用 FreeRDP 自动重连。因此受管连接断开后，包括网络中断，都需要用户手动连接。短暂切到后台再返回仍可继续未断开的既有手机会话。

## 安装和移除

在目标设备的管理员 PowerShell 中执行项目 `scripts/Install-ControlHost.ps1`（候选包根目录也附有同名脚本），参数 `-PackageDirectory` 指向本地候选的 `ControlHost` 文件夹，`-Address` 为该设备的 EasyTier 地址，`-PrivacyMode black` 或 `lock`。

脚本只允许五台已登记设备地址，检查地址确实属于本机。本机（10.126.126.2）的服务根目录为 `D:\Archives\20261015 1Remote\control-private\service`，程序和配置分别位于其中的 `code\control-host` 和 `data\control`；本机更新回滚备份统一存入 `D:\Archives\Backups\20261015 1Remote`。其他远程设备继续使用 `D:\WangJing\xRemote`，也可通过 `-InstallRoot` 显式指定。配置目录使用受保护的 ACL，并排除 Git。服务以 LocalSystem 运行，防火墙仅开放本机 EasyTier 地址的 TCP 24443，来源限定 `10.126.126.0/24`。现有服务或同名目录会停止安装，不覆盖。启动采用自动服务和服务失败重启；不重启 RDP、EasyTier 或 Windows。

安装后从受保护的配置目录将 `pairing.json` 导入自己的客户端。若要移除，管理员运行 `scripts/Uninstall-ControlHost.ps1`：停止并删除本服务、移除本服务防火墙规则，保留程序和私有配置，不禁用 RDP 或 EasyTier。

## 本地验证

构建 `control/host/ControlHost.csproj` 为 win-x64 自包含发布包。`control/tests/ControlTests.csproj` 使用模拟会话检查指定断开、会话身份变化、重放拒绝、本机认证来源和保留其他账户；同时运行真实本机 WTS 只读枚举、隔离的 HTTPS 服务和真实 Windows HTTP/DPAPI 客户端检查。隔离服务的 `--read-only` 禁止 API 修改、显示动作和本机解锁接管。

`control/ui-tests/UiChecks.csproj` 检查真实编译的 Windows 状态绑定、会话入口、窗口构造和 .rdp 重连覆盖保护，不显示窗口、不建立 RDP。`scripts/Test-RdpReconnectPolicy.ps1` 检查断开原因处理。Android 完成 assembleRelease、lintRelease 和 APK v2 签名验证。

实际“办公室连接家里 → 手机断开”“实体黑屏 → 本机 Windows 验证 → 收回控制”尚未执行；代码及模拟验证不能代替这些验收。
