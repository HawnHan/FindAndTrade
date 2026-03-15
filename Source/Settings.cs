using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using MGAutoSell.Filter;
using RimWorld;
using TD_Find_Lib;
using UnityEngine;
using Verse;

namespace MGAutoSell
{
    public class Settings : ModSettings
    {
        // TODO Icons in Menu (true)
        public bool scanEveryStack = true;
        public bool showAllMatchingItems = true;
        public bool showQuantityInsteadOfLabel = true;
        public bool colorRuleCountsOnWork = true;
#if DEBUG
        public OpenSetting MenuToOpen = OpenSetting.None;
#endif

        private static string _benchmarkLabel;
        private static string _benchmarkLabelDisabled;
        private static string _scanEveryStackLabel;
        private static string _scanEveryStackTooltip;
        private static string _showAllMatchingItemsLabel;
        private static string _showAllMatchingItemsTooltip;
        private static string _showQuantityInsteadOfLabelLabel;
        private static string _showQuantityInsteadOfLabelTooltip;
        private static string _colorRuleCountsOnWorkLabel;
        private static string _colorRuleCountsOnWorkTooltip;

        private static ItemsToSell _showAllMatchItemsEnabled;
        private static ItemsToSell _showAllMatchItemsDisabled;
        private static ItemsToSell _exampleTradeRulesCache;
        private static List<TradeRule> _exampleTradeRules = [];
        private static Vector2 sellScroll = Vector2.zero;
        private static float firstListingHeight;
        private static float maxHeightOfSides;

        public void Init()
        {
            _benchmarkLabel = "MGAutoSell.Settings.Benchmark".Translate();
            _benchmarkLabelDisabled = "MGAutoSell.Settings.BenchmarkDisabled".Translate();
            _scanEveryStackLabel = "MGAutoSell.Settings.scanEveryStackLabel".Translate();
            _scanEveryStackTooltip = "MGAutoSell.Settings.scanEveryStackTooltip".Translate();
            _showAllMatchingItemsLabel = "MGAutoSell.Settings.showAllMatchingItemsLabel".Translate();
            _showAllMatchingItemsTooltip = "MGAutoSell.Settings.showAllMatchingItemsTooltip".Translate();
            _showQuantityInsteadOfLabelLabel = "MGAutoSell.Settings.showQuantityInsteadOfLabelLabel".Translate();
            _showQuantityInsteadOfLabelTooltip = "MGAutoSell.Settings.showQuantityInsteadOfLabelTooltip".Translate();
            _colorRuleCountsOnWorkLabel = "MGAutoSell.Settings.colorRuleCountsOnWorkLabel".Translate();
            _colorRuleCountsOnWorkTooltip = "MGAutoSell.Settings.colorRuleCountsOnWorkTooltip".Translate();



            var search = new QuerySearch();
            search.name = "Drugs";
            var drugsQuery = ThingQueryMaker.MakeQuery<ThingQueryThingDefCategory>();
            drugsQuery.sel = "drugs";
            search.Children.Add(drugsQuery);

            var drugsPossible = DefDatabase<ThingDef>.AllDefsListForReading
                .Where(x => x.FirstThingCategory == ThingCategoryDefOf.Drugs)
                .Select(x => new PotentialItem(x, "(" + search.name + ")"))
                .ToList();

            var yayo = ThingDefOf.Yayo;
            var totals = new ItemAndLabel<float>(yayo.BaseMarketValue, yayo.BaseMarketValue.ToStringMoney());
            var drugsActual = new List<SellRecord>()
            {
                new(ThingDefOf.Yayo, 1, totals, totals)
            };

            _showAllMatchItemsEnabled = new ItemsToSell(drugsActual, drugsPossible, drugsActual[0].Total, null, null);
            _showAllMatchItemsDisabled = new ItemsToSell(drugsActual, [], drugsActual[0].Total, null, null);

            firstListingHeight = 0;
            firstListingHeight += Text.CalcSize(_scanEveryStackLabel).y;
#if DEBUG
            firstListingHeight += 30;
#endif

            var itemRules = new Dictionary<TradeRule, ItemAndLabel<int>>();
            var steelTradeRule = new TradeRule("Steel")
            {
                Import = 1000,
                ImportBuffer = "1000",
                Mode = TradeMode.Import
            };
            itemRules[steelTradeRule] = new ItemAndLabel<int>(900, "x900");
            _exampleTradeRules.Add(steelTradeRule);

            var meals = new TradeRule("Meals")
            {
                Import = 20,
                ImportBuffer = "20",
                Mode = TradeMode.Import,
            };
            itemRules[meals] = new ItemAndLabel<int>(24, "x24");
            _exampleTradeRules.Add(meals);

            var pleasurableDrugs = new TradeRule("Pleasurable Drugs")
            {
                Import = 5,
                ImportBuffer = "5",
                Mode = TradeMode.Maintain,
                Export = 30,
                ExportBuffer = "30"
            };
            itemRules[pleasurableDrugs] = new ItemAndLabel<int>(4, "x4");
            _exampleTradeRules.Add(pleasurableDrugs);

            var organs = new TradeRule("Organs")
            {
                Export = 0,
                ExportBuffer = "0",
                Mode = TradeMode.Export
            };
            itemRules[organs] = new ItemAndLabel<int>(2, "x2");
            _exampleTradeRules.Add(organs);

            var lowQualityArt = new TradeRule("Low Quality Art")
            {
                Export = 0,
                ExportBuffer = "0",
                Mode = TradeMode.Export
            };
            itemRules[lowQualityArt] = new ItemAndLabel<int>(0, "x0");
            _exampleTradeRules.Add(lowQualityArt);

            _exampleTradeRulesCache = new ItemsToSell(null, null, null, null, itemRules);
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref scanEveryStack, "scanEveryStack", true);
            Scribe_Values.Look(ref showAllMatchingItems, "showAllMatchingItems", true);
            Scribe_Values.Look(ref showQuantityInsteadOfLabel, "showQuantityInsteadOfLabel", true);
            Scribe_Values.Look(ref colorRuleCountsOnWork, "colorRuleCountsOnWork", true);

#if DEBUG
            Scribe_Values.Look(ref MenuToOpen, "MenuToOpen");
#endif
        }

