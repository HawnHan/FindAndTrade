using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TD_Find_Lib;
using Verse;

namespace MGAutoSell
{
    public class TradeRulesListDrawer : SearchGroupDrawerBase<TradeRulesGroup,TradeRule>
    {
        private TradeRulesGameComp comp;
        public TradeRulesListDrawer(TradeRulesGroup list) : base(list)
        {
            comp = Current.Game.GetComponent<TradeRulesGameComp>();
        }

        public override void DrawRowButtons(WidgetRow row, TradeRule item, int i)
        {
            if (row.ButtonIcon(FindTex.Edit, "TD.EditThisSearch".Translate()))
                Find.WindowStack.Add(new TradeRuleEditor(item));

            if (row.ButtonIcon(FindTex.Trash))
                comp.tradeRules.Remove(item);

        }

        public override string Name => "TD.ActiveSearches".Translate();
    }
}
