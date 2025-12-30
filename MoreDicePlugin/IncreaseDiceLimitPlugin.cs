using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using PluginUtilities;
using UnityEngine;

namespace IncreaseDiceLimit
{
    [BepInPlugin(Guid, "Increase Dice Limit Plugin", Version)]
    [BepInDependency(SetInjectionFlag.Guid)]
    public class IncreaseDiceLimitPlugin : DependencyUnityPlugin
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

        /// <summary>
        /// Awake plugin
        /// </summary>
        protected override void OnAwake()
        {
            Debug.Log("Increase Dice Limit loaded");
            _diceLimit = Config.Bind("Limits", "Dice", 400);

            harmony = new Harmony(Guid);
            harmony.PatchAll();
        }

        protected override void OnDestroyed()
        {
            harmony.UnpatchSelf();
        }
    }
}
