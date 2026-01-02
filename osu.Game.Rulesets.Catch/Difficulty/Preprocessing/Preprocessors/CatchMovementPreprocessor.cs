// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using osu.Game.Rulesets.Catch.Difficulty.Preprocessing.Data;
using osu.Game.Rulesets.Catch.Difficulty.Preprocessing.Utils;
using osu.Game.Rulesets.Difficulty.Preprocessing;

namespace osu.Game.Rulesets.Catch.Difficulty.Preprocessing.Preprocessors
{
    /// <summary>
    /// Utility class that calculates Movement-related properties.
    /// </summary>
    public static class CatchMovementPreprocessor
    {
        private const double lower_q_bound = 0.03;
        private const double upper_q_bound = 0.85;
        private const double standing_bound = 0.6;

        /// <summary>
        /// Processes a list of <see cref="CatchDifficultyHitObject"/>s and populates their corresponding <see cref="CatchMovementData"/>s.
        /// </summary>
        /// <param name="hitObjects"></param>
        /// <param name="catcherWidth"></param>
        /// <param name="clockRate"></param>
        /// <param name="frameTime"></param>
        /// <param name="playfieldBorder"></param>
        public static void Process(List<DifficultyHitObject> hitObjects, double catcherWidth, double clockRate, double frameTime, double playfieldBorder)
        {
            // TODO: Special handling for the first and last objects of the map, as they lack a previous or future object
            CatchDifficultyHitObject first = (CatchDifficultyHitObject)hitObjects[0];
            first.MovementData.NotePattern = PatternType.FirstNote;
            first.MovementData.ActionProbability = 0;
            updateInitialData(first, (CatchDifficultyHitObject)hitObjects[0], catcherWidth, frameTime);

            CatchDifficultyHitObject last = (CatchDifficultyHitObject)hitObjects[^1];
            last.MovementData.NotePattern = PatternType.LastNote;
            last.MovementData.ActionProbability = 0;
            last.MovementData.IsDirectionChange = false; // There is no next note to change direction to.

            for (int i = 1; i < hitObjects.Count - 1; i++)
            {
                CatchDifficultyHitObject note = (CatchDifficultyHitObject)hitObjects[i];
                CatchDifficultyHitObject next = (CatchDifficultyHitObject)hitObjects[i + 1];

                updateInitialData(note, next, catcherWidth, frameTime);
            }

            for (int i = 1; i < hitObjects.Count - 1; i++)
            {
                CatchDifficultyHitObject note = (CatchDifficultyHitObject)hitObjects[i];
                CatchDifficultyHitObject prev = (CatchDifficultyHitObject)hitObjects[i - 1];
                CatchDifficultyHitObject next = (CatchDifficultyHitObject)hitObjects[i + 1];

                CatchMovementData data = note.MovementData;

                data.NotePattern = Classify(note, prev, next, catcherWidth, clockRate);

                UpdateData(note, prev, next, catcherWidth, clockRate, frameTime, playfieldBorder);

                // Handling curved stack
                handleCurvedStack(note, prev, next, catcherWidth, clockRate, frameTime, playfieldBorder);

                PatternType type = Classify(note, prev, next, catcherWidth, clockRate);

                // Hack for akarui taiyo
                if (type == PatternType.PotentialStackBeginning && note.DeltaPosition <= standing_bound * catcherWidth && !note.IsHyper)
                {
                    data.ActionProbability = 0;
                    data.NotePattern = PatternType.Ignored;
                }

                if (data.ActionProbability < lower_q_bound)
                {
                    data.ActionProbability = 0;
                }
                else if (data.ActionProbability > upper_q_bound)
                {
                    data.ActionProbability = 1;
                }
                else
                {
                    data.ActionProbability = (data.ActionProbability - lower_q_bound) / (upper_q_bound - lower_q_bound);
                }

                // Debug
                note.DisplayData.PrevToNextDistance = CatchPreprocessingUtils.CalculateHighestDistance(note, prev, next);
                note.DisplayData.MinimalHyperdashSpeed = CatchPreprocessingUtils.CalculateMinimalHyperdashSpeed(note, prev, catcherWidth, frameTime);
                note.DisplayData.PerfectHyperdashSpeed = CatchPreprocessingUtils.CalculatePerfectHyperdashSpeed(note, prev, frameTime);
                note.DisplayData.AverageHyperdashSpeed = CatchPreprocessingUtils.CalculateAverageHyperdashSpeed(note, prev, frameTime);

                if (data.OriginalPattern == PatternType.None)
                {
                    data.OriginalPattern = data.NotePattern;
                }
            }
        }

