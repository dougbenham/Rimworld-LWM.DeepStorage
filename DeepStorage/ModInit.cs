using System;
using Verse;
using RimWorld;
using HarmonyLib;
using System.Linq;

namespace LWM.DeepStorage
{
    [StaticConstructorOnStartup]
    public static class ModInit
    {
        static ModInit()
        {
            // Check for RimFridge and try to enable ugly stacking appearance for compatibility
            bool rimFridgeActive = ModsConfig.ActiveModsInLoadOrder.Any(m => m.PackageId == "rimfridge.kv.rw");
            if (rimFridgeActive)
            {
                try
                {
                    // Deep Storage's own stacking graphic setting
                    bool useBoringOldStackingGraphic = LWM.DeepStorage.Settings.useBoringOldStackingGraphic;

                    // Try to find RimFridge's Settings type via reflection
                    var rimFridgeSettingsType = GenTypes.AllTypes.FirstOrDefault(t => t.FullName == "RimFridge.Settings");
                    if (rimFridgeSettingsType != null)
                    {
                        // Get the uglyStackAppearance static field
                        var uglyStackField = rimFridgeSettingsType.GetField("uglyStackAppearance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                        if (uglyStackField != null)
                        {
                            // Set the setting to match Deep Storage, only if we need it
                            uglyStackField.SetValue(null, useBoringOldStackingGraphic);
                            Log.Message("LWM.DeepStorage: Set RimFridge uglyStackAppearance = " + useBoringOldStackingGraphic);

                            // Save RimFridge settings so it persists after restart
                            var settingsControllerType = GenTypes.AllTypes.FirstOrDefault(t => t.FullName == "RimFridge.SettingsController");
                            if (settingsControllerType != null)
                            {
                                // Call WriteSettings via any available instance, or static if available
                                var mod = LoadedModManager.ModHandles.FirstOrDefault(m => m.GetType().FullName == "RimFridge.SettingsController");
                                if (mod != null)
                                {
                                    settingsControllerType.GetMethod("WriteSettings").Invoke(mod, null);
                                }
                            }

                            // Show warning to the user ONLY if stacking setting is enabled
                            if (useBoringOldStackingGraphic)
                            {
                                Messages.Message("Automatically enabled 'Ugly stack appearance' in RimFridge for compatibility.", MessageTypeDefOf.CautionInput, false);
                            }
                        }
                    }
                }
                catch (System.Exception)
                {
                    // Suppress harmless reflection errors
                }
            }

            Log.Message("LWM.DeepStorage: 🐾 Init starting for RimWorld 1.5+ build!");

            RemoveAnyMultipleCompProps();
            Log.Message("LWM.DeepStorage: Finished removing multiple comp props.");

            LWM.DeepStorage.Settings.DefsLoaded();
            Log.Message("LWM.DeepStorage: Settings defs loaded.");

            if (ModLister.GetActiveModWithIdentifier("rwmt.Multiplayer") != null)
            {
                Settings.multiplayerIsActive = true;
                Log.Message("LWM.DeepStorage: Multiplayer detected!");
            }
            else
            {
                Log.Message("LWM.DeepStorage: Multiplayer not detected.");
            }

            var harmony = new Harmony("net.littlewhitemouse.LWM.DeepStorage");
            harmony.PatchAll();
            Log.Message("LWM.DeepStorage: ✅ Harmony patches applied.");
        }

        public static void RemoveAnyMultipleCompProps()
        {
            int processed = 0;
            foreach (var d in DefDatabase<ThingDef>.AllDefs)
            {
                if (typeof(Building_Storage).IsAssignableFrom(d.thingClass))
                {
                    var cmps = d.comps;
                    for (int i = cmps.Count - 1; i >= 0; i--)
                    {
                        if (cmps[i] is LWM.DeepStorage.Properties && i > 0)
                        {
                            for (i--; i >= 0; i--)
                            {
                                if (cmps[i] is LWM.DeepStorage.Properties)
                                    cmps.RemoveAt(i);
                            }
                            break;
                        }
                    }
                    processed++;
                }
            }
            Log.Message($"LWM.DeepStorage: Checked {processed} Building_Storage ThingDefs for duplicate comp props.");
        }
    }
}
