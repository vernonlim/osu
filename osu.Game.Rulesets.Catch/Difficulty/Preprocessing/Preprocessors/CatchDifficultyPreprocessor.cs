// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Catch.Difficulty.Preprocessing.Data;
using osu.Game.Rulesets.Catch.Difficulty.Preprocessing.Utils;
using osu.Game.Rulesets.Difficulty.Preprocessing;

namespace osu.Game.Rulesets.Catch.Difficulty.Preprocessing.Preprocessors
{
    public static class CatchDifficultyPreprocessor
    {
        public static void Process(List<DifficultyHitObject> hitObjects)
        {
            List<CatchDifficultyHitObject> leftGuaranteedActions = new List<CatchDifficultyHitObject>();
            List<CatchDifficultyHitObject> rightGuaranteedActions = new List<CatchDifficultyHitObject>();
            List<CatchDifficultyHitObject> leftAmbiguousActions = new List<CatchDifficultyHitObject>();
            List<CatchDifficultyHitObject> rightAmbiguousActions = new List<CatchDifficultyHitObject>();
            CatchDifficultyHitObject? lastLeftHyper = null;
            CatchDifficultyHitObject? lastRightHyper = null;
            CatchDifficultyHitObject? furthestLeft = null;
            CatchDifficultyHitObject? furthestRight = null;
            CatchDifficultyHitObject? lastActionNote = null;

            for (int i = 1; i < hitObjects.Count - 1; i++)
            {
                CatchDifficultyHitObject note = (CatchDifficultyHitObject)hitObjects[i];
                CatchDifficultyHitObject prev = (CatchDifficultyHitObject)hitObjects[i - 1];
                CatchDifficultyHitObject next = (CatchDifficultyHitObject)hitObjects[i + 1];
                CatchMovementData data = note.MovementData;
                CatchMovementData prevData = prev.MovementData;

                data.NotePrecision = calculatePrecision(note, prev, next);

                if (prevData.ActionProbability == 1)
                {
                    if (prevData.KeyPress == MovementKey.Left)
                    {
                        leftGuaranteedActions.Add(prev);
                    }
                    else if (prevData.KeyPress == MovementKey.Right)
                    {
                        rightGuaranteedActions.Add(prev);
                    }
                }
                else if (prevData.ActionProbability > 0.0)
                {
                    if (prevData.KeyPress == MovementKey.Left)
                    {
                        leftAmbiguousActions.Add(prev);
                    }
                    else if (prevData.KeyPress == MovementKey.Right)
                    {
                        rightAmbiguousActions.Add(prev);
                    }
                }

                if (prevData.ActionProbability > 0.0)
                {
                    lastActionNote = prev;
                }

                if (prev.IsHyper)
                {
                    if (note.IsMovingRight)
                    {
                        lastRightHyper = prev;
                        furthestRight = null;
                    }
                    else
                    {
                        lastLeftHyper = prev;
                        furthestLeft = null;
                    }
                }

                if (!note.IsHyper && (furthestRight is null || furthestRight.Position < note.Position))
                {
                    furthestRight = note;
                }

                if (!note.IsHyper && (furthestLeft is null || furthestLeft.Position > note.Position))
                {
                    furthestLeft = note;
                }

                data.FurthestLeft = furthestLeft;
                data.FurthestRight = furthestRight;

                if (note.IsHyper)
                {
                    double lastActionTime = lastActionNote is not null ? (lastActionNote.MovementData.IsRealAction ? lastActionNote.StartTime : lastActionNote.MovementData.EffectiveTime) : -1;

                    if (next.Position - note.Position >= 0)
                    {
                        if (lastLeftHyper != null
                            && (lastActionNote is null || lastActionTime <= lastLeftHyper.StartTime)
                            && (!lastLeftHyper.MovementData.IsStack)
                            && data.ActionProbability == 0
                            && Math.Abs(lastLeftHyper.Position - note.Position) > note.HalfCatcherWidth)
                        {
                            data.IsRealAction = false;
                            data.ActionProbability = 1;
                            data.EffectiveTime = (lastLeftHyper.StartTime + note.StartTime) / 2.0;
                            data.KeyPress = MovementKey.Right;

                            if (furthestLeft is not null)
                            {
                                CatchDifficultyHitObject? furPrev = furthestLeft.PreviousNote(0);
                                CatchDifficultyHitObject? furNext = furthestLeft.NextNote(0);

                                if (furPrev is not null && furNext is not null)
                                {
                                    double actionProbability = furthestLeft.MovementData.ActionProbability;
                                    PatternType notePattern = furthestLeft.MovementData.NotePattern;
                                    furthestLeft.MovementData.NotePattern = CatchMovementPreprocessor.ClassifyAsDirectionChange(furthestLeft, furPrev);
                                    CatchMovementPreprocessor.UpdateData(furthestLeft, furPrev, furNext);

                                    data.NotePrecision = calculatePrecision(furthestLeft, furPrev, furNext);

                                    furthestLeft.MovementData.NotePattern = CatchMovementPreprocessor.Classify(furthestLeft, furPrev, furNext);
                                    CatchMovementPreprocessor.UpdateData(furthestLeft, furPrev, furNext);

                                    furthestLeft.MovementData.ActionProbability = actionProbability;
                                    furthestLeft.MovementData.NotePattern = notePattern;
                                }
                            }
                        }
                    }
                    else
                    {
                        if (lastRightHyper != null
                            && (lastActionNote is null || lastActionTime <= lastRightHyper.StartTime)
                            && (!lastRightHyper.MovementData.IsStack)
                            && data.ActionProbability == 0
                            && Math.Abs(lastRightHyper.Position - note.Position) > note.HalfCatcherWidth)
                        {
                            data.IsRealAction = false;
                            data.ActionProbability = 1;
                            data.EffectiveTime = (lastRightHyper.StartTime + note.StartTime) / 2.0;
                            data.KeyPress = MovementKey.Left;

                            if (furthestRight is not null)
                            {
                                CatchDifficultyHitObject? furPrev = furthestRight.PreviousNote(0);
                                CatchDifficultyHitObject? furNext = furthestRight.NextNote(0);

                                if (furPrev is not null && furNext is not null)
                                {
                                    double actionProbability = furthestRight.MovementData.ActionProbability;
                                    PatternType notePattern = furthestRight.MovementData.NotePattern;
                                    furthestRight.MovementData.NotePattern = CatchMovementPreprocessor.ClassifyAsDirectionChange(furthestRight, furPrev);
                                    CatchMovementPreprocessor.UpdateData(furthestRight, furPrev, furNext);

                                    data.NotePrecision = calculatePrecision(furthestRight, furPrev, furNext);

                                    furthestRight.MovementData.NotePattern = CatchMovementPreprocessor.Classify(furthestRight, furPrev, furNext);
                                    CatchMovementPreprocessor.UpdateData(furthestRight, furPrev, furNext);

                                    furthestRight.MovementData.ActionProbability = actionProbability;
                                    furthestRight.MovementData.NotePattern = notePattern;
                                }
                            }
                        }
                    }
                }

                data.RawPrecisionStrain = calculatePrecisionStrain(note);
                if (data.NotePattern == PatternType.Hyperjumps)
                    data.PrecisionStrain = (0.86 * data.RawPrecisionStrain + 0.14 * prevData.RawPrecisionStrain * prevData.ActionProbability) * data.ActionProbability;
                else if (data.NotePattern == PatternType.HyperjumpAfterJump)
                    data.PrecisionStrain = data.PrecisionStrain = (0.9 * data.RawPrecisionStrain + 0.1 * prevData.RawPrecisionStrain * prevData.ActionProbability) * data.ActionProbability;
                else if (data.NotePattern == PatternType.JumpAfterHyperjump)
                    data.PrecisionStrain = data.PrecisionStrain = (0.93 * data.RawPrecisionStrain + 0.07 * prevData.RawPrecisionStrain * prevData.ActionProbability) * data.ActionProbability;
                else if (data.NotePattern == PatternType.Jumps)
                    data.PrecisionStrain = data.PrecisionStrain = (0.95 * data.RawPrecisionStrain + 0.05 * prevData.RawPrecisionStrain * prevData.ActionProbability) * data.ActionProbability;
                else
                    data.PrecisionStrain = 1.0 * data.RawPrecisionStrain * data.ActionProbability;

                var recentGuaranteed = new[] { leftGuaranteedActions.LastOrDefault(), rightGuaranteedActions.LastOrDefault() }
                                       .Where(n => n is not null)
                                       .MaxBy(n => n!.MovementData.EffectiveTime);

                var recentAmbiguous = new[] { leftAmbiguousActions.LastOrDefault(), rightAmbiguousActions.LastOrDefault() }
                                      .Where(n => n is not null)
                                      .MaxBy(n => n!.MovementData.EffectiveTime);

                double burst = 0;
                double consistency = 0;
                double snap = 0;

                if (data.KeyPress == MovementKey.Left)
                {
                    burst = calculateSpeed(note, leftGuaranteedActions.LastOrDefault(), leftAmbiguousActions.LastOrDefault(), timeToSpeedBurst);
                    consistency = calculateSpeed(note, leftGuaranteedActions.AsEnumerable().Reverse().Skip(1).FirstOrDefault(), leftAmbiguousActions.AsEnumerable().Reverse().Skip(1).FirstOrDefault(), timeToSpeedConsistency);
                    snap = calculateSpeed(note, recentGuaranteed, recentAmbiguous, timeToSpeedSnap);
                }
                else if (data.KeyPress == MovementKey.Right)
                {
                    burst = calculateSpeed(note, rightGuaranteedActions.LastOrDefault(), rightAmbiguousActions.LastOrDefault(), timeToSpeedBurst);
                    consistency = calculateSpeed(note, rightGuaranteedActions.AsEnumerable().Reverse().Skip(1).FirstOrDefault(), leftAmbiguousActions.AsEnumerable().Reverse().Skip(1).FirstOrDefault(), timeToSpeedConsistency);
                    snap = calculateSpeed(note, recentGuaranteed, recentAmbiguous, timeToSpeedSnap);
                }

                data.BurstSpeed = burst * 2 * 12 * 120;
                data.ConsistencySpeed = consistency * 2 * 12 * 120;
                data.SnapSpeed = snap * 2 * 12 * 120;
            }
        }

