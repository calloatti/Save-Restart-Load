using System;
using System.Diagnostics;
using System.IO;
using Timberborn.GameSaveRepositorySystem;
using UnityEngine;

namespace Calloatti.SaveRestartLoad;

/// <summary>
/// Restarts the game process and optionally auto-loads a save file on boot using command-line arguments.
/// </summary>
public static class GameRestarter {
  private static readonly string LogFilePath = Path.Combine(Application.persistentDataPath, "SaveRestartLoad.log");
  private const string LogPrefix = "[SaveRestartLoad]";

  /// <summary>
  /// Appends directly to a dedicated log file to ensure messages flush to disk before Application.Quit().
  /// </summary>
  private static void LogDirect(string message) {
    try {
      File.AppendAllText(LogFilePath, $"{LogPrefix} [{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
    } catch { }

    // Also log to Unity standard log for active runtime tracking
    UnityEngine.Debug.Log($"{LogPrefix} {message}");
  }

  /// <summary>
  /// Restarts the game and auto-loads the given save file when the game finishes booting.
  /// </summary>
  public static void RequestRestartAndLoad(SaveReference saveReference) {
    if (saveReference == null) {
      LogDirect("ERROR: Cannot perform Restart + Load. SaveReference is null.");
      return;
    }

    var saveName = saveReference.SaveName;
    var settlementName = saveReference.SettlementReference?.SettlementName ?? "";

    var extraArgs = new[] {
      "-skipModManager",
      "-settlementName", settlementName,
      "-saveName", saveName
    };

    ExecuteRestartSequence(extraArgs);
  }

  private static void ExecuteRestartSequence(string[] args) {
    // Reset the file on sequence initialization
    try { if (File.Exists(LogFilePath)) File.Delete(LogFilePath); } catch { }

    LogDirect("Restart sequence initiated.");

    var rootPath = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
    string exePath;
    var currentPid = Process.GetCurrentProcess().Id;
    LogDirect($"Current Process ID: {currentPid}");

    var psi = new ProcessStartInfo {
      UseShellExecute = true
    };

    if (Application.platform == RuntimePlatform.WindowsPlayer) {
      exePath = Path.Combine(rootPath, "Timberborn.exe");
      var argString = string.Join(" ", Array.ConvertAll(args, a => $"\"{a}\""));

      var psCommand = $"Wait-Process -Id {currentPid} -ErrorAction SilentlyContinue; & '{exePath}' {argString}";
      LogDirect($"Encoding PowerShell payload to preserve formatting: {psCommand}");

      // Convert command to Base64 (UTF-16LE) to safely bypass all Windows quote parsing rules
      var bytes = System.Text.Encoding.Unicode.GetBytes(psCommand);
      var encodedCommand = Convert.ToBase64String(bytes);

      psi.FileName = "powershell.exe";
      psi.Arguments = $"-NoProfile -WindowStyle Hidden -EncodedCommand {encodedCommand}";
      psi.RedirectStandardInput = false;
      psi.UseShellExecute = true; // Forces OS-level execution context to fix MyDocuments
      psi.WindowStyle = ProcessWindowStyle.Hidden;
      psi.WorkingDirectory = rootPath; // Locks the environment to the game root

      try {
        Process.Start(psi);
        LogDirect("PowerShell background process successfully deployed.");
      } catch (Exception ex) {
        LogDirect($"PowerShell Process Error: {ex.Message}");
      }
    } else // Linux or macOS Player Execution Environment
    {
      if (Application.platform == RuntimePlatform.OSXPlayer) {
        exePath = Path.Combine(rootPath, "Timberborn.app/Contents/MacOS/Timberborn");
      } else {
        exePath = Path.Combine(rootPath, "Timberborn.x86_64");
      }

      var unixArgs = string.Join(" ", Array.ConvertAll(args, a => $"\"{a}\""));
      var shCommand = $"while kill -0 {currentPid} 2>/dev/null; do sleep 1; done; nohup \"{exePath}\" {unixArgs} > /dev/null 2>&1 &";
      LogDirect($"Piping Bash payload: {shCommand}");

      psi.FileName = "/bin/bash";
      psi.Arguments = $"-c \"{shCommand}\"";

      try {
        Process.Start(psi);
        LogDirect("Bash script loop successfully detached.");
      } catch (Exception ex) {
        LogDirect($"Bash Process Error: {ex.Message}");
      }
    }

    // Terminate application immediately to release runtime system locks
    try {
      LogDirect("Calling Application.Quit(). Clearing process memory bounds...");
      Application.Quit();
    } catch (Exception ex) {
      LogDirect($"Error during Application.Quit execution loop: {ex.Message}");
    }
  }
}
