# 开发说明

## 技术栈

- UI：.NET 10 WPF
- UI 框架：QING.UIKIT
- 核心逻辑：`FH.LanguageComboTool.Core`
- 测试：MSTest

## 前置条件

- Windows 10 / Windows 11
- .NET 10 SDK
- 本地 QING.UIKIT 仓库

默认目录结构：

```text
parent/
  FH Language Combo Tool/
  QING.UIKIT/
```

其他目录可通过 `-p:QingUiKitRoot="..."` 指定。

## 构建

```powershell
dotnet build .\dotnet\FH.LanguageComboTool.slnx
```

## 测试

```powershell
dotnet test .\dotnet\tests\FH.LanguageComboTool.Core.Tests\FH.LanguageComboTool.Core.Tests.csproj
```

测试必须使用临时目录，不得写入真实游戏目录、Steam appmanifest 或用户
`LOCALAPPDATA`。

## 代码边界

- WPF 层负责展示、输入收集与操作确认。
- 文件扫描、备份、应用、恢复、状态判断与 Steam 配置必须位于 Core。
- 所有覆盖操作必须先创建并校验备份。
- 新增文件写入逻辑时，必须添加临时目录单元测试。

## 发布

推送 `v*` 标签后，GitHub Actions 会检出固定版本的 QING.UIKIT，运行测试，
并发布自包含与框架依赖两种单文件 `win-x64` EXE。
