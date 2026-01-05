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
        public static void Process(List<DifficultyHitObject> hitObjects, double catcherWidth, double clockRate, double frameTime, double playfieldBorder)
        {
            List<CatchDifficultyHitObject> guaranteedActions = new List<CatchDifficultyHitObject>();
            List<CatchDifficultyHitObject> leftGuaranteedActions = new List<CatchDifficultyHitObject>();
            List<CatchDifficultyHitObject> rightGuaranteedActions = new List<CatchDifficultyHitObject>();
            List<CatchDifficultyHitObject> ambiguousActions = new List<CatchDifficultyHitObject>();
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

                (data.NotePrecision, data.EffectiveTime) = calculatePrecision(note, prev, next, data.NotePattern, catcherWidth, frameTime);

                if (prevData.ActionProbability == 1)
                {
                    guaranteedActions.Add(prev);

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
                    ambiguousActions.Add(prev);

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

                note.DisplayData.FurthestLeft = furthestLeft;
                note.DisplayData.FurthestRight = furthestRight;

                if (note.IsHyper)
                {
                    double lastActionTime = lastActionNote is not null ? (lastActionNote.MovementData.IsRealAction ? lastActionNote.StartTime : lastActionNote.MovementData.EffectiveTime) : -1;

                    if (next.Position - note.Position >= 0)
                    {
                        if (lastLeftHyper != null
                            && (lastActionNote is null || lastActionTime <= lastLeftHyper.StartTime)
                            && (!lastLeftHyper.MovementData.IsStack)
                            && data.ActionProbability == 0
                            && Math.Abs(lastLeftHyper.Position - note.Position) > catcherWidth / 2.0)
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
                                    CatchMovementPreprocessor.UpdateData(furthestLeft, furPrev, furNext, catcherWidth, clockRate, frameTime, playfieldBorder);

                                    (data.NotePrecision, _) = calculatePrecision(furthestLeft, furPrev, furNext, furthestLeft.MovementData.NotePattern, catcherWidth, frameTime);

                                    furthestLeft.MovementData.NotePattern = CatchMovementPreprocessor.Classify(furthestLeft, furPrev, furNext, catcherWidth, clockRate);
                                    CatchMovementPreprocessor.UpdateData(furthestLeft, furPrev, furNext, catcherWidth, clockRate, frameTime, playfieldBorder);

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
                            && Math.Abs(lastRightHyper.Position - note.Position) > catcherWidth / 2.0)
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
                                    CatchMovementPreprocessor.UpdateData(furthestRight, furPrev, furNext, catcherWidth, clockRate, frameTime, playfieldBorder);

                                    (data.NotePrecision, _) = calculatePrecision(furthestRight, furPrev, furNext, furthestRight.MovementData.NotePattern, catcherWidth, frameTime);

                                    furthestRight.MovementData.NotePattern = CatchMovementPreprocessor.Classify(furthestRight, furPrev, furNext, catcherWidth, clockRate);
                                    CatchMovementPreprocessor.UpdateData(furthestRight, furPrev, furNext, catcherWidth, clockRate, frameTime, playfieldBorder);

                                    furthestRight.MovementData.ActionProbability = actionProbability;
                                    furthestRight.MovementData.NotePattern = notePattern;
                                }
                            }
                        }
                    }
                }

                data.OriginalPrecision = data.NotePrecision;

                // Future precision
                if ((i + 2) < hitObjects.Count
                    && (next.MovementData.NotePattern == PatternType.AcceleratingStream
                        || (next.MovementData.NotePattern == PatternType.ExtendedDirectionChange && !next.IsHyper)))
                {
                    CatchDifficultyHitObject nextNext = (CatchDifficultyHitObject)hitObjects[i + 2];

                    double? currentPrecision = data.NotePrecision;

                    PatternType type = CatchMovementPreprocessor.Classify(note, prev, nextNext, catcherWidth, clockRate);

                    (double? futurePrecision, _) = calculatePrecision(note, prev, nextNext, type, catcherWidth, frameTime);

                    double? weightedPrecision;

                    if (currentPrecision is null && next.MovementData.ActionProbability == 0)
                    {
                        weightedPrecision = futurePrecision;
                    }
                    else if (futurePrecision is null && next.MovementData.ActionProbability == 1)
                    {
                        weightedPrecision = currentPrecision;
                    }
                    else
                    {
                        weightedPrecision = next.MovementData.ActionProbability * currentPrecision + (1.0 - next.MovementData.ActionProbability) * futurePrecision;
                    }

                    data.FuturePrecision = futurePrecision;

                    if (currentPrecision == null)
                    {
                        data.NotePrecision = weightedPrecision;

                        if (weightedPrecision != null)
                        {
                            data.FuturePrecisionUtilized = true;
                        }
                    }
                    else if (weightedPrecision == null)
                    {
                        data.NotePrecision = currentPrecision;
                    }
                    else
                    {
                        if (currentPrecision.Value > weightedPrecision.Value)
                        {
                            data.FuturePrecisionUtilized = true;
                            note.DisplayData.FuturePrecisionDifference = weightedPrecision.Value - currentPrecision.Value;
                        }

                        data.NotePrecision = Math.Min(currentPrecision.Value, weightedPrecision.Value);
                    }
                }

                // Precision calculation
                const double raw_weight_hyperjumps = 0.93;
                const double raw_weight_hyperjump_after_jump = 0.98;
                const double raw_weight_jump_after_hyperjump = 0.98;
                const double raw_weight_jumps = 1.0;

                data.RawPrecisionStrain = calculatePrecisionStrain(note);
                if (data.NotePattern == PatternType.Hyperjumps)
                    data.PrecisionStrain = (raw_weight_hyperjumps * data.RawPrecisionStrain + (1.0 - raw_weight_hyperjumps) * prevData.RawPrecisionStrain * prevData.ActionProbability) * data.ActionProbability;
                else if (data.NotePattern == PatternType.HyperjumpAfterJump)
                    data.PrecisionStrain = data.PrecisionStrain = (raw_weight_hyperjump_after_jump * data.RawPrecisionStrain + (1.0 - raw_weight_hyperjump_after_jump) * prevData.RawPrecisionStrain * prevData.ActionProbability) * data.ActionProbability;
                else if (data.NotePattern == PatternType.JumpAfterHyperjump)
                    data.PrecisionStrain = data.PrecisionStrain = (raw_weight_jump_after_hyperjump * data.RawPrecisionStrain + (1.0 - raw_weight_jump_after_hyperjump) * prevData.RawPrecisionStrain * prevData.ActionProbability) * data.ActionProbability;
                else if (data.NotePattern == PatternType.Jumps)
                    data.PrecisionStrain = data.PrecisionStrain = (raw_weight_jumps * data.RawPrecisionStrain + (1.0 - raw_weight_jumps) * prevData.RawPrecisionStrain * prevData.ActionProbability) * data.ActionProbability;
                else
                    data.PrecisionStrain = data.RawPrecisionStrain * data.ActionProbability;

                // Delayed precision
                const double delayed_precision_weight = 0.85;

                CatchDifficultyHitObject? prevAction = guaranteedActions.LastOrDefault() ?? ambiguousActions.LastOrDefault();

                if (prevAction?.MovementData != null && data.PrecisionStrain > 0)
                {
                    double prevPrecision = prevAction.MovementData.PrecisionStrain;

                    data.PrecisionStrain = delayed_precision_weight * data.PrecisionStrain + (1.0 - delayed_precision_weight) * prevPrecision;
                }

                // Speed calculation
                var recentGuaranteedDirectionized =
                    new[] { leftGuaranteedActions.LastOrDefault(), rightGuaranteedActions.LastOrDefault() }
                        .Where(n => n is not null)
                        .MaxBy(n => n!.MovementData.EffectiveTime);

                var recentAmbiguousDirectionized =
                    new[] { leftAmbiguousActions.LastOrDefault(), rightAmbiguousActions.LastOrDefault() }
                        .Where(n => n is not null)
                        .MaxBy(n => n!.MovementData.EffectiveTime);

                double burst = 0;
                double consistency = 0;
                double snap = 0;

                if (data.KeyPress == MovementKey.Left)
                {
                    burst = calculateSpeed(note, leftGuaranteedActions.LastOrDefault(), leftAmbiguousActions.LastOrDefault(), timeToSpeedBurst);
                    consistency = calculateSpeed(note, leftGuaranteedActions.AsEnumerable().Reverse().Skip(1).FirstOrDefault(), leftAmbiguousActions.AsEnumerable().Reverse().Skip(1).FirstOrDefault(), timeToSpeedConsistency);
                    snap = calculateSpeed(note, recentGuaranteedDirectionized, recentAmbiguousDirectionized, timeToSpeedSnap);
                }
                else if (data.KeyPress == MovementKey.Right)
                {
                    burst = calculateSpeed(note, rightGuaranteedActions.LastOrDefault(), rightAmbiguousActions.LastOrDefault(), timeToSpeedBurst);
                    consistency = calculateSpeed(note, rightGuaranteedActions.AsEnumerable().Reverse().Skip(1).FirstOrDefault(), leftAmbiguousActions.AsEnumerable().Reverse().Skip(1).FirstOrDefault(), timeToSpeedConsistency);
                    snap = calculateSpeed(note, recentGuaranteedDirectionized, recentAmbiguousDirectionized, timeToSpeedSnap);
                }

                data.BurstSpeed = burst * 2 * 12 * 120;
                data.ConsistencySpeed = consistency * 2 * 12 * 120;
                data.SnapSpeed = snap * 2 * 12 * 120;
            }
        }

        private static double calculatePrecisionStrain(CatchDifficultyHitObject note)
        {
            const double amplitude = 40.0; //governs how much very low precision values are worth
            const double shift = -15.0; //shifts the boundary between concave and convex part (shifts the curve)
            const double pace = 25.0; //measures how fast strain decreases between easy and hard jumps
            const double multiplier = 39.0;

            double precision = note.MovementData.NotePrecision is null
                ? 0
                : 1.0 + amplitude / (1 + Math.Exp(((double)note.MovementData.NotePrecision + shift) / pace));

            return precision / 18 * multiplier;
        }

        private const double max_precision_correction = 1.3;

        /// <summary>
        /// Calculates the precision value for a given note, and adjusts its effective time if needed.
        /// </summary>
        /// <param name="note"></param>
        /// <param name="prev"></param>
        /// <param name="next"></param>
        /// <param name="catcherWidth"></param>
        /// <param name="frameTime"></param>
        /// <returns></returns>
        private static (double?, double) calculatePrecision(CatchDifficultyHitObject note, CatchDifficultyHitObject prev, CatchDifficultyHitObject next, PatternType type, double catcherWidth, double frameTime)
        {
            CatchMovementData data = note.MovementData;

            switch (type)
            {
                case PatternType.HyperjumpAfterJump:
                {
                    double? rawPrecision = calculateRawPrecision(note, prev, next, PatternType.HyperjumpAfterJump, catcherWidth, frameTime);

                    double precisionCorrection = CatchPreprocessingUtils.CalculatePrecisionCorrection(note.DeltaPosition, note.DeltaTime, catcherWidth, max_precision_correction, false);
                    note.DisplayData.PrecisionCorrection = precisionCorrection;

                    double standstillTime = CatchPreprocessingUtils.CalculatePotentialStandstillEffectiveTime(note, next, catcherWidth, frameTime);
                    double scaledMaxCorrection = (precisionCorrection - 1.0) * 1.0 / (max_precision_correction - 1.0);
                    double effectiveTime = standstillTime * scaledMaxCorrection + data.EffectiveTime * (1.0 - scaledMaxCorrection);

                    return (precisionCorrection * rawPrecision, effectiveTime);
                }

                case PatternType.Jumps:
                {
                    double? rawPrecision = calculateRawPrecision(note, prev, next, PatternType.Jumps, catcherWidth, frameTime);

                    double precisionCorrection = CatchPreprocessingUtils.CalculatePrecisionCorrection(note.DeltaPosition, note.DeltaTime, catcherWidth, max_precision_correction, false);
                    note.DisplayData.PrecisionCorrection = precisionCorrection;

                    double standstillTime = (data.Directionize(prev.Position - next.Position) - catcherWidth / 2.0 + 2 * note.StartTime) / 2.0;
                    double scaledMaxCorrection = (precisionCorrection - 1.0) * 1.0 / (max_precision_correction - 1.0);
                    double effectiveTime = standstillTime * scaledMaxCorrection + data.EffectiveTime * (1.0 - scaledMaxCorrection);

                    return (precisionCorrection * rawPrecision, effectiveTime);
                }

                case PatternType.PotentialStandstill:
                {
                    double? rawPrecision = calculateRawPrecision(note, prev, next, PatternType.PotentialStandstill, catcherWidth, frameTime);

                    double precisionCorrection = CatchPreprocessingUtils.CalculatePrecisionCorrection(note.DeltaPosition, note.DeltaTime, catcherWidth, max_precision_correction, true);
                    note.DisplayData.PrecisionCorrection = precisionCorrection;

                    return (precisionCorrection * rawPrecision, data.EffectiveTime);
                }

                case PatternType.AcceleratingStream:
                {
                    double? rawPrecision = calculateRawPrecision(note, prev, next, PatternType.AcceleratingStream, catcherWidth, frameTime);

                    double precisionCorrection = CatchPreprocessingUtils.CalculatePrecisionCorrection(note.DeltaPosition, note.DeltaTime, catcherWidth, max_precision_correction, true);
                    note.DisplayData.PrecisionCorrection = precisionCorrection;

                    return (precisionCorrection * rawPrecision, data.EffectiveTime);
                }

                default:
                {
                    return (calculateRawPrecision(note, prev, next, type, catcherWidth, frameTime), data.EffectiveTime);
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
        /// <param name="catcherWidth"></param>
        /// <param name="frameTime"></param>
        /// <returns>The precision value in milliseconds, or null if it is infinite.</returns>
        private static double? calculateRawPrecision(CatchDifficultyHitObject note, CatchDifficultyHitObject prev, CatchDifficultyHitObject next, PatternType type, double catcherWidth, double frameTime)
        {
            CatchMovementData data = note.MovementData;
            CatchMovementData prevData = prev.MovementData;

            double prevForwardCatcherPosition = note.IsMovingRight ? prevData.RightCatcherPosition : prevData.LeftCatcherPosition;
            double minimalVelocity = CatchPreprocessingUtils.CalculateMinimalHyperdashSpeed(note, prev, catcherWidth, frameTime);
            double nextToPrevDeltaTime = next.StartTime - prev.StartTime;

            // To avoid the hard-coded assumption that these values are in relation to the previous note index-wise.
            double nextDeltaTime = next.StartTime - note.StartTime;
            double nextDeltaPosition = Math.Abs(next.Position - note.Position);

            switch (type)
            {
                case PatternType.StackContinuation:
                {
                    break;
                }

                case PatternType.JumpAfterHyperjump:
                {
                    if (nextDeltaPosition - nextDeltaTime >= catcherWidth / 2.0)
                    {
                        return (catcherWidth) / (2.0 * minimalVelocity);
                    }

                    double first = catcherWidth / 2.0 - nextDeltaPosition + nextToPrevDeltaTime;
                    double second = (data.Directionize(note.Position - prevForwardCatcherPosition) - catcherWidth / 2.0) / minimalVelocity;
                    double third = first - second;

                    return third / 2.0;
                }

                case PatternType.Hyperjumps:
                {
                    double first = (catcherWidth / 2.0 - nextDeltaPosition) / CatchPreprocessingUtils.CalculatePerfectHyperdashSpeed(next, note, frameTime);
                    double second = (data.Directionize(note.Position - prevForwardCatcherPosition) - catcherWidth / 2.0) / minimalVelocity;

                    return (first - second + nextToPrevDeltaTime) / 2.0;
                }

                case PatternType.HyperjumpAfterJump:
                {
                    double optimalVelocity = Math.Abs(next.Position - (prevForwardCatcherPosition + data.Directionize(note.DeltaTime))) / Math.Max(nextDeltaTime - frameTime, 1);

                    if (data.Directionize(note.Position - prevForwardCatcherPosition) <= note.DeltaTime - catcherWidth / 2.0)
                    {
                        return catcherWidth / 2.0;
                    }

                    double first = data.Directionize(next.Position - prevForwardCatcherPosition);
                    double second = (first + catcherWidth / 2.0 - note.DeltaTime) / optimalVelocity;
                    double third = nextToPrevDeltaTime + data.Directionize(prevForwardCatcherPosition - note.Position) + catcherWidth / 2.0;
                    double fourth = second + third;

                    return fourth / 2.0;
                }

                case PatternType.Jumps:
                {
                    return (nextDeltaTime + catcherWidth - nextDeltaPosition) / 2.0;
                }

                case PatternType.PotentialStandstill:
                {
                    if (note.DeltaPosition <= catcherWidth / 2.0)
                    {
                        double first = (catcherWidth - 2 * nextDeltaPosition) / (2 * CatchPreprocessingUtils.CalculatePerfectHyperdashSpeed(next, note, frameTime));
                        double second = nextDeltaTime + note.DeltaPosition + catcherWidth / 2.0;
                        return (first + second) / 2.0;
                    }

                    double third = (catcherWidth - 2 * nextDeltaPosition) / (2 * CatchPreprocessingUtils.CalculateSpeedFrom(next, note, note.BackwardNoteBorder, frameTime));
                    double fourth = nextDeltaTime + catcherWidth;

                    return (third + fourth) / 2.0;
                }

                case PatternType.AcceleratingStream:
                {
                    if (nextDeltaPosition > nextDeltaTime / 2.0 + catcherWidth / 2.0)
                    {
                        return (nextDeltaTime + catcherWidth - nextDeltaPosition) / 2.0;
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
            const double max_ratio = 0.2;
            const double effective_importance = 0.05;

            double effectiveRatio = Math.Min(max_ratio, (note.StartTime - data.EffectiveTime) / note.DeltaTime);
            double maxTime = data.EffectiveTime;
            if (data.EffectiveTime <= note.StartTime)
                maxTime = note.StartTime - note.DeltaTime * effectiveRatio * effective_importance / max_ratio;

            if (data.ActionProbability > 0)
            {
                if (data.ActionProbability < 1
                    && data.OriginalPattern != PatternType.StackEnd
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

        // Functions below are identical, but splitting them may be useful in the future.
        private static double timeToSpeedSnap(double time)
        {
            const double amplitude = 13.0; // governs how much very low speed values are worth
            const double shift = -50.0; // measures how fast strain decreases between slow and fast jumps (shifts the curve)
            const double pace = 35.0; // normalises shift
            const double multiplier = 0.88;

            double speed = 1.0 + amplitude / (1 + Math.Exp((time + shift) / pace));

            return multiplier * speed / 10000;
        }

        private static double timeToSpeedBurst(double time)
        {
            const double amplitude = 13.0; // governs how much very low speed values are worth
            const double shift = -50.0; // measures how fast strain decreases between slow and fast jumps (shifts the curve)
            const double pace = 35.0; // normalises shift
            const double multiplier = 0.99;

            double speed = 1.0 + amplitude / (1 + Math.Exp((time / 2 + shift) / pace));

            return multiplier * speed / 10000;
        }

        private static double timeToSpeedConsistency(double time)
        {
            const double amplitude = 13.0; // governs how much very low speed values are worth
            const double shift = -50.0; // measures how fast strain decreases between slow and fast jumps (shifts the curve)
            const double pace = 35.0; // normalises shift
            const double multiplier = 1.14;

            double speed = 1.0 + amplitude / (1 + Math.Exp((time / 4 + shift) / pace));

            return multiplier * speed / 10000;
        }
    }
}
