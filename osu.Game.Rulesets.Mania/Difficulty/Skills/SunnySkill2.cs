// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.Difficulty.Skills
{
    public class SunnySkill2 : Skill
    {
        public SunnySkill2(Mod[] mods)
            : base(mods)
        {
        }

        private static readonly List<List<ManiaDifficultyHitObject>> list_column_objects = new List<List<ManiaDifficultyHitObject>>();

        public override void Process(DifficultyHitObject current)
        {
            throw new System.NotImplementedException();
        }

        public override double DifficultyValue()
        {
            throw new System.NotImplementedException();
        }
    }
}
