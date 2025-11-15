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

            double starRating = numMiss switch
            {
                0 => catchAttributes.StarRating,
                1 => catchAttributes.SROneMiss,
                2 => catchAttributes.SRTwoMiss,
                var x when x < 4 => double.Lerp(catchAttributes.SRTwoMiss, catchAttributes.SRFourMiss, (x - 2.0) / (4.0 - 2.0)),
                var x when x < 7 => double.Lerp(catchAttributes.SRFourMiss, catchAttributes.SRSevenMiss, (x - 4.0) / (7.0 - 4.0)),
                var x => double.Lerp(catchAttributes.SRSevenMiss, catchAttributes.SRTwelveMiss, (x - 7.0) / (12.0 - 7.0)),
            };

            // We are heavily relying on aim in catch the beat
            double value = Math.Pow(5.0 * Math.Max(1.0, starRating / 0.0049) - 4.0, 2.0) / 100000.0;

            // Longer maps are worth more. "Longer" means how many hits there are approximately
            // We add some undetected actions approximated with 15% of the maximum combo
            double totalActions = ((CatchDifficultyAttributes)attributes).TotalActions + 0.15 * catchAttributes.MaxCombo;

            double lengthBonus =
                0.86 + 0.57 * Math.Min(1.0, totalActions / 1600.0) +
                (totalActions > 1600 ? Math.Log10(totalActions / 1600.0) * 0.3 : 0.0);
            value *= lengthBonus;

            value *= Math.Pow(0.98, Math.Max(0, numMiss - 5));

            // Combo scaling
            if (catchAttributes.MaxCombo > 0)
                value *= Math.Min(Math.Pow(score.MaxCombo, 0.35) / Math.Pow(catchAttributes.MaxCombo, 0.35), 1.0);

            var difficulty = score.BeatmapInfo!.Difficulty.Clone();

            score.Mods.OfType<IApplicableToDifficulty>().ForEach(m => m.ApplyToDifficulty(difficulty));

            double clockRate = ModUtils.CalculateRateWithMods(score.Mods);

            double preempt = IBeatmapDifficultyInfo.DifficultyRange(difficulty.ApproachRate, 1800, 1200, 450) / clockRate;

            double flashlight_visibility_time = 203.125 * 0.77 / 440.0; //203.125 pixels above are visible at 200 combo; 440 pixels is the height of the visible playfield

            if (score.Mods.Any(m => m is ModFlashlight))
                preempt *= flashlight_visibility_time;

            double approachRate = preempt > 1200.0 ? -(preempt - 1800.0) / 120.0 : -(preempt - 1200.0) / 150.0 + 5.0;

            double approachRateFactor = 1.0;
            if (approachRate > 9.0)
                approachRateFactor += 0.1 * (approachRate - 9.0); // 10% for each AR above 9
            if (approachRate > 10.2)
                approachRateFactor += 0.25 * (approachRate - 10.2); // Additional 20% at AR 11, 40% total
            if (approachRate > 11)
                approachRateFactor += 0.1 * (approachRate - 11.0); // Additional bonus for FL (starting at around AR8)

            value *= approachRateFactor;

            if (score.Mods.Any(m => m is ModHidden))
            {
                // Hiddens gives almost nothing on max approach rate, and more the lower it is
                if (approachRate <= 10.0)
                    value *= 1.04 + 0.12 * (10.0 - approachRate); // 7.5% for each AR below 10
                else if (approachRate > 10.0)
                    value *= 1.01 + 0.04 * (11.0 - Math.Min(11.0, approachRate)); // 5% at AR 10, 1% at AR 11
            }

            if (score.Mods.Any(m => m is ModFlashlight))
                value *= 1.1 * lengthBonus;

            double circleSize = difficulty.CircleSize;
            double circleSizePower = 1.5;
            double circleSizeBonus = Math.Pow(Math.Max(0, circleSize - 3.0) / 10, circleSizePower) * 0.32;

            value *= 1 + circleSizeBonus;

            value *= Math.Pow(accuracy(), 5.5);

            if (score.Mods.Any(m => m is ModNoFail))
                value *= Math.Max(0.90, 1.0 - 0.02 * numMiss);

            return new CatchPerformanceAttributes
            {
                Total = value
            };
        }

        private double accuracy() => totalHits() == 0 ? 0 : Math.Clamp((double)totalSuccessfulHits() / totalHits(), 0, 1);
        private int totalHits() => num50 + num100 + num300 + numMiss + numKatu;
        private int totalSuccessfulHits() => num50 + num100 + num300;
        private int totalComboHits() => numMiss + num100 + num300;
    }
}
