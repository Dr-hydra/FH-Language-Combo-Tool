# FH Language Combo Tool

FH Language Combo Tool lets you choose separate voice and text languages for
the Steam versions of Forza Horizon 5 / 6 and the Xbox version of Forza
Horizon 6.

The application is a native .NET 10 WPF desktop tool with Simplified Chinese
and English interfaces.

## Download

Choose one file from this project's Releases page:

- `FH-Language-Combo-Tool-win-x64-self-contained.exe`: no .NET installation
  required; recommended for most users.
- `FH-Language-Combo-Tool-win-x64-framework-dependent.exe`: smaller download;
  requires the .NET 10 Desktop Runtime x64.

## First Run

1. Choose the interface language. Simplified Chinese is selected by default.
2. Read and check the four usage notices.
3. Select **Accept and Continue**.

The interface language can be changed later from **Settings**.

## Apply a Combination

1. Let the tool detect Steam installations automatically. For the Xbox
   version, select `Forza Horizon 6 (Xbox)` and specify its actual installation
   directory manually.
2. Choose a voice language.
3. Choose a text language.
4. Review the operation preview.
5. Select **Apply Configuration** and confirm.

The tool creates and verifies a backup before replacing a language pack. It
also synchronizes `UserPreferredLang`; Steam installations additionally
synchronize the appmanifest language.

## Restore

Open **Backups**, select a valid backup, and choose **Restore Selected Backup**.
Backups with invalid paths or SHA-256 mismatches cannot be restored.

## Notes

- Close the game before applying or restoring files.
- Download the required language packs through Steam first for Steam installs.
- The Xbox version of FH6 initially includes only Chinese and English language
  packs. To download another language, launch the game, select that language
  in the in-game settings, wait for the download to finish, close the game,
  and validate the installation directory again in the tool.
- Game updates, Steam file verification, or Xbox App repairs may overwrite
  modifications.
- The tool does not modify executables, save files, network data, or
  anti-cheat components.

## Disclaimer

This is an unofficial project and is not affiliated with Playground Games,
Microsoft, Xbox, or Turn 10. Use it at your own risk.