        /// <summary>
        /// Updates data needed before classification or updates can take place.
        /// </summary>
        /// <param name="note">The current note.</param>
        /// <param name="next">The next note.</param>
        /// <param name="catcherWidth"></param>
        /// <param name="frameTime"></param>
        private static void updateInitialData(CatchDifficultyHitObject note, CatchDifficultyHitObject next, double catcherWidth, double frameTime)
        {
            CatchMovementData data = note.MovementData;
            data.IsDirectionChange = note.IsMovingRight ? next.Position < note.Position : next.Position > note.Position;
            double maximalPosition = next.Position - note.Position < 0 ? note.RightNoteBorder : note.LeftNoteBorder;
            double maximalDistance = Math.Abs(next.Position - maximalPosition);
            double maximalVelocity = maximalDistance / Math.Max(next.DeltaTime - frameTime, 1);
            data.IsHyperWalk = maximalVelocity * next.DeltaTime / 2.0 >= maximalDistance - catcherWidth / 2.0 && note.IsHyper;
        }

        /// <summary>
        /// Classifies each note into a certain <see cref="PatternType"/>.
        /// </summary>
        /// <remarks>
        /// Assumes that all previous notes within the map have been run through the main preprocessing loop.
        /// </remarks>
        /// <param name="note">The current note.</param>
        /// <param name="prev">The previous note.</param>
        /// <param name="next">The next note.</param>
        /// <param name="catcherWidth"></param>
        /// <param name="clockRate"></param>
        /// <param name="skipToDirectionChange"></param>
        /// <returns>The <see cref="PatternType"/> corresponding to the note.</returns>
        public static PatternType Classify(CatchDifficultyHitObject note, CatchDifficultyHitObject prev, CatchDifficultyHitObject next, double catcherWidth, double clockRate, bool skipToDirectionChange = false)
        {
            // Stacks
            PatternType stackType = classifyAsStack(note, prev, next, catcherWidth);

            if (stackType != PatternType.None && !skipToDirectionChange)
            {
                return stackType;
            }

            // Direction changes
            PatternType directionChangeType = ClassifyAsDirectionChange(note, prev);

            if (directionChangeType != PatternType.None)
            {
                return directionChangeType;
            }

            // Streams
            PatternType streamType = classifyAsStream(note, prev, next, catcherWidth, clockRate);

            if (streamType != PatternType.None)
            {
                return streamType;
            }

            return PatternType.None;
        }

        /// <summary>
        /// Attempts to classify a note as a stack.
        /// </summary>
        /// <param name="note">The current note.</param>
        /// <param name="prev">The previous note.</param>
        /// <param name="next">The next note.</param>
        /// <param name="catcherWidth"></param>
        /// <returns>The <see cref="PatternType"/> corresponding to the stack-related pattern, or null if none match.</returns>
        private static PatternType classifyAsStack(CatchDifficultyHitObject note, CatchDifficultyHitObject prev, CatchDifficultyHitObject next, double catcherWidth)
        {
            CatchMovementData data = note.MovementData;
            CatchMovementData prevData = prev.MovementData;

            // Future Precision
            double nextDeltaPosition = Math.Abs(next.Position - note.Position);

            if (prevData.IsStack
                && ((next.Position + catcherWidth / 2.0 < prevData.LeftStandingPosition || next.Position - catcherWidth / 2.0 > prevData.RightStandingPosition)
                    || (Math.Max(note.Position - catcherWidth / 2.0, next.Position - catcherWidth / 2.0) > Math.Min(note.Position + catcherWidth / 2.0, next.Position + catcherWidth / 2.0))))
            {
                data.OriginalPattern = PatternType.StackEnd;
                return PatternType.StackEnd;
            }

            if (prevData.IsStack
                && (prevData.LeftStandingPosition <= note.Position + catcherWidth / 2.0)
                && (prevData.RightStandingPosition >= note.Position - catcherWidth / 2.0))
            {
                data.OriginalPattern = PatternType.StackContinuation;
                return PatternType.StackContinuation;
            }

            if (prevData.LeftStandingPosition is not null
                && nextDeltaPosition <= standing_bound * catcherWidth
                && Math.Abs(next.Position - prev.Position) <= catcherWidth)
            {
                data.OriginalPattern = PatternType.NarrowStack;
                return PatternType.NarrowStack;
            }

            if (prevData.LeftStandingPosition is not null
                && nextDeltaPosition <= catcherWidth
                && Math.Abs(next.Position - prev.Position) <= catcherWidth)
            {
                data.OriginalPattern = PatternType.PotentialStack;
                return PatternType.PotentialStack;
            }

            // direction change check to exclude streams
            if ((data.IsDirectionChange)
                && nextDeltaPosition <= catcherWidth)
            {
                // There should be other cases covering this
                Debug.Assert(prevData.IsStack != true);

                data.OriginalPattern = PatternType.PotentialStackBeginning;
                return PatternType.PotentialStackBeginning;
            }

            return PatternType.None;
        }

