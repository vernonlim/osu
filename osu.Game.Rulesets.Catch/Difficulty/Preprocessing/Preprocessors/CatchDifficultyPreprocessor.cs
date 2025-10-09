// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Rulesets.Catch.Difficulty.Evaluators;
using osu.Game.Rulesets.Catch.Difficulty.Preprocessing.Data;
using osu.Game.Rulesets.Catch.Difficulty.Preprocessing.Utils;
using osu.Game.Rulesets.Difficulty.Preprocessing;

namespace osu.Game.Rulesets.Catch.Difficulty.Preprocessing.Preprocessors
{
    public static class CatchDifficultyPreprocessor
    {
        public static void Process(List<DifficultyHitObject> hitObjects)
        {
            for (int i = 1; i < hitObjects.Count - 1; i++)
            {
                CatchDifficultyHitObject note = (CatchDifficultyHitObject)hitObjects[i];
                CatchDifficultyHitObject prev = (CatchDifficultyHitObject)hitObjects[i - 1];
                CatchDifficultyHitObject next = (CatchDifficultyHitObject)hitObjects[i + 1];

                CatchMovementData data = note.MovementData;

                data.NotePrecision = calculatePrecision(note, prev, next);
                data.NoteAim = calculateAim(note, prev, next);
                data.NoteSpeed = SpeedEvaluator.CalculateSpeed(note, prev, next) * 14;

                double precisionStrain = AimEvaluator.EvaluateDifficultyOf(note);
                double aimStrain = AimEvaluator.EvaluateDifficultyOf(note);
                double speedStrain = SpeedEvaluator.EvaluateDifficultyOf(note);

                data.PartialLocalStarRating = CatchDifficultyCalculator.CalculatePartialLocalStarRating(data.ActionProbability, precisionStrain, speedStrain);
                data.LocalStarRating = CatchDifficultyCalculator.CalculateLocalStarRating(data.ActionProbability, precisionStrain, speedStrain, aimStrain);
            }
        }

        /// <summary>
        /// Calculates the precision value for a given note.
        /// </summary>
        /// <param name="note">The current note.</param>
        /// <param name="prev">The previous note.</param>
        /// <param name="next">The next note.</param>
        /// <returns>The precision value in milliseconds, or null if it is infinite.</returns>
        private static double? calculatePrecision(CatchDifficultyHitObject note, CatchDifficultyHitObject prev, CatchDifficultyHitObject next)
        {
            CatchMovementData data = note.MovementData;
            CatchMovementData prevData = prev.MovementData;

            double prevForwardCatcherPosition = note.IsMovingRight ? prevData.RightCatcherPosition : prevData.LeftCatcherPosition;
            _ = CatchPreprocessingUtils.CalculateMinimalDistance(note, prev);
            double minimalVelocity = CatchPreprocessingUtils.CalculateMinimalHyperdashSpeed(note, prev, next);
            double nextToPrevDeltaTime = next.StartTime - prev.StartTime;

            switch (data.NotePattern)
            {
                case PatternType.HyperdashAfterBreak:
                {
                    return note.CatcherWidth;
                }

                case PatternType.EdgedashAfterBreak:
                {
                    return Math.Abs(data.RightCatcherPosition - data.LeftCatcherPosition);
                }

                case PatternType.StackContinuation:
                {
                    break;
                }

                case PatternType.JumpAfterHyperjump:
                {
                    if (next.DeltaPosition - next.DeltaTime >= note.HalfCatcherWidth)
                    {
                        return (note.CatcherWidth + next.DeltaTime - data.Directionize(next.DeltaPosition)) / (2.0 * minimalVelocity);
                    }

                    double first = note.HalfCatcherWidth - next.DeltaPosition + nextToPrevDeltaTime;
                    double second = (data.Directionize(note.Position - prevForwardCatcherPosition) - note.HalfCatcherWidth) / minimalVelocity;
                    double third = first - second;

                    return third / 2.0;
                }

                case PatternType.Hyperjumps:
                {
                    double first = (note.HalfCatcherWidth - next.DeltaPosition) / CatchPreprocessingUtils.CalculatePerfectHyperdashSpeed(next);
                    double second = (data.Directionize(note.Position - prevForwardCatcherPosition) - note.HalfCatcherWidth) / minimalVelocity;

                    return (first - second + nextToPrevDeltaTime) / 2.0;
                }

                case PatternType.HyperjumpAfterJump:
                {
                    double optimalVelocity = Math.Abs(next.Position - (prevForwardCatcherPosition + data.Directionize(note.DeltaTime))) / Math.Max(next.DeltaTime - 1000.0 / 60.0, 1);

                    if (data.Directionize(note.Position - prevForwardCatcherPosition) <= note.DeltaTime - note.HalfCatcherWidth)
                    {
                        return note.HalfCatcherWidth;
                    }

                    double first = data.Directionize(next.Position - prevForwardCatcherPosition);
                    double second = (first + note.HalfCatcherWidth - note.DeltaTime) / optimalVelocity;
                    double third = nextToPrevDeltaTime + data.Directionize(prevForwardCatcherPosition - note.Position) + note.HalfCatcherWidth;
                    double fourth = second + third;

                    return fourth / 2.0;
                }

                case PatternType.Jumps:
                {
                    return (next.DeltaTime + note.CatcherWidth - next.DeltaPosition) / 2.0;
                }

                case PatternType.PotentialStandstill:
                {
                    return note.DeltaTime;
                }

                case PatternType.AcceleratingStream:
                {
                    if (next.DeltaPosition > next.DeltaTime / 2.0 + note.HalfCatcherWidth)
                    {
                        return Math.Abs((note.Position + data.Directionize(note.HalfCatcherWidth)) - (next.Position - data.Directionize(note.HalfCatcherWidth + next.DeltaTime)));
                    }

                    break;
                }
            }

            return null;
        }

        /// <summary>
        /// Calculates the aim value for a given note.
        /// </summary>
        /// <param name="note"></param>
        /// <param name="prev"></param>
        /// <param name="next"></param>
        /// <returns></returns>
        private static double? calculateAim(CatchDifficultyHitObject note, CatchDifficultyHitObject prev, CatchDifficultyHitObject next)
        {
            CatchMovementData data = note.MovementData;
            CatchMovementData prevData = prev.MovementData;

            return data.NotePattern switch
            {
                PatternType.BreakBeginningRequiringMovement => note.CatcherWidth,
                PatternType.BreakBeginningWithoutMovement => note.CatcherWidth,
                PatternType.SingleNote => note.CatcherWidth,
                PatternType.StackAfterBreak => note.CatcherWidth,
                PatternType.PotentialStack => data.ActionProbability == 0 ? note.CatcherWidth - next.DeltaPosition : null,
                PatternType.NarrowStack => note.CatcherWidth - next.DeltaPosition,
                PatternType.StackContinuation => prevData.ActionProbability == 1 && data.ActionProbability == 0 ? note.CatcherWidth - next.DeltaPosition : null,
                _ => null
            };
        }
    }
}
