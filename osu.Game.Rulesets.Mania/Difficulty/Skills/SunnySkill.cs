// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mania.Difficulty.Evaluators;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.Difficulty.Skills
{
    public class SunnySkill : Skill
    {
        // Balancing constants
        public const double LAMBDA_N = 4.0;
        public const double LAMBDA_1 = 0.11;
        public const double LAMBDA_2 = 5.0;
        public const double LAMBDA_3 = 5.0;
        public const double LAMBDA_4 = 0.1;
        private const double w_0 = 0.37;
        private const double w_1 = 2.7;
        private const double w_2 = 0.27;
        private const double p_0 = 1.2;
        private const double p_1 = 1.5;

        private readonly int totalColumns;
        private readonly double granularity;
        private readonly double hitLeniency;

        private readonly List<ManiaDifficultyHitObject> noteList;
        private readonly List<ManiaDifficultyHitObject>[] perColumnNoteList;

        public SunnySkill(Mod[] mods, int totalColumns, double od, double granularity, int objectCount)
            : base(mods)
        {
            // To align with sunny's implementation
            hitLeniency = 0.3 * Math.Pow((64.5 - Math.Ceiling(od * 3.0)) / 500.0, 0.5);

            this.totalColumns = totalColumns;
            this.granularity = granularity;

            noteList = new List<ManiaDifficultyHitObject>(objectCount);

            perColumnNoteList = new List<ManiaDifficultyHitObject>[totalColumns];
            for (int i = 0; i < totalColumns; i++)
                perColumnNoteList[i] = new List<ManiaDifficultyHitObject>(objectCount / totalColumns);
        }

        // Mania difficulty hit objects are already sorted in the difficulty calculator, we just need to populate the lists.
        public override void Process(DifficultyHitObject current)
        {
            ManiaDifficultyHitObject currObj = (ManiaDifficultyHitObject)current;

            noteList.Add(currObj);
            perColumnNoteList[currObj.Column].Add(currObj);
        }

        public override double DifficultyValue()
        {
            if (noteList.Count <= 0)
                return 0;

            int noteCount = noteList.Count;
            int lnCount = noteList.Count(obj => obj.BaseObject is HoldNote);

            int mapLength = (int)noteList.Max(obj => obj.AdjustedEndTime) + 1;

            double[] j = SameColumnPressure.EvaluateSameColumnPressure(perColumnNoteList, totalColumns, mapLength, hitLeniency, granularity);
            double[] x = CrossColumnPressure.EvaluateCrossColumnPressure(perColumnNoteList, totalColumns, mapLength, hitLeniency, granularity);
            double[] p = PressingIntensity.EvaluatePressingIntensity(noteList, totalColumns, mapLength, hitLeniency, granularity);
            double[] a = Unevenness.EvaluateUnevenness(perColumnNoteList, totalColumns, mapLength, hitLeniency, granularity);
            double[] r = ReleaseFactor.EvaluateReleaseFactor(noteList, totalColumns, mapLength, hitLeniency, granularity);

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

                while (start < noteList.Count && noteList[start].AdjustedStartTime < t - 500 / granularity)
                {
                    start += 1;
                }

                while (end < noteList.Count && noteList[end].AdjustedStartTime < t + 500 / granularity)
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
            starRating *= (noteCount + 0.5 * lnCount) / (noteCount + 0.5 * lnCount + 60);

            // Buff high column counts
            starRating *= 0.88 + 0.03 * totalColumns;

            // rescale lower SRs
            if (starRating <= 2.00)
            {
                starRating = Math.Sqrt(starRating * 2);
            }

            return starRating;
        }
    }
}
