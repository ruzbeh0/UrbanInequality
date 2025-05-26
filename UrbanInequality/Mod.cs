using Colossal.IO.AssetDatabase;
using Colossal.Logging;
using Colossal.PSI.Environment;
using Game;
using Game.Input;
using Game.Modding;
using Game.Policies;
using Game.SceneFlow;
using Game.Settings;
using Game.Simulation;
using System.IO;
using Unity.Entities;
using UnityEngine;
using UrbanInequality.Systems;

namespace UrbanInequality
{
    public class Mod : IMod
    {
        public static ILog log = LogManager.GetLogger($"{nameof(UrbanInequality)}.{nameof(Mod)}").SetShowsErrorsInUI(false);
        public static UrbanInequality.Setting m_Setting;

        // Mods Settings Folder
        public static string SettingsFolder = Path.Combine(EnvPath.kUserDataPath, "ModsSettings", nameof(UrbanInequality));

        public void OnLoad(UpdateSystem updateSystem)
        {
            log.Info(nameof(OnLoad));

            if (!Directory.Exists(SettingsFolder))
            {
                Directory.CreateDirectory(SettingsFolder);
            }

            if (GameManager.instance.modManager.TryGetExecutableAsset(this, out var asset))
                log.Info($"Current mod asset at {asset.path}");

            m_Setting = new Setting(this);
            m_Setting.RegisterInOptionsUI();
            GameManager.instance.localizationManager.AddSource("en-US", new UrbanInequality.Setting.LocaleEN(m_Setting));

            AssetDatabase.global.LoadSettings(nameof(UrbanInequality), m_Setting, new UrbanInequality.Setting(this));

            // Disable original systems
            World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<Game.Simulation.BuildingUpkeepSystem>().Enabled = false;

            updateSystem.UpdateAt<LevelCapSystem>(SystemUpdatePhase.GameSimulation);
            updateSystem.UpdateAt<UrbanInequalityBuildingUpkeepSystem>(SystemUpdatePhase.GameSimulation);
        }

        public void OnDispose()
        {
            log.Info(nameof(OnDispose));
        }
    }
}
