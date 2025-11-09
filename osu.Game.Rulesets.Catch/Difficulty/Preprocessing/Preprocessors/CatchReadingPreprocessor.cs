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

        private const double similar_distance_penalty = 1.0;
        private const uint similar_distance_note_count = 4;
        private const double similar_distance_leniency = 0.05;

        private const double hyperchain_penalty = 0.89;
        private const uint hyperchain_note_count = 8;

        private const double high_velocity_buff = 1.0;
        private const double high_velocity_distance_threshold = 512.0;
        private const double high_velocity_threshold = 2.5;
        private const double high_velocity_threshold_multiplier = 2.0;

        private const double high_distance_buff = 0.18;
        private const double high_distance_threshold = 256.0;
        private const double high_distance_power = 1.4;

        public static void Process(List<DifficultyHitObject> hitObjects)
        {
            List<CatchDifficultyHitObject> cdhos = hitObjects.Select(n => (CatchDifficultyHitObject)n).ToList();
            List<CatchDifficultyHitObject> actionNotes = cdhos.Where(n => n.MovementData.ActionProbability == 1).ToList();

            localRhythmPenalty(cdhos);
            explicitRhythmPenalty(actionNotes);
            implicitRhythmPenalty(actionNotes);
            similarDistancePenalty(cdhos);
            hyperchainPenalty(cdhos);
            highVelocityBuff(cdhos);
            highDistanceBuff(cdhos);
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

                note.ReadingData.ReadingFactors.Add(1.0 - penalty);
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

                if ((delta > lower && delta < higher)) //|| (delta / 2 > lower && delta / 2 < higher) || (delta * 2 > lower && delta * 2 < higher))
                {
                    counter++;
                    double penalty = raw_penalty * Math.Min(counter / explicit_rhythm_note_count, 1);
                    note.ReadingData.ReadingFactors.Add(1.0 - penalty);
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
                    note.ReadingData.ReadingFactors.Add(1.0 - penalty);
                }
                else
                {
                    counter = 0;
                }
            }
        }

        private static void similarDistancePenalty(List<CatchDifficultyHitObject> cdhos)
        {
            double counter = 0;
            const double raw_penalty = (1.0 - similar_distance_penalty);

            // doesn't count first note
            for (int i = 2; i < cdhos.Count; i++)
            {
                CatchDifficultyHitObject note = cdhos[i];
                CatchDifficultyHitObject prev = cdhos[i - 1];

                double lower = prev.DeltaPosition * (1.0 - similar_distance_leniency);
                double higher = prev.DeltaPosition * (1.0 + similar_distance_leniency);

                if (note.DeltaPosition > lower && note.DeltaPosition < higher)
                {
                    counter++;
                    double penalty = raw_penalty * Math.Min(counter / similar_distance_note_count, 1);
                    note.ReadingData.ReadingFactors.Add(1.0 - penalty);
                }
                else
                {
                    counter = 0;
                }
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

                if (note.IsHyper && prev.IsHyper && prevPrev.IsHyper || (counter>0 && note.MovementData.ActionProbability < 0.15))
                {
                    counter++;
                    double penalty = raw_penalty * Math.Min(counter / hyperchain_note_count, 1);
                    note.ReadingData.ReadingFactors.Add(1.0 - penalty);
                }
                else
                {
                    counter = 0;
                }
            }
        }

        private static void highVelocityBuff(List<CatchDifficultyHitObject> cdhos)
        {
            const double raw_buff = high_velocity_buff - 1.0;

            for (int i = 1; i < cdhos.Count; i++)
            {
                CatchDifficultyHitObject note = cdhos[i];
                CatchDifficultyHitObject prev = cdhos[i - 1];
                double speed = CatchPreprocessingUtils.CalculatePerfectHyperdashSpeed(note);

                if (prev.IsHyper
                    && speed > high_velocity_threshold
                    && note.DeltaPosition < high_velocity_distance_threshold)
                {
                    double distanceFactor = 1.0 - note.DeltaPosition / high_velocity_distance_threshold;
                    double velocityFactor = Math.Min((speed - high_velocity_threshold) / (high_velocity_threshold * high_velocity_threshold_multiplier - high_velocity_threshold), 1.0);

                    note.ReadingData.ReadingFactors.Add(1.0 + distanceFactor * velocityFactor * raw_buff);
                }
            }
        }

        private static void highDistanceBuff(List<CatchDifficultyHitObject> cdhos)
        {
            for (int i = 0; i < cdhos.Count - 1; i++)
            {
                CatchDifficultyHitObject note = cdhos[i];
                CatchDifficultyHitObject next = cdhos[i + 1];
                double average_distance = (note.DeltaPosition + next.DeltaPosition) / 2;

                if (average_distance > high_distance_threshold)
                {
                    note.ReadingData.ReadingFactors.Add(1.0 + high_distance_buff * Math.Pow((average_distance - high_distance_threshold) / (512.0 - high_distance_threshold), high_distance_power));
                }
            }
        }
    }
}
