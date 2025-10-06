// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Catch.Difficulty.Preprocessing;
using osu.Game.Rulesets.Catch.Difficulty.Preprocessing.Data;
using osu.Game.Rulesets.Difficulty.Preprocessing;

namespace osu.Game.Rulesets.Catch.Difficulty.Evaluators
{
    public class SpeedEvaluator
    {
        public static double EvaluateDifficultyOf(DifficultyHitObject current)
        {
            CatchDifficultyHitObject note = (CatchDifficultyHitObject)current;

            CatchDifficultyHitObject? prev = note.PreviousNote(0);
            CatchDifficultyHitObject? next = note.PreviousNote(0);

            if (prev is null)
            {
                return 0;
            }

            double speed = CalculateSpeed(note, prev, next);

            return speed * 9;
        }

        /// <summary>
        /// Calculates the speed value for a given note.
        /// </summary>
        /// <param name="note"></param>
        /// <param name="prev"></param>
        /// <param name="next"></param>
        /// <returns></returns>
        public static double CalculateSpeed(CatchDifficultyHitObject note, CatchDifficultyHitObject prev, CatchDifficultyHitObject? next)
        {
            CatchMovementData data = note.MovementData;
            _ = prev.MovementData;
            _ = next?.MovementData;

            CatchDifficultyHitObject? prevGuaranteedAction = data.PreviousGuaranteedActionNote(0);
            CatchDifficultyHitObject? prevAmbiguousAction = data.PreviousActionNote(0);

            // hack
            double timeBonus = data.NotePattern == PatternType.BreakBeginningRequiringMovement
                ? (next is not null ? Math.Max((next.StartTime + prev.StartTime) / 2.0 - note.StartTime, note.StartTime) : 10000)
                : 0;

            if (data.ActionProbability > 0)
            {
                if (data.ActionProbability < 1
                    && data.DisplayPattern != PatternType.StackEnd)
                {
                    return 1.0 / Math.Max(note.DeltaTime + timeBonus, 1);
                }

                if (prevAmbiguousAction is null && prevGuaranteedAction is not null)
                {
                    return 1.0 / Math.Max(note.StartTime + timeBonus - prevGuaranteedAction.StartTime, 1);
                }

                if (prevGuaranteedAction is null && prevAmbiguousAction is not null)
                {
                    return prevAmbiguousAction.MovementData.ActionProbability / Math.Max(note.StartTime + timeBonus - prevAmbiguousAction.StartTime, 1);
                }

                if (prevAmbiguousAction is not null && prevGuaranteedAction is not null)
                {
                    if (prevGuaranteedAction.StartTime >= prevAmbiguousAction.StartTime)
                    {
                        return 1.0 / Math.Max(note.StartTime + timeBonus - prevGuaranteedAction.StartTime, 1);
                    }

                    double ambiguousSpeed = 1.0 / Math.Max(note.StartTime + timeBonus - prevAmbiguousAction.StartTime, 1);
                    double guaranteedSpeed = 1.0 / Math.Max(note.StartTime + timeBonus - prevGuaranteedAction.StartTime, 1);
                    double prevActionProbability = prevAmbiguousAction.MovementData.ActionProbability;

                    return prevActionProbability * ambiguousSpeed + (1 - prevActionProbability) * guaranteedSpeed;
                }
            }

            return 0;
        }
    }
}
