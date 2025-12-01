// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Catch.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Catch.Difficulty.Skills
{
    public class Movement : StrainDecaySkill
    {
        private const double direction_change_bonus = 21.0;

        protected override double SkillMultiplier => 1;

        protected override double StrainDecayBase => 0.2;

        protected override double DecayWeight => 0.94;

        private double clockRate;

        public Movement(Mod[] mods, double clockRate)
            : base(mods)
        {
            this.clockRate = clockRate;
        }

        protected override double StrainValueOf(DifficultyHitObject current)
        {
            CatchDifficultyHitObject note = (CatchDifficultyHitObject)current;
            CatchDifficultyHitObject? prev = note.PreviousNote(0);
            CatchDifficultyHitObject? prevPrev = note.PreviousNote(1);

            double strain = 0;

            if (prev is not null && prevPrev is not null)
            {
                double strainTime = Math.Max(40, note.StartTime - prev.StartTime);
                double weightedStrainTime = strainTime + 13 + (3 / clockRate);

                double distanceAddition = (Math.Pow(Math.Abs(note.DistanceMoved), 1.3) / 510);
                double sqrtStrain = Math.Sqrt(weightedStrainTime);
                double edgeDashBonus = 0;

                // Direction change bonus.
                if (Math.Abs(note.DistanceMoved) > 0.1)
                {
                    if (current.Index >= 1 && Math.Abs(prev.DistanceMoved) > 0.1 && Math.Sign(note.DistanceMoved) != Math.Sign(prev.DistanceMoved))
                    {
                        double bonusFactor = Math.Min(50, Math.Abs(note.DistanceMoved)) / 50;
                        double antiflowFactor = Math.Max(Math.Min(70, Math.Abs(prev.DistanceMoved)) / 70, 0.38);

                        distanceAddition += direction_change_bonus / Math.Sqrt(prev.StrainTime + 16) * bonusFactor * antiflowFactor * Math.Max(1 - Math.Pow(weightedStrainTime / 1000, 3), 0);
                    }

                    // Base bonus for every movement, giving some weight to streams.
                    distanceAddition += 12.5 * Math.Min(Math.Abs(note.DistanceMoved), CatchDifficultyHitObject.NORMALIZED_HALF_CATCHER_WIDTH * 2)
                                        / (CatchDifficultyHitObject.NORMALIZED_HALF_CATCHER_WIDTH * 6) / sqrtStrain;
                }

                // Bonus for edge dashes.
                if (note.LastObject.DistanceToHyperDash <= 20.0f)
                {
                    if (!note.LastObject.HyperDash)
                        edgeDashBonus += 5.7;

                    distanceAddition *= 1.0 + edgeDashBonus * ((20 - note.LastObject.DistanceToHyperDash) / 20)
                                                            * Math.Pow((Math.Min(note.StrainTime * clockRate, 265) / 265), 1.5); // Edge Dashes are easier at lower ms values
                }

                // There is an edge case where horizontal back and forth sliders create "buzz" patterns which are repeated "movements" with a distance lower than
                // the platter's width but high enough to be considered a movement due to the absolute_player_positioning_error and NORMALIZED_HALF_CATCHER_WIDTH offsets
                // We are detecting this exact scenario. The first back and forth is counted but all subsequent ones are nullified.
                // To achieve that, we need to store the exact distances (distance ignoring absolute_player_positioning_error and NORMALIZED_HALF_CATCHER_WIDTH)
                if (current.Index >= 2 && Math.Abs(note.ExactDistanceMoved) <= CatchDifficultyHitObject.NORMALIZED_HALF_CATCHER_WIDTH * 2
                                       && note.ExactDistanceMoved == -prev.ExactDistanceMoved && prev.ExactDistanceMoved == -prevPrev.ExactDistanceMoved
                                       && note.StrainTime == prev.StrainTime && prev.StrainTime == prevPrev.StrainTime)
                    distanceAddition = 0;

                strain = distanceAddition / weightedStrainTime;
            }

            return strain * 20;
        }
    }
}
