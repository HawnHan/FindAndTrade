using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace RimMod
{
    public class Mod : Verse.Mod
    {
        public Mod(ModContentPack content) : base(content)
        {

        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            base.DoSettingsWindowContents(inRect);
        }

        public override string SettingsCategory() => "RimMod.Settings".Translate();
    }
}
