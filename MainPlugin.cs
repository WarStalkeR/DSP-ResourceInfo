using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace ResourceInfo
{
    [BepInPlugin(ModGuid, ModName, ModVersion)]
    [BepInProcess("DSPGAME.exe")]
    public class MainPlugin : BaseUnityPlugin
    {
        public const string ModGuid = "WarStalkeR.DSP.plugin.ResourceInfo";
        public const string ModName = "ResourceInfo";
        public const string ModVersion = "2.0.0";

        public static Dictionary<int, double> SpeedAssembler =
            new Dictionary<int, double>()
            {
                { 1, 0.75 },
                { 2, 1.00 },
                { 3, 1.50 },
                { 4, 3.00 }
            };
        public static Dictionary<int, double> SpeedSmelter =
            new Dictionary<int, double>()
            {
                { 1, 1.00 },
                { 2, 2.00 },
                { 3, 3.00 }
            };
        public static Dictionary<int, double> SpeedChemical =
            new Dictionary<int, double>()
            {
                { 1, 1.00 },
                { 2, 2.00 }
            };
        public static Dictionary<int, double> SpeedLaboratory =
            new Dictionary<int, double>()
            {
                { 1, 1.00 },
                { 2, 3.00 }
            };
        public static ConfigEntry<KeyCode> HotkeyPerMinute { get; set; }
        public static ConfigEntry<KeyCode> HotkeyBeltSpeeds { get; set; }
        public static ConfigEntry<KeyCode> HotkeyRelatedComponents { get; set; }
        public static ConfigEntry<KeyCode> HotkeyRelatedBuildings { get; set; }
        public static ConfigEntry<bool> DefaultBeltsToMinutes { get; set; }
        public static ConfigEntry<bool> IgnoreResearchUnlock { get; set; }
        public static ConfigEntry<bool> AllowCombinedRecipes { get; set; }

        public void Awake()
        {
            LoadConfig();

            var harmony = new Harmony("WarStalkeR.DSP.plugin.ResourceInfo");
            harmony.PatchAll(typeof(Patch_UIItemTip_SetTip));
            harmony.PatchAll(typeof(Patch_UIItemTip_OnDisable));
        }

        private void LoadConfig()
        {
            HotkeyPerMinute = Config.Bind<KeyCode>("Hotkeys", "Info_Per_Minute", KeyCode.LeftShift, "Hotkey that triggers appearance of per minute production for recipes in tooltip.");
            HotkeyBeltSpeeds = Config.Bind<KeyCode>("Hotkeys", "Info_Belt_Speeds", KeyCode.LeftShift, "Hotkey that triggers appearance of belt speeds and sorter cycles per minute.");
            HotkeyRelatedComponents = Config.Bind<KeyCode>("Hotkeys", "Info_Related_Components", KeyCode.LeftControl, "Hotkey that triggers appearance of all item related component recipes in a tooltip.");
            HotkeyRelatedBuildings = Config.Bind<KeyCode>("Hotkeys", "Info_Related_Buildings", KeyCode.LeftAlt, "Hotkey that triggers appearance of all item related building recipes in a tooltip.");
            DefaultBeltsToMinutes = Config.Bind<bool>("Settings", "Default_Belts_ToMinutes", true, "Set belts and sorters to show speed and cycles in minutes, except when hotkey is pressed.");
            IgnoreResearchUnlock = Config.Bind<bool>("Settings", "Ignore_Research_Unlock", false, "Show related recipes, even if prerequisite technology isn't researched.");
            AllowCombinedRecipes = Config.Bind<bool>("Settings", "Allow_Combined_Recipes", true, "Allow to use both recipe hotkeys at same time to show all recipes in same list.");
        }
    }
}
