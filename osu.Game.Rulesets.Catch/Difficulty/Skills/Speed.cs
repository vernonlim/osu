// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Catch.Difficulty.Evaluators;
using osu.Game.Rulesets.Catch.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Catch.Difficulty.Skills
{
    public class Speed : StrainDecaySkill
    {
        protected override double SkillMultiplier => 1.0;

        protected override double StrainDecayBase => 0.0;

        public Speed(Mod[] mods)
            : base(mods)
        {
        }

        protected override double StrainValueOf(DifficultyHitObject current)
        {
            double actionProbability = ((CatchDifficultyHitObject)current).MovementData.ActionProbability;

            return SpeedEvaluator.EvaluateDifficultyOf(current) * Math.Pow(actionProbability, 2);
        }

        protected override double StrainDecay(double ms) => Math.Pow(StrainDecayBase, ms / 1000);
    }
}
