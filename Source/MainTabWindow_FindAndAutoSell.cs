using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using TD_Find_Lib;
using UnityEngine;
using Verse;
using Verse.Noise;
using static HarmonyLib.Code;

namespace MGAutoSell
{
    public record ItemsToSell(List<SellEntry> Items, float TotalSilver, string TraderName, Texture TraderIcon, string TraderImprovement);
    public class MainTabWindow_FindAndAutoSell : MainTabWindow
    {
        private TradeRulesListDrawer drawer;
        private Vector2 _scroll = Vector2.zero;

        private TradeRulesGameComp comp;
        private TradeRuleEditor editor;

        private ItemsToSell sellCache;
        private long nextCache = 0;
        private long nextQuickCache = 0;

        public override Vector2 RequestedTabSize => new(1010f, 300f);
        protected override float Margin => 8f;
        public bool SellListDirty => comp.tradeRules.Any(y => y.search.changed);

        public MainTabWindow_FindAndAutoSell()
        {
            preventCameraMotion = false;
            doCloseX = true;
        }

        public override void PreOpen()
        {
            base.PreOpen();
            comp = Current.Game.GetComponent<TradeRulesGameComp>();
            drawer = new TradeRulesListDrawer(comp.tradeRules, this);
        }

        public override void Close(bool doCloseSound = true)
        {
            editor?.PostClose();
            editor = null;
            SelectedTradeRule = null;
            base.Close(doCloseSound);
        }

        Vector2 listerScroll = Vector2.zero;
        private static string buyLabel = "Minimum", sellLabel = "Maximum";
        Vector2? SellingSize, BuyingSize;
        private long previousRenderTime = 0;
        public override void DoWindowContents(Rect inRect)
        {
            var timestamp = Stopwatch.GetTimestamp();
            var color = GUI.color;
            var fadedColor = new Color(1, 1, 1, 0.4f);
            var font = Text.Font;
            Text.Font = GameFont.Small;
            BuyingSize ??= Text.CalcSize(buyLabel);
            SellingSize ??= Text.CalcSize(sellLabel);
            var sellingSize = SellingSize.Value;
            var buyingSize = BuyingSize.Value;


            var width = editor != null ? 600f : 400f;

            var rulesRect = inRect.LeftPartPixels(width);

            //Widgets.DrawBox(rulesRect);

            if (editor != null)
            {
                editor.DoWindowContents(rulesRect);
                var buttonRect = rulesRect.BottomPartPixels(30f).LeftPartPixels(30f);
                if (Widgets.ButtonImage(buttonRect, TexButton.Banish))
                {
                    editor?.PostClose();
                    editor = null;
                    SelectedTradeRule = null;
                }
            }
            else
            {
                inRect.y -= 4;
                Text.Font = GameFont.Medium;
                GUI.color = new Color(1, 1, 1, 0.4f);
                Widgets.Label(inRect, $"<i>Find / Auto Sell {previousRenderTime}</i>");
                Text.Font = GameFont.Small;
                GUI.color = color;
                inRect.y += 4;

                var height = 300f;
                var header = rulesRect.TopPartPixels(30).LeftPartPixels(rulesRect.width - 16);
                var body = rulesRect.MiddlePartPixels(rulesRect.width, rulesRect.height - 60);

                var middle = 30;
                var left = header.RightHalf();
                left.x += middle - buyingSize.x / 2 + 20;
                Widgets.Label(left, buyLabel);
                var sellLabelRect = header.RightPartPixels(Math.Max(middle + sellingSize.x / 2, sellingSize.x));
                Widgets.Label(sellLabelRect, sellLabel);

                var drawerListing = new Listing_StandardIndent();
                drawerListing.BeginScrollView(body, ref listerScroll, body.LeftPartPixels(body.width - 16).TopPartPixels(comp.tradeRules.Count * 30).AtZero());
                drawer.DrawQuerySearchList(drawerListing);
                drawerListing.EndScrollView(ref height);

                var controlsRect = rulesRect.BottomPartPixels(Text.LineHeight);
                var controls = new WidgetRow(controlsRect.x, controlsRect.y);
                if (controls.ButtonIcon(FindTex.GreyPlus))
                    CreateRule();
            }


            var toSellRect = inRect.RightPartPixels(inRect.width - rulesRect.width - 12 - 16);
            toSellRect.x -= 16;

            GUI.color = fadedColor;
            Widgets.DrawLineVertical(rulesRect.width + 6, 0, 300f);
            GUI.color = color;

            CacheItemsToSell();

            toSellRect.SplitHorizontally(Text.LineHeight, out var itemHeader, out toSellRect);

            Widgets.DrawLightHighlight(itemHeader);

            Widgets.Label(itemHeader.RightPartPixels(itemHeader.width - 40f), "Item");
            GUI.DrawTexture(itemHeader.RightPartPixels(24f), ThingDefOf.Silver.uiIcon);

            toSellRect.SplitHorizontally(4, out var gapHeader, out toSellRect);
            Widgets.DrawLineHorizontal(gapHeader.x, gapHeader.y, gapHeader.width, fadedColor);

            int i = 0;
            foreach (var (thingDef, count, total) in sellCache.Items)
            {
                toSellRect.SplitHorizontally(30f, out var row, out toSellRect);

                if (i % 2 == 1)
                    Widgets.DrawLightHighlight(row);
                i++;

                GUI.DrawTexture(row.LeftPartPixels(30), thingDef.uiIcon);
                row.x += 40;
                Widgets.Label(row, thingDef.GetLabel() + $" x{count}");
                row.x -= 40;
                var totalLabel = Math.Round(total, 0).ToStringSafe();
                var size = Text.CalcSize(totalLabel);
                Widgets.Label(row.RightPartPixels(size.x + 4), totalLabel);
            }

            var footer = toSellRect.BottomPartPixels(Text.LineHeight);
            Widgets.DrawLightHighlight(footer);
            var iconRect = footer.LeftPartPixels(Text.LineHeight);
            iconRect.y -= 4;
            GUI.DrawTexture(iconRect, sellCache.TraderIcon);
            //TradeSession.playerNegotiator.GetStatValue(StatDefOf.TradePriceImprovement).ToStringPercent()
            Widgets.Label(footer.RightPartPixels(footer.width - Text.LineHeight), sellCache.TraderName + " - " + sellCache.TraderImprovement);

            var footerRow = new WidgetRow(footer.xMax - 4, footer.y, UIDirection.LeftThenDown);
            footerRow.Label(sellCache.TotalSilver.ToStringSafe());
            footerRow.Icon(ThingDefOf.Silver.uiIcon);
            footerRow.Label("Total:");
            Text.Font = font;
            previousRenderTime = Stopwatch.GetTimestamp() - timestamp;
        }

