using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using UnityEngine;
using static LWM.DeepStorage.Utils.DBF; // trace utils

namespace LWM.DeepStorage
{
    public class CompDeepStorage : ThingComp, IExposable, IHoldMultipleThings.IHoldMultipleThings
    {
        ////////////////////////////////
        /////// GIZMOS
        ////////////////////////////////

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo g in base.CompGetGizmosExtra())
                yield return g;

            foreach (Gizmo g in DSStorageGroupUtility.GetDSStorageGizmos())
                yield return g;

#if DEBUG
            yield return new Command_Action
            {
                defaultLabel = "Clear DS Cache",
                action = delegate ()
                {
                    foreach (var c in parent.OccupiedRect().Cells)
                    {
                        Log.Warning("Dirtying cache for " + c);
                        parent.Map.GetComponent<MapComponentDS>().DirtyCache(c);
                    }
                }
            };
            yield return new Command_Action
            {
                defaultLabel = "Recalculate Cache",
                action = delegate ()
                {
                    foreach (var c in parent.OccupiedRect().Cells)
                    {
                        Log.Warning("Updating cache for " + c);
                        parent.Map.GetComponent<MapComponentDS>().UpdateCache(c, this);
                    }
                }
            };
            yield return new Command_Action
            {
                defaultLabel = "Items in Region",
                action = delegate ()
                {
                    Log.Warning("ListerThings for " + parent + " (at region at position " + parent.Position + ")");
                    foreach (var t in parent.Position.GetRegion(parent.Map).ListerThings
                             .ThingsMatching(ThingRequest.ForGroup(ThingRequestGroup.HaulableEver)))
                    {
                        Log.Message("  " + t);
                    }
                }
            };
#endif
        }

        ////////////////////////////////
        /////// INITIALIZATION
        ////////////////////////////////

        public override void Initialize(CompProperties props)
        {
            base.Initialize(props);

            if (((Properties)props).altStat != null)
                stat = ((Properties)props).altStat;
            if (((Properties)props).maxTotalMass > 0f)
                limitingTotalFactorForCell = ((Properties)props).maxTotalMass + .0001f;
            if (((Properties)props).maxMassOfStoredItem > 0f)
                limitingFactorForItem = ((Properties)props).maxMassOfStoredItem + .0001f;
        }

        ////////////////////////////////
        /////// MAP CACHE
        ////////////////////////////////

        public void DirtyMapCache()
        {
            if (parent is Building_Storage && parent.Spawned)
            {
                MapComponentDS dsm = parent.Map.GetComponent<MapComponentDS>();
                foreach (var cell in (parent as Building_Storage).AllSlotCells())
                    dsm.DirtyCache(cell);
            }
        }

        ////////////////////////////////
        /////// CAPACITY LOGIC
        ////////////////////////////////

        public virtual int TimeStoringTakes(Map map, IntVec3 cell, Pawn pawn)
        {
            if (CdsProps.minTimeStoringTakes < 0)
                return CdsProps.timeStoringTakes;

            Thing thing = pawn?.carryTracker?.CarriedThing;
            if (thing == null)
            {
                Log.Error("LWM.DeepStorage: null CarriedThing");
                return 0;
            }

            int t = CdsProps.minTimeStoringTakes;
            var l = map.thingGrid.ThingsListAtFast(cell).FindAll(x => x.def.EverStorable(false));

            bool thingToPlaceIsDifferent = l.Count > 0;

            for (int i = 0; i < l.Count; i++)
            {
                t += CdsProps.additionalTimeEachStack;
                if (CdsProps.additionalTimeEachDef > 0 &&
                    l[i].CanStackWith(thing))
                {
                    thingToPlaceIsDifferent = false;
                }
            }

            if (CdsProps.additionalTimeEachDef > 0)
            {
                if (thingToPlaceIsDifferent)
                    t += CdsProps.additionalTimeEachDef;

                List<Thing> l2 = new List<Thing>(l);
                int i = 0;
                for (; i < l2.Count; i++)
                {
                    int j = i + 1;
                    while (j < l2.Count)
                    {
                        if (l2[i].CanStackWith(l2[j]))
                            l2.RemoveAt(j);
                        else
                            j++;
                    }
                }
                if (l2.Count > 1)
                    t += (CdsProps.additionalTimeEachDef * (l2.Count - 1));
            }

            if (Settings.storingTimeConsidersStackSize && CdsProps.additionalTimeStackSize > 0f)
            {
                float factor = 1f;
                if (thing.def.smallVolume ||
                    (!CdsProps.quickStoringItems.NullOrEmpty() && CdsProps.quickStoringItems.Contains(thing.def)))
                {
                    factor = 0.05f;
                }
                t += (int)(CdsProps.additionalTimeStackSize *
                    pawn.carryTracker.CarriedThing.stackCount *
                    factor);
            }
            return t;
        }

        public virtual int CapacityToStoreThingAt(Thing thing, Map map, IntVec3 cell)
        {
            return map.GetComponent<MapComponentDS>().CapacityToStoreItemAt(this, thing, cell);
        }

        public virtual int CapacityToStoreThingAtDirect(Thing thing, Map map, IntVec3 cell)
        {
            Utils.Warn(CheckCapacity, $"Checking Capacity to store {thing.stackCount}x {thing} at {(map?.ToString() ?? "NULL MAP")} {cell}");
            int capacity = 0;

            if (limitingFactorForItem > 0f &&
                thing.GetStatValue(stat) > limitingFactorForItem)
            {
                Utils.Warn(CheckCapacity, $"Cannot store: {stat} of {thing.GetStatValue(stat)} > limit {limitingFactorForItem}");
                return 0;
            }

            float totalWeight = 0f;
            int stacksHere = 0;
            var list = map.thingGrid.ThingsListAt(cell);

            foreach (var thingInCell in list)
            {
                if (thingInCell.def.EverStorable(false))
                {
                    stacksHere++;
                    if (limitingTotalFactorForCell > 0f)
                    {
                        totalWeight += thingInCell.GetStatValue(stat) * thingInCell.stackCount;
                        if (totalWeight > limitingTotalFactorForCell && stacksHere >= MinNumberStacks)
                            return 0;
                    }
                    if (thingInCell == thing)
                    {
                        if (stacksHere > MaxNumberStacks)
                            return 0;
                        return thing.stackCount;
                    }
                    if (thingInCell.CanStackWith(thing) && thingInCell.stackCount < thingInCell.def.stackLimit)
                        capacity += thingInCell.def.stackLimit - thingInCell.stackCount;
                }
            }

            if (limitingTotalFactorForCell > 0f)
            {
                if (stacksHere <= MinNumberStacks)
                {
                    capacity += (MinNumberStacks - stacksHere) * thing.def.stackLimit;
                    totalWeight += (MinNumberStacks - stacksHere) * thing.GetStatValue(stat) * thing.def.stackLimit;
                    stacksHere = MinNumberStacks;
                }
                float remainingWeight = limitingTotalFactorForCell - totalWeight;
                if (remainingWeight <= 0f)
                    return stacksHere > MinNumberStacks ? 0 : capacity;

                if (stacksHere < MaxNumberStacks)
                    capacity += Math.Min(
                        (MaxNumberStacks - stacksHere) * thing.def.stackLimit,
                        (int)(remainingWeight / thing.GetStatValue(stat)));
                return capacity;
            }

            if (MaxNumberStacks > stacksHere)
                capacity += (MaxNumberStacks - stacksHere) * thing.def.stackLimit;

            return capacity;
        }

        ////////////////////////////////
        /////// MULTIPLE THINGS INTERFACE
        ////////////////////////////////

        public bool CapacityAt(Thing thing, IntVec3 cell, Map map, out int capacity)
        {
            capacity = map.GetComponent<MapComponentDS>().CapacityToStoreItemAt(this, thing, cell);
            return capacity > 0;
        }

        public bool StackableAt(Thing thing, IntVec3 cell, Map map)
        {
            return map.GetComponent<MapComponentDS>().CanStoreItemAt(this, thing, cell);
        }

        ////////////////////////////////
        /////// SAVE / LOAD
        ////////////////////////////////

        public override void PostExposeData()
        {
            Scribe_Values.Look(ref maxNumberStacks, "LWM_DS_DSU_maxNumberStacks", null, false);
        }

        public void ExposeData()
        {
            PostExposeData();
            Scribe_References.Look(ref parent, "LWM_DS_Comp_Parent");

            string dn = null;
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                dn = CdsProps?.parent.defName;
                Scribe_Values.Look(ref dn, "LWM_DS_Comp_CdsPropDefName");
            }
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                Scribe_Values.Look(ref dn, "LWM_DS_Comp_CdsPropDefName");
                this.props = DefDatabase<ThingDef>.GetNamed(dn).GetCompProperties<DeepStorage.Properties>();
            }
        }


        ////////////////////////////////
        /////// SETTINGS & PROPS
        ////////////////////////////////

        public Properties CdsProps => (Properties)this.props;
        public int MinNumberStacks => CdsProps.minNumberStacks;

        public int MaxNumberStacks
        {
            get => maxNumberStacks ?? CdsProps.maxNumberStacks;
            set
            {
                int? newValue = (value == CdsProps.maxNumberStacks ? (int?)null : (int?)value);
                if (parent is IStorageGroupMember storage && storage.Group != null)
                {
                    foreach (var c in DSStorageGroupUtility.GetDSCompsFromGroup(storage.Group))
                        c.SetMaxNumberStacksDirect(newValue);
                }
                else SetMaxNumberStacksDirect(newValue);
            }
        }

        [Multiplayer.API.SyncMethod]
        public void SetMaxNumberStacksDirect(int? n)
        {
            maxNumberStacks = n;
            DirtyMapCache();
        }

        public virtual bool ShowContents => CdsProps.showContents;

        public void ResetSettings()
        {
            if (parent is IStorageGroupMember storage && storage.Group != null)
            {
                foreach (var c in DSStorageGroupUtility.GetDSCompsFromGroup(storage.Group))
                    c.ResetSettingsDirect();
            }
            else ResetSettingsDirect();
        }

        [Multiplayer.API.SyncMethod]
        public void ResetSettingsDirect()
        {
            maxNumberStacks = null;
            DirtyMapCache();
        }

        public void CopySettingsFrom(CompDeepStorage other)
        {
            SetMaxNumberStacksDirect(other.maxNumberStacks);
        }

        ////////////////////////////////
        /////// FIELDS
        ////////////////////////////////

        private int? maxNumberStacks;
        public StatDef stat = StatDefOf.Mass;
        public float limitingFactorForItem = 0f;
        public float limitingTotalFactorForCell = 0f;
    }
}
