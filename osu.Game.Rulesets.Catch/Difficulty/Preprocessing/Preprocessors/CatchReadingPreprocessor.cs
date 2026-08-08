// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using osu.Game.Rulesets.Catch.Difficulty;
using osu.Game.Rulesets.Catch.Difficulty.Preprocessing.Utils;
using osu.Game.Rulesets.Difficulty.Preprocessing;

namespace osu.Game.Rulesets.Catch.Difficulty.Preprocessing.Preprocessors
{
    public static class CatchReadingPreprocessor
    {
        private const double high_cs_threshold = 3.0;
        private const double very_high_cs_power = 0.7;
        private const double very_high_cs_rate = 0.06;

        private const double local_rhythm_range = 20.0;
        private const double local_rhythm_sensitivity = 2.0;

        private const uint explicit_rhythm_note_count = 4; // number of actions in a row before full penalty
        private const double explicit_rhythm_leniency = 0.1;

        private const uint implicit_rhythm_note_count = 4; // number of actions in a row before full penalty
        private const double implicit_rhythm_leniency = 0.05;

        private const uint similar_distance_note_count = 3;
        private const double similar_distance_leniency = 0.1;
        private const double similar_distance_sensitivity = 1.5;

        private const uint alternating_distance_note_count = 3;
        private const double alternating_distance_leniency = 0.1;
        private const double alternating_distance_sensitivity = 1.5;

        private const uint hyperchain_note_count = 6;

        private const uint non_hyperchain_note_count = 4;

        private const double high_velocity_threshold = 4.5;
        private const double max_velocity_nerf_threshold = 7.0;
        private const double high_velocity_power = 0.75;

        private const double high_distance_threshold = 256.0;

        private const double max_precision_ratio = 0.5;
        private const double max_delta_time = 300.0;
        private const double time_power = 0.5;

        public static void Process(List<DifficultyHitObject> hitObjects, double circleSize, double clockRate, double frameTime, CatchDifficultyConstants tuning)
        {
            List<CatchDifficultyHitObject> cdhos = hitObjects.Select(n => (CatchDifficultyHitObject)n).ToList();
            List<CatchDifficultyHitObject> actionNotes = cdhos.Where(n => n.MovementData.ActionProbability == 1).ToList();

            // Sets HighCSFactor
            highCSBuff(actionNotes, circleSize, tuning);

            // Sets CombinedReadingFactor
            localRhythmPenalty(cdhos, tuning);
            explicitRhythmPenalty(actionNotes, tuning);
            implicitRhythmPenalty(actionNotes, tuning);
            similarDistancePenalty(actionNotes, clockRate, tuning);
            alternatingDistancePenalty(actionNotes, clockRate, tuning);
            hyperchainPenalty(cdhos, tuning);
            nonHyperchainPenalty(actionNotes, tuning);
            // highVelocityNerf(cdhos, frameTime, tuning);
            highDistanceBuff(actionNotes, clockRate, tuning);
            futurePrecisionBuff(cdhos, tuning);
        }

        private static void localRhythmPenalty(List<CatchDifficultyHitObject> cdhos, CatchDifficultyConstants tuning)
        {
            foreach (var note in cdhos)
            {
                if (note.MovementData.ActionProbability == 0) continue;

                double timeDifference = Math.Abs(note.MovementData.EffectiveTime - note.StartTime);

                double filteredTimeDifference = Math.Max(timeDifference - 2.0, 0);

                double multiplier = Math.Min(filteredTimeDifference / local_rhythm_range, 1.0);

                double penalty = (1.0 - tuning.ReadingLocalRhythmPenalty) * Math.Pow(1.0 - multiplier, local_rhythm_sensitivity);

                note.ReadingData.CombinedReadingFactor *= 1.0 - penalty;
            }
        }

        private static void explicitRhythmPenalty(List<CatchDifficultyHitObject> actionNotes, CatchDifficultyConstants tuning)
        {
            double counter = 0;
            double raw_penalty = (1.0 - tuning.ReadingExplicitRhythmPenalty);

            // doesn't count first note
            for (int i = 3; i < actionNotes.Count; i++)
            {
                CatchDifficultyHitObject note = actionNotes[i];
                CatchDifficultyHitObject prev = actionNotes[i - 1];
                CatchDifficultyHitObject prevPrev = actionNotes[i - 2];

                double prevDelta = prev.StartTime - prevPrev.StartTime;
                double delta = note.StartTime - prev.StartTime;

                double lower = prevDelta * (1.0 - explicit_rhythm_leniency);
                double higher = prevDelta * (1.0 + explicit_rhythm_leniency);

                if (delta > lower && delta < higher)
                {
                    counter++;
                    double penalty = raw_penalty * Math.Min(counter / explicit_rhythm_note_count, 1);
                    note.ReadingData.CombinedReadingFactor *= 1.0 - penalty;
                }
                else
                {
                    counter = 0;
                }
            }
        }

