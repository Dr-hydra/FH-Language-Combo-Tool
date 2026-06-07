# Changelog

All notable changes to FH Language Combo Tool are documented here.

## [1.2.0] - 2026-06-07

### Added

- Native .NET 10 WPF application using QING.UIKIT.
- Independent Core service layer for detection, scanning, backup, apply,
  restore, and status operations.
- Simplified Chinese and English interfaces.
- First-run language selection and language switching from Settings.
- Automatic Steam library detection for Forza Horizon 5 / 6.
- SHA-256 verified backups and safe restore.
- Detection of applied, restored, outdated, externally modified, duplicated,
  and JP/CHS externally swapped language packs.
- Self-contained `win-x64` release workflow.
- Compressed single-file executable release with only English and Simplified
  Chinese framework resources.

### Safety

- Blocks file changes while the game is running.
- Restores an active target before switching combinations.
- Validates backup paths and hashes before restore.
- Prevents externally modified files without a valid backup from being
  overwritten automatically.

[1.2.0]: https://github.com/Dr-hydra/FH-Language-Combo-Tool/releases/tag/v1.2.0
