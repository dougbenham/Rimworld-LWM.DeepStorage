using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace LWM.DeepStorage
{
    [StaticConstructorOnStartup]
    public class ITab_DeepStorage_Inventory : ITab
    {
        static ITab_DeepStorage_Inventory()
        {
            Drop = (Texture2D)AccessTools.Field(AccessTools.TypeByName("Verse.TexButton"), "Drop").GetValue(null);
        }

        private static Texture2D Drop;
        private Vector2 scrollPosition = Vector2.zero;
        private float scrollViewHeight = 1000f;
        private Building_Storage buildingStorage;
        private float ambientTemp = 21f;
        public static float tempForHowLongWillLast = 21f;

        private const float TopPadding = 20f;
        public static readonly Color ThingLabelColor = new Color(0.9f, 0.9f, 0.9f, 1f);
        public static readonly Color HighlightColor = new Color(0.5f, 0.5f, 0.5f, 1f);
        public static readonly Color ColdColor = new Color(0.4f, 0.6f, 0.75f);
        public static readonly Color RottenColor = new Color32(214, 90, 24, byte.MaxValue);

        public ITab_DeepStorage_Inventory()
        {
            this.size = new Vector2(460f, 450f);
            this.labelKey = "Contents";
        }

        protected override void FillTab()
        {
            buildingStorage = this.SelThing as Building_Storage;
            if (buildingStorage == null) return;

            Text.Font = GameFont.Small;
            Rect frame = new Rect(10f, 10f, this.size.x - 10, this.size.y - 10);
            GUI.BeginGroup(frame);
            Text.Font = GameFont.Small;
            GUI.color = Color.white;

            float curY = 0f;
            Widgets.ListSeparator(ref curY, frame.width, labelKey.Translate());
            curY += 5f;

            string header, headerTooltip;
            CompDeepStorage cds = buildingStorage.GetComp<CompDeepStorage>();
            List<Thing> storedItems = (cds != null)
                ? cds.GetContentsHeader(out header, out headerTooltip)
                : ITab_Inventory_HeaderUtil.GenericContentsHeader(buildingStorage, out header, out headerTooltip);

            Rect tmpRect = new Rect(8f, curY, frame.width - 16, Text.CalcHeight(header, frame.width - 16));
            Widgets.Label(tmpRect, header);
            curY += tmpRect.height;

            storedItems = storedItems
                .OrderBy(x => x.def.defName)
                .ThenByDescending(x => x.TryGetQuality(out var q) ? (int)q : 0)
                .ThenByDescending(x => (x.HitPoints / (float)x.MaxHitPoints)).ToList();

            Rect outRect = new Rect(0f, 10f + curY, frame.width, frame.height - curY);
            Rect viewRect = new Rect(0f, 0f, frame.width - 16f, this.scrollViewHeight);

            try
            {
                Widgets.BeginScrollView(outRect, ref this.scrollPosition, viewRect, true);
                curY = 0f;

                if (storedItems.Count < 1)
                {
                    Widgets.Label(viewRect, "NoItemsAreStoredHere".Translate());
                    curY += 22;
                }
                else
                {
                    ambientTemp = buildingStorage.AmbientTemperature;
                    foreach (var thing in storedItems)
                    {
                        this.DrawThingRow(ref curY, viewRect.width, thing);
                    }
                }

                if (Event.current.type == EventType.Layout)
                {
                    this.scrollViewHeight = curY + 25f;
                }
            }
            finally
            {
                Widgets.EndScrollView();
            }

            GUI.EndGroup();
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
        }

        private void DrawThingRow(ref float y, float width, Thing thing)
        {
            float originalWidth = width;

            width -= 24f;
            Widgets.InfoCardButton(width, y, thing);

            width -= 24f;
            Rect forbidRect = new Rect(width, y, 24f, 24f);
            bool allowFlag = !thing.IsForbidden(Faction.OfPlayer);
            bool tmpFlag = allowFlag;
            TooltipHandler.TipRegion(forbidRect, allowFlag ? "CommandNotForbiddenDesc".Translate() : "CommandForbiddenDesc".Translate());
            Widgets.Checkbox(forbidRect.x, forbidRect.y, ref allowFlag, 24f, false, true);
            if (allowFlag != tmpFlag)
                ForbidUtility.SetForbidden(thing, !allowFlag, false);

            if (Settings.useEjectButton)
            {
                width -= 24f;
                Rect ejectRect = new Rect(width, y, 24f, 24f);
                TooltipHandler.TipRegion(ejectRect, "LWM.ContentsDropDesc".Translate());
                if (Widgets.ButtonImage(ejectRect, Drop, Color.gray, Color.white, false))
                    EjectTarget(thing);
            }

            width -= 60f;
            Rect massRect = new Rect(width, y, 60f, 28f);
            RimWorld.Planet.CaravanThingsTabUtility.DrawMass(thing, massRect);

            var compRottable = thing.TryGetComp<CompRottable>();
            if (compRottable != null)
                DrawRotLabel(ref width, y, compRottable);

            Rect itemRect = new Rect(0f, y, width, 28f);
            if (Mouse.IsOver(itemRect))
            {
                GUI.color = ITab_Pawn_Gear.HighlightColor;
                GUI.DrawTexture(itemRect, TexUI.HighlightTex);
            }
            if (thing.def.DrawMatSingle != null && thing.def.DrawMatSingle.mainTexture != null)
            {
                Widgets.ThingIcon(new Rect(4f, y, 28f, 28f), thing, 1f);
            }

            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = ITab_Pawn_Gear.ThingLabelColor;
            Rect textRect = new Rect(36f, y, itemRect.width - 36f, itemRect.height);
            string label = thing.LabelCap;
            Text.WordWrap = false;
            Widgets.Label(textRect, label.Truncate(textRect.width));
            if (Widgets.ButtonInvisible(itemRect))
            {
                Find.Selector.ClearSelection();
                Find.Selector.Select(thing);
            }
            Text.WordWrap = true;

            string tooltip = thing.DescriptionDetailed;
            if (thing.def.useHitPoints)
            {
                tooltip += $"\n{thing.HitPoints} / {thing.MaxHitPoints}";
            }
            TooltipHandler.TipRegion(itemRect, tooltip);
            y += 28f;
        }

        private void DrawRotLabel(ref float width, float y, CompRottable compRottable)
        {
            Rect rotRect;
            Text.Anchor = TextAnchor.MiddleLeft;
            if (compRottable.Stage != RotStage.Fresh)
            {
                string rotten = "LWM.AlreadyRottenShort".Translate();
                var textSize = Text.CalcSize(rotten);
                width -= textSize.x;
                rotRect = new Rect(width, y, textSize.x, textSize.y);
                GUI.color = RottenColor;
                Widgets.Label(rotRect, rotten);
                TooltipHandler.TipRegionByKey(rotRect, "LWM.AlreadyRottenDesc");
            }
            else
            {
                float daysAt21 = (ambientTemp <= tempForHowLongWillLast)
                    ? Math.Min(int.MaxValue, compRottable.TicksUntilRotAtTemp(tempForHowLongWillLast)) / 60000f
                    : -1;

                float rotInDays = Math.Min(int.MaxValue, compRottable.TicksUntilRotAtTemp(ambientTemp)) / 60000f;
                bool longTime = rotInDays >= 99 && daysAt21 >= 99;

                width -= longTime ? 30f : 42f;
                rotRect = new Rect(width, y, longTime ? 28f : 40f, 28f);

                if (longTime)
                {
                    GUI.color = TransferableOneWayWidget.ItemMassColor;
                    Widgets.Label(rotRect, "--");
                    TooltipHandler.TipRegion(rotRect, "LWM.DaysUntilWillNotRotSoonDesc"
                        .Translate(ambientTemp.ToStringTemperature("F0")));
                }
                else
                {
                    GUI.color = (ambientTemp <= 0f) ? ColdColor : Color.yellow;
                    Widgets.Label(rotRect, rotInDays.ToString("0.#"));
                    TooltipHandler.TipRegion(rotRect, "DaysUntilRotTip".Translate());
                }
            }
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
        }

        public static void EjectTarget(Thing thing)
        {
            IntVec3 loc = thing.Position;
            Map map = thing.Map;
            thing.DeSpawn();
            if (!GenPlace.TryPlaceThing(thing, loc, map, ThingPlaceMode.Near, null,
                c => !map.thingGrid.ThingsListAtFast(c).Any(t => t is Building_Storage)))
            {
                GenSpawn.Spawn(thing, loc, map);
            }
            if (!thing.Spawned || thing.Position == loc)
            {
                Messages.Message("You have filled the map.",
                    new LookTargets(loc, map), MessageTypeDefOf.NegativeEvent);
            }
        }
    }
}
