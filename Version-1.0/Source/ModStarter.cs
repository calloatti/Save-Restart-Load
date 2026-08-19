using HarmonyLib;
using Timberborn.ModManagerScene;

namespace Calloatti.SaveRestartLoad;

/// <summary>
/// Mod entry point. Applies all Harmony patches when the mod is loaded.
/// </summary>
public class ModStarter : IModStarter {
  public void StartMod(IModEnvironment modEnvironment) {
    new Harmony("calloatti.saverestartload").PatchAll();
  }
}