        private static void implicitRhythmPenalty(List<CatchDifficultyHitObject> actionNotes, CatchDifficultyConstants tuning)
        {
            double counter = 0;
            double raw_penalty = (1.0 - tuning.ReadingImplicitRhythmPenalty);

            // doesn't count first note
            for (int i = 3; i < actionNotes.Count; i++)
            {
                CatchDifficultyHitObject note = actionNotes[i];
                CatchDifficultyHitObject prev = actionNotes[i - 1];
                CatchDifficultyHitObject prevPrev = actionNotes[i - 2];

                double prevDelta = prev.MovementData.EffectiveTime - prevPrev.MovementData.EffectiveTime;
                double delta = note.MovementData.EffectiveTime - prev.MovementData.EffectiveTime;

                double lower = prevDelta * (1.0 - implicit_rhythm_leniency);
                double higher = prevDelta * (1.0 + implicit_rhythm_leniency);

                if (delta > lower && delta < higher)
                {
                    counter++;
                    double penalty = raw_penalty * Math.Min(counter / implicit_rhythm_note_count, 1);
                    note.ReadingData.CombinedReadingFactor *= 1.0 - penalty;
                }
                else
                {
                    counter = 0;
                }
            }
        }

        private static void similarDistancePenalty(List<CatchDifficultyHitObject> actionNotes, double clockRate, CatchDifficultyConstants tuning)
        {
            uint counter = 0;
            double distanceToRemember = 0.0;

            // Don't count first note
            for (int i = 3; i < actionNotes.Count; i++)
            {
                CatchDifficultyHitObject note = actionNotes[i];
                CatchDifficultyHitObject prev = actionNotes[i - 1];

                if (prev.IsHyper)
                    continue;

                double higher = Math.Max(note.DeltaPosition * clockRate, distanceToRemember);
                double lower = Math.Min(note.DeltaPosition * clockRate, distanceToRemember);

                double ratio = (higher - lower) / higher;
                double halfRatio = (higher - lower) / Math.Max(lower, higher / 2.0);

                if (ratio <= similar_distance_leniency || halfRatio <= similar_distance_leniency)
                {
                    counter = Math.Min(counter + 1, similar_distance_note_count);

                    if (counter == similar_distance_note_count)
                    {
                        double penalty = (1.0 - tuning.ReadingSimilarDistancePenalty) * Math.Pow(1.0 - ratio / similar_distance_leniency, similar_distance_sensitivity);
                        note.ReadingData.CombinedReadingFactor *= 1.0 - penalty;
                    }
                }

                else if (halfRatio <= similar_distance_leniency)
                {
                    counter = Math.Min(counter + 1, similar_distance_note_count);

                    if (counter == similar_distance_note_count)
                    {
                        double penalty = (1.0 - tuning.ReadingSimilarDistancePenalty) * Math.Pow(1.0 - halfRatio / similar_distance_leniency, similar_distance_sensitivity);
                        note.ReadingData.CombinedReadingFactor *= 1.0 - penalty;
                    }
                }

                else
                {
                    counter = Math.Max(counter - 1, 0);
                }

                distanceToRemember = note.DeltaPosition * clockRate;
            }
        }