        public void DoSettingsWindow(Rect rect)
        {
            var benchmarkTray = Rect.zero;

            rect.SplitHorizontallyWithMargin(out var top, out var bottom, out _, topHeight: firstListingHeight, compressibleMargin: 20);
            bottom.SplitVerticallyWithMargin(out var left, out var right, 16f);
            var listing = new Listing_Standard();
            listing.Begin(top);

            listing.CheckboxLabeled(_scanEveryStackLabel, ref scanEveryStack, _scanEveryStackTooltip);
#if DEBUG
            if (listing.ButtonTextLabeled("DEBUG: Open window on launch", MenuToOpen.ToString(), TextAnchor.MiddleLeft))
            {
                Find.WindowStack.Add(new FloatMenu([
                    new(OpenSetting.None.ToString(), () => MenuToOpen = OpenSetting.None),
                    new(OpenSetting.Settings.ToString(), () => MenuToOpen = OpenSetting.Settings),
                    new(OpenSetting.MainMenuTab.ToString(), () => MenuToOpen = OpenSetting.MainMenuTab),

                ]));
            }
#endif
            listing.End();
            // Sell list
            listing.Begin(right);

            listing.CheckboxLabeled(_showAllMatchingItemsLabel, ref showAllMatchingItems, _showAllMatchingItemsTooltip);
            if(listing.CurHeight > maxHeightOfSides)
                maxHeightOfSides = listing.CurHeight;
            else if(listing.CurHeight < maxHeightOfSides)
                listing.Gap(maxHeightOfSides - listing.CurHeight);
            listing.GapLine(4);


            var sellList = listing.GetRect(150);
            TabUtility.DrawSellPanel(sellList, showAllMatchingItems ? _showAllMatchItemsEnabled : _showAllMatchItemsDisabled, ref sellScroll);

            listing.End();
            // Rules
            listing.Begin(left);

            listing.CheckboxLabeled(_showQuantityInsteadOfLabelLabel, ref showQuantityInsteadOfLabel, _showQuantityInsteadOfLabelTooltip);
            listing.CheckboxLabeled(_colorRuleCountsOnWorkLabel, ref colorRuleCountsOnWork, _colorRuleCountsOnWorkTooltip);

            var height = 300f;
            if (listing.CurHeight > maxHeightOfSides)
                maxHeightOfSides = listing.CurHeight;
            else if (listing.CurHeight < maxHeightOfSides)
                listing.Gap(maxHeightOfSides - listing.CurHeight);
            listing.GapLine(4);

            var body = listing.GetRect(height);
            var drawerListing = new Listing_Standard();
            drawerListing.Begin(body);

            for (var index = 0; index < _exampleTradeRules.Count; index++)
            {
                var tradeRule = _exampleTradeRules[index];
                TradeRuleDrawUtility.DrawRow(drawerListing.GetRect(30), tradeRule, index, _exampleTradeRulesCache, -1);
            }

            height = drawerListing.CurHeight;
            drawerListing.End();
            listing.End();

            var biggestHeight = height > 150f ? height : 150f;
            var color = GUI.color;
            GUI.color = new(1, 1, 1, 0.4f);
            Widgets.DrawLineVertical(body.width + 8f, body.y + bottom.y, biggestHeight);
            GUI.color = color;

            //if (benchmarkTray == Rect.zero)
            //    return;

            bottom.SplitHorizontallyWithMargin(out bottom, out benchmarkTray, out _, 20f, biggestHeight + maxHeightOfSides);
            listing.Begin(benchmarkTray);

            listing.GapLine(8f);
            
            if(Find.CurrentMap == null)
            {
                listing.Label(_benchmarkLabelDisabled);
                listing.End();
                return;
            }

            if (listing.ButtonText(_benchmarkLabel))
            {

            }

            listing.End();
        }
    }
}
