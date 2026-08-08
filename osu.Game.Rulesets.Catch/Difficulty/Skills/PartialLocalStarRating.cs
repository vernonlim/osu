// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Catch.Difficulty.Evaluators;
using osu.Game.Rulesets.Catch.Difficulty;
using osu.Game.Rulesets.Catch.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Catch.Difficulty.Skills
{
    // For debug purposes (displays on osu-tools PerformanceCalculatorGUI)
    public class PartialLocalStarRating : StrainDecaySkill
    {
        protected override double SkillMultiplier => 1;

        protected override double StrainDecayBase => 0.05;

        private readonly CatchDifficultyConstants tuning;

        public PartialLocalStarRating(Mod[] mods, CatchDifficultyConstants tuning)
            : base(mods)
        {
            this.tuning = tuning;
        }

        protected override double StrainValueOf(DifficultyHitObject current)
        {
            double precision = PrecisionEvaluator.EvaluateDifficultyOf(current);
            double speed = SpeedEvaluator.EvaluateDifficultyOf(current);

            return CatchDifficultyCalculator.CalculatePartialLocalStarRating(precision, speed, tuning) / 3.0;
        }
    }
}
