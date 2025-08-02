using HarmonyLib;
using Verse;
using Verse.AI;

namespace LWM.DeepStorage
{
	[HarmonyPatch(typeof(Verse.AI.ReservationManager), "Reserve")]
	class Patch_Reservation_Reservation_CompDeepStorage
	{
		static bool Prefix(Pawn claimant, Job job, LocalTargetInfo target, ref bool __result, Map ___map)
		{
			if (target.HasThing == false && ___map != null && target.Cell.InBounds(___map))
			{
				if (target.Cell.GetThingList(___map).Any(t => t is ThingWithComps twc && twc.TryGetComp<CompDeepStorage>() != null))
				{
					__result = true;
					return false;
				}
            }
			return true;
		}
	}
}