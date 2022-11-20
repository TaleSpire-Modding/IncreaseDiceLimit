using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;

namespace IncreaseDiceLimit.Patches
{
    [HarmonyPatch(typeof(UIAddDiceButton), "PointerDown")]
    public class AddDiceDownPatch
    {
        private const BindingFlags Flags = 
            BindingFlags.Static | BindingFlags.Public | 
            BindingFlags.GetField | BindingFlags.NonPublic |
            BindingFlags.GetProperty | BindingFlags.Instance;

        static bool Prefix(
            BaseEventData data,
            ref UIAddDiceButton __instance
            )
        {
            var count = typeof(UIAddDiceButton).GetField("_count", Flags)
                    .GetValue(__instance);
            var number = (int) count.GetType().GetField("Count",Flags).GetValue(count);
            if (Input.GetMouseButton(0))
            {
                if (Input.GetKey(KeyCode.LeftControl))
                    number += 100;
                else if (Input.GetKey(KeyCode.LeftAlt))
                    number += 10;
                else
                    number++;
            }
            else if (Input.GetMouseButton(1))
            {
                if (Input.GetKey(KeyCode.LeftControl))
                    number -= 100;
                else if (Input.GetKey(KeyCode.LeftAlt))
                    number -= 10;
                else
                    number--;
            }
            number = Math.Clamp(number, 0, IncreaseDiceLimitPlugin.DiceLimit);

            var SetNumberMethodInfo = typeof(UIAddDiceButton).GetMethod("SetNumber", Flags);
            
            SetNumberMethodInfo.Invoke(__instance, new object[] {number});
            
            if (__instance.m_SendDiceData != null)
            {
                var resourceName = 
                        (string) typeof(UIAddDiceButton).GetField("_resourceName", Flags)
                            .GetValue(__instance);
                __instance.m_SendDiceData.Invoke(resourceName, number, -1);
                
            }
            data.Use();
            return false;
        }
    }
}
