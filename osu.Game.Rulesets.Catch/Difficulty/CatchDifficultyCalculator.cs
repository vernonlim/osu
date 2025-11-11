// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Catch.Beatmaps;
using osu.Game.Rulesets.Catch.Difficulty.Preprocessing;
using osu.Game.Rulesets.Catch.Difficulty.Preprocessing.Preprocessors;
using osu.Game.Rulesets.Catch.Difficulty.Preprocessing.Utils;
using osu.Game.Rulesets.Catch.Difficulty.Skills;
using osu.Game.Rulesets.Catch.Mods;
using osu.Game.Rulesets.Catch.Objects;
using osu.Game.Rulesets.Catch.UI;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Catch.Difficulty
{
    public class CatchDifficultyCalculator : DifficultyCalculator
    {
        private const double difficulty_multiplier = 0.015;

        private float catcherWidth;

        public override int Version => 20250306;

        public CatchDifficultyCalculator(IRulesetInfo ruleset, IWorkingBeatmap beatmap)
            : base(ruleset, beatmap)
        {
        }

        protected override DifficultyAttributes CreateDifficultyAttributes(IBeatmap beatmap, Mod[] mods, Skill[] skills, double clockRate)
        {
            if (beatmap.HitObjects.Count == 0)
                return new CatchDifficultyAttributes { Mods = mods };

            double totalMovements = DifficultyHitObjects
                                    .Select(n => (CatchDifficultyHitObject)n)
                                    .Select(n => n.MovementData.ActionProbability)
                                    .Sum();

            double totalAims = DifficultyHitObjects
                               .Select(n =>
                                   (CatchDifficultyHitObject)n)
                               .Count(n =>
                                   n.MovementData.ActionProbability < 0.03 && n.MovementData.NoteAim != null);

            double totalActions = totalMovements + totalAims;

            int hyperWalkCount = DifficultyHitObjects.Count(n => ((CatchDifficultyHitObject)n).MovementData.IsHyperWalk);

            List<double> startTimes = DifficultyHitObjects.Select(n => ((CatchDifficultyHitObject)n).StartTime).ToList();
            List<double> actionProbabilities = DifficultyHitObjects.Select(n => ((CatchDifficultyHitObject)n).MovementData.ActionProbability).ToList();
            List<double> precisionStrains = skills.OfType<Precision>().Single().GetObjectStrains().ToList();
            List<double> speedStrains = skills.OfType<Speed>().Single().GetObjectStrains().ToList();
            List<double> aimStrains = skills.OfType<Aim>().Single().GetObjectStrains().ToList();
            List<double> readingFactors = DifficultyHitObjects.Select(n => ((CatchDifficultyHitObject)n).ReadingData.CombinedReadingFactor).ToList();

            List<double> sameSpeedStrains = skills.OfType<SameDirectionSpeed>().Single().GetObjectStrains().ToList();
            List<double> delayedSameSpeedStrains = skills.OfType<DelayedSameDirectionSpeed>().Single().GetObjectStrains().ToList();
            List<double> alternatingSpeedStrains = skills.OfType<AlternatingSpeed>().Single().GetObjectStrains().ToList();

            List<double> zeroes = Enumerable.Repeat(0.0, precisionStrains.Count).ToList();

            List<double> combinedStrains = combineStrains(actionProbabilities, precisionStrains, speedStrains, aimStrains, readingFactors);

            // 2B Hotfix
            for (int i = 1; i < combinedStrains.Count - 1; i++)
            {
                if (startTimes[i] - startTimes[i - 1] <= 2)
                {
                    combinedStrains[i + 1] = 0;
                    combinedStrains[i] = 0;
                    combinedStrains[i - 1] = 0;
                }
            }

            double sr = calculateSr(startTimes, combinedStrains);

            double precision = calculateSr(startTimes, combineStrains(actionProbabilities, precisionStrains, zeroes, zeroes, readingFactors));
            double speed = calculateSr(startTimes, combineStrains(actionProbabilities, speedStrains, zeroes, zeroes, readingFactors));
            double aim = calculateSr(startTimes, combineStrains(actionProbabilities, zeroes, zeroes, aimStrains, readingFactors));
            double sameSpeed = calculateSr(startTimes, combineStrains(actionProbabilities, zeroes, sameSpeedStrains, zeroes, readingFactors));
            double delayedSameSpeed = calculateSr(startTimes, combineStrains(actionProbabilities, zeroes, delayedSameSpeedStrains, zeroes, readingFactors));
            double alternatingSpeed = calculateSr(startTimes, combineStrains(actionProbabilities, zeroes, alternatingSpeedStrains, zeroes, readingFactors));

            CatchDifficultyAttributes attributes = new CatchDifficultyAttributes
            {
                StarRating = sr,
                Mods = mods,
                MaxCombo = beatmap.GetMaxCombo(),
                TotalActions = totalActions,
                PrecisionSR = precision,
                SpeedSR = speed,
                SameDirectionSpeedSR = sameSpeed,
                DelayedSameDirectionSpeedSR = delayedSameSpeed,
                AlternatingSpeedSR = alternatingSpeed,
                AimSR = aim,
            };

            return attributes;
        }

        private double calculateSr(List<double> startTimes, List<double> strains)
        {
            double sr = calculateDifficultyValue(startTimes, strains);
            // sr = 3.52 * Math.Pow(sr, 0.8);

            sr *= difficulty_multiplier;

            sr = srScaler(sr);

            sr *= 1.015;

            return sr;
        }

        private double srScaler(double sr)
        {
            const double x0 = 1.0;
            const double y0 = 1.9;

            const double x1 = 4.05;
            const double y1 = 4.5;

            const double x2 = 6.0;
            const double y2 = 7.1;

            const double x3 = 8.0;
            const double y3 = 10.0;

            if (sr <= x0) return lerp(sr, 0.0, 0.0, x0, y0);
            if (sr <= x1) return lerp(sr, x0, y0, x1, y1);
            if (sr <= x2) return lerp(sr, x1, y1, x2, y2);

            return lerp(sr, x2, y2, x3, y3);
        }

        private static double lerp(double x, double x0, double y0, double x1, double y1)
            => y0 + (x - x0) * (y1 - y0) / (x1 - x0);

        /// <summary>
        /// Replicates StrainSkill behaviour with Strain Peaks.
        /// </summary>
        /// <param name="strains"></param>
        /// <param name="accuracy"></param>
        /// <returns></returns>
        private double calculateDifficultyValue(List<double> startTimes, List<double> strains, double accuracy = 1.0)
        {
            const double decay_weight = 0.9;

            const double region = 500.0;
            const int limit = 15;

            const double time_penalty_cutoff = 45000;
            const double time_penalty_power = 0.2;

            List<(double, double)> notes = startTimes.Zip(strains).ToList();

            double firstNoteStartTime = notes[0].Item1;

            for (int i = 0; i < notes.Count; i++)
            {
                notes[i] = (notes[i].Item1 - firstNoteStartTime, notes[i].Item2);
            }

            for (int i = 0; i < notes.Count; i++)
            {
                double time = notes[i].Item1;
                double strain = notes[i].Item2;

                if (time < time_penalty_cutoff)
                    strain *= Math.Pow(time / time_penalty_cutoff, time_penalty_power);

                notes[i] = (time, strain);
            }

            List<(double, double)> sorted = notes.OrderByDescending(x => x.Item2).ToList();

            double difficulty = 0.0;
            double weight = 1.0;

            Stack<double> stack = new Stack<double>();
            List<(double, double)> sets = new List<(double, double)>();

            foreach ((double time, double strain) in sorted)
            {
                if (sets.Count < limit)
                {
                    if (isTimeInSets(sets, time))
                    {
                        stack.Push(strain);
                        continue;
                    }

                    sets.Add((time - region, time + region));
                }

                if (sets.Count >= limit && stack.Count != 0)
                {
                    while (stack.Count != 0)
                    {
                        difficulty += stack.Pop() * weight;
                        weight *= decay_weight;
                    }
                }

                difficulty += strain * weight;
                weight *= decay_weight;
            }

            while (stack.Count != 0)
            {
                difficulty += stack.Pop() * weight;
                weight *= decay_weight;
            }

            return difficulty;
        }

        private bool isTimeInSets(List<(double, double)> sets, double time)
        {
            foreach ((double start, double end) set in sets)
            {
                if (time >= set.start && time <= set.end)
                {
                    return true;
                }
            }

            return false;
        }

        private List<double> combineStrains(List<double> actionProbabilities, List<double> precisionStrains, List<double> speedStrains, List<double> aimStrains, List<double> readingFactors)
        {
            List<double> combinedStrains = new List<double>();

            for (int i = 0; i < precisionStrains.Count; i++)
            {
                double actionProbability = actionProbabilities[i];
                double precisionStrain = precisionStrains[i];
                double speedStrain = speedStrains[i];
                double aimStrain = aimStrains[i];
                double readingFactor = readingFactors[i];

                combinedStrains.Add(CalculateLocalStarRating(actionProbability, precisionStrain, speedStrain, aimStrain, readingFactor));
            }

            return combinedStrains;
        }

        public static double CalculatePartialLocalStarRating(double precisionStrain, double speedStrain)
        {
            return 0.9 * Math.Max(precisionStrain, speedStrain) + 0.7 * Math.Min(precisionStrain, speedStrain);
            //return Math.Pow(Math.Pow(precisionStrain, alpha) + Math.Pow(speedStrain, alpha), 1 / alpha);
            //return precisionStrain + speedStrain;
            //return 1.1 * Math.Sqrt(Math.Pow(precisionStrain, 2) + Math.Pow(speedStrain, 2) - 0.2 * precisionStrain * speedStrain);
        }

        public static double CalculateLocalStarRating(double actionProbability, double precisionStrain, double speedStrain, double aimStrain, double readingFactor)
        {
            double plsr = CalculatePartialLocalStarRating(precisionStrain, speedStrain);

            return plsr * readingFactor;
            //return Math.Sqrt(Math.Pow(plsr, 2) + Math.Pow(1 - actionProbability, 2) * Math.Pow(aimStrain, 2)) * readingFactor;
        }

        protected override IEnumerable<DifficultyHitObject> CreateDifficultyHitObjects(IBeatmap beatmap, double clockRate)
        {
            CatchHitObject? lastObject = null;

            List<DifficultyHitObject> objects = new List<DifficultyHitObject>();
            List<CatchDifficultyHitObject> noteObjects = new List<CatchDifficultyHitObject>();

            // In 2B beatmaps, it is possible that a normal Fruit is placed in the middle of a JuiceStream.
            foreach (var hitObject in CatchBeatmap.GetPalpableObjects(beatmap.HitObjects))
            {
                // We want to only consider fruits that contribute to the combo.
                if (hitObject is Banana || hitObject is TinyDroplet)
                    continue;

                if (lastObject != null)
                    objects.Add(new CatchDifficultyHitObject(hitObject, lastObject, clockRate, catcherWidth, objects, noteObjects, objects.Count));

                lastObject = hitObject;
            }

            CatchMovementPreprocessor.Process(objects);
            CatchDifficultyPreprocessor.Process(objects);
            CatchReadingPreprocessor.Process(objects);
            CatchPreprocessingUtils.PopulateDifficultyData(noteObjects);
            // CatchPreprocessorTest.Process(objects, beatmap);

            return objects;
        }

        protected override Skill[] CreateSkills(IBeatmap beatmap, Mod[] mods, double clockRate)
        {
            catcherWidth = Catcher.CalculateCatchWidth(beatmap.Difficulty);

            return new Skill[]
            {
                new Aim(mods),
                new Precision(mods),
                new Speed(mods),
                new AlternatingSpeed(mods),
                new SameDirectionSpeed(mods),
                new DelayedSameDirectionSpeed(mods),
                new PartialLocalStarRating(mods),
                new LocalStarRating(mods),
            };
        }

        protected override Mod[] DifficultyAdjustmentMods => new Mod[]
        {
            new CatchModDoubleTime(),
            new CatchModHalfTime(),
            new CatchModHardRock(),
            new CatchModEasy(),
        };
    }
}
