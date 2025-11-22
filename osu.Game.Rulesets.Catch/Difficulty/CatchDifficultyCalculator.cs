// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Extensions.IEnumerableExtensions;
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

            List<double> startTimes = DifficultyHitObjects.Select(n => ((CatchDifficultyHitObject)n).StartTime).ToList();
            List<double> actionProbabilities = DifficultyHitObjects.Select(n => ((CatchDifficultyHitObject)n).MovementData.ActionProbability).ToList();
            List<double> precisionStrains = skills.OfType<Precision>().Single().GetObjectStrains().ToList();
            List<double> speedStrains = skills.OfType<Speed>().Single().GetObjectStrains().ToList();
            List<double> aimStrains = skills.OfType<Aim>().Single().GetObjectStrains().ToList();
            List<double> readingFactors = DifficultyHitObjects.Select(n => ((CatchDifficultyHitObject)n).ReadingData.CombinedReadingFactor).ToList();

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

            List<(double, double)> notes = startTimes.Zip(combinedStrains).ToList();

            nerfBeginning(notes);

            List<(double, double)> sorted = notes.OrderByDescending(n => n.Item2).ToList();

            var difficulty = beatmap.BeatmapInfo.Difficulty.Clone();
            mods.OfType<IApplicableToDifficulty>().ForEach(m => m.ApplyToDifficulty(difficulty));

            double approachRate = difficulty.ApproachRate;
            double circleSize = difficulty.CircleSize;

            double sr = calculateSr(notes, sorted);
            List<double> srWithMisses = new[] { 1, 2, 4, 7, 12 }.Select(m => calculateSr(notes, sorted, m)).ToList();

            double precision = calculateSr(startTimes, combineStrains(actionProbabilities, precisionStrains, zeroes, zeroes, readingFactors));
            double speed = calculateSr(startTimes, combineStrains(actionProbabilities, speedStrains, zeroes, zeroes, readingFactors));

            double adjustedApproachRate = CatchPerformanceCalculator.CalculateApproachRate(mods, approachRate, CatchPerformanceCalculator.CorrectedClockRate(clockRate));

            double approachRateFactor = 1.0;
            if (adjustedApproachRate > 9.5)
                approachRateFactor += 0.15 * (adjustedApproachRate - 9.5); // 15% for each AR above 9.5
            if (adjustedApproachRate > 10.2)
                approachRateFactor += 0.25 * (adjustedApproachRate - 10.2); // Additional 20% at AR 11, 42.5% total
            if (adjustedApproachRate > 11)
                approachRateFactor += 0.1 * (adjustedApproachRate - 11.0); // Additional bonus for FL (starting at around AR8) or Lazer's extended AR scale

            approachRateFactor = Math.Sqrt(approachRateFactor);

            double hiddenFactor = 1.0;
            double hiddenFullBonusSR = 4.5;

            if (mods.Any(m => m is ModHidden))
            {
                // Hidden gives almost nothing on max approach rate, and more the lower it is
                if (adjustedApproachRate <= 10.0)
                    hiddenFactor = Math.Sqrt(1.04 + 0.12 * (10.0 - adjustedApproachRate)); // 12% for each AR below 10
                else if (adjustedApproachRate > 10.0)
                    hiddenFactor = Math.Sqrt(1.0 + 0.04 * (11.0 - Math.Min(11.0, adjustedApproachRate))); // 4% at AR 10, 0% at AR 11

                hiddenFactor = 1.0 + (hiddenFactor - 1.0) * Math.Min(hiddenFullBonusSR, sr) / hiddenFullBonusSR; // Easier maps have lower AR by default; HD doesn't change much there
            }

            const double circle_size_power = 1.5;
            double circleSizeBonus = Math.Pow(Math.Max(0.0, circleSize - 3.0) / 10.0, circle_size_power) * 0.32;
            double circleSizeFactor = Math.Sqrt(1.0 + circleSizeBonus);

            CatchDifficultyAttributes attributes = new CatchDifficultyAttributes
            {
                StarRating = sr * approachRateFactor * circleSizeFactor * hiddenFactor,
                Mods = mods,
                MaxCombo = beatmap.GetMaxCombo(),
                TotalActions = totalActions,
                ApproachRateFactor = approachRateFactor,
                HiddenFactor = hiddenFactor,
                CircleSizeFactor = circleSizeFactor,
                PrecisionSR = precision,
                SpeedSR = speed,
                StarRatingWithMisses = srWithMisses.Select(s => s * approachRateFactor * circleSizeFactor * hiddenFactor).ToList(),
            };

            return attributes;
        }

        private void nerfBeginning(List<(double, double)> notes)
        {
            const double time_penalty_cutoff = 60000;
            const double time_penalty_power = 0.23;

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
        }

        private double calculateSr(List<double> startTimes, List<double> strains, int missCount = 0)
        {
            List<(double, double)> notes = startTimes.Zip(strains).ToList();

            nerfBeginning(notes);

            List<(double, double)> sorted = notes.OrderByDescending(n => n.Item2).ToList();

            return calculateSr(notes, sorted, missCount);
        }

        private double calculateSr(List<(double, double)> notes, List<(double, double)> sorted, int missCount = 0)
        {
            double sr = calculateDifficultyValue(notes, sorted, missCount);
            // sr = 3.52 * Math.Pow(sr, 0.8);

            sr *= difficulty_multiplier;

            sr = srScaler(sr);

            sr *= 0.98;

            return sr;
        }

        private double srScaler(double sr)
        {
            const double x0 = 1.1;
            const double y0 = 2.0;

            const double x1 = 4.1;
            const double y1 = 4.25;

            const double x2 = 6.55;
            const double y2 = 8.0;

            if (sr <= x0) return CatchPreprocessingUtils.Lerp(sr, 0.0, 0.0, x0, y0);
            if (sr <= x1) return CatchPreprocessingUtils.Lerp(sr, x0, y0, x1, y1);

            return CatchPreprocessingUtils.Lerp(sr, x1, y1, x2, y2);
        }

        /// <summary>
        /// Replicates StrainSkill behaviour with Strain Peaks.
        /// </summary>
        /// <param name="startTimes"></param>
        /// <param name="strains"></param>
        /// <param name="missCount"></param>
        /// <returns></returns>
        private double calculateDifficultyValue(List<(double, double)> notes, List<(double, double)> sorted, int missCount = 0)
        {
            const double decay_weight = 0.9;

            const double region = 500.0;
            const int limit = 15;

            const int miss_note_region = 5;
            const double miss_region = 500.0;

            List<(double, double)> filteredNotes = new List<(double, double)>();
            List<double> peakSeparateStrainTimes = new List<double>();

            foreach ((double time, double strain) note in sorted)
            {
                if (peakSeparateStrainTimes.Any(t => Math.Abs(t - note.time) <= miss_region))
                    continue;

                if (peakSeparateStrainTimes.Count < missCount)
                {
                    peakSeparateStrainTimes.Add(note.time);
                    continue;
                }

                filteredNotes.Add(note);
            }

            Stack<double> stack = new Stack<double>();
            List<(double, double)> skipSets = new List<(double, double)>();
            List<(double, double)> missSets = new List<(double, double)>();

            foreach (double missTime in peakSeparateStrainTimes)
            {
                int index = notes.FindIndex(n => n.Item1 == missTime);

                int lower = Math.Max(0, index - miss_note_region);
                int upper = Math.Min(notes.Count - 1, index + miss_note_region);

                missSets.Add((notes[lower].Item1, notes[upper].Item1));
            }

            double difficulty = 0.0;
            double weight = 0.9;
            int counter = 0;

            foreach ((double time, double strain) in filteredNotes)
            {
                if (skipSets.Count < limit)
                {
                    if (isTimeInSets(skipSets, time))
                    {
                        stack.Push(strain);
                        continue;
                    }

                    skipSets.Add((time - region, time + region));
                }

                if (skipSets.Count >= limit && stack.Count != 0)
                {
                    while (stack.Count != 0)
                    {
                        double newWeight = (counter == 1)
                            ? Math.Pow(weight, 1.5)
                            : Math.Pow(weight, Math.Max(1, counter));

                        difficulty += stack.Pop() * newWeight;
                        counter++;
                    }
                }

                if (isTimeInSets(missSets, time))
                    continue;

                double appliedWeight = (counter == 1)
                    ? Math.Pow(weight, 1.5)
                    : Math.Pow(weight, Math.Max(1, counter));

                difficulty += strain * appliedWeight;

                counter++;
            }

            while (stack.Count != 0)
            {
                double finalWeight = (counter == 1)
                    ? Math.Pow(weight, 1.5)
                    : Math.Pow(weight, Math.Max(1, counter));

                difficulty += stack.Pop() * finalWeight;
                counter++;
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
            return Math.Max(precisionStrain, speedStrain) + 0.8 * Math.Min(precisionStrain, speedStrain) + 0.3 * Math.Pow(precisionStrain, 0.25) * Math.Pow(speedStrain, 0.5);
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
                new SnapSpeed(mods),
                new BurstSpeed(mods),
                new ConsistencySpeed(mods),
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