        /// <summary>
        /// Attempts to classify a note as a direction change.
        /// </summary>
        /// <param name="note">The current note.</param>
        /// <param name="prev">The previous note.</param>
        /// <returns>The <see cref="PatternType"/> corresponding to the direction change-related pattern, or null if none match.</returns>
        public static PatternType ClassifyAsDirectionChange(CatchDifficultyHitObject note, CatchDifficultyHitObject prev)
        {
            CatchMovementData data = note.MovementData;

            if (data.IsDirectionChange)
            {
                if (prev.IsHyper && !note.IsHyper)
                    return PatternType.JumpAfterHyperjump;

                if (prev.IsHyper && note.IsHyper)
                    return PatternType.Hyperjumps;

                if (!prev.IsHyper && note.IsHyper)
                    return PatternType.HyperjumpAfterJump;

                if (!prev.IsHyper && !note.IsHyper)
                    return PatternType.Jumps;
            }

            return PatternType.None;
        }

        /// <summary>
        /// Attempts to classify a note as a stream.
        /// </summary>
        /// <param name="note">The current note.</param>
        /// <param name="prev">The previous note.</param>
        /// <param name="next">The next note.</param>
        /// <param name="catcherWidth"></param>
        /// <param name="clockRate"></param>
        /// <returns>The <see cref="PatternType"/> corresponding to the stream-related pattern, or null if none match.</returns>
        private static PatternType classifyAsStream(CatchDifficultyHitObject note, CatchDifficultyHitObject prev, CatchDifficultyHitObject next, double catcherWidth, double clockRate)
        {
            CatchMovementData data = note.MovementData;

            // Future Precision
            double nextDeltaPosition = Math.Abs(next.Position - note.Position);

            if (!data.IsDirectionChange)
            {
                MovementDirection currentDirection = note.IsMovingRight ? MovementDirection.Right : MovementDirection.Left;
                MovementDirection previousDirection = prev.IsMovingRight ? MovementDirection.Right : MovementDirection.Left;

                if (prev.IsHyper)
                {
                    return PatternType.HyperStream;
                }

                if (!prev.IsHyper
                    && note.IsHyper
                    && prev.SignificantMovementDirection(catcherWidth, clockRate) == currentDirection)
                {
                    return PatternType.PotentialStandstill;
                }

                if (!prev.IsHyper
                    && previousDirection != currentDirection)
                {
                    return PatternType.ExtendedDirectionChange;
                }

                if (!prev.IsHyper
                    && !note.IsHyper
                    //&& prev.SignificantMovementDirection == currentDirection
                    && CatchPreprocessingUtils.CalculateSpeed(note) <= CatchPreprocessingUtils.CalculateSpeed(next)
                    && nextDeltaPosition > catcherWidth / 2.0)
                {
                    return PatternType.AcceleratingStream;
                }

                if (!prev.IsHyper
                    && !note.IsHyper
                    && ( // (prev.SignificantMovementDirection != currentDirection)
                        (CatchPreprocessingUtils.CalculateSpeed(note) > CatchPreprocessingUtils.CalculateSpeed(next))
                        || nextDeltaPosition <= catcherWidth / 2.0))
                {
                    return PatternType.FreeStream;
                }
            }

            return PatternType.None;
        }

