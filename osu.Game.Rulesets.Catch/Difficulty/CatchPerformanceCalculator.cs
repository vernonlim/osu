// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Extensions.IEnumerableExtensions;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Mods;
using osu.Game.Scoring;
using osu.Game.Scoring.Legacy;
using osu.Game.Utils;

namespace osu.Game.Rulesets.Catch.Difficulty
{
    public class CatchPerformanceCalculator : PerformanceCalculator
    {
        private readonly CatchDifficultyConstants fallbackTuning;
        private int num300;
        private int num100;
        private int num50;
        private int numKatu;
        private int numMiss;

        public CatchPerformanceCalculator(CatchDifficultyConstants? tuning = null)
            : base(new CatchRuleset(tuning))
        {
            fallbackTuning = tuning ?? CatchDifficultyConstants.Default;
        }

        protected override PerformanceAttributes CreatePerformanceAttributes(ScoreInfo score, DifficultyAttributes attributes)
        {
            var catchAttributes = (CatchDifficultyAttributes)attributes;
            var tuning = catchAttributes.Tuning ?? fallbackTuning;

            num300 = score.GetCount300() ?? 0; // HitResult.Great
            num100 = score.GetCount100() ?? 0; // HitResult.LargeTickHit
            num50 = score.GetCount50() ?? 0; // HitResult.SmallTickHit
            numKatu = score.GetCountKatu() ?? 0; // HitResult.SmallTickMiss
            numMiss = score.GetCountMiss() ?? 0; // HitResult.Miss PLUS HitResult.LargeTickMiss

            double value = calculateValue(catchAttributes.StarRating);


            // Miss penalty: as our system is highly sensitive towards "difficulty spikes" (which allows us to reward more variety of skillsets),
                // it is important to make sure that scores with high misscount don't give too much pp.
                // Otherwise, it could create some absurd possibilities, for example player could intentionally miss on all hard edge jumps in the map and still get pp for it.
                // Moreover, we keep penalty for the first miss at 95% level (lower than "expected") to separate FC and non-FC.
                // At the same time, combo scaling is slightly reduced compared to the previous pp system.
            const double base_penalty = 0.965;

            double penalty = numMiss switch
            {
                0 => 1.0,
                1 => 0.95,
                2 => 0.93,
                3 => 0.90,
                var x => Math.Pow(base_penalty, 4) * Math.Pow(Math.Max(0, base_penalty - 0.001 * (x - 4)), (x - 4))
            };

            value *= penalty;

            // Combo scaling power is adjusted from 0.35 to 0.32 to compensate for the harsher misscount penalties.
            const double scaling_power = 0.32;
            if (catchAttributes.MaxCombo > 0)
                value *= Math.Min(Math.Pow(score.MaxCombo, scaling_power) / Math.Pow(catchAttributes.MaxCombo, scaling_power), 1.0);

            var difficulty = score.BeatmapInfo!.Difficulty.Clone();

            score.Mods.OfType<IApplicableToDifficulty>().ForEach(m => m.ApplyToDifficulty(difficulty));

            double clockRate = ModUtils.CalculateRateWithMods(score.Mods);

            double approachRate = CalculateApproachRate(score.Mods, difficulty.ApproachRate, CorrectedClockRate(clockRate));

            // Length bonus: the longer the map is, the hardest it is to set a good score (FC/low misscount).
                // This bonus is excluded from the SR on purpose: star rating should be an information about "how hard patterns are", while pp: "how hard getting full combo is".
            // The base length measure is the number of actions: our calculations performed in preprocessing let us approximate how many times player has to change combination of keys they are holding.
                // Some undetected actions aren't detected due to limitations of the algorithm. We approximate their number with 25% of the maximum combo.
            const double combo_percentage = 0.25;
            double maxCombo = catchAttributes.MaxCombo;
            double totalActions = ((CatchDifficultyAttributes)attributes).TotalActions + combo_percentage * maxCombo;

            double linear_pace = tuning.PerformanceLengthLinearPace;
            double cutoff = tuning.PerformanceLengthCutoff;
            double logarithmic_pace = tuning.PerformanceLengthLogarithmicPace;

            // Pace is linear at first, then it's logarithmic (growth is slower).
            double lengthBonus =
                1.0 + linear_pace * Math.Min(1.0, totalActions / cutoff) +
                (totalActions > cutoff ? Math.Log10(totalActions / cutoff) * logarithmic_pace : 0.0);

            // Length bonus should depend on approachRate: if it's high enough, it's either draining or it requires memorisation.
            if (score.Mods.Any(m => m is ModFlashlight))
                lengthBonus = Math.Pow(lengthBonus, 1.0 + Math.Max(0, approachRate - 8.0) / 2.0);
            else
                lengthBonus = Math.Pow(lengthBonus, 1.0 + Math.Max(0, approachRate - 10.3) / 2.0);

            // Additional length bonus for FL and HDFL (this part is not dependent on the AR).
            if (score.Mods.Any(m => m is ModFlashlight))
            {
                if (score.Mods.Any(m => m is ModHidden))
                    lengthBonus = Math.Pow(lengthBonus, 2.4);
                else
                    lengthBonus = Math.Pow(lengthBonus, 2.0);
            }

            value *= Math.Pow(accuracy(), 5.5);

            if (score.Mods.Any(m => m is ModNoFail))
                value *= Math.Max(0.90, 1.0 - 0.02 * numMiss);

            double lengthBonusPP = value / (tuning.PerformanceValueMultiplier) * (lengthBonus - 1.0);

            return new CatchPerformanceAttributes
            {
                LengthBonus = lengthBonusPP,
                Total = (value + lengthBonusPP),
                Tuning = tuning,
            };
        }

        // The following function returns "adjusted approach rate"
        // For DT (or other rates greater than 1) we are taking into account that the faster catcher's velocity is, the more player can delay their moves.
            // That means, the faster the catcher is, the easier reacting to the falling notes is. We are approximating it in CorrectedClockRate function.
        // For HT (or any rates smaller than 1) we are using the opposite logic: movement of the catcher takes more time.
        public static double CalculateApproachRate(Mod[] mods, double approachRate, double correctedClockRate)
        {
            double preempt = IBeatmapDifficultyInfo.DifficultyRange(approachRate, 1800, 1200, 450) / correctedClockRate;
            return preempt > 1200.0 ? (1800.0 - preempt) / 120.0 : (1200.0 - preempt) / 150.0 + 5.0;
        }

        public static double CorrectedClockRate(double clockRate) => 1.0 + (clockRate - 1.0) * 0.8; // AR9+DT approximately equals AR10.15 after correction

        private double calculateValue(double sr) => Math.Pow(5.0 * Math.Max(1.0, sr / 0.0049) - 4.0, 2.0) / 100000.0;

        private double accuracy() => totalHits() == 0 ? 0 : Math.Clamp((double)totalSuccessfulHits() / totalHits(), 0, 1);
        private int totalHits() => num50 + num100 + num300 + numMiss + numKatu;
        private int totalSuccessfulHits() => num50 + num100 + num300;
        private int totalComboHits() => numMiss + num100 + num300;
    }
}
