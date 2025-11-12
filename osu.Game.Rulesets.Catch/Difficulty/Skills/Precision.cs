// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Catch.Difficulty.Evaluators;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Catch.Difficulty.Skills
{
    public class Precision : StrainSkill
    {
        /// <summary>
        /// The current strain level.
        /// </summary>
        protected double CurrentStrain { get; private set; }

        public Precision(Mod[] mods)
            : base(mods)
        {
        }

        protected override double StrainValueAt(DifficultyHitObject current)
        {
            double strain = PrecisionEvaluator.EvaluateDifficultyOf(current);

            CurrentStrain = strain;

            return strain;
        }

        protected override double CalculateInitialStrain(double time, DifficultyHitObject current) => CurrentStrain;
    }
}
