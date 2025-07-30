using System;
using RimWorld;
using Verse;
using HarmonyLib;

namespace LWM.DeepStorage
{
    [HarmonyPatch(typeof(RimWorld.Frame), "CompleteConstruction")]
    public static class Patch_Frame_CompleteConstruction
    {
        public static CompDeepStorage compDS;

        static void Prefix(Frame __instance)
        {
            compDS = null;
            if (__instance == null)
            {
                Log.Error("DeepStorage: null Frame instance in CompleteConstruction");
                return;
            }
            if (__instance.Map == null)
            {
                // Log.Error("DeepStorage: null Map in Frame " + __instance);
                return;
            }

            // Only access Map if it's not null (fixed for Replace Stuff)
            var mc = __instance.Map.GetComponent<MapComponentDS>();
            if (mc != null)
            {
                mc.settingsForBlueprintsAndFrames.Remove(__instance, out compDS);
            }
        }

        static void Postfix(Frame __instance)
        {
            if (compDS == null || __instance?.Map == null)
                return;

            foreach (var thing in __instance.Position.GetThingList(__instance.Map))
            {
                if (thing is Building_Storage storage)
                {
                    Log.Message($"DeepStorage: transferring compDS settings from Frame to new {storage}");
                    storage.TryGetComp<CompDeepStorage>()?.CopySettingsFrom(compDS);
                    break;
                }
            }
        }
    }
}