        public void DoWindowContentsOld(Rect inRect)
        {
            var timestamp = Stopwatch.GetTimestamp();

            var listing = new Listing_Standard();
            listing.Begin(inRect);

            var drawerListing = new Listing_StandardIndent();
            var height = 300f;

            var topRect = listing.GetRect(height);
            //var color = GUI.color;
            //GUI.color = new Color(1, 1, 1, 0.4f);
            //var lineRect = topRect.MiddlePartPixels(1, height);
            //Widgets.DrawLineVertical(lineRect.x, lineRect.y, height);
            //GUI.color = color;
            var controlsRect = topRect.LeftHalf().BottomPartPixels(Text.LineHeight);
            var controls = new WidgetRow(controlsRect.x, controlsRect.y);
            if (controls.ButtonIcon(FindTex.GreyPlus))
                CreateRule();

            var rect = topRect.LeftHalf();
            var header = rect.TopPartPixels(30).LeftPartPixels(rect.width - 16);
            var body = rect.MiddlePartPixels(rect.width, rect.height - 60);
            Widgets.Label(header.LeftHalf(), $"Find and (Auto) Sell ({previousRenderTime}ts)");

            var middle = 30;
            var left = header.RightHalf().LeftHalf();
            left.x += middle - (BuyingSize.Value.x / 2);
            Widgets.Label(left, sellLabel);
            Widgets.Label(header.RightPartPixels(Math.Max(middle + SellingSize.Value.x / 2, SellingSize.Value.x)), buyLabel);

            drawerListing.BeginScrollView(body, ref listerScroll, body.LeftPartPixels(body.width - 16).TopPartPixels(comp.tradeRules.Count * 30).AtZero());
            drawer.DrawQuerySearchList(drawerListing);
            drawerListing.EndScrollView(ref height);
            listing.GapLine();

            if (editor != null)
            {
                var editRect = topRect.RightHalf();
                
                editor.DoWindowContents(editRect);
            }

            var bottom = listing.GetRect(inRect.height - listing.CurHeight);

            var toSellRect = bottom.LeftHalf();
            CacheItemsToSell();

            int i = 0;
            foreach (var (thingDef, count, total) in sellCache.Items)
            {
                toSellRect.SplitHorizontally(30f, out var row, out toSellRect);

                if (i % 2 == 1)
                    Widgets.DrawLightHighlight(row);
                i++;

                GUI.DrawTexture(row.LeftPartPixels(30), thingDef.uiIcon);
                row.x += 40;
                Widgets.Label(row, thingDef.GetLabel() + $" ({count})");
                row.x -= 40;
                var totalLabel = Math.Round(total, 0).ToStringSafe();
                var size = Text.CalcSize(totalLabel);
                Widgets.Label(row.RightPartPixels(size.x + 4), totalLabel);
            }

            var footer = toSellRect.BottomPartPixels(Text.LineHeight);
            Widgets.DrawLightHighlight(footer);
            

            var footerRow = new WidgetRow(footer.xMax - 4, footer.y, UIDirection.LeftThenDown);
            footerRow.Label(sellCache.TotalSilver.ToStringSafe());
            footerRow.Icon(ThingDefOf.Silver.uiIcon);
            footerRow.Label("Total:");

            listing.End();
            previousRenderTime = Stopwatch.GetTimestamp() - timestamp;
        }

