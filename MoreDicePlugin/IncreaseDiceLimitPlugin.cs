using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using ModdingTales;
using UnityEngine;

namespace IncreaseDiceLimit
{
    [BepInPlugin(Guid, "Increase Dice Limit Plugin", Version)]
    public class IncreaseDiceLimitPlugin : BaseUnityPlugin
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

        /// <summary>
        /// Awake plugin
        /// </summary>
        void Awake()
        {
            Debug.Log("Increase Dice Limit loaded");
            _diceLimit = Config.Bind("Limits", "Dice", 400);

            ModdingUtils.Initialize(this, Logger, "HolloFoxes'");
            var harmony = new Harmony(Guid);
            harmony.PatchAll();
        }
    }
}
