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
        private const double difficulty_multiplier = 0.0147;
        private const double large_droplet_buff = 1.01;
        private const double large_droplet_buff_hidden = 1.02;

        private float catcherWidth;
        private float circleSize;

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

            double totalActions = totalMovements;

            List<double> startTimes = DifficultyHitObjects.Select(n => ((CatchDifficultyHitObject)n).StartTime).ToList();
            List<double> actionProbabilities = DifficultyHitObjects.Select(n => ((CatchDifficultyHitObject)n).MovementData.ActionProbability).ToList();
            List<double> precisionStrains = skills.OfType<Precision>().Single().GetObjectStrains().ToList();
            List<double> speedStrains = skills.OfType<Speed>().Single().GetObjectStrains().ToList();

            List<double> readingFactors = DifficultyHitObjects.Select(n =>
            {
                CatchDifficultyHitObject note = ((CatchDifficultyHitObject)n);

                if (note.BaseObject is Droplet)
                {
                    note.ReadingData.CombinedReadingFactor *= mods.Any(m => m is ModHidden)
                        ? large_droplet_buff_hidden
                        : large_droplet_buff;
                }

                return ((CatchDifficultyHitObject)n).ReadingData.CombinedReadingFactor;
            }).ToList();

            List<double> highCSFactors = DifficultyHitObjects.Select(n => ((CatchDifficultyHitObject)n).ReadingData.HighCSFactor).ToList();

            // List<double> zeroes = Enumerable.Repeat(0.0, precisionStrains.Count).ToList();

            List<double> combinedStrains = combineStrains(actionProbabilities, precisionStrains, speedStrains, readingFactors, highCSFactors);

            // 2B Hotfix
            // for (int i = 1; i < combinedStrains.Count - 1; i++)
            // {
            //     if (startTimes[i] - startTimes[i - 1] <= 2)
            //     {
            //         combinedStrains[i + 1] = 0;
            //         combinedStrains[i] = 0;
            //         combinedStrains[i - 1] = 0;
            //     }
            // }

            List<(double, double)> notes = startTimes.Zip(combinedStrains).ToList();

            nerfBeginning(notes);

            List<(double, double)> sorted = notes.OrderByDescending(n => n.Item2).ToList();

            var difficulty = beatmap.BeatmapInfo.Difficulty.Clone();
            mods.OfType<IApplicableToDifficulty>().ForEach(m => m.ApplyToDifficulty(difficulty));

            double approachRate = difficulty.ApproachRate;

            double sr = calculateSr(startTimes, combinedStrains);
            double srBeginningNerfed = calculateSr(notes, sorted);
            // List<double> srWithMisses = new[] { 1, 2, 4, 7, 12 }.Select(m => calculateSr(notes, sorted, m)).ToList();

            // double precision = calculateSr(startTimes, combineStrains(actionProbabilities, precisionStrains, zeroes, readingFactors, highCSFactors));
            // double speed = calculateSr(startTimes, combineStrains(actionProbabilities, speedStrains, zeroes, readingFactors, highCSFactors));

            double adjustedApproachRate = CatchPerformanceCalculator.CalculateApproachRate(mods, approachRate, CatchPerformanceCalculator.CorrectedClockRate(clockRate));

            const double first_threshold = 9.2;
            const double second_threshold = 10.15; //adjusted AR for AR9+DT
            const double third_threshold = 11.0;

            const double first_power = 1.8;
            const double second_power = 1.2;
            const double first_constant = 0.1;
            const double second_constant = 0.34;
            const double third_constant = 0.15; // Additional bonus for FL (starting at around AR8) or Lazer's extended AR scale

            double approachRateFactor = 1.0;
            if (adjustedApproachRate >= first_threshold && adjustedApproachRate < second_threshold)
                approachRateFactor = 1.0 + Math.Pow((adjustedApproachRate - first_threshold) / (second_threshold - first_threshold), first_power) * first_constant;
            if (adjustedApproachRate >= second_threshold)
                approachRateFactor = 1.0 + first_constant + Math.Pow((adjustedApproachRate - second_threshold) / (third_threshold - second_threshold), second_power) * second_constant;
            if (adjustedApproachRate > third_threshold)
                approachRateFactor += third_constant * (adjustedApproachRate - 11.0);

            approachRateFactor = Math.Sqrt(approachRateFactor);


            // While for DT (clockRate > 1) we want to measure reaction time, for HT (clockRate < 1) we measure difference between moments of note disappearing and being caught
            // That's why we take original AR (instead of adjusted one that is higher) for calculating low AR bonus
            double minApproachRate = Math.Min(approachRate, adjustedApproachRate);
            const double low_ar_bonus = 0.015;
            const double min_ar_threshold = 7.0; // Threshold is chosen so that low AR doesn't affect range common for EZDT mod combination
            const double low_ar_full_bonus_sr = 5.0; // Easier maps have lower AR by default; low AR doesn't change difficulty much

            if (minApproachRate <= min_ar_threshold && !mods.Any(m => m is ModHidden) && !mods.Any(m => m is ModFlashlight)) // visual mods are affected by their respective bonuses
                approachRateFactor = Math.Sqrt(1.0 + low_ar_bonus * (min_ar_threshold - minApproachRate));
            approachRateFactor *= Math.Min(low_ar_full_bonus_sr, sr) / low_ar_full_bonus_sr;

            double hiddenFactor = 1.0;
            const double min_hidden_bonus = 0.01;
            const double threshold_linear = 8.0; // AR threshold between linear decrease and smooth (and less steep) curve
            const double hidden_growth = 0.235; // Value determining AR bonus at threshold_linear (and pace of growth of the function for higher AR values)
            const double hidden_power = 1.65;

            if (mods.Any(m => m is ModHidden))
            {
                // Hidden gives almost nothing on max approach rate, and more the lower it is
                if (minApproachRate >= 11.0)
                    hiddenFactor = 1.0 + min_hidden_bonus;
                if (minApproachRate >= threshold_linear && adjustedApproachRate < 11.0)
                    hiddenFactor = 1.0 + min_hidden_bonus + hidden_growth * Math.Pow(((11.0 - adjustedApproachRate) / (11.0 - threshold_linear)), hidden_power);
                if (minApproachRate < threshold_linear)
                    hiddenFactor = 1.0 + min_hidden_bonus + hidden_growth * (1.0 - hidden_power * (adjustedApproachRate - threshold_linear) / (11.0 - threshold_linear)); //tangent line to the function above at point threshold_linear

                hiddenFactor = Math.Sqrt(hiddenFactor); // SR-pp scaling
                hiddenFactor = 1.0 + (hiddenFactor - 1.0) * Math.Min(low_ar_full_bonus_sr, sr) / low_ar_full_bonus_sr;
            }

            CatchDifficultyAttributes attributes = new CatchDifficultyAttributes
            {
                StarRating = sr * approachRateFactor * hiddenFactor,
                Mods = mods,
                MaxCombo = beatmap.GetMaxCombo(),
                TotalActions = totalActions,
                ApproachRateFactor = approachRateFactor,
                HiddenFactor = hiddenFactor,
                // PrecisionSR = precision,
                // SpeedSR = speed,
                SRBeginningNerfed = srBeginningNerfed * approachRateFactor * hiddenFactor,
            };

            return attributes;
        }

        private void nerfBeginning(List<(double, double)> notes)
        {
            if (notes.Count < 2)
            {
                return;
            }

            const double time_penalty_cutoff = 60000; // No notes above the cutoff are affected
            const double time_penalty_power = 0.3;
            const double full_penalty = 0.5; // Penalty for the first note

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
                    strain *= full_penalty + (1.0 - full_penalty) * Math.Pow(time / time_penalty_cutoff, time_penalty_power);

                notes[i] = (time, strain);
            }
        }

        private double calculateSr(List<double> startTimes, List<double> strains, int missCount = 0, bool nerfBeginning = false)
        {
            List<(double, double)> notes = startTimes.Zip(strains).ToList();

            if (nerfBeginning)
                this.nerfBeginning(notes);

            List<(double, double)> sorted = notes.OrderByDescending(n => n.Item2).ToList();

            return calculateSr(notes, sorted, missCount);
        }

        private double calculateSr(List<(double, double)> notes, List<(double, double)> sorted, int missCount = 0)
        {
            double sr = calculateDifficultyValue(notes, sorted, missCount);

            sr *= difficulty_multiplier;

            sr = srScaler(sr);

            sr *= 1.06;

            return sr;
        }

        private double srScaler(double sr)
        {
            const double x0 = 0.87;
            const double y0 = 1.7;

            const double x1 = 4.23;
            const double y1 = 4.55;

            const double x2 = 6.5;
            const double y2 = 6.9;

            const double x3 = 7.5;
            const double y3 = 8.7;

            const double x4 = 8.5;
            const double y4 = 9.4;

            const double x5 = 9.0;
            const double y5 = 10.2;

            const double x6 = 9.5;
            const double y6 = 11.0;

            if (sr <= x0) return CatchPreprocessingUtils.Lerp(sr, 0.0, 0.0, x0, y0);
            if (sr <= x1) return CatchPreprocessingUtils.Lerp(sr, x0, y0, x1, y1);
            if (sr <= x2) return CatchPreprocessingUtils.Lerp(sr, x1, y1, x2, y2);
            if (sr <= x3) return CatchPreprocessingUtils.Lerp(sr, x2, y2, x3, y3);
            if (sr <= x4) return CatchPreprocessingUtils.Lerp(sr, x3, y3, x4, y4);
            if (sr <= x5) return CatchPreprocessingUtils.Lerp(sr, x4, y4, x5, y5);

            return CatchPreprocessingUtils.Lerp(sr, x5, y5, x6, y6);
        }

        /// <summary>
        /// Replicates StrainSkill behaviour with Strain Peaks.
        /// </summary>
        /// <param name="notes"></param>
        /// <param name="sorted"></param>
        /// <param name="missCount"></param>
        /// <returns></returns>
        private double calculateDifficultyValue(List<(double, double)> notes, List<(double, double)> sorted, int missCount = 0)
        {
            const double default_decay_weight = 0.9;
            double[] decayWeights = new[] { 0.9, 0.86, 0.81, 0.729, 0.6561 };

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
                        difficulty += stack.Pop() * calculateWeight();
                        counter++;
                    }
                }

                if (isTimeInSets(missSets, time))
                    continue;

                difficulty += strain * calculateWeight();

                counter++;
            }

            while (stack.Count != 0)
            {
                difficulty += stack.Pop() * calculateWeight();
                counter++;
            }

            return difficulty;

            double calculateWeight() => counter < decayWeights.Length
                ? decayWeights[counter]
                : Math.Pow(default_decay_weight, counter);
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

        private List<double> combineStrains(List<double> actionProbabilities, List<double> precisionStrains, List<double> speedStrains, List<double> readingFactors, List<double> highCSFactors)
        {
            List<double> combinedStrains = new List<double>();

            for (int i = 0; i < precisionStrains.Count; i++)
            {
                double actionProbability = actionProbabilities[i];
                double precisionStrain = precisionStrains[i];
                double speedStrain = speedStrains[i];
                double readingFactor = readingFactors[i];
                double highCSFactor = highCSFactors[i];

                combinedStrains.Add(CalculateLocalStarRating(actionProbability, precisionStrain, speedStrain, readingFactor, highCSFactor));
            }

            return combinedStrains;
        }

        public static double CalculatePartialLocalStarRating(double precisionStrain, double speedStrain)
        {
            const double max_constant = 1.0;
            const double min_constant = 0.8;
            const double correlation_constant = 0.15;
            return max_constant * Math.Max(precisionStrain, speedStrain) + min_constant * Math.Min(precisionStrain, speedStrain) + correlation_constant * Math.Pow(precisionStrain, 0.25) * Math.Pow(speedStrain, 0.5);
        }

        public static double CalculateLocalStarRating(double actionProbability, double precisionStrain, double speedStrain, double readingFactor, double highCSFactor)
        {
            double plsr = CalculatePartialLocalStarRating(precisionStrain, speedStrain);

            return plsr * readingFactor * highCSFactor;
            //return Math.Sqrt(Math.Pow(plsr, 2) + Math.Pow(1 - actionProbability, 2) * Math.Pow(aimStrain, 2)) * readingFactor;
        }

        protected override IEnumerable<DifficultyHitObject> CreateDifficultyHitObjects(IBeatmap beatmap, double clockRate)
        {
            const double simultaneous_time = 0.5;
            double normalizedCatcherWidth = catcherWidth / clockRate;

            PalpableCatchHitObject? lastObject = null;
            PalpableCatchHitObject? lastLastObject = null;

            List<DifficultyHitObject> objects = new List<DifficultyHitObject>();
            List<CatchDifficultyHitObject> noteObjects = new List<CatchDifficultyHitObject>();

            List<PalpableCatchHitObject> simultaneousObjects = new List<PalpableCatchHitObject>();

            // In 2B beatmaps, it is possible that a normal Fruit is placed in the middle of a JuiceStream.
            foreach (var hitObject in CatchBeatmap.GetPalpableObjects(beatmap.HitObjects))
            {
                // We want to only consider fruits that contribute to the combo.
                if (hitObject is Banana || hitObject is TinyDroplet)
                    continue;

                // If there are simultaneous notes, store them
                if (lastObject != null && hitObject.StartTime - lastObject.StartTime < simultaneous_time)
                {
                    if (simultaneousObjects.Count == 0)
                        simultaneousObjects.Add(lastObject);

                    simultaneousObjects.Add(hitObject);

                    continue;
                }

                // From the list of simultaneous notes, select a hyperdash if it exists, otherwise select any note
                if (simultaneousObjects.Count > 0)
                {
                    List<PalpableCatchHitObject> hyperObjects = simultaneousObjects.Where(o => o.HyperDash).ToList();

                    if (hyperObjects.Count > 0)
                    {
                        lastObject = hyperObjects[0];
                    }
                    else
                    {
                        lastObject = simultaneousObjects[0];
                    }

                    simultaneousObjects.Clear();
                }

                if (lastObject != null && lastLastObject != null)
                    objects.Add(new CatchDifficultyHitObject(lastObject, lastLastObject, clockRate, normalizedCatcherWidth, objects, noteObjects, objects.Count));

                lastLastObject = lastObject;
                lastObject = hitObject;
            }

            // Add the last object of the map
            if (lastObject != null && lastLastObject != null && lastObject.StartTime - lastLastObject.StartTime > simultaneous_time)
                objects.Add(new CatchDifficultyHitObject(lastObject, lastLastObject, clockRate, normalizedCatcherWidth, objects, noteObjects, objects.Count));

            if (objects.Count >= 2)
            {
                double frameTime = 1000.0 / 60.0 / clockRate;
                double playfieldBorder = 512.0 / clockRate;

                CatchMovementPreprocessor.Process(objects, normalizedCatcherWidth, clockRate, frameTime, playfieldBorder);
                CatchDifficultyPreprocessor.Process(objects, normalizedCatcherWidth, clockRate, frameTime, playfieldBorder);
                CatchReadingPreprocessor.Process(objects, circleSize, clockRate, frameTime);
                CatchPreprocessingUtils.PopulateDifficultyData(noteObjects, normalizedCatcherWidth, clockRate);
                // CatchPreprocessorTest.Process(objects, beatmap);
            }

            return objects;
        }

        protected override Skill[] CreateSkills(IBeatmap beatmap, Mod[] mods, double clockRate)
        {
            catcherWidth = Catcher.CalculateCatchWidth(beatmap.Difficulty);

            var difficulty = beatmap.BeatmapInfo.Difficulty.Clone();
            mods.OfType<IApplicableToDifficulty>().ForEach(m => m.ApplyToDifficulty(difficulty));

            circleSize = difficulty.CircleSize;

            return new Skill[]
            {
                new Precision(mods),
                new Speed(mods),
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