        /// <summary>
        /// Updates the Movement data of a note according to its <see cref="PatternType"/>
        /// </summary>
        /// <remarks>
        /// For each note, should be run after <see cref="updateInitialData"/>
        /// and setting its <see cref="PatternType"/> to the result of <see cref="Classify"/>
        /// </remarks>
        /// <param name="note">The current note.</param>
        /// <param name="prev">The previous note.</param>
        /// <param name="next">The next note.</param>
        /// <param name="catcherWidth"></param>
        /// <param name="clockRate"></param>
        /// <param name="frameTime"></param>
        /// <param name="playfieldBorder"></param>
        public static void UpdateData(CatchDifficultyHitObject note, CatchDifficultyHitObject prev, CatchDifficultyHitObject next, double catcherWidth, double clockRate, double frameTime, double playfieldBorder)
        {
            CatchMovementData data = note.MovementData;
            CatchMovementData prevData = prev.MovementData;

            double prevForwardCatcherPosition = note.IsMovingRight ? prevData.RightCatcherPosition : prevData.LeftCatcherPosition;
            double prevBackwardCatcherPosition = note.IsMovingRight ? prevData.LeftCatcherPosition : prevData.RightCatcherPosition;
            double minimalSpeed = CatchPreprocessingUtils.CalculateMinimalHyperdashSpeed(note, prev, catcherWidth, frameTime);

            switch (data.NotePattern)
            {
                case PatternType.NarrowStack:
                {
                    data.LeftStandingPosition = Math.Max(note.Position - catcherWidth / 2.0, next.Position - catcherWidth / 2.0);
                    data.RightStandingPosition = Math.Min(note.Position + catcherWidth / 2.0, next.Position + catcherWidth / 2.0);
                    data.LeftCatcherPosition = (double)data.LeftStandingPosition;
                    data.RightCatcherPosition = (double)data.RightStandingPosition;

                    data.IsStack = true;
                    data.ActionProbability = 0;

                    break;
                }

                case PatternType.PotentialStackBeginning:
                {
                    data.LeftStandingPosition = Math.Max(note.Position - catcherWidth / 2.0, next.Position - catcherWidth / 2.0);
                    data.RightStandingPosition = Math.Min(note.Position + catcherWidth / 2.0, next.Position + catcherWidth / 2.0);

                    if ((note.Position < catcherWidth / 2.0 && next.Position < catcherWidth / 2.0)
                        || (playfieldBorder - note.Position < catcherWidth / 2.0 && playfieldBorder - next.Position < catcherWidth / 2.0))
                    {
                        // wallhugger
                        data.NotePattern = PatternType.NarrowStack;
                    }
                    else
                    {
                        data.NotePattern = Classify(note, prev, next, catcherWidth, clockRate, true);
                    }

                    UpdateData(note, prev, next, catcherWidth, clockRate, frameTime, playfieldBorder);

                    break;
                }

                case PatternType.PotentialStack:
                {
                    if (next.DeltaPosition <= standing_bound * catcherWidth)
                    {
                        data.NotePattern = PatternType.NarrowStack;
                        UpdateData(note, prev, next, catcherWidth, clockRate, frameTime, playfieldBorder);
                        break;
                    }

                    if (next.Position + catcherWidth / 2.0 < prevData.LeftStandingPosition || next.Position - catcherWidth / 2.0 > prevData.RightStandingPosition)
                    {
                        data.LeftStandingPosition = null;
                        data.RightStandingPosition = null;
                        data.IsStack = false;

                        data.NotePattern = Classify(note, prev, next, catcherWidth, clockRate, true);
                        UpdateData(note, prev, next, catcherWidth, clockRate, frameTime, playfieldBorder);
                        break;
                    }

                    data.LeftStandingPosition = Math.Max(note.Position - catcherWidth / 2.0, next.Position - catcherWidth / 2.0);
                    data.RightStandingPosition = Math.Min(note.Position + catcherWidth / 2.0, next.Position + catcherWidth / 2.0);

                    data.IsStack = true;

                    double scale = 1.0;

                    if (prevData.NotePattern == PatternType.JumpAfterHyperjump)
                    {
                        CatchDifficultyHitObject? prevPrev = prev.PreviousNote(0);

                        if (prevPrev is not null)
                        {
                            scale = Math.Sqrt(CatchPreprocessingUtils.CalculateMinimalHyperdashSpeed(prev, prevPrev, catcherWidth, frameTime));
                        }
                    }

                    if (next.DeltaPosition / catcherWidth * Math.Pow(scale, 2.0) >= CatchPreprocessingUtils.MillisecondsToCatcherStandingWidth(next.DeltaTime, 0, clockRate) && !note.IsHyper)
                    {
                        // wiggle
                        data.StackWiggleCount += 1;
                        data.NotePattern = Classify(note, prev, next, catcherWidth, clockRate, true);
                        UpdateData(note, prev, next, catcherWidth, clockRate, frameTime, playfieldBorder);
                    }
                    else
                    {
                        // stand
                        data.ActionProbability = 0;

                        if (scale != 1.0)
                        {
                            data.NotePattern = PatternType.PotentialStackAfterJumpAfterHyperjump;
                        }
                    }

                    break;
                }

                case PatternType.StackContinuation:
                {
                    double rawCatcherStandingWidthBoundary = CatchPreprocessingUtils.MillisecondsToCatcherStandingWidth(next.DeltaTime, 0, clockRate);
                    bool isWigglingRawBetter = next.DeltaPosition / catcherWidth >= rawCatcherStandingWidthBoundary;

                    if (isWigglingRawBetter)
                    {
                        data.StackWiggleCount = prevData.StackWiggleCount + 1;
                    }

                    double catcherStandingWidthBoundary = CatchPreprocessingUtils.MillisecondsToCatcherStandingWidth(next.DeltaTime, prevData.StackWiggleCount, clockRate);
                    bool isWigglingBetter = next.DeltaPosition / catcherWidth >= catcherStandingWidthBoundary;

                    if (isWigglingBetter && !note.IsHyper)
                    {
                        data.KeyPress = next.Position > note.Position ? MovementKey.Right : MovementKey.Left;
                        data.NotePattern = Classify(note, prev, next, catcherWidth, clockRate, true);
                        UpdateData(note, prev, next, catcherWidth, clockRate, frameTime, playfieldBorder);
                    }
                    else
                    {
                        data.ActionProbability = 0;
                    }

                    data.IsStack = true;

                    Debug.Assert(prevData.LeftStandingPosition != null, "prevData.LeftStandingPosition != null");
                    Debug.Assert(prevData.RightStandingPosition != null, "prevData.RightStandingPosition != null");

                    // Unchanged
                    data.LeftCatcherPosition = (double)prevData.LeftStandingPosition;
                    data.RightCatcherPosition = (double)prevData.RightStandingPosition;

                    data.LeftStandingPosition = prevData.LeftStandingPosition;
                    data.RightStandingPosition = prevData.RightStandingPosition;

                    break;
                }

                case PatternType.StackEnd:
                {
                    data.IsStack = false;
                    data.LeftStandingPosition = null;
                    data.RightStandingPosition = null;

                    prevData.LeftCatcherPosition = prev.LeftNoteBorder;
                    prevData.RightCatcherPosition = prev.RightNoteBorder;

                    data.BackwardCatcherPosition = note.BackwardNoteBorder;
                    data.ForwardCatcherPosition = prev.Position + data.Directionize(catcherWidth / 2.0 + note.DeltaTime);

                    // We need to re-classify the note as not a stack, then run this method again
                    data.NotePattern = Classify(note, prev, next, catcherWidth, clockRate, true);
                    UpdateData(note, prev, next, catcherWidth, clockRate, frameTime, playfieldBorder);

                    break;
                }

                // Direction changes
                case PatternType.JumpAfterHyperjump:
                {
                    data.ActionProbability = 1 * CatchPreprocessingUtils.CalculateDirectionChangeWeight(next, note, minimalSpeed, catcherWidth);
                    note.DisplayData.DirectionChangeWeight = CatchPreprocessingUtils.CalculateDirectionChangeWeight(next, note, minimalSpeed, catcherWidth);
                    data.KeyPress = data.BackwardKeyPress;
                    data.ForwardCatcherPosition = next.Position + data.Directionize(catcherWidth / 2.0 + next.DeltaTime);

                    if (data.Directionize(next.Position - note.Position) <= -(catcherWidth / 2.0 + next.DeltaTime))
                    {
                        double first = data.Directionize(note.Position + next.Position - prevData.LeftCatcherPosition - prevData.RightCatcherPosition + next.DeltaTime) / minimalSpeed;
                        double second = 2 * prev.StartTime + 2 * note.StartTime;
                        data.EffectiveTime = (first + second) / 4.0;

                        break;
                    }

                    double third = data.Directionize(note.Position - data.Directionize(catcherWidth / 2.0) - prevForwardCatcherPosition) / minimalSpeed;
                    double fourth = catcherWidth / 2.0 - next.DeltaPosition + prev.StartTime + 2 * note.StartTime + next.StartTime;
                    data.EffectiveTime = (third + fourth) / 4.0;

                    break;
                }

                case PatternType.Hyperjumps:
                {
                    data.ActionProbability = 1;
                    data.KeyPress = data.BackwardKeyPress;
                    data.ForwardCatcherPosition =
                        next.Position + data.Directionize(catcherWidth / 2.0 + next.DeltaTime * CatchPreprocessingUtils.CalculatePerfectHyperdashSpeed(next, note, frameTime));

                    double first = data.Directionize(note.Position - data.Directionize(catcherWidth / 2.0) - prevForwardCatcherPosition) / minimalSpeed;
                    double second = (catcherWidth / 2.0 - next.DeltaPosition) / CatchPreprocessingUtils.CalculatePerfectHyperdashSpeed(next, note, frameTime);
                    double third = prev.StartTime + 2 * note.StartTime + next.StartTime;

                    data.EffectiveTime = (first + second + third) / 4.0;
                    break;
                }

                case PatternType.HyperjumpAfterJump:
                {
                    data.ActionProbability = 1;
                    data.KeyPress = data.BackwardKeyPress;
                    double velocity2 = CatchPreprocessingUtils.CalculateHighestDistance(note, prev, next) / Math.Max(1, next.DeltaTime - frameTime);

                    // data.ForwardCatcherPosition = next.Position + data.Directionize(catcherWidth / 2.0 + calculatePrevToNextDistance(note, prev, next) / (next.DeltaTime - note.FrameTime) * next.DeltaTime);
                    data.ForwardCatcherPosition =
                        next.Position + data.Directionize(catcherWidth / 2.0
                                                          + velocity2 * next.DeltaTime);

                    if (Math.Abs(note.Position - prevBackwardCatcherPosition) <= note.DeltaTime - catcherWidth / 2.0)
                    {
                        data.EffectiveTime = (data.Directionize(2 * note.Position - prevData.LeftCatcherPosition - prevData.RightCatcherPosition) + 2 * prev.StartTime + 2 * note.StartTime) / 4.0;

                        break;
                    }

                    double first = data.Directionize(note.Position - prevForwardCatcherPosition) - catcherWidth / 2.0;
                    double second = (data.Directionize(next.Position - prevBackwardCatcherPosition) + catcherWidth / 2.0 - note.DeltaTime) / CatchPreprocessingUtils.CalculatePerfectHyperdashSpeed(next, note, frameTime);
                    double third = prev.StartTime + 2 * note.StartTime + next.StartTime;

                    data.EffectiveTime = (first + second + third) / 4.0;
                    break;
                }

                case PatternType.Jumps:
                {
                    data.ActionProbability = 1 * CatchPreprocessingUtils.CalculateDirectionChangeWeight(next, note, 1, catcherWidth);
                    note.DisplayData.DirectionChangeWeight = CatchPreprocessingUtils.CalculateDirectionChangeWeight(next, note, 1, catcherWidth);
                    data.KeyPress = data.BackwardKeyPress;
                    data.ForwardCatcherPosition = data.FurthestBackward(prevForwardCatcherPosition + data.Directionize(note.DeltaTime), next.Position + data.Directionize(catcherWidth / 2.0 + next.DeltaTime));

                    double first = data.Directionize(note.Position + next.Position - prevData.LeftCatcherPosition - prevData.RightCatcherPosition);
                    double second = 2 * prev.StartTime + note.StartTime + next.StartTime;

                    data.EffectiveTime = (first + second) / 4.0;

                    break;
                }

                // Streams
                case PatternType.HyperStream:
                {
                    data.ActionProbability = 0;
                    data.BackwardCatcherPosition = note.Position;
                    data.ForwardCatcherPosition = note.Position;
                    break;
                }

                case PatternType.PotentialStandstill:
                {
                    data.BackwardCatcherPosition = note.Position - data.Directionize(catcherWidth / 2.0);
                    data.ForwardCatcherPosition = data.FurthestBackward(prevForwardCatcherPosition + data.Directionize(note.DeltaTime), note.Position + data.Directionize(catcherWidth / 2.0));

                    data.EffectiveTime = CatchPreprocessingUtils.CalculatePotentialStandstillEffectiveTime(note, next, catcherWidth, frameTime);

                    // if (data.IsHyperWalk)
                    // {
                    //     if (note.IsMovingRight)
                    //     {
                    //         data.ActionProbability =
                    //             Math.Max(0, CatchPreprocessingUtils.NormalCdfForNote(note.Position - catcherWidth / 2.0 - note.DeltaTime / 2.0, prev)
                    //                         - CatchPreprocessingUtils.NormalCdfForNote(note.Position + catcherWidth / 2.0 - note.DeltaTime, prev));
                    //     }
                    //     else
                    //     {
                    //         data.ActionProbability =
                    //             Math.Max(0, CatchPreprocessingUtils.NormalCdfForNote(note.Position - catcherWidth / 2.0 + note.DeltaTime, prev)
                    //                         - CatchPreprocessingUtils.NormalCdfForNote(note.Position + catcherWidth / 2.0 + note.DeltaTime / 2.0, prev));
                    //     }

                    //     data.KeyPress = data.ForwardKeyPress;

                    //     break;
                    // }

                    // Temporary fix, might not be logical actually
                    if ((prevData.LeftCatcherPosition + prevData.RightCatcherPosition) / 2.0 <= note.Position)
                    {
                        data.ActionProbability = 1 - CatchPreprocessingUtils.NormalCdfForNote(note.Position + catcherWidth / 2.0 - note.DeltaTime, prev);
                    }
                    else
                    {
                        data.ActionProbability = CatchPreprocessingUtils.NormalCdfForNote(note.Position - catcherWidth / 2.0 + note.DeltaTime, prev);
                    }

                    data.KeyPress = data.ForwardKeyPress;

                    break;
                }

                case PatternType.ExtendedDirectionChange:
                {
                    data.ActionProbability = 0;
                    data.LeftCatcherPosition = note.LeftNoteBorder;
                    data.RightCatcherPosition = note.RightNoteBorder;

                    break;
                }

                case PatternType.AcceleratingStream:
                {
                    if (note.IsMovingRight)
                    {
                        data.ActionProbability = Math.Max(0,
                            (CatchPreprocessingUtils.NormalCdfForNote(next.Position - catcherWidth / 2.0 - (note.DeltaTime + next.DeltaTime) / 2.0, prev)
                             - CatchPreprocessingUtils.NormalCdfForNote(note.Position + catcherWidth / 2.0 - note.DeltaTime, prev)));
                    }
                    else
                    {
                        data.ActionProbability = Math.Max(0,
                            (CatchPreprocessingUtils.NormalCdfForNote(note.Position - catcherWidth / 2.0 + note.DeltaTime, prev)
                             - CatchPreprocessingUtils.NormalCdfForNote(next.Position + catcherWidth / 2.0 + (note.DeltaTime + next.DeltaTime) / 2.0, prev)));
                    }

                    data.BackwardCatcherPosition = data.FurthestForward(next.Position - data.Directionize(catcherWidth / 2.0 + next.DeltaTime),
                        note.Position - data.Directionize(catcherWidth / 2.0));
                    data.ForwardCatcherPosition = data.FurthestBackward(prevForwardCatcherPosition + data.Directionize(note.DeltaTime), next.Position + data.Directionize(catcherWidth / 2.0));

                    data.EffectiveTime = (data.Directionize(prev.Position - next.Position) - catcherWidth / 2.0 + 2 * note.StartTime) / 2.0;
                    data.KeyPress = data.ForwardKeyPress;

                    break;
                }

                case PatternType.FreeStream:
                {
                    data.ActionProbability = 0;

                    data.BackwardCatcherPosition = note.BackwardNoteBorder;
                    data.ForwardCatcherPosition = data.FurthestBackward(prevForwardCatcherPosition + data.Directionize(note.DeltaTime), note.ForwardNoteBorder);

                    break;
                }

                default:
                {
                    data.ActionProbability = 0;
                    break;
                }
            }
        }