        public void CacheItemsToSell(bool force = false)
        {
            var shouldUpdate = force || (SellListDirty && nextQuickCache < DateTime.UtcNow.Ticks) ||
                               nextCache < Find.TickManager.TicksGame;

            if (!shouldUpdate)
                return;

            var timestamp = Stopwatch.GetTimestamp();
            var allItems = TradeUtility.AllLaunchableThingsForTrade(Find.CurrentMap).ToList();
            var sellDictionary = new Dictionary<ThingDef, int>();
            var thingDictionary = new Dictionary<ThingDef, List<Thing>>();

            var junk = allItems.Where(x => x.Map.designationManager.DesignationOn(x, MGDesignatorDefOf.MGAutoSell) != null).ToList();
            junk.ForEach(x => allItems.Remove(x));
            var junkGrouped = junk.GroupBy(x => x.def).ToList();

            thingDictionary.AddRange(junkGrouped.ToDictionary(x => x.Key,
                x => x.ToList()));

            foreach (var rule in comp.tradeRules.Where(x => x.Enabled && x.AllowSell && x.search.Children.queries.Any()))
            {
                var items = allItems.Where(x => rule.search.AppliesTo(x)).ToList();

                items.ForEach(x =>
                {
                    allItems.Remove(x);
                });

                var itemsGrouped = items
                    .GroupBy(x => x.def).ToList();

                foreach (var (thingDef, list) in itemsGrouped.ToDictionary(x => x.Key, x => x.ToList()))
                {
                    if(!thingDictionary.TryAdd(thingDef, list))
                        thingDictionary[thingDef].AddRange(list);

                    sellDictionary.Add(thingDef, rule.SellDownTo);
                }
            }

            var stat = StatDefOf.TradePriceImprovement;
            var socialPawn = Find.CurrentMap.mapPawns.FreeColonists
                .Where(x => x.RaceProps.Humanlike && !stat.Worker.IsDisabledFor(x))
                .MaxBy(x => x.GetStatValue(stat));

            var traderPriceType = PriceType.Normal.PriceMultiplier();
            var playerNegotiator = socialPawn.GetStatValue(StatDefOf.TradePriceImprovement);
            var settlement = socialPawn.TradePriceImprovementOffsetForPlayer;
            var drugBonus = socialPawn.GetStatValue(StatDefOf.DrugSellPriceImprovement);
            var animalProduceBonus = socialPawn.GetStatValue(StatDefOf.AnimalProductsSellImprovement);
            thingDictionary.RemoveAll(x => !x.Value.Any());
            var sellEntries = thingDictionary.Select(x =>
            {
                var (thingDef, items) = x;
                var humanPawn = items.FirstOrDefault() is Pawn pawn && pawn.RaceProps.Humanlike ? 0.6f : 1f;
                var priceTotal = items.Select(y => TradeUtility.GetPricePlayerSell(y, traderPriceType, humanPawn, playerNegotiator, settlement, drugBonus, animalProduceBonus) * y.stackCount).Sum();
                var itemsTotal = items.Sum(x => x.stackCount);
                var pricePer = priceTotal / itemsTotal;
                var sellDown = sellDictionary.TryGetValue(thingDef);

                if (itemsTotal <= sellDown)
                    return null;

                priceTotal -= pricePer * sellDown;
                itemsTotal -= sellDown;

                return new SellEntry(thingDef, itemsTotal, (float)Math.Round(priceTotal, 0));
            })
            .Where(x => x != null)
            .OrderByDescending(x => x.Total)
            .ToList();

            sellCache = new ItemsToSell(sellEntries, (float)Math.Round(sellEntries.Sum(x => x.Total), 0),
                socialPawn.LabelShort, PortraitsCache.Get(socialPawn, new Vector2(24, 24), Rot4.South, ColonistBarColonistDrawer.PawnTextureCameraOffset, 1.28205f), playerNegotiator.ToStringPercent());

            nextCache = Find.TickManager.TicksGame + 3600;
            nextQuickCache = DateTime.UtcNow.AddSeconds(1).Ticks;

            var duration = Stopwatch.GetTimestamp() - timestamp;
            Log.Message($"Generated list in {duration}ts");
            comp.tradeRules.ForEach(x => x.search.changed = false);
        }

        public TradeRule SelectedTradeRule;
        public void DoEdit(TradeRule tradeRule)
        {
            editor?.PostClose();

            if (SelectedTradeRule == tradeRule)
            {
                editor = null;
                SelectedTradeRule = null;
            }
            else
            {
                editor = new TradeRuleEditor(tradeRule);
                SelectedTradeRule = tradeRule;
            }
        }

        public void CreateRule()
        {
            Find.WindowStack.Add(new Dialog_Name("TD.NewAlert".Translate(), n =>
                {
                    TradeRule tradeRule = new(n);
                    comp.tradeRules.Add(tradeRule);

                    editor = new TradeRuleEditor(tradeRule);
                },
                "TD.NameForNewAlert".Translate(),
                name => comp.tradeRules.Any(x => name == x.Search.name)));
        }
    }

    public record SellEntry(ThingDef Item, int Count, float Total);
}
