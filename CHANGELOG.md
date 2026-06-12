# Changelog

All notable changes to FH Language Combo Tool are documented here.

## [2.1.0] - 2026-06-12

### Added

- Automatic detection for the Xbox version of Forza Horizon 6 under
  `<drive>:\XboxGames\Forza Horizon 6`.
- Support for Xbox installations that place game content in a `Content`
  subdirectory.
- Manual Xbox installation detection and Xbox channel labeling in the game
  list.
- Xbox language-pack guidance explaining how to trigger downloads from the
  in-game language setting.

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
- Optional framework-dependent single-file build for users who already have
  the .NET 10 Desktop Runtime x64.

### Safety

- Blocks file changes while the game is running.
- Restores an active target before switching combinations.
- Validates backup paths and hashes before restore.
- Prevents externally modified files without a valid backup from being
  overwritten automatically.

[2.1.0]: https://github.com/Dr-hydra/FH-Language-Combo-Tool/releases/tag/v2.1.0
[1.2.0]: https://github.com/Dr-hydra/FH-Language-Combo-Tool/releases/tag/v1.2.0
