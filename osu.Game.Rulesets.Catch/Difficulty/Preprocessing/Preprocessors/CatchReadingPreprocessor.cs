// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Catch.Difficulty.Preprocessing.Utils;
using osu.Game.Rulesets.Difficulty.Preprocessing;

namespace osu.Game.Rulesets.Catch.Difficulty.Preprocessing.Preprocessors
{
    public static class CatchReadingPreprocessor
    {
        private const double local_rhythm_penalty = 0.95;
        private const double local_rhythm_range = 20.0;
        private const double local_rhythm_sensitivity = 2.0;

        private const double explicit_rhythm_penalty = 0.93;
        private const uint explicit_rhythm_note_count = 4; // number of actions in a row before full penalty
        private const double explicit_rhythm_leniency = 0.1;

        private const double implicit_rhythm_penalty = 1.0;
        private const uint implicit_rhythm_note_count = 4; // number of actions in a row before full penalty
        private const double implicit_rhythm_leniency = 0.05;

        private const double similar_distance_penalty = 0.85;
        private const uint similar_distance_note_count = 3;
        private const double similar_distance_leniency = 0.1;
        private const double similar_distance_sensitivity = 1.5;

        private const double hyperchain_penalty = 0.92;
        private const uint hyperchain_note_count = 6;

        private const double non_hyperchain_penalty = 0.96;
        private const uint non_hyperchain_note_count = 4;

        private const double high_velocity_nerf = 0.12;
        private const double high_velocity_threshold = 4.5;
        private const double max_velocity_nerf_threshold = 7.5;
        private const double high_velocity_power = 0.75;

        private const double high_distance_buff = 0.15;
        private const double high_distance_threshold = 256.0;
        private const double high_distance_power = 1.4;

        private const double high_cs_threshold = 3.5;
        private const double high_cs_power = 1.6;
        private const double high_cs_rate = 0.39;
        private const double high_cs_penalty_hypers = 0.75;

        private const double density_buff = 1.02;

        public static void Process(List<DifficultyHitObject> hitObjects, double circleSize, double clockRate, double frameTime)
        {
            List<CatchDifficultyHitObject> cdhos = hitObjects.Select(n => (CatchDifficultyHitObject)n).ToList();
            List<CatchDifficultyHitObject> actionNotes = cdhos.Where(n => n.MovementData.ActionProbability == 1).ToList();

            localRhythmPenalty(cdhos);
            explicitRhythmPenalty(actionNotes);
            implicitRhythmPenalty(actionNotes);
            similarDistancePenalty(actionNotes, clockRate);
            hyperchainPenalty(cdhos);
            nonHyperchainPenalty(actionNotes);
            highVelocityNerf(cdhos, frameTime);
            highDistanceBuff(actionNotes, clockRate);
            highCSBuff(actionNotes, circleSize);
            densityBuff(cdhos);
        }

        private static void localRhythmPenalty(List<CatchDifficultyHitObject> cdhos)
        {
            foreach (var note in cdhos)
            {
                if (note.MovementData.ActionProbability == 0) continue;

                double timeDifference = Math.Abs(note.MovementData.EffectiveTime - note.StartTime);

                double filteredTimeDifference = Math.Max(timeDifference - 2.0, 0);

                double multiplier = Math.Min(filteredTimeDifference / local_rhythm_range, 1.0);

                double penalty = (1.0 - local_rhythm_penalty) * Math.Pow(1.0 - multiplier, local_rhythm_sensitivity);

                note.ReadingData.CombinedReadingFactor *= 1.0 - penalty;
            }
        }

