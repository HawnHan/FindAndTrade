using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MGAutoSell.Extensions;
using TD_Find_Lib;
using UnityEngine.UIElements;
using UnityEngine;
using Verse;

namespace MGAutoSell
{
    internal static class TabUtility
    {
        private static Color _fadedColor = new(1, 1, 1, 0.4f);
        private static string sellHeader = "MGAutoSell.SellHeader".Translate();

        public static void DrawSellPanel(Rect toSellRect, ItemsToSell sellCache, ref Vector2 sellScroll, WindowTab currentTab = WindowTab.Rules, Pawn sellerOverride = null, Action invalidateCache = null)
        {
            toSellRect.SplitHorizontally(Text.LineHeight, out var itemHeader, out toSellRect);

            Widgets.DrawLightHighlight(itemHeader);
            Widgets.Label(itemHeader.RightPartPixels(itemHeader.width - Text.LineHeight - 10), sellHeader);
            GUI.DrawTexture(itemHeader.RightPartPixels(24f), ThingDefOf.Silver.uiIcon);

            toSellRect.SplitHorizontally(4, out var gapHeader, out toSellRect);
            Widgets.DrawLineHorizontal(gapHeader.x, gapHeader.y, gapHeader.width, _fadedColor);

            int i = -1;
            var anchor = Text.Anchor;
            var totalItems = sellCache.Items.Count + (Mod.Settings.showAllMatchingItems ? sellCache.PotentialItems.Count : 0);
            var viewRect = sellCache.Trader != null ? toSellRect.TopPartPixels(toSellRect.height - Text.LineHeight) : toSellRect;
            var totalHeight = totalItems * Text.LineHeight;
            var shouldScroll = totalHeight > viewRect.height;
            var areWeInSettingsMenu = invalidateCache == null;
            var row = new Rect(0, 0, 0, 0);
            var listing = new Listing_StandardIndent();
            if (shouldScroll)
            {
                if(!areWeInSettingsMenu)
                    viewRect.width += 16;
                listing.BeginScrollView(viewRect, ref sellScroll, viewRect.LeftPartPixels(viewRect.width - 16).TopPartPixels(totalHeight).AtZero());
            }

            var minRenderIndex = shouldScroll ? Math.Floor(sellScroll.y / Text.LineHeight) : 0;
            var maxRenderIndex = shouldScroll ? Math.Ceiling(viewRect.height / Text.LineHeight) + minRenderIndex : 0;
            foreach (var (thingDef, count, (total, totalLabel), (pricePer, pricePerLabel)) in sellCache.Items)
            {
                i++;
                if (shouldScroll)
                {
                    if (i < minRenderIndex || i > maxRenderIndex)
                    {
                        listing.Gap(Text.LineHeight);
                        continue;
                    }
                    row = listing.GetRect(Text.LineHeight);
                }
                else
                    viewRect.SplitHorizontally(Text.LineHeight, out row, out viewRect);

                if (i % 2 == 1)
                    Widgets.DrawLightHighlight(row);
                var color = GUI.color;
                GUI.color = thingDef.uiIconColor;
                GUI.DrawTexture(row.LeftPartPixels(row.height), thingDef.uiIcon);
                GUI.color = color;

                row.x += row.height + 10;
                Widgets.Label(row, thingDef.GetLabel() + $" x{count}");
                row.x -= row.height + 10;

                if (currentTab != WindowTab.Edit)
                {
                    var middle = row.MiddlePartPixels(50, row.height);
                    Text.Anchor = TextAnchor.MiddleCenter;
                    Widgets.Label(middle, pricePerLabel);
                    Text.Anchor = anchor;
                }

                if (!Mod.Settings.LabelSizeCache.TryGetValue(totalLabel, out var size))
                {
                    size = Text.CalcSize(totalLabel);
                    Mod.Settings.LabelSizeCache[totalLabel] = size;
                }
                Widgets.Label(row.RightPartPixels(size.x + 4), totalLabel);
            }

            if (Mod.Settings.showAllMatchingItems)
            {
                foreach (var potentialItem in sellCache.PotentialItems)
                {
                    i++;
                    if (shouldScroll)
                    {
                        if (i < minRenderIndex || i > maxRenderIndex)
                        {
                            listing.Gap(Text.LineHeight);
                            continue;
                        }
                        row = listing.GetRect(Text.LineHeight);
                    }
                    else
                        viewRect.SplitHorizontally(Text.LineHeight, out row, out viewRect);

                    if (i % 2 == 1)
                        Widgets.DrawLightHighlight(row);

                    var color = GUI.color;
                    GUI.color = potentialItem.Item.uiIconColor;
                    GUI.DrawTexture(row.LeftPartPixels(row.height), potentialItem.Item.uiIcon);
                    GUI.color = _fadedColor;

                    row.x += row.height + 10;
                    Widgets.Label(row, potentialItem.Item.GetLabel());
                    row.x -= row.height + 10;

                    if (!Mod.Settings.LabelSizeCache.TryGetValue(potentialItem.Rule, out var size))
                    {
                        size = Text.CalcSize(potentialItem.Rule);
                        Mod.Settings.LabelSizeCache[potentialItem.Rule] = size;
                    }

                    Widgets.Label(row.RightPartPixels(size.x), potentialItem.Rule);
                    GUI.color = color;


                }
            }

            if (shouldScroll)
            {
                var height = 0f;
                listing.EndScrollView(ref height);
            }

            if (sellCache.Trader == null) 
                return;

            var footer = toSellRect.BottomPartPixels(Text.LineHeight);
            Widgets.DrawLightHighlight(footer);

            var iconRect = footer.LeftPartPixels(Text.LineHeight);
            iconRect.y -= 4;
            GUI.DrawTexture(iconRect, sellCache.Trader.Icon.Invoke());

            var sellerLabel = sellCache.Trader.Name + $" ({sellCache.Trader.ImprovementLabel})";
            if (sellerOverride != null)
                sellerLabel = $"<i>{sellerLabel}</i>";

            var sellerLabelWidth = Text.CalcSize(sellerLabel);
            Widgets.Label(footer.RightPartPixels(footer.width - Text.LineHeight), sellerLabel);

            var sellerOverrideRect = footer.LeftPartPixels(iconRect.width + sellerLabelWidth.x + 8);
            Widgets.DrawHighlightIfMouseover(sellerOverrideRect);
            if (Widgets.ButtonInvisible(sellerOverrideRect) && Event.current.button == (int)MouseButton.RightMouse)
            {
                var pawns = GetTraders().Select(x => new FloatMenuOption(
                    x.Name + $" ({x.ImprovementLabel})", () =>
                    {
                        sellerOverride = x.Pawn;
                        invalidateCache?.Invoke();
                    }, x.Pawn, Color.white)).ToList();

                if (sellerOverride != null)
                    pawns.Add(new FloatMenuOption("Auto", () =>
                    {
                        sellerOverride = null;
                        invalidateCache?.Invoke();
                    }));

                Find.WindowStack.Add(new FloatMenu(pawns));
            }

            var footerRow = new WidgetRow(footer.xMax - 4, footer.y, UIDirection.LeftThenDown);
            footerRow.LabelFast(sellCache.TotalSilver.Label);
            footerRow.Icon(ThingDefOf.Silver.uiIcon);
            footerRow.LabelFast("Total:");
        }

