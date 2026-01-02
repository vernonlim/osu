// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Catch.Difficulty.Evaluators;
using osu.Game.Rulesets.Catch.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Catch.Difficulty.Skills
{
    // For debug purposes (displays on osu-tools PerformanceCalculatorGUI)
    public class LocalStarRating : StrainDecaySkill
    {
        protected override double SkillMultiplier => 1;

        protected override double StrainDecayBase => 0.05;

        public LocalStarRating(Mod[] mods)
            : base(mods)
        {
        }

        protected override double StrainValueOf(DifficultyHitObject current)
        {
            double precision = PrecisionEvaluator.EvaluateDifficultyOf(current);
            double speed = SpeedEvaluator.EvaluateDifficultyOf(current);
            double actionProbability = ((CatchDifficultyHitObject)current).MovementData.ActionProbability;
            double readingFactor = ((CatchDifficultyHitObject)current).ReadingData.CombinedReadingFactor;
            double highCSFactor = ((CatchDifficultyHitObject)current).ReadingData.HighCSFactor;

            return CatchDifficultyCalculator.CalculateLocalStarRating(actionProbability, precision, speed, readingFactor, highCSFactor) / 3.0;
        }
    }
}
