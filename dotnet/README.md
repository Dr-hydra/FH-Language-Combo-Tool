# FH Language Combo Tool WPF

This directory contains the .NET 10 WPF implementation of FH Language Combo
Tool.

## Projects

- `src/FH.LanguageComboTool.Core` - independent backend/service layer.
- `src/FH.LanguageComboTool.Wpf` - WPF application using `QING.UIKIT`.
- `tests/FH.LanguageComboTool.Core.Tests` - core logic tests.

## Build

```powershell
dotnet build .\FH.LanguageComboTool.slnx
```

## Test

```powershell
dotnet test .\tests\FH.LanguageComboTool.Core.Tests\FH.LanguageComboTool.Core.Tests.csproj
```

## Run

```powershell
dotnet run --project .\src\FH.LanguageComboTool.Wpf\FH.LanguageComboTool.Wpf.csproj
```

## Publish

```powershell
dotnet publish .\src\FH.LanguageComboTool.Wpf\FH.LanguageComboTool.Wpf.csproj `
  -p:PublishProfile=win-x64-single-file
```

The result is one self-contained executable. Native runtime components are
extracted automatically when the application starts.
