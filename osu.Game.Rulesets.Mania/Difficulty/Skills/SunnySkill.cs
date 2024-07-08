// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mania.Difficulty.Evaluators;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.Difficulty.Skills
{
    public class SunnySkill : Skill
    {
        // Balancing constants
        public const double LAMBDA_N = 4.0;
        public const double LAMBDA_1 = 0.11;
        public const double LAMBDA_2 = 5.0;
        public const double LAMBDA_3 = 8.0;
        public const double LAMBDA_4 = 0.1;
        private const double w_0 = 0.37;
        private const double w_1 = 2.7;
        private const double w_2 = 0.27;
        private const double p_0 = 1.2;
        private const double p_1 = 1.5;

        private readonly int totalColumns;

        private readonly List<ManiaDifficultyHitObject> noteList = new List<ManiaDifficultyHitObject>();

        public SunnySkill(Mod[] mods, int totalColumns)
            : base(mods)
        {
            this.totalColumns = totalColumns;
        }

        // Mania difficulty hit objects are already sorted in the difficulty calculator, we just need to populate the lists.
        public override void Process(DifficultyHitObject current)
        {
            ManiaDifficultyHitObject currObj = (ManiaDifficultyHitObject)current;

            noteList.Add(currObj);
        }

        public override double DifficultyValue()
        {
            int mapLength = (int)noteList.Last().EndTime + 1;

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Restart();

            double[] j = SameColumnPressure.EvaluateSameColumnPressure(noteList, totalColumns);
            stopwatch.Stop();
            Console.WriteLine($"j: {stopwatch.Elapsed.TotalMilliseconds}");
            stopwatch.Restart();
            double[] x = CrossColumnPressure.EvaluateCrossColumnPressure(noteList, totalColumns);
            stopwatch.Stop();
            Console.WriteLine($"x: {stopwatch.Elapsed.TotalMilliseconds}");
            stopwatch.Restart();
            double[] p = PressingIntensity.EvaluatePressingIntensity(noteList, totalColumns);
            stopwatch.Stop();
            Console.WriteLine($"p: {stopwatch.Elapsed.TotalMilliseconds}");
            stopwatch.Restart();
            double[] a = Unevenness.EvaluateUnevenness(noteList, totalColumns);
            stopwatch.Stop();
            Console.WriteLine($"a: {stopwatch.Elapsed.TotalMilliseconds}");
            stopwatch.Restart();
            double[] r = ReleaseFactor.EvaluateReleaseFactor(noteList, totalColumns);
            stopwatch.Stop();
            Console.WriteLine($"r: {stopwatch.Elapsed.TotalMilliseconds}");
            stopwatch.Restart();

            double sum1 = 0;
            double sum2 = 0;

            int start = 0;
            int end = 0;

            for (int t = 0; t < mapLength; t++)
            {
                // Clamp each pressure value to [0-inf]
                j[t] = Math.Max(0, j[t]);
                x[t] = Math.Max(0, x[t]);
                p[t] = Math.Max(0, p[t]);
                a[t] = Math.Max(0, a[t]);
                r[t] = Math.Max(0, r[t]);

                while (start < noteList.Count && noteList[start].StartTime < t - 500)
                {
                    start += 1;
                    end += 1;
                }

                while (end < noteList.Count && noteList[end].StartTime < t + 500)
                {
                    end += 1;
                }

                int c = end - start;

                double strain = Math.Pow(w_0 * Math.Pow(Math.Pow(a[t], 1.0 / 2.0) * j[t], 1.5) + (1 - w_0) * Math.Pow(Math.Pow(a[t], 2.0 / 3.0) * (p[t] + r[t]), 1.5), 2.0 / 3.0);
                double twist = x[t] / (x[t] + strain + 1);

                double deez = w_1 * Math.Pow(strain, 1.0 / 2.0) * Math.Pow(twist, p_1) + strain * w_2;

                sum1 += Math.Pow(deez, LAMBDA_N) * c;
                sum2 += c;
            }

            double starRating = Math.Pow(sum1 / sum2, 1.0 / LAMBDA_N);
            starRating = Math.Pow(starRating, p_0) / Math.Pow(8, p_0) * 8;

            // Nerf short maps
            starRating = starRating * noteList.Count / (noteList.Count + 50);

            stopwatch.Stop();
            Console.WriteLine($"sr: {stopwatch.Elapsed.TotalMilliseconds}");

            // Buff high column counts
            return starRating * (0.88 + 0.03 * totalColumns);
        }
    }
}
