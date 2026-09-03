using Bindito.Core;
using Timberborn.ModManagerScene;

namespace Calloatti.SaveRestartLoad;

/// <summary>
/// Registers mod services in the Game context so the game menu buttons can receive their dependencies.
/// </summary>
[Context("Game")]
public class ModConfigurator : Configurator {
  protected override void Configure() {
    Bind<GameOptionsBoxPatchInitializer>().AsSingleton();
  }
}
