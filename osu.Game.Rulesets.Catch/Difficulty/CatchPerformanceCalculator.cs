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
        private int num300;
        private int num100;
        private int num50;
        private int numKatu;
        private int numMiss;

        public CatchPerformanceCalculator()
            : base(new CatchRuleset())
        {
        }

        protected override PerformanceAttributes CreatePerformanceAttributes(ScoreInfo score, DifficultyAttributes attributes)
        {
            var catchAttributes = (CatchDifficultyAttributes)attributes;

            num300 = score.GetCount300() ?? 0; // HitResult.Great
            num100 = score.GetCount100() ?? 0; // HitResult.LargeTickHit
            num50 = score.GetCount50() ?? 0; // HitResult.SmallTickHit
            numKatu = score.GetCountKatu() ?? 0; // HitResult.SmallTickMiss
            numMiss = score.GetCountMiss() ?? 0; // HitResult.Miss PLUS HitResult.LargeTickMiss

            double value = calculateValue(catchAttributes.SRBeginningNerfed);

            const double base_penalty = 0.965;

            double penalty = numMiss switch
            {
                0 => 1.0,
                1 => 0.95,
                2 => 0.93,
                3 => 0.90,
                var x => Math.Pow(base_penalty, 4) * Math.Pow(base_penalty - 0.001 * (x - 4), (x - 4))
            };

            value *= penalty;

            // Combo scaling power is adjusted from 0.35 to 0.30 to compensate for the harsher misscount penalties
            const double scaling_power = 0.32;

            if (catchAttributes.MaxCombo > 0)
                value *= Math.Min(Math.Pow(score.MaxCombo, scaling_power) / Math.Pow(catchAttributes.MaxCombo, scaling_power), 1.0);

            var difficulty = score.BeatmapInfo!.Difficulty.Clone();

            score.Mods.OfType<IApplicableToDifficulty>().ForEach(m => m.ApplyToDifficulty(difficulty));

            double clockRate = ModUtils.CalculateRateWithMods(score.Mods);

            double approachRate = CalculateApproachRate(score.Mods, difficulty.ApproachRate, CorrectedClockRate(clockRate));

            // Longer maps are worth more. "Longer" means how many hits there are approximately
            // We add some undetected actions approximated with 20% of the maximum combo
            double totalActions = ((CatchDifficultyAttributes)attributes).TotalActions + 0.2 * catchAttributes.MaxCombo;

            const double linear_pace = 0.34;
            const int cutoff = 1700;
            const double logarithmic_pace = 0.25;

            double lengthBonus =
                1.0 + linear_pace * Math.Min(1.0, totalActions / cutoff) +
                (totalActions > cutoff ? Math.Log10(totalActions / cutoff) * logarithmic_pace : 0.0);

            // Length bonus should depend on approachRate (including FlashLight): if it's high enough, it's either draining or it requires memorisation
            lengthBonus = Math.Pow(lengthBonus, 1.0 + Math.Max(0, approachRate - 10.5) / 2.0);

            if (score.Mods.Any(m => m is ModFlashlight))
                lengthBonus = Math.Pow(lengthBonus, 2.0);

            value *= Math.Pow(accuracy(), 5.5);

            if (score.Mods.Any(m => m is ModNoFail))
                value *= Math.Max(0.90, 1.0 - 0.02 * numMiss);

            double lengthBonusPP = value * (lengthBonus - 1.0);

            const double value_multiplier = 1.07;

            value *= value_multiplier;

            return new CatchPerformanceAttributes
            {
                LengthBonus = lengthBonusPP,
                Total = value + lengthBonusPP,
            };
        }

        public static double CalculateApproachRate(Mod[] mods, double approachRate, double correctedClockRate)
        {
            double preempt = IBeatmapDifficultyInfo.DifficultyRange(approachRate, 1800, 1200, 450) / correctedClockRate;

            const double flashlight_visibility_time = 203.125 * 0.77 / 440.0; // 203.125 pixels above catcher are visible at 200 combo; 440 pixels is the height of the visible playfield

            if (mods.Any(m => m is ModFlashlight))
                preempt *= flashlight_visibility_time;

            return preempt > 1200.0 ? (1800.0 - preempt) / 120.0 : (1200.0 - preempt) / 150.0 + 5.0;
        }

        public static double CorrectedClockRate(double clockRate) => 1.0 + (clockRate - 1.0) * 0.8; // AR9+DT is approximately AR10.15 after correction

        private double calculateValue(double sr) => Math.Pow(5.0 * Math.Max(1.0, sr / 0.0047) - 4.0, 2.0) / 100000.0;

        private double accuracy() => totalHits() == 0 ? 0 : Math.Clamp((double)totalSuccessfulHits() / totalHits(), 0, 1);
        private int totalHits() => num50 + num100 + num300 + numMiss + numKatu;
        private int totalSuccessfulHits() => num50 + num100 + num300;
        private int totalComboHits() => numMiss + num100 + num300;
    }
}
