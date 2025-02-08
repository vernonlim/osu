// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Mania.Difficulty
{
    public class ManiaPerformanceCalculator : PerformanceCalculator
    {
        private int countPerfect;
        private int countGreat;
        private int countGood;
        private int countOk;
        private int countMeh;
        private int countMiss;
        private double scoreAccuracy;

        public ManiaPerformanceCalculator()
            : base(new ManiaRuleset())
        {
        }

        protected override PerformanceAttributes CreatePerformanceAttributes(ScoreInfo score, DifficultyAttributes attributes)
        {
            var maniaAttributes = (ManiaDifficultyAttributes)attributes;

            countPerfect = score.Statistics.GetValueOrDefault(HitResult.Perfect);
            countGreat = score.Statistics.GetValueOrDefault(HitResult.Great);
            countGood = score.Statistics.GetValueOrDefault(HitResult.Good);
            countOk = score.Statistics.GetValueOrDefault(HitResult.Ok);
            countMeh = score.Statistics.GetValueOrDefault(HitResult.Meh);
            countMiss = score.Statistics.GetValueOrDefault(HitResult.Miss);
            scoreAccuracy = calculateCustomAccuracy();

            double multiplier = 1.0;

            if (score.Mods.Any(m => m is ModNoFail))
                multiplier *= 0.75;
            if (score.Mods.Any(m => m is ModEasy))
                multiplier *= 0.5;

            double difficultyValue = computeDifficultyValue(maniaAttributes);
            double totalValue = difficultyValue * multiplier;

            return new ManiaPerformanceAttributes
            {
                Difficulty = difficultyValue,
                Total = totalValue
            };
        }

        private double computeDifficultyValue(ManiaDifficultyAttributes attributes)
        {
            // The proportion of pp rewarded for a given accuracy.
            double proportion = calculatePerformanceProportion(scoreAccuracy);

            double difficultyValue = Math.Pow(Math.Max(attributes.StarRating - 0.15, 0.05), 2.2) // Star rating to pp curve
                                     * 1.1 / (1.0 + (1.5 / Math.Sqrt(totalHits))) // length bonus
                                     * 1.18 // arbitrary scaling factor
                                     * proportion; // multiply by the proportion

            return difficultyValue;
        }

        private double totalHits => countPerfect + countOk + countGreat + countGood + countMeh + countMiss;

        /// <summary>
        /// Accuracy used to weight judgements independently from the score's actual accuracy.
        /// </summary>
        private double calculateCustomAccuracy()
        {
            if (totalHits == 0)
                return 0;

            return (countPerfect * 320 + countGreat * 300 + countGood * 200 + countOk * 100 + countMeh * 50) / (totalHits * 320);
        }

        /// <summary>
        /// Converts an accuracy value to the portion of max pp rewarded.
        /// </summary>
        /// <param name="accuracy"></param>
        /// <returns></returns>
        private double calculatePerformanceProportion(double accuracy)
        {
            if (accuracy > 0.99)
                return (1.00 - 0.85) * (accuracy - 0.99) / 0.01 + 0.85;

            if (accuracy > 0.96)
                return (0.85 - 0.64) * (accuracy - 0.96) / 0.03 + 0.64;

            if (accuracy > 0.80)
                return (0.64 - 0.00) * (accuracy - 0.8) / 0.16;

            return 0;
        }
    }
}