        private static void explicitRhythmPenalty(List<CatchDifficultyHitObject> actionNotes)
        {
            double counter = 0;
            const double raw_penalty = (1.0 - explicit_rhythm_penalty);

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

        private static void implicitRhythmPenalty(List<CatchDifficultyHitObject> actionNotes)
        {
            double counter = 0;
            const double raw_penalty = (1.0 - implicit_rhythm_penalty);

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

        private static void similarDistancePenalty(List<CatchDifficultyHitObject> actionNotes, double clockRate)
        {
            uint counter = 0;
            double distanceToRemember = 0.0;

            // doesn't count first note
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
                        double penalty = (1.0 - similar_distance_penalty) * Math.Pow(1.0 - ratio / similar_distance_leniency, similar_distance_sensitivity);
                        note.ReadingData.CombinedReadingFactor *= 1.0 - penalty;
                    }
                }

                else if (halfRatio <= similar_distance_leniency)
                {
                    counter = Math.Min(counter + 1, similar_distance_note_count);

                    if (counter == similar_distance_note_count)
                    {
                        double penalty = (1.0 - similar_distance_penalty) * Math.Pow(1.0 - halfRatio / similar_distance_leniency, similar_distance_sensitivity);
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

        private static void hyperchainPenalty(List<CatchDifficultyHitObject> cdhos)
        {
            double counter = 0;
            const double raw_penalty = (1.0 - hyperchain_penalty);

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

        private static void nonHyperchainPenalty(List<CatchDifficultyHitObject> actionNotes)
        {
            double counter = 0;
            const double raw_penalty = (1.0 - non_hyperchain_penalty);

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
        private static void highVelocityNerf(List<CatchDifficultyHitObject> cdhos, double frameTime)
        {
            for (int i = 1; i < cdhos.Count; i++)
            {
                CatchDifficultyHitObject note = cdhos[i];
                CatchDifficultyHitObject prev = cdhos[i - 1];
                double speed = CatchPreprocessingUtils.CalculatePerfectHyperdashSpeed(note, frameTime);

                if (prev.IsHyper && speed > high_velocity_threshold)
                    note.ReadingData.CombinedReadingFactor *= 1.0 - high_velocity_nerf * Math.Max(1.0, Math.Pow((speed - high_velocity_threshold) / (max_velocity_nerf_threshold - high_velocity_threshold), high_velocity_power));
            }
        }

        private static void highDistanceBuff(List<CatchDifficultyHitObject> actionNotes, double clockRate)
        {
            for (int i = 1; i < actionNotes.Count - 1; i++)
            {
                CatchDifficultyHitObject prev = actionNotes[i - 1];
                CatchDifficultyHitObject note = actionNotes[i];
                CatchDifficultyHitObject next = actionNotes[i + 1];
                double currentDistance = (note.Position - prev.Position) * clockRate;
                double nextDistance = (next.Position - note.Position) * clockRate;
                double averageDistance = (currentDistance + nextDistance) / 2.0;

                if (averageDistance > high_distance_threshold)
                {
                    note.ReadingData.CombinedReadingFactor *= 1.0 + high_distance_buff * Math.Pow((averageDistance - high_distance_threshold) / (512.0 - high_distance_threshold), high_distance_power);
                }
            }
        }

        private static void highCSBuff(List<CatchDifficultyHitObject> actionNotes, double circleSize)
        {
            double circleSizeBonus = Math.Pow(Math.Max(0.0, circleSize - high_cs_threshold) / 10.0, high_cs_power) * high_cs_rate;
            double circleSizeBonusHypers = high_cs_penalty_hypers * circleSizeBonus;

            for (int i = 0; i < actionNotes.Count - 1; i++)
            {
                CatchDifficultyHitObject note = actionNotes[i];
                if (note.IsHyper)
                    note.ReadingData.CombinedReadingFactor *= 1.0 + circleSizeBonusHypers;
                else
                    note.ReadingData.CombinedReadingFactor *= 1.0 + circleSizeBonus;
            }
        }

        // Especially on rain/overdose level, it is harder to read direction changes when there's at least one note between them
        private static void densityBuff(List<CatchDifficultyHitObject> cdhos)
        {
            for (int i = 1; i < cdhos.Count; i++)
            {
                CatchDifficultyHitObject note = cdhos[i];
                CatchDifficultyHitObject prev = cdhos[i - 1];

                if (prev.MovementData.ActionProbability == 0)
                    note.ReadingData.CombinedReadingFactor *= density_buff;
            }
        }
    }
}
