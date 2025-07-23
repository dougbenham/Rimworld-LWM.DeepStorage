using System;
using RimWorld;
using Verse;
using HarmonyLib;
using System.Reflection;
using System.Reflection.Emit;
using System.Collections.Generic;
using System.Linq;

namespace LWM.DeepStorage
{
    /***************************************************
 * Patch Building_Storage's GetGizmos()
 *
 * Problem: If you have 345435 items in storage, your screen will fill up
 *   with those cute little bracket markers "Select stored item" thingys.
 *   Which is not great.  They are fantastic gizmos, but for enthusiastic
 *   hoarders, they are a problem.
 *
 * Goal: Patch the Gizmos to only show, say 10 or something.
 *
 * Complication: GetGizmos is an IEnumerable. Patching them sucks...
 *
 * Realistic Goal: only show those Gizmos if there are less than, say, 10
 *   Heck, it's easy enough to make it a mod setting.  Only show if there
 *   are less than 5, 10, 0, etc.  0 can be "don't show them at all" - an
 *   easy first step!
 *
 * Solution: Transpile.  The code to produce "select stored item" gizmos
 *   is inside an `if (Find.Selector.NumSelected == 1)` block.  And that
 *   `NumSelected`? That is in ONE PLACE in the code and is easy to find
 *   AND has a branch to skip those gizmos!
 *
 */
    public static class Patch_Building_Storage_Gizmos
    {
        // -1 for "don't change anything." 0 for "don't display anything"
        // X for "show if <= X items in this storage building"
        public static int cutoffBuildingStorageGizmos = cutoffDefault;
        public const int cutoffDefault = 12;

        public static bool Prepare(Harmony instance)
        {
            try
            {
                var nested = typeof(Building_Storage)
                    .GetNestedTypes(AccessTools.all)
                    .FirstOrDefault(t => typeof(IEnumerator<Gizmo>).IsAssignableFrom(t));

                if (nested == null)
                {
                    Log.Warning("LWM.DeepStorage: No nested IEnumerator<Gizmo> type found -> skipping Gizmo patch.");
                    return false;
                }

                var method = nested.GetMethod("MoveNext", AccessTools.all);
                if (method == null)
                {
                    Log.Warning("LWM.DeepStorage: Nested IEnumerator<Gizmo> has no MoveNext method -> skipping Gizmo patch.");
                    return false;
                }
            }
            catch (Exception e)
            {
                Log.Warning("LWM.DeepStorage: Error locating GetGizmos MoveNext method -> skipping Gizmo patch: " + e);
                return false;
            }

            return cutoffBuildingStorageGizmos >= 0;
        }


        public static MethodBase TargetMethod()
        {
            var nested = typeof(Building_Storage).GetNestedTypes(AccessTools.all)
                .FirstOrDefault(t => typeof(IEnumerator<Gizmo>).IsAssignableFrom(t));

            if (nested == null)
            {
                Log.Error("LWM.DeepStorage: Failed to find nested IEnumerator<Gizmo> for GetGizmos.");
                return null;
            }

            var method = nested.GetMethod("MoveNext", AccessTools.all);
            if (method == null)
            {
                Log.Error("LWM.DeepStorage: Failed to find MoveNext in nested type.");
            }

            return method;
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructionsEnumerable)
        {
            var get_Selector = typeof(Verse.Find).GetMethod("get_Selector");
            var get_NumSelected = typeof(RimWorld.Selector).GetMethod("get_NumSelected");
            Label skipGizmosLabel;
            bool found1 = false;
            var instructions = instructionsEnumerable.ToList();

            for (int i = 0; i < instructions.Count; i++)
            {
                if (instructions[i].opcode == OpCodes.Call
                    && instructions[i].OperandIs(get_Selector)
                    && instructions[i + 1].opcode == OpCodes.Callvirt
                    && instructions[i + 1].OperandIs(get_NumSelected))
                {
                    skipGizmosLabel = (Label)instructions[i + 3].operand;
                    if (skipGizmosLabel == null)
                    {
                        Log.Error("Transpiler failed to find label");
                        yield return instructions[i];
                        continue;
                    }

                    found1 = true;
                    if (cutoffBuildingStorageGizmos < 1)
                    {
                        yield return new CodeInstruction(OpCodes.Br, skipGizmosLabel);
                        i = i + 3;
                        continue;
                    }

                    yield return instructions[i];
                    i++;
                    yield return instructions[i];
                    i++;
                    yield return instructions[i];
                    i++;
                    yield return instructions[i];

                    yield return new CodeInstruction(OpCodes.Ldloc_2);
                    yield return new CodeInstruction(OpCodes.Ldfld, typeof(Building_Storage)
                        .GetField("slotGroup", AccessTools.all));
                    yield return new CodeInstruction(OpCodes.Callvirt, typeof(SlotGroup)
                        .GetMethod("get_HeldThings", AccessTools.all));
                    yield return new CodeInstruction(OpCodes.Call,
                        typeof(Patch_Building_Storage_Gizmos).GetMethod("IsOverThreshold", AccessTools.all));
                    yield return new CodeInstruction(OpCodes.Brtrue, skipGizmosLabel);
                }
                else
                {
                    yield return instructions[i];
                }
            }

            if (!found1)
                Log.Warning("LWM.DeepStorage: failed to Transpile Gizmos");

            yield break;
        }

        public static bool IsOverThreshold(IEnumerable<Thing> things)
        {
            var thingsEnumerator = things.GetEnumerator();
            for (int i = 0; i <= cutoffBuildingStorageGizmos; i++)
            {
                if (thingsEnumerator.MoveNext())
                    continue;
                return false;
            }
            return true;
        }

        public static bool IsOverThresholdX(IEnumerator<Thing> thingsEnumerator)
        {
            for (int i = 0; i <= cutoffBuildingStorageGizmos; i++)
            {
                if (thingsEnumerator.MoveNext())
                    continue;
                return false;
            }
            return true;
        }
    }
}
