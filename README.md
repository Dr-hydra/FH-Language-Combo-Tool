# FH Language Combo Tool

FH Language Combo Tool 是一个面向 Windows 的《Forza Horizon 5 / 6》Steam
版语言组合工具，可分别选择语音语言与文字语言。

应用基于 `.NET 10 WPF`，界面直接使用
[QING.UIKIT](https://github.com/Dr-hydra/QING.UIKIT)。

## 技术栈

- UI：WPF + QING.UIKIT
- 业务逻辑：.NET 10 独立服务层
- 目标框架：`net10.0-windows`
- 支持平台：Windows 10 / Windows 11

## 项目结构

```text
dotnet/
  FH.LanguageComboTool.slnx
  src/
    FH.LanguageComboTool.Core/
    FH.LanguageComboTool.Wpf/
  tests/
    FH.LanguageComboTool.Core.Tests/
```

默认从仓库同级目录 `QING.UIKIT` 引用 UI 框架。其他位置可通过 MSBuild
属性指定：

```powershell
dotnet build .\dotnet\FH.LanguageComboTool.slnx `
  -p:QingUiKitRoot="E:\Dr.Hydra\QING.UIKIT"
```

## 构建与测试

```powershell
dotnet build .\dotnet\FH.LanguageComboTool.slnx
dotnet test .\dotnet\tests\FH.LanguageComboTool.Core.Tests\FH.LanguageComboTool.Core.Tests.csproj
```

## 发布

```powershell
dotnet publish .\dotnet\src\FH.LanguageComboTool.Wpf\FH.LanguageComboTool.Wpf.csproj `
  -p:PublishProfile=win-x64-folder
```

## 功能

- 自动检测 Steam 版 FH5 / FH6
- 手动验证游戏目录
- 扫描 StringTables 语言包
- 分别选择语音语言与文字语言
- 修改前自动创建 SHA-256 校验备份
- 列出并恢复历史备份
- 识别已应用、已恢复、需要重新应用及外部修改状态
- 游戏运行时阻止危险操作
- 同步 Steam appmanifest 与 `UserPreferredLang`
- 简体中文与英文界面，可在首次启动或设置页切换

## 免责声明

本工具为非官方项目，与 Playground Games、Microsoft、Xbox 或 Turn 10
没有关联。工具会修改本地游戏资源文件，请自行承担使用风险。
