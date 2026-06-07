using System.Diagnostics;
using FH.LanguageComboTool.Core.Models;

namespace FH.LanguageComboTool.Core.Services;

public static class ProcessService
{
    public static bool IsGameRunning(GameId gameId)
    {
        var exe = Path.GetFileNameWithoutExtension(GameDetector.GetExecutableName(gameId));
        var processes = Process.GetProcessesByName(exe);
        try
        {
            return processes.Length > 0;
        }
        finally
        {
            foreach (var process in processes)
                process.Dispose();
        }
    }
}
