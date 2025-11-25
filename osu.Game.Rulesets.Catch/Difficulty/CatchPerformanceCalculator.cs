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

            double adjustedStarRating = numMiss switch
            {
                0 => catchAttributes.StarRating,
                1 => catchAttributes.SROneMiss,
                2 => catchAttributes.SRTwoMiss,
                var x when x < 4 => double.Lerp(catchAttributes.SRTwoMiss, catchAttributes.SRFourMiss, (x - 2.0) / (4.0 - 2.0)),
                var x when x < 7 => double.Lerp(catchAttributes.SRFourMiss, catchAttributes.SRSevenMiss, (x - 4.0) / (7.0 - 4.0)),
                var x => double.Lerp(catchAttributes.SRSevenMiss, catchAttributes.SRTwelveMiss, (x - 7.0) / (12.0 - 7.0)),
            };

            // Misscount-adjusted pathway - low combo scaling and misscount penalty but the SR of the map is lowered
            double withMiss = calculateValue(adjustedStarRating);

            withMiss *= Math.Pow(0.985, Math.Max(0, numMiss - 1));

            if (catchAttributes.MaxCombo > 0)
                withMiss *= Math.Min(0.8 + (score.MaxCombo / (double)catchAttributes.MaxCombo) * 0.2, 1.0);

            // Original pathway - moderate combo scaling and higher misscount penalty, no SR adjustment
            double original = calculateValue(catchAttributes.StarRating);

            original *= Math.Pow(0.97, Math.Max(0, numMiss - 1));

            if (catchAttributes.MaxCombo > 0)
                original *= Math.Min(Math.Pow(score.MaxCombo, 0.35) / Math.Pow(catchAttributes.MaxCombo, 0.35), 1.0);

            // We take the maximum of either pathway to ensure that ending chokes are not overly penalized from the misscount pathway
            // Afterwards, we apply a universal 0.925 non-FC penalty (first miss penalty)
            double value = Math.Max(original, withMiss);
            value = numMiss == 0 ? value : 0.925 * value;

            var difficulty = score.BeatmapInfo!.Difficulty.Clone();

            score.Mods.OfType<IApplicableToDifficulty>().ForEach(m => m.ApplyToDifficulty(difficulty));

            double clockRate = ModUtils.CalculateRateWithMods(score.Mods);

            double approachRate = CalculateApproachRate(score.Mods, difficulty.ApproachRate, CorrectedClockRate(clockRate));

            // Longer maps are worth more. "Longer" means how many hits there are approximately
            // We add some undetected actions approximated with 20% of the maximum combo
            double totalActions = ((CatchDifficultyAttributes)attributes).TotalActions + 0.2 * catchAttributes.MaxCombo;

            double lengthBonus =
                0.95 + 0.3 * Math.Min(1.0, totalActions / 1600.0) +
                (totalActions > 1600 ? Math.Log10(totalActions / 1600.0) * 0.35 : 0.0);

            // Length bonus should depend on approachRate (including FlashLight): if it's high enough, it's either draining or it requires memorisation
            lengthBonus = Math.Pow(lengthBonus, 1.0 + Math.Max(0, approachRate - 10.5) / 2.0);

            if (score.Mods.Any(m => m is ModFlashlight))
                lengthBonus = Math.Pow(lengthBonus, 1.8);

            value *= Math.Pow(accuracy(), 5.5);

            if (score.Mods.Any(m => m is ModNoFail))
                value *= Math.Max(0.90, 1.0 - 0.02 * numMiss);

            double lengthBonusPP = value * (lengthBonus - 1.0);

            value *= 1.07;

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

        private double calculateValue(double sr) => Math.Pow(5.0 * Math.Max(1.0, sr / 0.0049) - 4.0, 2.0) / 100000.0;

        private double accuracy() => totalHits() == 0 ? 0 : Math.Clamp((double)totalSuccessfulHits() / totalHits(), 0, 1);
        private int totalHits() => num50 + num100 + num300 + numMiss + numKatu;
        private int totalSuccessfulHits() => num50 + num100 + num300;
        private int totalComboHits() => numMiss + num100 + num300;
    }
}
