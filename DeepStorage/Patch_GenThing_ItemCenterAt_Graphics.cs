using HarmonyLib;
using RimWorld;
using System.Linq;
using UnityEngine;
using Verse;

namespace LWM.DeepStorage
{
    [HarmonyPatch(typeof(GenThing), "ItemCenterAt")]
    public static class Patch_GenThing_ItemCenterAt
    {
        public static bool Prefix(Thing thing, ref Vector3 __result)
        {
            var map = thing.Map;
            if (map == null || thing.stackCount <= 1)
            {
                __result = thing.Position.ToVector3Shifted();
                return false; // skip original
            }

            var cellThings = map.thingGrid.ThingsListAt(thing.Position);
            var storageBuilding = cellThings.FirstOrDefault(t => t is Building_Storage);
            if (storageBuilding == null)
            {
                __result = thing.Position.ToVector3Shifted();
                return false;
            }

            var sameDefThings = cellThings.Where(t => t.def == thing.def).ToList();
            if (sameDefThings.Count <= 1)
            {
                __result = thing.Position.ToVector3Shifted();
                return false;
            }

            int indexInStack = sameDefThings.IndexOf(thing);
            float offsetX = (0.22f / (sameDefThings.Count - 1)) * indexInStack;
            float offsetZ = (0.48f / (sameDefThings.Count - 1)) * indexInStack;

            var pos = thing.Position.ToVector3Shifted();
            __result = new Vector3(pos.x + offsetX, pos.y, pos.z + offsetZ);

            // Optional debug
            // Log.Message($"[DeepStorage] Adjusted position for {thing.LabelCap} at stack {indexInStack + 1}/{sameDefThings.Count} to {__result}");

            return false;
        }
    }
}
