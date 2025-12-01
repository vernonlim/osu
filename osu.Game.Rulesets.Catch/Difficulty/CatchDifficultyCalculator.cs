// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Extensions.IEnumerableExtensions;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Catch.Beatmaps;
using osu.Game.Rulesets.Catch.Difficulty.Preprocessing;
using osu.Game.Rulesets.Catch.Difficulty.Preprocessing.Data;
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
            List<double> movementStrains = skills.OfType<Movement>().Single().GetObjectStrains().ToList();
            List<double> speedStrains = skills.OfType<Speed>().Single().GetObjectStrains().ToList();
            List<double> readingFactors = DifficultyHitObjects.Select(n => ((CatchDifficultyHitObject)n).ReadingData.CombinedReadingFactor).ToList();

            List<double> zeroes = Enumerable.Repeat(0.0, precisionStrains.Count).ToList();

            List<double> combinedStrains = combineStrains(actionProbabilities, precisionStrains, speedStrains, readingFactors);

            Func<PatternType, bool> streamChecker = type => type == PatternType.AcceleratingStream
                                                            || type == PatternType.ExtendedDirectionChange
                                                            || type == PatternType.FreeStream
                                                            || type == PatternType.HyperStream
                                                            || type == PatternType.PotentialStandstill;

            Func<PatternType, bool> jumpChecker = type => type == PatternType.Hyperjumps
                                                          || type == PatternType.JumpAfterHyperjump
                                                          || type == PatternType.Jumps
                                                          || type == PatternType.HyperjumpAfterJump;

            List<bool> isActionlessStreamNote =
                DifficultyHitObjects
                    .OfType<CatchDifficultyHitObject>()
                    .Select(n => streamChecker(n.MovementData.NotePattern)
                                 && n.MovementData.ActionProbability < 0.5).ToList();

            List<bool> isActionJumpNote =
                DifficultyHitObjects
                    .OfType<CatchDifficultyHitObject>()
                    .Select(n => jumpChecker(n.MovementData.NotePattern)
                                 && n.MovementData.ActionProbability > 0).ToList();

            List<bool> isActionNote =
                DifficultyHitObjects
                    .OfType<CatchDifficultyHitObject>()
                    .Select(n => n.MovementData.ActionProbability > 0).ToList();

            List<double> filteredMovementStrains =
                movementStrains
                    .Zip(isActionNote)
                    .Select(n => n.Second
                        ? Math.Sqrt(n.First) * 30
                        : 0).ToList();

            // double groupPeakStrain = -1;
            // int firstIndex = 0;
            //
            // for (int i = 0; i < filteredMovementStrains.Count; i++)
            // {
            //     if (filteredMovementStrains[i] == 0)
            //     {
            //         if (groupPeakStrain > 0)
            //         {
            //             for (int j = firstIndex; j < i; j++)
            //             {
            //                 filteredMovementStrains[j] = 0;
            //             }
            //
            //             double startTime = startTimes[firstIndex];
            //             double endTime = startTimes[i - 1];
            //
            //             double total = endTime - startTime;
            //
            //             int center = (firstIndex + i) / 2;
            //             filteredMovementStrains[center] = (1.0 - Math.Pow(Math.Max(200 - total, 0), 0.5) / Math.Pow(200, 0.5)) * groupPeakStrain;
            //         }
            //
            //         groupPeakStrain = -1;
            //         firstIndex = i + 1;
            //         continue;
            //     }
            //
            //     if (filteredMovementStrains[i] > groupPeakStrain)
            //         groupPeakStrain = filteredMovementStrains[i];
            // }
            //
            // if (groupPeakStrain > 0)
            // {
            //     for (int j = firstIndex; j < filteredMovementStrains.Count; j++)
            //         filteredMovementStrains[j] = 0;
            //
            //     int center = (firstIndex + filteredMovementStrains.Count) / 2;
            //     filteredMovementStrains[center] = groupPeakStrain;
            // }

            combinedStrains = combinedStrains
                              .Zip(filteredMovementStrains)
                              .Select(s => 0.7 * s.First + 0.3 * s.Second).ToList();

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

            double sr = calculateSr(notes, sorted);
            List<double> srWithMisses = new[] { 1, 2, 4, 7, 12 }.Select(m => calculateSr(notes, sorted, m)).ToList();

            double precision = calculateSr(startTimes, combineStrains(actionProbabilities, precisionStrains, zeroes, readingFactors));
            double speed = calculateSr(startTimes, combineStrains(actionProbabilities, speedStrains, zeroes, readingFactors));

            double adjustedApproachRate = CatchPerformanceCalculator.CalculateApproachRate(mods, approachRate, CatchPerformanceCalculator.CorrectedClockRate(clockRate));

            double approachRateFactor = 1.0;
            if (adjustedApproachRate > 9.5)
                approachRateFactor += 0.15 * (adjustedApproachRate - 9.5); // 15% for each AR above 9.5
            if (adjustedApproachRate > 10.2)
                approachRateFactor += 0.21 * (adjustedApproachRate - 10.2); // Bonus for high AR, 40.5% at AR11
            if (adjustedApproachRate > 11)
                approachRateFactor += 0.125 * (adjustedApproachRate - 11.0); // Additional bonus for FL (starting at around AR8) or Lazer's extended AR scale

            approachRateFactor = Math.Sqrt(approachRateFactor);

            double hiddenFactor = 1.0;
            const double hidden_full_bonus_sr = 4.5;

            if (mods.Any(m => m is ModHidden))
            {
                // Hidden gives almost nothing on max approach rate, and more the lower it is
                if (adjustedApproachRate <= 9.0)
                    hiddenFactor = Math.Sqrt(1.08 + 0.1 * (9.5 - adjustedApproachRate)); // 10% for each AR below 9.5
                else if (adjustedApproachRate <= 10.0)
                    hiddenFactor = Math.Sqrt(1.04 + 0.08 * (10.0 - adjustedApproachRate)); // 4% for AR10, 8% for AR9.5
                else if (adjustedApproachRate > 10.0)
                    hiddenFactor = Math.Sqrt(1.0 + 0.04 * (11.0 - Math.Min(11.0, adjustedApproachRate))); // 4% at AR 10, 0% at AR 11

                hiddenFactor = 1.0 + (hiddenFactor - 1.0) * Math.Min(hidden_full_bonus_sr, sr) / hidden_full_bonus_sr; // Easier maps have lower AR by default; HD doesn't change much there
            }

            CatchDifficultyAttributes attributes = new CatchDifficultyAttributes
            {
                StarRating = sr * approachRateFactor * hiddenFactor,
                // StarRating = skills.OfType<Movement>().Single().DifficultyValue() * 4.59,
                Mods = mods,
                MaxCombo = beatmap.GetMaxCombo(),
                TotalActions = totalActions,
                ApproachRateFactor = approachRateFactor,
                HiddenFactor = hiddenFactor,
                PrecisionSR = precision,
                SpeedSR = speed,
                StarRatingWithMisses = srWithMisses.Select(s => s * approachRateFactor * hiddenFactor).ToList(),
            };

            return attributes;
        }

        private void nerfBeginning(List<(double, double)> notes)
        {
            if (notes.Count < 2)
            {
                return;
            }

            const double time_penalty_cutoff = 60000;
            const double time_penalty_power = 0.2;

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
            const double weight = decay_weight;
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

        private List<double> combineStrains(List<double> actionProbabilities, List<double> precisionStrains, List<double> speedStrains, List<double> readingFactors)
        {
            List<double> combinedStrains = new List<double>();

            for (int i = 0; i < precisionStrains.Count; i++)
            {
                double actionProbability = actionProbabilities[i];
                double precisionStrain = precisionStrains[i];
                double speedStrain = speedStrains[i];
                double readingFactor = readingFactors[i];

                combinedStrains.Add(CalculateLocalStarRating(actionProbability, precisionStrain, speedStrain, readingFactor));
            }

            return combinedStrains;
        }

        public static double CalculatePartialLocalStarRating(double precisionStrain, double speedStrain)
        {
            return 1.05 * Math.Max(precisionStrain, speedStrain) + 0.85 * Math.Min(precisionStrain, speedStrain) + 0.18 * Math.Pow(precisionStrain, 0.25) * Math.Pow(speedStrain, 0.5);
            //return Math.Pow(Math.Pow(precisionStrain, alpha) + Math.Pow(speedStrain, alpha), 1 / alpha);
            //return precisionStrain + speedStrain;
            //return 1.1 * Math.Sqrt(Math.Pow(precisionStrain, 2) + Math.Pow(speedStrain, 2) - 0.2 * precisionStrain * speedStrain);
        }

        public static double CalculateLocalStarRating(double actionProbability, double precisionStrain, double speedStrain, double readingFactor)
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

            double previousStartTime = -1;

            // In 2B beatmaps, it is possible that a normal Fruit is placed in the middle of a JuiceStream.
            foreach (var hitObject in CatchBeatmap.GetPalpableObjects(beatmap.HitObjects))
            {
                // We want to only consider fruits that contribute to the combo.
                if (hitObject is Banana || hitObject is TinyDroplet)
                    continue;

                if (lastObject != null && hitObject.StartTime - previousStartTime > 2)
                    objects.Add(new CatchDifficultyHitObject(hitObject, lastObject, clockRate, catcherWidth, objects, noteObjects, objects.Count));

                lastObject = hitObject;
                previousStartTime = hitObject.StartTime;
            }

            if (objects.Count >= 2)
            {
                CatchMovementPreprocessor.Process(objects);
                CatchDifficultyPreprocessor.Process(objects);
                CatchReadingPreprocessor.Process(objects, circleSize, clockRate);
                CatchPreprocessingUtils.PopulateDifficultyData(noteObjects);
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
                new Movement(mods, clockRate)
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
