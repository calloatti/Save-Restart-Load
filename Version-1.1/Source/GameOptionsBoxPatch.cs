using System;
using HarmonyLib;
using Timberborn.CoreUI;
using Timberborn.GameSaveRepositorySystem;
using Timberborn.GameSaveRuntimeSystem;
using Timberborn.Localization;
using Timberborn.OptionsGame;
using Timberborn.SingletonSystem;
using UnityEngine;
using UnityEngine.UIElements;

namespace Calloatti.SaveRestartLoad;

/// <summary>
/// Injects the "Restart + Load" and "Save + Restart + Load" buttons into the in-game options menu,
/// immediately after the existing "Load game" button.
/// </summary>
[HarmonyPatch(typeof(GameOptionsBox), "GetPanel")]
public static class GameOptionsBoxPatch {
  private const string RestartLoadButtonLocKey = "Calloatti.SaveRestartLoad.Button.RestartLoad";
  private const string SaveRestartLoadButtonLocKey = "Calloatti.SaveRestartLoad.Button.SaveRestartLoad";
  private const string NoSaveReferenceMessageLocKey = "Calloatti.SaveRestartLoad.Message.NoSaveReference";

  private static ILoc _loc;
  private static GameLoader _gameLoader;
  private static GameSaver _gameSaver;
  private static DialogBoxShower _dialogBoxShower;

  public static void SetServices(ILoc loc, GameLoader gameLoader, GameSaver gameSaver, DialogBoxShower dialogBoxShower) {
    _loc = loc;
    _gameLoader = gameLoader;
    _gameSaver = gameSaver;
    _dialogBoxShower = dialogBoxShower;
  }

  private static void Postfix(VisualElement __result) {
    if (__result == null) return;

    // Prevent duplicates when the menu is opened multiple times in a session.
    if (__result.Q("RestartLoadButton") != null) return;

    var loadGameButton = __result.Q<Button>("LoadGameButton");
    if (loadGameButton == null) {
      Debug.Log("[SaveRestartLoad] Could not find 'LoadGameButton'.");
      return;
    }

    var restartLoadButton = CreateButton(loadGameButton, "RestartLoadButton", RestartLoadButtonLocKey, RestartLoadClicked);
    var saveRestartLoadButton = CreateButton(loadGameButton, "SaveRestartLoadButton", SaveRestartLoadButtonLocKey, SaveRestartLoadClicked);

    var container = loadGameButton.parent;
    if (container == null) {
      Debug.Log("[SaveRestartLoad] Could not find the options container.");
      return;
    }

    var loadIndex = container.IndexOf(loadGameButton);
    container.Insert(loadIndex + 1, restartLoadButton);
    container.Insert(loadIndex + 2, saveRestartLoadButton);
  }

  private static Button CreateButton(Button referenceButton, string name, string locKey, EventCallback<ClickEvent> clickHandler) {
    var button = (Button)Activator.CreateInstance(referenceButton.GetType());
    button.name = name;
    button.text = _loc.T(locKey);

    var sheetCount = referenceButton.styleSheets.count;
    for (var i = 0; i < sheetCount; i++) {
      button.styleSheets.Add(referenceButton.styleSheets[i]);
    }

    foreach (var className in referenceButton.GetClasses()) {
      button.AddToClassList(className);
    }

    button.style.width = referenceButton.style.width;
    button.style.height = referenceButton.style.height;

    button.RegisterCallback<ClickEvent>(clickHandler);
    return button;
  }

  private static void RestartLoadClicked(ClickEvent evt) {
    if (!TryGetCurrentSave(out var saveReference)) return;
    GameRestarter.RequestRestartAndLoad(saveReference);
  }

  private static void SaveRestartLoadClicked(ClickEvent evt) {
    if (!TryGetCurrentSave(out var saveReference)) return;
    _gameSaver.SaveInstantlySkippingNameValidation(saveReference, () => GameRestarter.RequestRestartAndLoad(saveReference));
  }

  /// <summary>
  /// Returns the currently loaded save reference. If the current game has never been saved,
  /// shows an informational dialog box and aborts the operation.
  /// </summary>
  private static bool TryGetCurrentSave(out SaveReference saveReference) {
    saveReference = _gameLoader.LoadedSave;
    if (saveReference != null) return true;

    _dialogBoxShower.Create()
      .SetMessage(_loc.T(NoSaveReferenceMessageLocKey))
      .SetConfirmButton(() => { })
      .Show();
    return false;
  }
}

/// <summary>
/// Injects the services required by <see cref="GameOptionsBoxPatch"/> into the static patch class.
/// </summary>
public class GameOptionsBoxPatchInitializer : ILoadableSingleton {
  public GameOptionsBoxPatchInitializer(ILoc loc, GameLoader gameLoader, GameSaver gameSaver, DialogBoxShower dialogBoxShower) {
    GameOptionsBoxPatch.SetServices(loc, gameLoader, gameSaver, dialogBoxShower);
  }

  public void Load() {
  }
}