        private static void alternatingDistancePenalty(List<CatchDifficultyHitObject> actionNotes, double clockRate, CatchDifficultyConstants tuning)
        {
            uint counter = 0;

            double rememberedDistanceOdd = 0.0;
            double rememberedDistanceEven = 0.0;
            int savedDistances = 0;

            int validNoteIndex = 0; // Only non-hypers

            // Don't count first notes
            for (int i = 3; i < actionNotes.Count; i++)
            {
                CatchDifficultyHitObject note = actionNotes[i];
                CatchDifficultyHitObject prev = actionNotes[i - 1];

                if (prev.IsHyper)
                    continue;

                double currentDistance = note.DeltaPosition * clockRate;
                int oddEvenIndex = validNoteIndex & 1; // 0 for evens, 1 for odds

                if (savedDistances == 2)
                {
                    double higherOdd = Math.Max(currentDistance, rememberedDistanceOdd);
                    double higherEven = Math.Max(currentDistance, rememberedDistanceEven);
                    double lowerOdd = Math.Min(currentDistance, rememberedDistanceOdd);
                    double lowerEven = Math.Min(currentDistance, rememberedDistanceEven);

                    double ratioOdd = (higherOdd - lowerOdd) / higherOdd;
                    double halfRatioOdd = (higherOdd - lowerOdd) / Math.Max(lowerOdd, higherOdd / 2.0);
                    double ratioEven = (higherEven - lowerEven) / higherEven;
                    double halfRatioEven = (higherEven - lowerEven) / Math.Max(lowerEven, higherEven / 2.0);

                    double lowerRatio = Math.Min(ratioOdd, ratioEven);
                    double lowerHalfRatio = Math.Min(halfRatioOdd, halfRatioEven);

                    if (lowerRatio <= alternating_distance_leniency || lowerHalfRatio <= alternating_distance_leniency)
                    {
                        counter = Math.Min(counter + 1, alternating_distance_note_count);

                        if (counter == alternating_distance_note_count)
                        {
                            double effectiveRatio = Math.Min(lowerRatio, lowerHalfRatio);

                            double penalty =
                                (1.0 - tuning.ReadingAlternatingDistancePenalty) *
                                Math.Pow(
                                    1.0 - effectiveRatio / alternating_distance_leniency,
                                    alternating_distance_sensitivity
                                );

                            note.ReadingData.CombinedReadingFactor *= 1.0 - penalty;
                        }
                    }
                    else
                        counter = Math.Max(counter - 1, 0);
                }

                if (oddEvenIndex == 1)
                    rememberedDistanceOdd = currentDistance;
                else
                    rememberedDistanceEven = currentDistance;

                savedDistances = Math.Min(savedDistances + 1, 2);
                validNoteIndex++;
            }
        }

        private static void hyperchainPenalty(List<CatchDifficultyHitObject> cdhos, CatchDifficultyConstants tuning)
        {
            double counter = 0;
            double raw_penalty = (1.0 - tuning.ReadingHyperchainPenalty);

            // doesn't count first note
            for (int i = 3; i < cdhos.Count; i++)
            {
                CatchDifficultyHitObject note = cdhos[i];
                CatchDifficultyHitObject prev = cdhos[i - 1];
                CatchDifficultyHitObject prevPrev = cdhos[i - 2];

                if ((note.IsHyper && prev.IsHyper && prevPrev.IsHyper) || (counter > 0 && note.MovementData.ActionProbability < 0.15))
                {
                    counter++;
                    double penalty = raw_penalty * Math.Min(counter / hyperchain_note_count, 1);
                    note.ReadingData.CombinedReadingFactor *= 1.0 - penalty;
                }
                else
                {
                    counter = 0;
                }
            }
        }

        private static void nonHyperchainPenalty(List<CatchDifficultyHitObject> actionNotes, CatchDifficultyConstants tuning)
        {
            double counter = 0;
            double raw_penalty = (1.0 - tuning.ReadingNonHyperchainPenalty);

            // doesn't count first note
            for (int i = 3; i < actionNotes.Count; i++)
            {
                CatchDifficultyHitObject note = actionNotes[i];
                CatchDifficultyHitObject prev = actionNotes[i - 1];
                CatchDifficultyHitObject prevPrev = actionNotes[i - 2];

                if ((!note.IsHyper && !prev.IsHyper && !prevPrev.IsHyper) || (counter > 0 && note.MovementData.ActionProbability < 0.15))
                {
                    counter++;
                    double penalty = raw_penalty * Math.Min(counter / non_hyperchain_note_count, 1);
                    note.ReadingData.CombinedReadingFactor *= 1.0 - penalty;
                }
                else
                {
                    counter = 0;
                }
            }
        }

