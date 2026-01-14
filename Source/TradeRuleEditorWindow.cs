using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TD_Find_Lib;
using UnityEngine;
using Verse;

namespace MGAutoSell
{
    internal class TradeRuleEditorWindow : SearchEditorRevertableWindow
    {
        TradeRule searchAlert;
        public TradeRuleEditorWindow(QuerySearch search, string transferTag) : base(search, transferTag)
        {
        }

        public override void DoWindowContents(Rect fillRect)
        {
            base.DoWindowContents(fillRect);
        }
    }
}
