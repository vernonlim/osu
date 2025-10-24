// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Difficulty.Preprocessing;

namespace osu.Game.Rulesets.Catch.Difficulty.Preprocessing.Preprocessors
{
    public static class CatchReadingPreprocessor
    {
        private const double rhythm_penalty = 1.0;
        private const double rhythm_range = 20.0;

        private const double explicit_rhythm_penalty = 1.0;
        private const uint explicit_rhythm_note_count = 4; // number of actions in a row before full penalty
        private const double explicit_rhythm_leniency = 0.05;

        public static void Process(List<DifficultyHitObject> hitObjects)
        {
            List<CatchDifficultyHitObject> cdhos = hitObjects.Select(n => (CatchDifficultyHitObject)n).ToList();
            List<CatchDifficultyHitObject> actionNotes = cdhos.Where(n => n.MovementData.ActionProbability == 1).ToList();

            localRhythmPenalty(cdhos);
            explicitRhythmPenalty(actionNotes);
        }

        private static void localRhythmPenalty(List<CatchDifficultyHitObject> cdhos)
        {
            foreach (var note in cdhos)
            {
                if (note.MovementData.ActionProbability == 0) continue;

                double timeDifference = Math.Abs(note.MovementData.EffectiveTime - note.StartTime);

                double multiplier = Math.Min(timeDifference / rhythm_range, 1.0);

                double penalty = (1.0 - rhythm_penalty) * (1.0 - multiplier);

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

                if (delta > lower && delta < higher)
                {
                    counter++;
                    double penalty = raw_penalty * Math.Min(counter / explicit_rhythm_note_count, 1);
                    note.ReadingData.ReadingFactors.Add(1.0 - penalty);
                }
            }
        }
    }
}