        private static double calculatePrecisionStrain(CatchDifficultyHitObject note)
        {
            double amplitude = 44.5; //governs how much very low precision values are worth
            double limit = 1.0; //precision strain for very high precision values (easy jumps)
            double shift = -8.0; //shifts the boundary between concave and convex part (shifts the curve)
            double pace = 33.0; //measures how fast strain decreases between easy and hard jumps

            double precision = note.MovementData.NotePrecision is null
                ? 0
                : limit + amplitude / (1 + Math.Exp(((double)note.MovementData.NotePrecision + shift) / pace));

            return precision / 18 * 41;
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

                    double precisionCorrection = CatchPreprocessingUtils.CalculatePrecisionCorrection(note.DeltaPosition, Math.Max(0, note.DeltaTime - note.DeltaPosition), note.CatcherWidth);
                    data.PrecisionCorrection = precisionCorrection;

                    data.EffectiveTime = standstillTime * (precisionCorrection - 1) + data.EffectiveTime * (2 - precisionCorrection);

                    return precisionCorrection * rawPrecision;
                }

                case PatternType.Jumps:
                {
                    double acceleratingTime = (data.Directionize(prev.Position - next.Position) - note.HalfCatcherWidth + 2 * note.StartTime) / 2.0;
                    double? rawPrecision = calculateRawPrecision(note, prev, next, PatternType.Jumps);

                    double precisionCorrection = CatchPreprocessingUtils.CalculatePrecisionCorrection(note.DeltaPosition, Math.Max(0, note.DeltaTime - note.DeltaPosition), note.CatcherWidth);
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
        /// <param name="type"></param>
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
                    double optimalVelocity = Math.Abs(next.Position - (prevForwardCatcherPosition + data.Directionize(note.DeltaTime))) / Math.Max(next.DeltaTime - note.FrameTime, 1);

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
        /// Calculates the speed value for a given note.
        /// </summary>
        /// <param name="note"></param>
        /// <param name="prevGuaranteedAction"></param>
        /// <param name="prevAmbiguousAction"></param>
        /// <param name="timeToSpeed"></param>
        /// <returns></returns>
        private static double calculateSpeed(CatchDifficultyHitObject note, CatchDifficultyHitObject? prevGuaranteedAction, CatchDifficultyHitObject? prevAmbiguousAction, Func<double, double> timeToSpeed)
        {
            CatchMovementData data = note.MovementData;
            const double maxRatio = 0.2;
            const double effectiveImportance = 0.05;

            double effectiveRatio = Math.Min(maxRatio, (note.StartTime - data.EffectiveTime) / note.DeltaTime);
            double maxTime = data.EffectiveTime;
            if (data.EffectiveTime <= note.StartTime)
                maxTime = note.StartTime - note.DeltaTime * effectiveRatio * effectiveImportance / maxRatio;

            if (data.ActionProbability > 0)
            {
                if (data.ActionProbability < 1
                    && data.DisplayPattern != PatternType.StackEnd
                    && prevGuaranteedAction is not null)
                {
                    double minGuaranteedTime = Math.Min(prevGuaranteedAction.StartTime, prevGuaranteedAction.MovementData.EffectiveTime);
                    return timeToSpeed(Math.Max(maxTime - minGuaranteedTime, 1));
                }

                if (prevAmbiguousAction is null && prevGuaranteedAction is not null)
                {
                    double minGuaranteedTime = Math.Min(prevGuaranteedAction.StartTime, prevGuaranteedAction.MovementData.EffectiveTime);
                    return timeToSpeed(Math.Max(maxTime - minGuaranteedTime, 1));
                }

                if (prevGuaranteedAction is null && prevAmbiguousAction is not null)
                {
                    double minAmbiguousTime = Math.Min(prevAmbiguousAction.StartTime, prevAmbiguousAction.MovementData.EffectiveTime);
                    return prevAmbiguousAction.MovementData.ActionProbability * timeToSpeed(Math.Max(maxTime - minAmbiguousTime, 1));
                }

                if (prevAmbiguousAction is not null && prevGuaranteedAction is not null)
                {
                    double minGuaranteedTime = Math.Min(prevGuaranteedAction.StartTime, prevGuaranteedAction.MovementData.EffectiveTime);
                    double minAmbiguousTime = Math.Min(prevAmbiguousAction.StartTime, prevAmbiguousAction.MovementData.EffectiveTime);

                    if (prevGuaranteedAction.MovementData.EffectiveTime >= prevAmbiguousAction.MovementData.EffectiveTime)
                    {
                        return timeToSpeed(Math.Max(maxTime - minGuaranteedTime, 1));
                    }

                    double ambiguousSpeed = timeToSpeed(Math.Max(maxTime - minAmbiguousTime, 1));
                    double guaranteedSpeed = timeToSpeed(Math.Max(maxTime - minGuaranteedTime, 1));
                    double prevActionProbability = prevAmbiguousAction.MovementData.ActionProbability;

                    return prevActionProbability * ambiguousSpeed + (1 - prevActionProbability) * guaranteedSpeed;
                }
            }

            return 0;
        }

        //Functions below are identical, but splitting them may be useful in future.
        private static double timeToSpeedSnap(double time)
        {
            double amplitude = 19.1; //governs how much very low speed values are worth
            double limit = 1.0; //speed strain for very high speed values (easy jumps)
            double shift = -10.0; //measures how fast strain decreases between slow and fast jumps (shifts the curve)
            double pace = 50.0; //normalises shift

            double speed = limit + amplitude / (1 + Math.Exp((time + shift) / pace));

            return 0.88 * speed / 10000;
        }

        private static double timeToSpeedBurst(double time)
        {
            double amplitude = 19.1; //governs how much very low speed values are worth
            double limit = 1.0; //speed strain for very high speed values (easy jumps)
            double shift = -10.0; //measures how fast strain decreases between slow and fast jumps (shifts the curve)
            double pace = 50.0; //normalises shift

            double speed = limit + amplitude / (1 + Math.Exp((time / 2 + shift) / pace));

            return 1.01 * speed / 10000;
        }

        private static double timeToSpeedConsistency(double time)
        {
            double amplitude = 19.1; //governs how much very low speed values are worth
            double limit = 1.0; //speed strain for very high speed values (easy jumps)
            double shift = -10.0; //measures how fast strain decreases between slow and fast jumps (shifts the curve)
            double pace = 52.0; //normalises shift

            double speed = limit + amplitude / (1 + Math.Exp((time / 4 + shift) / pace));

            return 1.1 * speed / 10000;
        }
    }
}
