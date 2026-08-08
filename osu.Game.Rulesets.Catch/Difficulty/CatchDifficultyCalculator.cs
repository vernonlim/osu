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
        private const double large_droplet_buff = 1.0;
        private const double large_droplet_buff_hidden = 1.0;

        private readonly CatchDifficultyConstants tuning;

        private float catcherWidth;
        private float circleSize;

        public override int Version => 20250306;

        public CatchDifficultyCalculator(IRulesetInfo ruleset, IWorkingBeatmap beatmap, CatchDifficultyConstants? tuning = null)
            : base(ruleset, beatmap)
        {
            this.tuning = tuning ?? CatchDifficultyConstants.Default;
        }

        protected override DifficultyAttributes CreateDifficultyAttributes(IBeatmap beatmap, Mod[] mods, Skill[] skills, double clockRate)
        {
            if (beatmap.HitObjects.Count == 0)
                return new CatchDifficultyAttributes { Mods = mods, Tuning = tuning };

            double totalMovements = DifficultyHitObjects
                                    .Select(n => (CatchDifficultyHitObject)n)
                                    .Select(n => n.MovementData.ActionProbability)
                                    .Sum();

            double totalActions = totalMovements;

            List<double> startTimes = DifficultyHitObjects.Select(n => ((CatchDifficultyHitObject)n).StartTime).ToList();
            List<double> distanceBonuses = DifficultyHitObjects.Select(n => ((CatchDifficultyHitObject)n).MovementData.DistanceBonus).ToList();
            List<double> actionProbabilities = DifficultyHitObjects.Select(n => ((CatchDifficultyHitObject)n).MovementData.ActionProbability).ToList();
            List<double> precisionStrains = skills.OfType<Precision>().Single().GetObjectDifficulties().ToList();
            List<double> speedStrains = skills.OfType<Speed>().Single().GetObjectDifficulties().ToList();

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

            List<double> combinedStrains = combineStrains(actionProbabilities, precisionStrains, speedStrains, distanceBonuses, readingFactors, highCSFactors);

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

            double sr = calculateSr(notes, sorted);
            // List<double> srWithMisses = new[] { 1, 2, 4, 7, 12 }.Select(m => calculateSr(notes, sorted, m)).ToList();

            // double precision = calculateSr(startTimes, combineStrains(actionProbabilities, precisionStrains, zeroes, readingFactors, highCSFactors));
            // double speed = calculateSr(startTimes, combineStrains(actionProbabilities, speedStrains, zeroes, readingFactors, highCSFactors));


            // AR calculations
            // AR bonus and HD bonus contribute to the star rating; squared bonuses contribute to the pp value
            // adjustedApproachRate takes mods into account (DT, HT, FL), more on that in PerformanceCalculator
            double originalApproachRate = difficulty.ApproachRate;
            double approachRate = CatchPerformanceCalculator.CalculateApproachRate(mods, originalApproachRate, clockRate); // original AR including clockrate
            double adjustedApproachRate = CatchPerformanceCalculator.CalculateApproachRate(mods, originalApproachRate, CatchPerformanceCalculator.CorrectedClockRate(clockRate)); //AR artificially modified by changed clockrate or FL

            const double first_power = 1.7;
            const double second_power = 1.15;
            const double first_constant = 0.11;
            double second_constant = tuning.ApproachRateSecondConstant;

            double approachRateFactor = 1.0;
            if (adjustedApproachRate >= 9.0 && adjustedApproachRate < 10.15)
                approachRateFactor = 1.0 + Math.Pow((adjustedApproachRate - 9.0) / 1.15, first_power) * first_constant;
            if (adjustedApproachRate >= 10.15)
                approachRateFactor = 1.0 + first_constant + Math.Pow((adjustedApproachRate - 10.15) / 0.85, second_power) * second_constant;
            if (adjustedApproachRate > 11.0)
                approachRateFactor = 1.0 + first_constant + second_constant; // max bonus at AR11 (for extended Lazer's scale/for FL to avoid breaking further calculations)
            approachRateFactor = Math.Sqrt(approachRateFactor);


            // Low AR bonus: while for DT (clockRate > 1) we want to measure reaction time,
                // for HT (clockRate < 1) we measure difference between moments of note disappearing and being caught.
                    // That's why we take the original AR (instead of adjusted one that is higher) for calculating the low AR bonus.
                // Moreover, we are no longer adding any bonus below AR0.
            double minApproachRate = Math.Min(approachRate, adjustedApproachRate);
            const double low_ar_bonus = 0.012;
            const double min_ar_threshold = 7.0; // Threshold is chosen so that low AR doesn't affect range common for EZDT mod combination

            if (!mods.Any(m => m is ModHidden))
            {
                if (minApproachRate < min_ar_threshold) // hidden is affected by a separate bonus
                {
                    if (minApproachRate >= 5.0)
                        approachRateFactor = Math.Sqrt(1.0 + low_ar_bonus * (min_ar_threshold - minApproachRate)); // 2.4% for AR5
                    else // Pace of time->AR function is slower below AR5
                        approachRateFactor = Math.Sqrt(1.024 + low_ar_bonus * 1.25 * (5.0 - minApproachRate)); //9.9% for AR0
                }
            }


            // HD bonus: hidden gives almost nothing on max approach rate, and more the lower it is.
                // HD bonus for low AR (below min_ar_threshold) is always greater than low AR bonus for NM. Note that HD has been excluded from low AR bonus.
            double hiddenFactor = 1.0;
            const double min_hidden_bonus = 0.01;
            const double hidden_growth = 0.25; // Value determining AR bonus at threshold_linear (and pace of growth of the function for higher AR values)
            const double hidden_power = 1.65;

            if (mods.Any(m => m is ModHidden))
            {
                if (minApproachRate >= 11.0)
                    hiddenFactor = 1.01;
                if (minApproachRate >= 8.0 && minApproachRate < 11.0)
                    hiddenFactor = 1.01 + hidden_growth * Math.Pow(((11.0 - minApproachRate) / 3.0), hidden_power);
                if (minApproachRate < 8.0 && minApproachRate >= 5.0)
                    hiddenFactor = 1.01 + hidden_growth * (1.0 + hidden_power * (8.0 - minApproachRate) / 3.0); //tangent line to the function above at point threshold_linear
                if (minApproachRate < 5.0) // Pace of time->AR function is slower below AR5
                    hiddenFactor = 1.01 + hidden_growth * (1.0 + hidden_power * (3.0 + 1.25 * (5.0 - minApproachRate)) / 3.0);

                hiddenFactor = Math.Sqrt(hiddenFactor); // SR-pp scaling
            }


            // double lowARFullBonusSRRatio = Math.Min(low_ar_full_bonus_sr, sr) / low_ar_full_bonus_sr;
            // if (minApproachRate <= min_ar_threshold)
            //     approachRateFactor = 1.0 + (approachRateFactor - 1.0) * lowARFullBonusSRRatio;
            // hiddenFactor = 1.0 + (hiddenFactor - 1.0) * lowARFullBonusSRRatio;

            double maxLowARFactor = 1.0 + (Math.Sqrt(1.0 + low_ar_bonus * min_ar_threshold) - 1.0); // Max at AR0


            // FL (AR) bonus: the higher AR is, the harder flashlight is.
                // Length-based bonuses for FL can be found in PerformanceCalculator.
            // When FL (or HDFL) is applied, we're modifying approathRateFactor accordingly.
            if (mods.Any(m => m is ModFlashlight))
            {
                double flashlightApproachRateFactor = 1.0;
                const double base_fl_bonus = 0.04;
                const double first_fl_constant = 0.02;
                const double second_fl_constant = 0.05;

                if (adjustedApproachRate >= 0.0)
                    flashlightApproachRateFactor = 1.0 + first_fl_constant * adjustedApproachRate;
                if (adjustedApproachRate >= 8.0)
                    flashlightApproachRateFactor += second_fl_constant * (Math.Min(12.0, adjustedApproachRate) - 8.0);
                flashlightApproachRateFactor *= 1.0 + base_fl_bonus;

                // The following line makes sure that FL doesn't give less pp than NM
                approachRateFactor = Math.Max(flashlightApproachRateFactor, maxLowARFactor);
            }


            // HDFL bonus: when AR is low, the main struggle is HD, so we take hiddenFactor (note that it's calculated using original approachRate!).
                // When AR is high, the main struggle is FL, so we take approachRateFactor, which is the max of original approachRateFactor and flashlightApproachRateFactor.
                // On top of that, we are adding an additional bonus common for all HDFL scores. It's included in SR.
                // Length-based bonus for HDFL can be found in PerformanceCalculator.
            if (mods.Any(m => m is ModFlashlight) && mods.Any(m => m is ModHidden))
            {
                const double base_hdfl_bonus = 0.07;

                approachRateFactor = Math.Max(approachRateFactor, hiddenFactor) * (1.0 + base_hdfl_bonus);
                hiddenFactor = 1.0; // We have moved both bonuses into approachRateFactor so we set hiddenFactor to 1 to avoid double-counting
            }

            double combinedMultiplier = approachRateFactor * hiddenFactor * Math.Sqrt(tuning.FinalPPMultiplier) * Math.Sqrt(tuning.PerformanceValueMultiplier);
            if (clockRate >= 1.0)
                combinedMultiplier *= 1.0 - 2.0 * tuning.DoubleTimeNerf * (clockRate - 1.0);
            else if (clockRate > 0.0)
                combinedMultiplier *= 1.0 + 2.0 * tuning.DoubleTimeNerf * (1.0 / clockRate - 1.0);

            CatchDifficultyAttributes attributes = new CatchDifficultyAttributes
            {
                StarRating = sr * combinedMultiplier,
                Mods = mods,
                MaxCombo = beatmap.GetMaxCombo(),
                TotalActions = totalActions,
                ApproachRateFactor = approachRateFactor,
                HiddenFactor = hiddenFactor,
                // PrecisionSR = precision,
                // SpeedSR = speed,
                Tuning = tuning,
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
            double time_penalty_power = tuning.BeginningTimePenaltyPower;
            double full_penalty = tuning.BeginningFullPenalty; // Penalty for the first note

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

            // Parameters has been chosen with pp values in mind; to make SR->pp scaling similar to old one, we are scaling it once more.
            // This part doesn't affect pp values: in fact, only SR with nerf beginning is taken into account there.
            // The purpose of the SR below is only to show players how difficult patterns in the map are, which shouldn't depend on the map's length.
            const double multiplier_to_show = 0.95;

            double sr = calculateSr(notes, sorted, missCount);
            if (sr == 0.0)
                return 0.0;

            return sr * multiplier_to_show;
        }

        private double calculateSr(List<(double, double)> notes, List<(double, double)> sorted, int missCount = 0)
        {
            double sr = calculateDifficultyValue(notes, sorted, missCount);

            sr *= 0.015;

            sr = srScaler(sr);

            sr *= tuning.SrPostMultiplier;

            return sr;
        }

        private double srScaler(double sr)
        {
            const double x0 = 0.5;
            const double y0 = 1.2;

            const double x1 = 2.5;
            const double y1 = 2.2;

            const double x2 = 3.5;
            const double y2 = 3.3;

            const double x3 = 4.5;
            const double y3 = 4.7;

            const double x4 = 5.5;
            const double y4 = 5.8;

            const double x5 = 6.5;
            const double y5 = 6.9;

            const double x6 = 7.5;
            const double y6 = 8.7;

            const double x7 = 8.5;
            const double y7 = 9.4;

            const double x8 = 9.0;
            const double y8 = 10.15;

            const double x9 = 10.0;
            const double y9 = 10.6;


            if (sr <= x0) return CatchPreprocessingUtils.Lerp(sr, 0.0, 0.0, x0, y0);
            if (sr <= x1) return CatchPreprocessingUtils.Lerp(sr, x0, y0, x1, y1);
            if (sr <= x2) return CatchPreprocessingUtils.Lerp(sr, x1, y1, x2, y2);
            if (sr <= x3) return CatchPreprocessingUtils.Lerp(sr, x2, y2, x3, y3);
            if (sr <= x4) return CatchPreprocessingUtils.Lerp(sr, x3, y3, x4, y4);
            if (sr <= x5) return CatchPreprocessingUtils.Lerp(sr, x4, y4, x5, y5);
            if (sr <= x6) return CatchPreprocessingUtils.Lerp(sr, x5, y5, x6, y6);
            if (sr <= x7) return CatchPreprocessingUtils.Lerp(sr, x6, y6, x7, y7);
            if (sr <= x8) return CatchPreprocessingUtils.Lerp(sr, x7, y7, x8, y8);

            return CatchPreprocessingUtils.Lerp(sr, x8, y8, x9, y9);
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
            double default_decay_weight = tuning.DefaultDecayWeight;
            double[] decayWeights = tuning.DecayWeights ?? Array.Empty<double>();

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
                : Math.Pow(default_decay_weight, counter + 1);
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

        private List<double> combineStrains(List<double> actionProbabilities, List<double> precisionStrains, List<double> speedStrains, List<double> distanceBonuses, List<double> readingFactors, List<double> highCSFactors)
        {
            List<double> combinedStrains = new List<double>();

            for (int i = 0; i < precisionStrains.Count; i++)
            {
                double actionProbability = actionProbabilities[i];
                double precisionStrain = precisionStrains[i];
                double speedStrain = speedStrains[i];
                double distanceBonus = distanceBonuses[i];
                double readingFactor = readingFactors[i];
                double highCSFactor = highCSFactors[i];

                combinedStrains.Add(CalculateLocalStarRating(actionProbability, precisionStrain, speedStrain, distanceBonus, readingFactor, highCSFactor, tuning));
            }

            return combinedStrains;
        }

        public static double CalculatePartialLocalStarRating(double precisionStrain, double speedStrain, CatchDifficultyConstants tuning)
        {
            const double low_speed_power = 0.8;

            // "Low diffs +HR nerf": the purpose is to nerf precise notes supposing the pattern is sufficiently slow
            // Example of the affected map: 2696377 +HR
            if (speedStrain < tuning.LowSpeedThresholdLSR)
                precisionStrain = precisionStrain * (tuning.UnaffectedPercantagePrecisionLSR + (1.0 - tuning.UnaffectedPercantagePrecisionLSR) * Math.Pow(speedStrain / tuning.LowSpeedThresholdLSR, low_speed_power));

            return tuning.LocalStarRatingMaxConstant * Math.Max(precisionStrain, speedStrain)
                   + tuning.LocalStarRatingMinConstant * Math.Min(precisionStrain, speedStrain)
                   + tuning.LocalStarRatingCorrelationConstant * Math.Pow(precisionStrain, 0.25) * Math.Pow(speedStrain, 0.5);
        }

        public static double CalculateLocalStarRating(double actionProbability, double precisionStrain, double speedStrain, double distanceBonus, double readingFactor, double highCSFactor,
                                                      CatchDifficultyConstants tuning)
        {
            double plsr = CalculatePartialLocalStarRating(precisionStrain, speedStrain, tuning);

            // Distance-based term for very easy notes that raises star rating for "0* maps"
            plsr = Math.Max(plsr, 2.0 * distanceBonus);

            return plsr * readingFactor * highCSFactor;
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

                CatchMovementPreprocessor.Process(objects, normalizedCatcherWidth, clockRate, frameTime, playfieldBorder, tuning);
                CatchDifficultyPreprocessor.Process(objects, normalizedCatcherWidth, clockRate, frameTime, playfieldBorder, tuning);
                CatchReadingPreprocessor.Process(objects, circleSize, clockRate, frameTime, tuning);
                CatchPreprocessingUtils.PopulateDifficultyData(noteObjects, normalizedCatcherWidth, clockRate, tuning);
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
                new PartialLocalStarRating(mods, tuning),
                new LocalStarRating(mods, tuning),
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
