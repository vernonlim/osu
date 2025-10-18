// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
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
            CatchDifficultyHitObject? prevLeftGuaranteedAction = null;
            CatchDifficultyHitObject? prevRightGuaranteedAction = null;
            CatchDifficultyHitObject? prevLeftAmbiguousAction = null;
            CatchDifficultyHitObject? prevRightAmbiguousAction = null;

            for (int i = 1; i < hitObjects.Count - 1; i++)
            {
                CatchDifficultyHitObject note = (CatchDifficultyHitObject)hitObjects[i];
                CatchDifficultyHitObject prev = (CatchDifficultyHitObject)hitObjects[i - 1];
                CatchDifficultyHitObject next = (CatchDifficultyHitObject)hitObjects[i + 1];
                CatchMovementData prevData = prev.MovementData;

                if (prevData.ActionProbability > 0.97)
                {
                    if (prevData.KeyPress == MovementKey.Left)
                    {
                        prevLeftGuaranteedAction = prev;
                    }
                    else if (prevData.KeyPress == MovementKey.Right)
                    {
                        prevRightGuaranteedAction = prev;
                    }
                }
                else if (prevData.ActionProbability >= 0.03 && prevData.ActionProbability <= 0.97)
                {
                    if (prevData.KeyPress == MovementKey.Left)
                    {
                        prevLeftAmbiguousAction = prev;
                    }
                    else if (prevData.KeyPress == MovementKey.Right)
                    {
                        prevRightAmbiguousAction = prev;
                    }
                }

                CatchMovementData data = note.MovementData;

                data.NotePrecision = calculatePrecision(note, prev, next);
                data.NoteAim = calculateAim(note, prev, next);

                if (data.KeyPress == MovementKey.Left)
                {
                    data.RawNoteSpeed = calculateSpeed(note, prevLeftGuaranteedAction, prevLeftAmbiguousAction);
                }
                else if (data.KeyPress == MovementKey.Right)
                {
                    data.RawNoteSpeed = calculateSpeed(note, prevRightGuaranteedAction, prevRightAmbiguousAction);
                }
                else if (data.KeyPress == MovementKey.Dash)
                {
                    var recentGuaranteed = new[] { prevLeftGuaranteedAction, prevRightGuaranteedAction }
                                           .Where(n => n is not null)
                                           .MaxBy(n => n!.MovementData.EffectiveTime);

                    var recentAmbiguous = new[] { prevLeftAmbiguousAction, prevRightAmbiguousAction }
                                          .Where(n => n is not null)
                                          .MaxBy(n => n!.MovementData.EffectiveTime);

                    data.RawNoteSpeed = calculateSpeed(note, recentGuaranteed, recentAmbiguous);
                }

                double precisionStrain = PrecisionEvaluator.EvaluateDifficultyOf(note);
                double aimStrain = AimEvaluator.EvaluateDifficultyOf(note);
                double speedStrain = SpeedEvaluator.EvaluateDifficultyOf(note);

                data.NoteSpeed = speedStrain;

                data.PartialLocalStarRating = CatchDifficultyCalculator.CalculatePartialLocalStarRating(data.ActionProbability, precisionStrain, speedStrain);
                data.LocalStarRating = CatchDifficultyCalculator.CalculateLocalStarRating(data.ActionProbability, precisionStrain, speedStrain, aimStrain);
            }
        }

        /// <summary>
        /// Calculates the precision value for a given note, and adjusts its effective time if needed.
        /// </summary>
        /// <param name="note"></param>
        /// <param name="prev"></param>
        /// <param name="next"></param>
        /// <returns></returns>
        private static double? calculatePrecision(CatchDifficultyHitObject note, CatchDifficultyHitObject prev, CatchDifficultyHitObject next)
        {
            CatchMovementData data = note.MovementData;
            CatchMovementData prevData = prev.MovementData;

            switch (data.NotePattern)
            {
                case PatternType.HyperjumpAfterJump:
                {
                    double? rawPrecision = calculateRawPrecision(note, prev, next, PatternType.HyperjumpAfterJump);

                    double standstillTime = CatchPreprocessingUtils.CalculatePotentialStandstillEffectiveTime(note, next);

                    double precisionCorrection = CatchPreprocessingUtils.CalculatePrecisionCorrection(note.DeltaPosition, rawPrecision, note.CatcherWidth);
                    data.PrecisionCorrection = precisionCorrection;

                    data.EffectiveTime = standstillTime * (precisionCorrection - 1) + data.EffectiveTime * (2 - precisionCorrection);

                    return precisionCorrection * rawPrecision;
                }

                case PatternType.Jumps:
                {
                    double acceleratingTime = (data.Directionize(prev.Position - next.Position) - note.HalfCatcherWidth + 2 * note.StartTime) / 2.0;
                    double? rawPrecision = calculateRawPrecision(note, prev, next, PatternType.Jumps);

                    double precisionCorrection = CatchPreprocessingUtils.CalculatePrecisionCorrection(note.DeltaPosition, rawPrecision, note.CatcherWidth);
                    data.PrecisionCorrection = precisionCorrection;

                    data.EffectiveTime = acceleratingTime * (precisionCorrection - 1) + data.EffectiveTime * (2 - precisionCorrection);

                    return precisionCorrection * rawPrecision;
                }

                default:
                {
                    return calculateRawPrecision(note, prev, next, data.NotePattern);
                }
            }
        }

        /// <summary>
        /// Calculates the raw precision value for a given note.
        /// </summary>
        /// <param name="note">The current note.</param>
        /// <param name="prev">The previous note.</param>
        /// <param name="next">The next note.</param>
        /// <returns>The precision value in milliseconds, or null if it is infinite.</returns>
        private static double? calculateRawPrecision(CatchDifficultyHitObject note, CatchDifficultyHitObject prev, CatchDifficultyHitObject next, PatternType type)
        {
            CatchMovementData data = note.MovementData;
            CatchMovementData prevData = prev.MovementData;

            double prevForwardCatcherPosition = note.IsMovingRight ? prevData.RightCatcherPosition : prevData.LeftCatcherPosition;
            _ = CatchPreprocessingUtils.CalculateMinimalDistance(note, prev);
            double minimalVelocity = CatchPreprocessingUtils.CalculateMinimalHyperdashSpeed(note, prev);
            double nextToPrevDeltaTime = next.StartTime - prev.StartTime;

            switch (type)
            {
                case PatternType.StackContinuation:
                {
                    break;
                }

                case PatternType.JumpAfterHyperjump:
                {
                    if (next.DeltaPosition - next.DeltaTime >= note.HalfCatcherWidth)
                    {
                        return (note.CatcherWidth) / (2.0 * minimalVelocity);
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
                    if (note.DeltaPosition <= note.HalfCatcherWidth)
                    {
                        double first = (note.CatcherWidth - 2 * next.DeltaPosition) / (2 * CatchPreprocessingUtils.CalculatePerfectHyperdashSpeed(next));
                        double second = next.DeltaTime + note.DeltaPosition + note.HalfCatcherWidth;
                        return first + second;
                    }

                    double third = (note.CatcherWidth - 2 * next.DeltaPosition) / (2 * CatchPreprocessingUtils.CalculateSpeedFrom(next, note.BackwardNoteBorder));
                    double fourth = next.DeltaTime + note.CatcherWidth;

                    return third + fourth;
                }

                case PatternType.AcceleratingStream:
                {
                    if (next.DeltaPosition > next.DeltaTime / 2.0 + note.HalfCatcherWidth)
                    {
                        return next.DeltaTime + note.CatcherWidth - next.DeltaPosition;
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

            double minimalVelocity = CatchPreprocessingUtils.CalculateMinimalHyperdashSpeed(note, prev);

            if (data.BeltBeginning is not null)
            {
                return null;
            }

            double? aim = data.NotePattern switch
            {
                PatternType.SingleNote => note.DeltaPosition > note.HalfCatcherWidth ? note.CatcherWidth : null,
                PatternType.StackAfterBreak => note.CatcherWidth,
                PatternType.EdgedashAfterBreak => Math.Abs(data.RightCatcherPosition - data.LeftCatcherPosition),
                PatternType.HyperdashAfterBreak => note.CatcherWidth,
                PatternType.PotentialStack => data.ActionProbability == 0 ? note.CatcherWidth - Math.Min(note.DeltaPosition, next.DeltaPosition) : null,
                PatternType.NarrowStack => note.CatcherWidth - Math.Min(note.DeltaPosition, next.DeltaPosition),
                PatternType.StackContinuation => prevData.ActionProbability == 1 && data.ActionProbability == 0 ? note.CatcherWidth - Math.Min(note.DeltaPosition, next.DeltaPosition) : null,
                PatternType.JumpAfterHyperjump => note.CatcherWidth * (1 - next.DeltaPosition * Math.Pow(minimalVelocity, 0.5) / (note.CatcherWidth + (Math.Pow(minimalVelocity, 0.5) - 1) * next.DeltaPosition)),
                PatternType.Jumps => note.CatcherWidth - Math.Min(note.DeltaPosition, next.DeltaPosition),
                _ => null,
            };

            if (aim is not null)
            {
                aim = Math.Max((double)aim, 1.0);
            }

            return aim;
        }

        /// <summary>
        /// Calculates the speed value for a given note.
        /// </summary>
        /// <param name="note"></param>
        /// <param name="prevGuaranteedAction"></param>
        /// <param name="prevAmbiguousAction"></param>
        /// <returns></returns>
        private static double calculateSpeed(CatchDifficultyHitObject note, CatchDifficultyHitObject? prevGuaranteedAction, CatchDifficultyHitObject? prevAmbiguousAction)
        {
            CatchMovementData data = note.MovementData;

            if (data.ActionProbability >= 0.03)
            {
                if (data.ActionProbability <= 0.97
                    && data.DisplayPattern != PatternType.StackEnd
                    && prevGuaranteedAction is not null)
                {
                    return timeToSpeed(Math.Max(data.EffectiveTime - prevGuaranteedAction.MovementData.EffectiveTime, 1));
                }

                if (prevAmbiguousAction is null && prevGuaranteedAction is not null)
                {
                    return timeToSpeed(Math.Max(data.EffectiveTime - prevGuaranteedAction.MovementData.EffectiveTime, 1));
                }

                if (prevGuaranteedAction is null && prevAmbiguousAction is not null)
                {
                    return prevAmbiguousAction.MovementData.ActionProbability * timeToSpeed(Math.Max(data.EffectiveTime - prevAmbiguousAction.MovementData.EffectiveTime, 1));
                }

                if (prevAmbiguousAction is not null && prevGuaranteedAction is not null)
                {
                    if (prevGuaranteedAction.MovementData.EffectiveTime >= prevAmbiguousAction.MovementData.EffectiveTime)
                    {
                        return timeToSpeed(Math.Max(data.EffectiveTime - prevGuaranteedAction.MovementData.EffectiveTime, 1));
                    }

                    double ambiguousSpeed = timeToSpeed(Math.Max(data.EffectiveTime - prevAmbiguousAction.MovementData.EffectiveTime, 1));
                    double guaranteedSpeed = timeToSpeed(Math.Max(data.EffectiveTime - prevGuaranteedAction.MovementData.EffectiveTime, 1));
                    double prevActionProbability = prevAmbiguousAction.MovementData.ActionProbability;

                    return prevActionProbability * ambiguousSpeed + (1 - prevActionProbability) * guaranteedSpeed;
                }
            }

            return 0;
        }

        private static double timeToSpeed(double time)
        {
            const double alpha = 0.68;

            return Math.Pow(time, -alpha);
        }
    }
}
