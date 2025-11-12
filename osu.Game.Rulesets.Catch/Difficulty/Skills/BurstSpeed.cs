// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Catch.Difficulty.Evaluators;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Catch.Difficulty.Skills
{
    public class BurstSpeed : StrainSkill
    {
        /// <summary>
        /// The current strain level.
        /// </summary>
        protected double CurrentStrain { get; private set; }

        public BurstSpeed(Mod[] mods)
            : base(mods)
        {
        }

        protected override double CalculateInitialStrain(double time, DifficultyHitObject current) => CurrentStrain;

        protected override double StrainValueAt(DifficultyHitObject current)
        {
            CurrentStrain = SpeedEvaluator.EvaluateBurstDifficultyOf(current);

            return CurrentStrain;
        }
    }
}
