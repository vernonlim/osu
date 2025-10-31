// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Catch.Difficulty.Evaluators;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Catch.Difficulty.Skills
{
    public class Precision : StrainDecaySkill
    {
        protected override double SkillMultiplier => 1.0;

        protected override double StrainDecayBase => 0.0;

        public Precision(Mod[] mods)
            : base(mods)
        {
        }

        protected override double StrainValueOf(DifficultyHitObject current)
        {
            return PrecisionEvaluator.EvaluateDifficultyOf(current);
        }

        protected override double StrainDecay(double ms) => Math.Pow(StrainDecayBase, ms / 1000);
    }
}
