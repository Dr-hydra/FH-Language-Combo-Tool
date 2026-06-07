# .NET 模块边界

## FH.LanguageComboTool.Core

### GameDetector

读取 Steam 注册表路径和 `libraryfolders.vdf`，检测 FH5 / FH6，并验证手动
选择的游戏目录。

### ResourceScanner

扫描 StringTables ZIP，生成语言代码、显示名、文件大小、可读写状态和
SHA-256。

### LanguageMapper

维护语言显示名称、语音白名单与 Steam 语言代码，并生成应用计划。

### BackupManager

创建唯一备份目录，复制并校验原始语音包，写入 `manifest.json`，列出历史
备份并验证完整性。

### ApplyEngine

校验文件、创建备份、通过唯一临时文件原子替换目标、更新 Steam 语言，并在
失败时尝试回滚。

### ConfigurationEngine

所有普通应用操作的统一事务入口。若当前已有生效、过期或被外部修改的组合，
先从当前安装目录对应的最近有效备份恢复原始语音包，再应用新组合；相同且已
生效的组合不会重复创建备份。

### RestoreEngine

校验备份清单路径和 SHA-256，仅允许写回清单声明的资源目录，并恢复修改前的
Steam 语言状态。

### ReapplyEngine

读取最近有效组合，并以强制刷新模式调用 `ConfigurationEngine`。禁止直接
备份已覆盖的目标文件。

### StatusService

区分 `none`、`applied`、`reverted`、`outdated` 和 `modified`。

### SteamLanguageService

读写 appmanifest 的 `language` 与本地 `UserPreferredLang`，并能恢复原先不
存在配置的状态。

### ProcessService

通过可执行文件名检测 FH5 / FH6 是否正在运行。

## FH.LanguageComboTool.Wpf

WPF 层使用 QING.UIKIT 控件与主题资源，只负责页面状态、选择项、确认提示和
调用 Core 服务。界面文案固定为简体中文。
