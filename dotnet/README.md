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

Self-contained:

```powershell
dotnet publish .\src\FH.LanguageComboTool.Wpf\FH.LanguageComboTool.Wpf.csproj `
  -p:PublishProfile=win-x64-single-file
```

Framework-dependent:

```powershell
dotnet publish .\src\FH.LanguageComboTool.Wpf\FH.LanguageComboTool.Wpf.csproj `
  -p:PublishProfile=win-x64-framework-dependent
```

Both profiles produce one executable. The framework-dependent build requires
the .NET 10 Desktop Runtime x64.