        private static void handleCurvedStack(CatchDifficultyHitObject note, CatchDifficultyHitObject prev, CatchDifficultyHitObject next, double catcherWidth, double clockRate, double frameTime, double playfieldBorder)
        {
            CatchMovementData data = note.MovementData;

            CatchDifficultyHitObject? belt = prev.MovementData.BeltBeginning;

            bool inExistingBelt = belt is not null && CatchPreprocessingUtils.NoteWithinBelt(note, belt, belt.MovementData.NotePattern, catcherWidth);

            bool prevHasBelt = belt is not null;

            PatternType type = PatternType.None;

            if (data.IsDirectionChange)
            {
                if (prev.IsHyper && !note.IsHyper)
                    type = PatternType.JumpAfterHyperjump;
                else if (!prev.IsHyper && !note.IsHyper)
                    type = PatternType.Jumps;
            }

            double? curvedStackProbability = CatchPreprocessingUtils.CalculateCurvedStackProbability(note, prev, next, type, catcherWidth);

            bool nextInBelt = CatchPreprocessingUtils.NoteWithinBelt(next, note, type, catcherWidth);
            bool inBelt = CatchPreprocessingUtils.NoteWithinBelt(note, note, type, catcherWidth);

            bool isPotentialBeltBeginning = curvedStackProbability is not null && nextInBelt;

            bool beltHasAction = prev.MovementData.BeltHasAction;

            if (belt != null && ((belt.IsMovingRight && note.IsHyper && !next.IsMovingRight) || (!belt.IsMovingRight && note.IsHyper && next.IsMovingRight)))
                return;

            if (belt != null && (inExistingBelt && note.IsHyper && (next.Position - note.Position >= 0 ? !belt.IsMovingRight : belt.IsMovingRight)) && !beltHasAction && (note.IsMovingRight ? prev.Position > note.Position : prev.Position < note.Position))
            {
                prev.Position = note.Position - (next.Position - note.Position >= 0 ? -0.01 : 0.01);
                note.IsMovingRight = note.Position >= prev.Position;
                data.IsDirectionChange = note.IsMovingRight ? next.Position < note.Position : next.Position > note.Position;
                note.MovementData.NotePattern = Classify(note, prev, next, catcherWidth, clockRate);
                UpdateData(note, prev, next, catcherWidth, clockRate, frameTime, playfieldBorder);
            }
            else if (!inExistingBelt && isPotentialBeltBeginning && inBelt)
            {
                // starting a belt
                data.BeltBeginning = note;
                data.ActionProbability = Math.Min(curvedStackProbability!.Value, data.ActionProbability);
                data.BeltHasAction = beltHasAction || data.ActionProbability > 0;
                data.NotePattern = type;
            }
            else if (prevHasBelt && prev.IsHyper && isPotentialBeltBeginning)
            {
                // adjusting a belt
                data.ActionProbability *= belt!.MovementData.ActionProbability;
                data.BeltBeginning = note;
                data.BeltHasAction = beltHasAction || data.ActionProbability > 0;
                data.NotePattern = type;
            }
            else if (inExistingBelt)
            {
                // continuing a belt
                if ((note.IsHyper && (next.Position - note.Position >= 0 ? belt!.IsMovingRight : !belt!.IsMovingRight)) || CatchPreprocessingUtils.NoteWithinBelt(next, belt!, belt!.MovementData.NotePattern, catcherWidth))
                {
                    data.ActionProbability *= belt.MovementData.ActionProbability;
                    data.BeltBeginning = belt;
                    data.BeltHasAction = beltHasAction || data.ActionProbability > 0;
                }
            }
        }
    }
}