        public static List<TraderRecord> GetTraders(bool generatePictures = true)
        {
            var stat = StatDefOf.TradePriceImprovement;
            var freeColonists = Find.CurrentMap?.mapPawns?.FreeColonists ?? [];
            var isIdeologyActive = ModsConfig.IdeologyActive;
            var leader = isIdeologyActive ? Faction.OfPlayer.leader : null;
            var traders = new List<TraderRecord>(freeColonists.Count);

            foreach (var pawn in freeColonists)
            {
                if (!pawn.RaceProps.Humanlike || stat.Worker.IsDisabledFor(pawn))
                    continue;

                var improvement = pawn.GetStatValue(stat);
                var isLeader = pawn == leader;
                Func<Texture> iconGetter = generatePictures
                    ? () => PortraitsCache.Get(pawn, new Vector2(24, 24), Rot4.South,
                        ColonistBarColonistDrawer.PawnTextureCameraOffset, 1.28205f)
                    : () => null;

                traders.Add(new TraderRecord(pawn, pawn.LabelShort, iconGetter,
                    improvement.ToStringPercent(), improvement, isLeader));
            }

            traders.Sort(static (a, b) =>
            {
                var leaderCompare = b.IsLeader.CompareTo(a.IsLeader);
                return leaderCompare != 0 ? leaderCompare : b.Improvement.CompareTo(a.Improvement);
            });

            return traders;
        }

    }
}
