using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using PluginUtilities;
using UnityEngine;

namespace IncreaseDiceLimit
{
    [BepInPlugin(Guid, "Increase Dice Limit Plugin", Version)]
    [BepInDependency(SetInjectionFlag.Guid)]
    public class IncreaseDiceLimitPlugin : DependencyUnityPlugin<IncreaseDiceLimitPlugin>
    {
        // constants
        public const string Guid = "org.hollofox.plugins.IncreaseLimit";
        internal const string Version = "0.0.0.0";

        // Config
        private static ConfigEntry<int> _diceLimit { get; set; }

        internal static int DiceLimit
        {
            get => _diceLimit.Value; 
            set => _diceLimit.Value = value;
        }

        Harmony harmony;

        protected override void OnSetupConfig(ConfigFile config)
        {
            _diceLimit = config.Bind("Limits", "Dice", 400);
        }

        /// <summary>
        /// Awake plugin
        /// </summary>
        protected override void OnAwake()
        {
            Debug.Log("Increase Dice Limit loaded");
            
            harmony = new Harmony(Guid);
            harmony.PatchAll();
        }

        protected override void OnDestroyed()
        {
            harmony?.UnpatchSelf();
        }
    }
}