        // High velocity nerf may be seen as some kind of correction of precision - approximation error is higher at higher velocity.
        private static void highVelocityNerf(List<CatchDifficultyHitObject> cdhos, double frameTime, CatchDifficultyConstants tuning)
        {
            for (int i = 1; i < cdhos.Count; i++)
            {
                CatchDifficultyHitObject note = cdhos[i];
                CatchDifficultyHitObject prev = cdhos[i - 1];
                double velocity = CatchPreprocessingUtils.CalculatePerfectHyperdashSpeed(note, prev, frameTime);

                if (prev.IsHyper && velocity > high_velocity_threshold)
                    note.ReadingData.CombinedReadingFactor *= 1.0 - tuning.ReadingHighVelocityNerf * Math.Min(1.0, Math.Pow((velocity - high_velocity_threshold) / (max_velocity_nerf_threshold - high_velocity_threshold), high_velocity_power));
            }
        }

        private static void highDistanceBuff(List<CatchDifficultyHitObject> actionNotes, double clockRate, CatchDifficultyConstants tuning)
        {
            for (int i = 1; i < actionNotes.Count - 1; i++)
            {
                CatchDifficultyHitObject prev = actionNotes[i - 1];
                CatchDifficultyHitObject note = actionNotes[i];
                CatchDifficultyHitObject next = actionNotes[i + 1];
                double currentDistance = Math.Abs(note.Position - prev.Position) * clockRate;
                double nextDistance = Math.Abs(next.Position - note.Position) * clockRate;
                double averageDistance = (currentDistance + nextDistance) / 2.0;

                if (averageDistance > high_distance_threshold)
                {
                    note.ReadingData.CombinedReadingFactor *= 1.0 + tuning.ReadingHighDistanceBuff * Math.Pow((averageDistance - high_distance_threshold) / (512.0 - high_distance_threshold), tuning.ReadingHighDistancePower);
                    note.ReadingData.HighDistanceFactor *= 1.0 + tuning.ReadingHighDistanceBuff * Math.Pow((averageDistance - high_distance_threshold) / (512.0 - high_distance_threshold), tuning.ReadingHighDistancePower);
                }
            }
        }

        private static void highCSBuff(List<CatchDifficultyHitObject> actionNotes, double circleSize, CatchDifficultyConstants tuning)
        {
            double baseCircleSizeBonus = Math.Pow(Math.Max(0.0, circleSize - high_cs_threshold) / (10.0 - high_cs_threshold), tuning.ReadingHighCsPower) * tuning.ReadingHighCsRate;
            double veryHighCircleSizeBonus = Math.Pow(Math.Max(0.0, circleSize - high_cs_threshold) / (10.0 - high_cs_threshold), tuning.ReadingHighCsPower + very_high_cs_power * circleSize) * very_high_cs_rate;
            double circleSizeBonus = baseCircleSizeBonus + veryHighCircleSizeBonus;

            double circleSizeBonusHypers = tuning.ReadingHighCsPenaltyHypers * circleSizeBonus;

            for (int i = 0; i < actionNotes.Count - 1; i++)
            {
                CatchDifficultyHitObject note = actionNotes[i];
                if (note.IsHyper)
                    note.ReadingData.HighCSFactor *= 1.0 + circleSizeBonusHypers;
                else
                    note.ReadingData.HighCSFactor *= 1.0 + circleSizeBonus;
            }
        }


        private static void futurePrecisionBuff(List<CatchDifficultyHitObject> cdhos, CatchDifficultyConstants tuning)
        {
            for (int i = 1; i < cdhos.Count - 2; i++)
            {
                CatchDifficultyHitObject note = cdhos[i];
                CatchDifficultyHitObject next = cdhos[i + 1];
                CatchDifficultyHitObject nextNext = cdhos[i + 2];

                if (!note.MovementData.FuturePrecisionUtilized) continue;

                // This should never be null if future precision was utilized
                Debug.Assert(note.MovementData.FuturePrecision != null);

                double futurePrecision = (double)note.MovementData.FuturePrecision; // p'_1, non-weighted
                double? rawPrecision = note.MovementData.OriginalPrecision; // p_1, original precision
                double precisionRatio = rawPrecision == null ? 1.0 : Math.Max(1.0, (double)rawPrecision / futurePrecision); // p_1 / p'_1 <= 1
                double precisionTerm = Math.Min(precisionRatio - 1.0, max_precision_ratio) / max_precision_ratio;

                double longDeltaTime = nextNext.StartTime - note.StartTime;
                double timeRatio = Math.Pow(longDeltaTime / max_delta_time, time_power);

                double bonus = timeRatio * precisionTerm * (1.0 - next.MovementData.ActionProbability) * tuning.ReadingFuturePrecisionBuff;

                note.ReadingData.CombinedReadingFactor *= 1.0 + bonus;
            }
        }
    }
}
