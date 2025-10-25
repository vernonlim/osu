// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using osu.Game.Rulesets.Catch.Difficulty.Data;
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

        /// <summary>
        /// Processes a list of <see cref="CatchDifficultyHitObject"/>s and populates their corresponding <see cref="CatchMovementData"/>s.
        /// </summary>
        /// <param name="hitObjects"></param>
        public static void Process(List<DifficultyHitObject> hitObjects)
        {
            // TODO: Special handling for the first and last objects of the map, as they lack a previous or future object
            CatchDifficultyHitObject first = (CatchDifficultyHitObject)hitObjects[0];
            first.MovementData.NotePattern = PatternType.FirstNote;
            first.MovementData.ActionProbability = 0;
            updateInitialData(first, (CatchDifficultyHitObject)hitObjects[0]);

            CatchDifficultyHitObject last = (CatchDifficultyHitObject)hitObjects[^1];
            last.MovementData.NotePattern = PatternType.LastNote;
            last.MovementData.ActionProbability = 0;
            last.MovementData.IsDirectionChange = false; // There is no next note to change direction to.

            for (int i = 1; i < hitObjects.Count - 1; i++)
            {
                CatchDifficultyHitObject note = (CatchDifficultyHitObject)hitObjects[i];
                CatchDifficultyHitObject prev = (CatchDifficultyHitObject)hitObjects[i - 1];
                CatchDifficultyHitObject next = (CatchDifficultyHitObject)hitObjects[i + 1];

                CatchMovementData data = note.MovementData;

                updateInitialData(note, next);

                data.NotePattern = classify(note, prev, next);

                updateData(note, prev, next);

                // Handling curved stack
                handleCurvedStack(note, prev, next);

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
                data.PrevToNextDistance = CatchPreprocessingUtils.CalculateHighestDistance(note, prev, next);
                data.MinimalHyperdashSpeed = CatchPreprocessingUtils.CalculateMinimalHyperdashSpeed(note, prev);
                data.PerfectHyperdashSpeed = CatchPreprocessingUtils.CalculatePerfectHyperdashSpeed(note);
                data.AverageHyperdashSpeed = CatchPreprocessingUtils.CalculateAverageHyperdashSpeed(note, prev);

                if (data.DisplayPattern == PatternType.None)
                {
                    data.DisplayPattern = data.NotePattern;
                }
            }
        }

        /// <summary>
        /// Updates data needed before classification or updates can take place.
        /// </summary>
        /// <param name="note">The current note.</param>
        /// <param name="next">The next note.</param>
        private static void updateInitialData(CatchDifficultyHitObject note, CatchDifficultyHitObject next)
        {
            CatchMovementData data = note.MovementData;
            data.IsDirectionChange = note.IsMovingRight ? next.Position < note.Position : next.Position > note.Position;
            double maximalPosition = next.Position - note.Position < 0 ? note.RightNoteBorder : note.LeftNoteBorder;
            double maximalDistance = Math.Abs(next.Position - maximalPosition);
            double maximalVelocity = maximalDistance / Math.Max(next.DeltaTime - note.FrameTime, 1);
            data.IsHyperWalk = maximalVelocity * next.DeltaTime / 2.0 >= maximalDistance - note.HalfCatcherWidth && note.IsHyper;
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
        /// <param name="skipToDirectionChange"></param>
        /// <returns>The <see cref="PatternType"/> corresponding to the note.</returns>
        private static PatternType classify(CatchDifficultyHitObject note, CatchDifficultyHitObject prev, CatchDifficultyHitObject next, bool skipToDirectionChange = false)
        {
            CatchMovementData data = note.MovementData;

            // Breaks
            PatternType breakType = classifyAsBreak(note, prev, next);

            if (breakType != PatternType.None && !skipToDirectionChange)
            {
                return breakType;
            }

            // Stacks
            PatternType stackType = classifyAsStack(note, prev, next);

            if (stackType != PatternType.None && !skipToDirectionChange)
            {
                return stackType;
            }

            // Direction changes
            PatternType directionChangeType = classifyAsDirectionChange(note, prev);

            if (directionChangeType != PatternType.None)
            {
                return directionChangeType;
            }

            // Streams
            PatternType streamType = classifyAsStream(note, prev, next);

            if (streamType != PatternType.None)
            {
                return streamType;
            }

            return PatternType.None;
        }

        /// <summary>
        /// Attempts to classify a note as a break.
        /// </summary>
        /// <param name="note">The current note.</param>
        /// <param name="prev">The previous note.</param>
        /// <param name="next">The next note.</param>
        /// <returns>The <see cref="PatternType"/> corresponding to the break-related pattern, or null if none match.</returns>
        private static PatternType classifyAsBreak(CatchDifficultyHitObject note, CatchDifficultyHitObject prev, CatchDifficultyHitObject next)
        {
            CatchMovementData data = note.MovementData;
            CatchMovementData prevData = prev.MovementData;

            if (!prevData.IsBreak
                && next.DeltaPosition < next.DeltaTime - note.CatcherWidth)
            {
                return next.DeltaPosition > note.CatcherWidth
                    ? PatternType.BreakBeginningRequiringMovement
                    : PatternType.BreakBeginningWithoutMovement;
            }

            // Breaks
            if (prevData.IsBreak
                && next.DeltaPosition < next.DeltaTime - note.CatcherWidth)
            {
                return PatternType.SingleNote;
            }

            if (prevData.IsBreak
                && note.IsHyper)
            {
                return PatternType.HyperdashAfterBreak;
            }

            if (prevData.IsBreak
                && !note.IsHyper)
            {
                return PatternType.EdgedashAfterBreak;
            }

            if (prevData.IsBreak
                && next.DeltaTime > 2 * next.DeltaPosition
                && next.DeltaPosition <= note.CatcherWidth)
            {
                note.MovementData.DisplayPattern = PatternType.StackAfterBreak;
                return PatternType.StackAfterBreak;
            }

            return PatternType.None;
        }

        /// <summary>
        /// Attempts to classify a note as a stack.
        /// </summary>
        /// <param name="note">The current note.</param>
        /// <param name="prev">The previous note.</param>
        /// <param name="next">The next note.</param>
        /// <returns>The <see cref="PatternType"/> corresponding to the stack-related pattern, or null if none match.</returns>
        private static PatternType classifyAsStack(CatchDifficultyHitObject note, CatchDifficultyHitObject prev, CatchDifficultyHitObject next)
        {
            CatchMovementData data = note.MovementData;
            CatchMovementData prevData = prev.MovementData;

            if (prevData.IsStack
                && (next.Position + note.HalfCatcherWidth < prevData.LeftStandingPosition || next.Position - note.HalfCatcherWidth > prevData.RightStandingPosition))
            {
                data.DisplayPattern = PatternType.StackEnd;
                return PatternType.StackEnd;
            }

            if (prevData.IsStack
                && (prevData.LeftStandingPosition <= note.Position + note.HalfCatcherWidth)
                && (prevData.RightStandingPosition >= note.Position - note.HalfCatcherWidth))
            {
                data.DisplayPattern = PatternType.StackContinuation;
                return PatternType.StackContinuation;
            }

            if (prevData.LeftStandingPosition is not null
                && next.DeltaPosition <= 3.0 * note.CatcherWidth / 5.0
                && Math.Abs(next.Position - prev.Position) <= note.CatcherWidth)
            {
                data.DisplayPattern = PatternType.NarrowStack;
                return PatternType.NarrowStack;
            }

            if (prevData.LeftStandingPosition is not null
                && next.DeltaPosition <= note.CatcherWidth
                && Math.Abs(next.Position - prev.Position) <= note.CatcherWidth)
            {
                data.DisplayPattern = PatternType.PotentialStack;
                return PatternType.PotentialStack;
            }

            // direction change check to exclude streams
            if ((data.IsDirectionChange)
                && next.DeltaPosition <= note.CatcherWidth)
            {
                // There should be other cases covering this
                Debug.Assert(prevData.IsStack != true);

                data.DisplayPattern = PatternType.PotentialStackBeginning;
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
        private static PatternType classifyAsDirectionChange(CatchDifficultyHitObject note, CatchDifficultyHitObject prev)
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
        /// <returns>The <see cref="PatternType"/> corresponding to the stream-related pattern, or null if none match.</returns>
        private static PatternType classifyAsStream(CatchDifficultyHitObject note, CatchDifficultyHitObject prev, CatchDifficultyHitObject next)
        {
            CatchMovementData data = note.MovementData;

            if (!data.IsDirectionChange)
            {
                MovementDirection currentDirection = note.IsMovingRight ? MovementDirection.Right : MovementDirection.Left;

                if (prev.IsHyper)
                {
                    return PatternType.HyperStream;
                }

                if (!prev.IsHyper
                    && note.IsHyper
                    && prev.SignificantMovementDirection == currentDirection)
                {
                    return PatternType.PotentialStandstill;
                }

                if (!prev.IsHyper
                    && note.IsHyper
                    && prev.SignificantMovementDirection != currentDirection)
                {
                    return PatternType.ExtendedDirectionChange;
                }

                if (!prev.IsHyper
                    && !note.IsHyper
                    && prev.SignificantMovementDirection == currentDirection
                    && CatchPreprocessingUtils.CalculateSpeed(note) <= CatchPreprocessingUtils.CalculateSpeed(next)
                    && next.DeltaPosition > note.HalfCatcherWidth)
                {
                    return PatternType.AcceleratingStream;
                }

                if (!prev.IsHyper
                    && !note.IsHyper
                    && ((prev.SignificantMovementDirection != currentDirection)
                        || (CatchPreprocessingUtils.CalculateSpeed(note) > CatchPreprocessingUtils.CalculateSpeed(next))
                        || next.DeltaPosition <= note.HalfCatcherWidth))
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
        /// and setting its <see cref="PatternType"/> to the result of <see cref="classify"/>
        /// </remarks>
        /// <param name="note">The current note.</param>
        /// <param name="prev">The previous note.</param>
        /// <param name="next">The next note.</param>
        private static void updateData(CatchDifficultyHitObject note, CatchDifficultyHitObject prev, CatchDifficultyHitObject next)
        {
            CatchMovementData data = note.MovementData;
            CatchMovementData prevData = prev.MovementData;

            double prevForwardCatcherPosition = note.IsMovingRight ? prevData.RightCatcherPosition : prevData.LeftCatcherPosition;
            double prevBackwardCatcherPosition = note.IsMovingRight ? prevData.LeftCatcherPosition : prevData.RightCatcherPosition;
            double perfectSpeed = CatchPreprocessingUtils.CalculatePerfectHyperdashSpeed(note);
            double minimalSpeed = CatchPreprocessingUtils.CalculateMinimalHyperdashSpeed(note, prev);

            switch (data.NotePattern)
            {
                case PatternType.BreakBeginningRequiringMovement:
                {
                    data.IsBreak = true;
                    data.ActionProbability = 1;
                    data.KeyPress = next.Position > note.Position ? MovementKey.Right : MovementKey.Left;

                    if (prevData.KeyPress == data.KeyPress)
                    {
                        data.KeyPress = MovementKey.Dash;
                    }

                    data.EffectiveTime = (note.StartTime + next.StartTime) / 2.0;
                    break;
                }

                case PatternType.BreakBeginningWithoutMovement:
                {
                    data.IsBreak = true;
                    data.ActionProbability = 0;
                    break;
                }

                case PatternType.SingleNote:
                {
                    data.IsBreak = true;
                    data.ActionProbability = 0;
                    break;
                }

                case PatternType.StackAfterBreak:
                {
                    data.LeftStandingPosition = Math.Max(note.Position - note.HalfCatcherWidth, next.Position - note.HalfCatcherWidth);
                    data.RightStandingPosition = Math.Min(note.Position + note.HalfCatcherWidth, next.Position + note.HalfCatcherWidth);
                    data.LeftCatcherPosition = (double)data.LeftStandingPosition;
                    data.RightCatcherPosition = (double)data.RightStandingPosition;

                    data.ActionProbability = 0;

                    break;
                }

                case PatternType.HyperdashAfterBreak:
                {
                    data.ActionProbability = 0;
                    // Reset
                    data.LeftCatcherPosition = note.LeftNoteBorder;
                    data.RightCatcherPosition = note.RightNoteBorder;

                    data.EffectiveTime = (prev.StartTime + next.StartTime) / 2.0;
                    break;
                }

                case PatternType.EdgedashAfterBreak:
                {
                    data.ActionProbability = 0;
                    bool isNextRight = next.Position > note.Position;

                    double backwardPosition =
                        isNextRight
                            ? Math.Max(note.Position - note.HalfCatcherWidth, next.Position - note.HalfCatcherWidth - next.DeltaTime)
                            : Math.Min(note.Position + note.HalfCatcherWidth, next.Position + note.HalfCatcherWidth + next.DeltaTime);
                    double forwardPosition =
                        isNextRight
                            ? note.Position + note.HalfCatcherWidth
                            : note.Position - note.HalfCatcherWidth;

                    data.LeftCatcherPosition = isNextRight ? backwardPosition : forwardPosition;
                    data.RightCatcherPosition = isNextRight ? forwardPosition : backwardPosition;

                    data.EffectiveTime = (prev.StartTime + next.StartTime) / 2.0;
                    break;
                }

                case PatternType.NarrowStack:
                {
                    data.LeftStandingPosition = Math.Max(note.Position - note.HalfCatcherWidth, next.Position - note.HalfCatcherWidth);
                    data.RightStandingPosition = Math.Min(note.Position + note.HalfCatcherWidth, next.Position + note.HalfCatcherWidth);
                    data.LeftCatcherPosition = (double)data.LeftStandingPosition;
                    data.RightCatcherPosition = (double)data.RightStandingPosition;

                    data.IsStack = true;
                    data.ActionProbability = 0;

                    break;
                }

                case PatternType.PotentialStackBeginning:
                {
                    data.LeftStandingPosition = Math.Max(note.Position - note.HalfCatcherWidth, next.Position - note.HalfCatcherWidth);
                    data.RightStandingPosition = Math.Min(note.Position + note.HalfCatcherWidth, next.Position + note.HalfCatcherWidth);

                    data.NotePattern = classify(note, prev, next, true);
                    updateData(note, prev, next);

                    break;
                }

                case PatternType.PotentialStack:
                {
                    if (next.DeltaPosition <= 3 * note.CatcherWidth / 5.0)
                    {
                        data.NotePattern = PatternType.NarrowStack;
                        updateData(note, prev, next);
                        break;
                    }

                    if (next.Position + note.HalfCatcherWidth < prevData.LeftStandingPosition || next.Position - note.HalfCatcherWidth > prevData.RightStandingPosition)
                    {
                        data.LeftStandingPosition = null;
                        data.RightStandingPosition = null;
                        data.IsStack = false;

                        data.NotePattern = classify(note, prev, next, true);
                        updateData(note, prev, next);
                        break;
                    }

                    data.LeftStandingPosition = Math.Max(note.Position - note.HalfCatcherWidth, next.Position - note.HalfCatcherWidth);
                    data.RightStandingPosition = Math.Min(note.Position + note.HalfCatcherWidth, next.Position + note.HalfCatcherWidth);

                    data.IsStack = true;

                    double scale = 1.0;

                    if (prevData.NotePattern == PatternType.JumpAfterHyperjump)
                    {
                        CatchDifficultyHitObject? prevPrev = prev.PreviousNote(0);

                        if (prevPrev is not null)
                        {
                            scale = Math.Sqrt(CatchPreprocessingUtils.CalculateMinimalHyperdashSpeed(prev, prevPrev));
                        }
                    }

                    if (next.DeltaPosition / note.CatcherWidth * scale >= CatchPreprocessingUtils.MillisecondsToCatcherStandingWidth(next.DeltaTime))
                    {
                        // wiggle
                        data.NotePattern = classify(note, prev, next, true);
                        updateData(note, prev, next);
                    }
                    else
                    {
                        // stand
                        data.ActionProbability = 0;

                        if (scale != 1.0)
                        {
                            data.NotePattern = PatternType.PotentialStackAfterJumpAfterHyperjump;
                            data.AimModifier = scale;
                        }
                    }

                    break;
                }

                case PatternType.StackContinuation:
                {
                    double catcherStandingWidthBoundary = CatchPreprocessingUtils.MillisecondsToCatcherStandingWidth(next.DeltaTime);
                    bool isWigglingBetter = next.DeltaPosition / note.CatcherWidth >= catcherStandingWidthBoundary;

                    if (isWigglingBetter)
                    {
                        data.KeyPress = next.Position > note.Position ? MovementKey.Right : MovementKey.Left;
                        data.NotePattern = classify(note, prev, next, true);
                        updateData(note, prev, next);
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

                    if (note.IsMovingRight)
                    {
                        prevData.LeftCatcherPosition = prev.Position;
                        prevData.RightCatcherPosition = prev.RightNoteBorder;
                    }
                    else
                    {
                        prevData.LeftCatcherPosition = prev.LeftNoteBorder;
                        prevData.RightCatcherPosition = prev.Position;
                    }

                    data.BackwardCatcherPosition = note.BackwardNoteBorder;
                    data.ForwardCatcherPosition = prev.Position + data.Directionize(note.HalfCatcherWidth + note.DeltaTime);

                    // We need to re-classify the note as not a stack, then run this method again
                    data.NotePattern = classify(note, prev, next, true);
                    updateData(note, prev, next);

                    break;
                }

                // Direction changes
                case PatternType.JumpAfterHyperjump:
                {
                    data.ActionProbability = 1 * CatchPreprocessingUtils.CalculateDirectionChangeWeight(next, minimalSpeed);
                    data.DirectionChangeWeight = CatchPreprocessingUtils.CalculateDirectionChangeWeight(next, minimalSpeed);
                    data.KeyPress = data.BackwardKeyPress;
                    data.ForwardCatcherPosition = next.Position + data.Directionize(note.HalfCatcherWidth + next.DeltaTime);

                    if (data.Directionize(next.Position - note.Position) <= -(note.HalfCatcherWidth + next.DeltaTime))
                    {
                        double first = data.Directionize(note.Position + next.Position - prevData.LeftCatcherPosition - prevData.RightCatcherPosition + next.DeltaTime) / minimalSpeed;
                        double second = 2 * prev.StartTime + 2 * note.StartTime;
                        data.EffectiveTime = (first + second) / 4.0;

                        break;
                    }

                    double third = data.Directionize(note.Position - data.Directionize(note.HalfCatcherWidth) - prevForwardCatcherPosition) / minimalSpeed;
                    double fourth = note.HalfCatcherWidth - next.DeltaPosition + prev.StartTime + 2 * note.StartTime + next.StartTime;
                    data.EffectiveTime = (third + fourth) / 4.0;

                    break;
                }

                case PatternType.Hyperjumps:
                {
                    data.ActionProbability = 1;
                    data.KeyPress = data.BackwardKeyPress;
                    data.ForwardCatcherPosition =
                        next.Position + data.Directionize(note.HalfCatcherWidth + next.DeltaTime * CatchPreprocessingUtils.CalculatePerfectHyperdashSpeed(next));

                    double first = data.Directionize(note.Position - data.Directionize(note.HalfCatcherWidth) - prevForwardCatcherPosition) / minimalSpeed;
                    double second = (note.HalfCatcherWidth - next.DeltaPosition) / CatchPreprocessingUtils.CalculatePerfectHyperdashSpeed(next);
                    double third = prev.StartTime + 2 * note.StartTime + next.StartTime;

                    data.EffectiveTime = (first + second + third) / 4.0;
                    break;
                }

                case PatternType.HyperjumpAfterJump:
                {
                    data.ActionProbability = 1;
                    data.KeyPress = data.BackwardKeyPress;
                    double velocity2 = CatchPreprocessingUtils.CalculateHighestDistance(note, prev, next) / Math.Max(1, next.DeltaTime - note.FrameTime);

                    // data.ForwardCatcherPosition = next.Position + data.Directionize(note.HalfCatcherWidth + calculatePrevToNextDistance(note, prev, next) / (next.DeltaTime - note.FrameTime) * next.DeltaTime);
                    data.ForwardCatcherPosition =
                        next.Position + data.Directionize(note.HalfCatcherWidth
                                                          + velocity2 * next.DeltaTime);

                    if (Math.Abs(note.Position - prevBackwardCatcherPosition) <= note.DeltaTime - note.HalfCatcherWidth)
                    {
                        data.EffectiveTime = (data.Directionize(2 * note.Position - prevData.LeftCatcherPosition - prevData.RightCatcherPosition) + 2 * prev.StartTime + 2 * note.StartTime) / 4.0;

                        break;
                    }

                    double first = data.Directionize(note.Position - prevForwardCatcherPosition) - note.HalfCatcherWidth;
                    double second = (data.Directionize(next.Position - prevBackwardCatcherPosition) + note.HalfCatcherWidth - note.DeltaTime) / CatchPreprocessingUtils.CalculatePerfectHyperdashSpeed(next);
                    double third = prev.StartTime + 2 * note.StartTime + next.StartTime;

                    data.EffectiveTime = (first + second + third) / 4.0;
                    break;
                }

                case PatternType.Jumps:
                {
                    data.ActionProbability = 1 * CatchPreprocessingUtils.CalculateDirectionChangeWeight(next, 1);
                    data.DirectionChangeWeight = CatchPreprocessingUtils.CalculateDirectionChangeWeight(next, 1);
                    data.KeyPress = data.BackwardKeyPress;
                    data.ForwardCatcherPosition = data.FurthestBackward(prevForwardCatcherPosition + data.Directionize(note.DeltaTime), next.Position + data.Directionize(note.HalfCatcherWidth + next.DeltaTime));

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
                    data.BackwardCatcherPosition = note.Position - data.Directionize(note.HalfCatcherWidth);
                    data.ForwardCatcherPosition = data.FurthestBackward(prevForwardCatcherPosition + data.Directionize(note.DeltaTime), note.Position + data.Directionize(note.HalfCatcherWidth));

                    data.EffectiveTime = CatchPreprocessingUtils.CalculatePotentialStandstillEffectiveTime(note, next);

                    if (data.IsHyperWalk)
                    {
                        if (note.IsMovingRight)
                        {
                            data.ActionProbability =
                                Math.Max(0, CatchPreprocessingUtils.NormalCdfForNote(note.Position - note.HalfCatcherWidth - note.DeltaTime / 2.0, prev)
                                            - CatchPreprocessingUtils.NormalCdfForNote(note.Position + note.HalfCatcherWidth - note.DeltaTime, prev));
                        }
                        else
                        {
                            data.ActionProbability =
                                Math.Max(0, CatchPreprocessingUtils.NormalCdfForNote(note.Position - note.HalfCatcherWidth + note.DeltaTime, prev)
                                            - CatchPreprocessingUtils.NormalCdfForNote(note.Position + note.HalfCatcherWidth + note.DeltaTime / 2.0, prev));
                        }

                        data.KeyPress = data.ForwardKeyPress;

                        break;
                    }

                    // Temporary fix, might not be logical actually
                    if ((prevData.LeftCatcherPosition + prevData.RightCatcherPosition) / 2.0 <= note.Position)
                    {
                        data.ActionProbability = 1 - CatchPreprocessingUtils.NormalCdfForNote(note.Position + note.HalfCatcherWidth - note.DeltaTime, prev);
                    }
                    else
                    {
                        data.ActionProbability = CatchPreprocessingUtils.NormalCdfForNote(note.Position - note.HalfCatcherWidth + note.DeltaTime, prev);
                    }

                    data.KeyPress = data.ForwardKeyPress;

                    break;
                }

                case PatternType.ExtendedDirectionChange:
                {
                    data.ActionProbability = 0;
                    data.LeftCatcherPosition = note.LeftNoteBorder;
                    data.RightCatcherPosition = note.RightNoteBorder;

                    MovementDirection nextDirection = next.SignificantMovementDirection;

                    if (note.DeltaPosition == 0
                        && nextDirection != MovementDirection.None)
                    {
                        if ((prev.SignificantMovementDirection == MovementDirection.Left && nextDirection == MovementDirection.Right)
                            || (prev.SignificantMovementDirection == MovementDirection.Right && nextDirection == MovementDirection.Left))
                        {
                            data.ActionProbability = 1;
                        }
                    }

                    break;
                }

                case PatternType.AcceleratingStream:
                {
                    if (note.IsMovingRight)
                    {
                        data.ActionProbability = Math.Max(0,
                            (CatchPreprocessingUtils.NormalCdfForNote(next.Position - note.HalfCatcherWidth - (note.DeltaTime + next.DeltaTime) / 2.0, prev)
                             - CatchPreprocessingUtils.NormalCdfForNote(note.Position + note.HalfCatcherWidth - note.DeltaTime, prev)));
                    }
                    else
                    {
                        data.ActionProbability = Math.Max(0,
                            (CatchPreprocessingUtils.NormalCdfForNote(note.Position - note.HalfCatcherWidth + note.DeltaTime, prev)
                             - CatchPreprocessingUtils.NormalCdfForNote(next.Position + note.HalfCatcherWidth + (note.DeltaTime + next.DeltaTime) / 2.0, prev)));
                    }

                    data.BackwardCatcherPosition = data.FurthestForward(next.Position - data.Directionize(note.HalfCatcherWidth + next.DeltaTime),
                        note.Position - data.Directionize(note.HalfCatcherWidth));
                    data.ForwardCatcherPosition = data.FurthestBackward(prevForwardCatcherPosition + data.Directionize(note.DeltaTime), next.Position + data.Directionize(note.HalfCatcherWidth));

                    data.EffectiveTime = (data.Directionize(prev.Position - next.Position) - note.HalfCatcherWidth + 2 * note.StartTime) / 2.0;
                    data.KeyPress = MovementKey.Dash;

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

        private static void handleCurvedStack(CatchDifficultyHitObject note, CatchDifficultyHitObject prev, CatchDifficultyHitObject next)
        {
            CatchMovementData data = note.MovementData;

            CatchDifficultyHitObject? belt = prev.MovementData.BeltBeginning;

            bool inExistingBelt = belt is not null && CatchPreprocessingUtils.NoteWithinBelt(note, belt, belt.MovementData.NotePattern);

            bool prevHasBelt = belt is not null;

            PatternType type = PatternType.None;

            if (data.IsDirectionChange)
            {
                if (prev.IsHyper && !note.IsHyper)
                    type = PatternType.JumpAfterHyperjump;
                else if (!prev.IsHyper && !note.IsHyper)
                    type = PatternType.Jumps;
            }

            double? curvedStackProbability = CatchPreprocessingUtils.CalculateCurvedStackProbability(note, prev, next, type);

            bool nextInBelt = CatchPreprocessingUtils.NoteWithinBelt(next, note, type);
            bool inBelt = CatchPreprocessingUtils.NoteWithinBelt(note, note, type);

            bool isPotentialBeltBeginning = curvedStackProbability is not null && nextInBelt;

            if (!inExistingBelt && isPotentialBeltBeginning && inBelt)
            {
                data.BeltBeginning = note;
                data.ActionProbability = curvedStackProbability!.Value;
                data.NotePattern = type;
            }
            else if (prevHasBelt && prev.IsHyper && isPotentialBeltBeginning)
            {
                data.ActionProbability *= belt!.MovementData.ActionProbability;
                data.BeltBeginning = note;
                data.NotePattern = type;
            }
            else if (inExistingBelt)
            {
                if ((note.IsHyper && (next.Position - note.Position >= 0 ? belt!.IsMovingRight : !belt!.IsMovingRight)) || CatchPreprocessingUtils.NoteWithinBelt(next, belt!, belt!.MovementData.NotePattern))
                {
                    data.ActionProbability *= belt!.MovementData.ActionProbability;
                    data.BeltBeginning = belt;
                }
            }
        }
    }
}
