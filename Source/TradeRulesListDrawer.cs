using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TD_Find_Lib;
using UnityEngine;
using Verse;

namespace MGAutoSell
{
    public class TradeRulesListDrawer : SearchGroupDrawerBase<TradeRulesGroup,TradeRule>
    {
        private readonly MainTabWindow_FindAndAutoSell _parent;
        private TradeRulesGameComp comp;

        private Color altBackground = new(0.3f, 0.3f, 0.3f, 0.5f);

        public TradeRulesListDrawer(TradeRulesGroup list, MainTabWindow_FindAndAutoSell parent) : base(list)
        {
            _parent = parent;
            comp = Current.Game.GetComponent<TradeRulesGameComp>();
        }

        private string editThiSearch = "TD.EditThisSearch".Translate();
        public override void DrawRowButtons(WidgetRow row, TradeRule item, int i)
        {
            row.Checkbox(ref item.Enabled);

            if (row.ButtonIcon(FindTex.Edit, editThiSearch))
                _parent.DoEdit(item);

            if (row.ButtonIcon(FindTex.Trash))
                comp.tradeRules.Remove(item);
        }

        public override void DrawExtraRowRect(Rect rowRect, TradeRule item, int i)
        {
            var color = GUI.color;
            var fadedColor = new Color(1, 1, 1, 0.4f);

            if (item == _parent.SelectedTradeRule)
                Widgets.DrawHighlightSelected(rowRect);

            var rowSell = new WidgetRow(rowRect.xMax, rowRect.y, UIDirection.LeftThenDown);

            var alignment = Text.CurTextFieldStyle.alignment;
            Text.CurTextFieldStyle.alignment = TextAnchor.MiddleCenter;

            var sellDownToRect = rowSell.GetRect(60);
            sellDownToRect.height -= 4;
            sellDownToRect.y += 3;

            string sellDownToBuffer = null;
            if (!item.AllowSell)
                GUI.color = fadedColor;
            Widgets.TextFieldNumeric(sellDownToRect, ref item.SellDownTo, ref sellDownToBuffer);
            GUI.color = color;

            var rowBuyRect1 = rowRect.RightHalf();
            var rowBuy1 = new WidgetRow(rowBuyRect1.x + 20, rowBuyRect1.y, UIDirection.RightThenDown);

            var buyUpToRect1 = rowBuy1.GetRect(60);
            buyUpToRect1.height -= 4;
            buyUpToRect1.y += 3;

            string buyUpToBuffer1 = null;
            if (!item.AllowBuy)
            {
                GUI.color = fadedColor;
            }
            Widgets.TextFieldNumeric(buyUpToRect1, ref item.BuyUpTo, ref buyUpToBuffer1);
            GUI.color = color;
            Text.CurTextFieldStyle.alignment = alignment;
        }

        public override string Name => "TD.ActiveSearches".Translate();
    }
}
