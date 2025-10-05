// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Catch.Difficulty.Preprocessing;
using osu.Game.Rulesets.Catch.Difficulty.Preprocessing.Movement;
using osu.Game.Rulesets.Difficulty.Preprocessing;

namespace osu.Game.Rulesets.Catch.Difficulty.Evaluators
{
    public static class MovementEvaluator
    {
        public static double EvaluateDifficultyOf(DifficultyHitObject current)
        {
            CatchDifficultyHitObject note = (CatchDifficultyHitObject)current;
            CatchMovementData data = note.MovementData;

            double precision = EvaluatePrecisionOf(note);
            double plsr = EvaluatePartialLocalStarRatingOf(note);
            double aim = EvaluateAimOf(note);
            double speed = EvaluateSpeedOf(note);

            double lsr = Math.Sqrt(Math.Pow(plsr, 2) + Math.Pow(1 - data.ActionProbability, 2) * Math.Pow(aim, 2));

            // To switch to precision-only mode, comment out this line
            // return aim;
            // return plsr * 0.85;
            return lsr * 0.87;
            return speed * data.ActionProbability;

            return precision;
        }

        public static double EvaluatePrecisionOf(CatchDifficultyHitObject current)
        {
            double precision = current.MovementData.NotePrecision is null
                ? 0
                : 32 - 7 * Math.Log((double)current.MovementData.NotePrecision);

            return precision / 35;
        }

        public static double EvaluateAimOf(CatchDifficultyHitObject current)
        {
            double aim = current.MovementData.NoteAim is null
                ? 0
                : 32 - 7 * Math.Log((double)current.MovementData.NoteAim + 15.0);

            return aim / 35;
        }

        /// <summary>
        /// Calculates the speed value for a given note.
        /// </summary>
        /// <param name="note"></param>
        /// <param name="prev"></param>
        /// <returns></returns>
        public static double calculateSpeed(CatchDifficultyHitObject note, CatchDifficultyHitObject prev)
        {
            CatchMovementData data = note.MovementData;
            CatchMovementData prevData = prev.MovementData;

            CatchDifficultyHitObject? prevGuaranteedAction = data.PreviousGuaranteedActionNote(0);
            CatchDifficultyHitObject? prevAmbiguousAction = data.PreviousActionNote(0);

            if (data.ActionProbability > 0)
            {
                if (data.ActionProbability < 1
                    && data.DisplayPattern != PatternType.StackEnd)
                {
                    return 1.0 / Math.Max(note.DeltaTime, 1);
                }

                if (prevAmbiguousAction is null && prevGuaranteedAction is not null)
                {
                    return 1.0 / Math.Max(note.StartTime - prevGuaranteedAction.StartTime, 1);
                }

                if (prevGuaranteedAction is null && prevAmbiguousAction is not null)
                {
                    return prevAmbiguousAction.MovementData.ActionProbability / Math.Max(note.StartTime - prevAmbiguousAction.StartTime, 1);
                }

                if (prevAmbiguousAction is not null && prevGuaranteedAction is not null)
                {
                    if (prevGuaranteedAction.StartTime >= prevAmbiguousAction.StartTime)
                    {
                        return 1.0 / Math.Max(note.StartTime - prevGuaranteedAction.StartTime, 1);
                    }

                    double ambiguousSpeed = 1.0 / Math.Max(note.StartTime - prevAmbiguousAction.StartTime, 1);
                    double guaranteedSpeed = 1.0 / Math.Max(note.StartTime - prevGuaranteedAction.StartTime, 1);
                    double prevActionProbability = prevAmbiguousAction.MovementData.ActionProbability;

                    return prevActionProbability * ambiguousSpeed + (1 - prevActionProbability) * guaranteedSpeed;
                }
            }

            return 0;
        }

        public static double EvaluateSpeedOf(CatchDifficultyHitObject current)
        {
            CatchDifficultyHitObject? prev = current.PreviousNote(0);

            if (prev is null)
            {
                return 0;
            }

            double speed = calculateSpeed(current, prev);

            return speed * 14;
        }

        public static double EvaluatePartialLocalStarRatingOf(CatchDifficultyHitObject current)
        {
            double precision = EvaluatePrecisionOf(current);
            double aim = EvaluateAimOf(current);
            double speed = EvaluateSpeedOf(current);

            double plsr = current.MovementData.ActionProbability * Math.Sqrt(Math.Pow(precision, 2) + Math.Pow(speed, 2));

            return plsr;
        }
    }
}
