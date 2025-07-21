using System;
using Verse;
using RimWorld;
using HarmonyLib;

namespace LWM.DeepStorage
{
    [StaticConstructorOnStartup]
    public static class ModInit
    {
        static ModInit()
        {
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
