// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using osu.Game.Rulesets.Catch.Difficulty.Evaluators;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Utils;

namespace osu.Game.Rulesets.Catch.Difficulty.Preprocessing.Movement
{
    /// <summary>
    /// Utility class that calculates Movement-related properties.
    /// </summary>
    public class CatchMovementDifficultyPreprocessor
    {
        /// <summary>
        /// The custom SpeedWeight given to difficult actions.
        /// </summary>
        private const double speed_bonus = 1.5;

        /// <summary>
        /// Processes a list of <see cref="CatchDifficultyHitObject"/>s and populates their corresponding <see cref="CatchMovementData"/>s.
        /// </summary>
        /// <param name="hitObjects"></param>
        public static void ProcessAndAssign(List<DifficultyHitObject> hitObjects)
        {
            // TODO: Special handling for the first and last objects of the map, as they lack a previous or future object
            CatchDifficultyHitObject first = (CatchDifficultyHitObject)hitObjects[0];
            first.MovementData.NotePattern = PatternType.FirstNote;
            updateInitialData(first, (CatchDifficultyHitObject)hitObjects[0]);

            CatchDifficultyHitObject last = (CatchDifficultyHitObject)hitObjects[^1];
            last.MovementData.NotePattern = PatternType.LastNote;
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

                data.NotePrecision = calculatePrecision(note, prev, next);
                data.NoteAim = calculateAim(note, prev, next);

                if (data.ActionProbability > 0 && data.ActionProbability < 1)
                {
                    data.AmbiguousActionIndex = data.AmbiguousActionDifficultyHitObjects.Count;
                    data.AmbiguousActionDifficultyHitObjects.Add(note);
                }

                if (data.ActionProbability == 1)
                {
                    data.GuaranteedActionIndex = data.GuaranteedActionDifficultyHitObjects.Count;
                    data.GuaranteedActionDifficultyHitObjects.Add(note);
                }

                data.NoteSpeed = calculateSpeed(note, prev) * 10;

                // Debug
                data.PrevToNextDistance = calculatePrevToNextDistance(note, prev, next);
                data.MinimalHyperdashSpeed = calculateMinimalHyperdashSpeed(note, prev, next);
                data.PerfectHyperdashSpeed = calculatePerfectHyperdashSpeed(note);
                data.AverageHyperdashSpeed = calculateAverageHyperdashSpeed(note, prev);

                if (data.DisplayPattern == PatternType.None)
                {
                    data.DisplayPattern = data.NotePattern;
                }

                data.LocalStarRating = MovementEvaluator.EvaluateDifficultyOf(note);
                data.PartialLocalStarRating = MovementEvaluator.EvaluatePartialLocalStarRatingOf(note);
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
            data.IsDirectionChangeOrEqual = note.IsMovingRight ? next.Position <= note.Position : next.Position >= note.Position;
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
        /// <returns>The <see cref="PatternType"/> corresponding to the note.</returns>
        private static PatternType classify(CatchDifficultyHitObject note, CatchDifficultyHitObject prev, CatchDifficultyHitObject next)
        {
            CatchMovementData data = note.MovementData;

            // Breaks
            PatternType breakType = classifyAsBreak(note, prev, next);

            if (breakType != PatternType.None && !data.SkipToDirectionChange)
            {
                return breakType;
            }

            // Stacks
            PatternType stackType = classifyAsStack(note, prev, next);

            if (stackType != PatternType.None && !data.SkipToDirectionChange)
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
            CatchMovementData prevData = prev.MovementData;

            // Breaks
            if (next.DeltaPosition < next.DeltaTime - note.CatcherWidth
                && next.DeltaTime > 150)
            {
                return next.DeltaPosition > note.CatcherWidth
                    ? PatternType.BreakBeginningRequiringMovement
                    : PatternType.BreakBeginningWithoutMovement;
            }

            if (next.DeltaPosition < next.DeltaTime - note.CatcherWidth
                && prevData.IsBreak)
            {
                return PatternType.SingleNote;
            }

            if (prevData.IsBreak
                && note.IsHyper)
            {
                return PatternType.HyperdashAfterBreak;
            }

            if (prevData.IsBreak
                && !note.IsHyper
                && note.DeltaPosition > 0
                && (next.DeltaPosition > note.CatcherWidth
                    || (next.DeltaPosition <= note.CatcherWidth
                        && next.DeltaTime <= 2 * next.DeltaPosition)))
            {
                return PatternType.EdgedashAfterBreak;
            }

            if (prevData.IsBreak
                && !note.IsHyper
                && note.DeltaPosition >= 0
                && next.DeltaTime > 2 * next.DeltaPosition
                && next.DeltaPosition <= note.CatcherWidth)
            {
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
                && next.DeltaPosition <= 3.0 * note.CatcherWidth / 5.0)
            {
                data.DisplayPattern = PatternType.NarrowStack;
                return PatternType.NarrowStack;
            }

            if (prevData.LeftStandingPosition is not null
                && next.DeltaPosition <= note.CatcherWidth)
            {
                data.DisplayPattern = PatternType.PotentialStack;
                return PatternType.PotentialStack;
            }

            // direction change check to exclude streams
            if (data.IsDirectionChangeOrEqual
                && note.DeltaPosition <= note.CatcherWidth)
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
                    return PatternType.Hyperstream;
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
                    return PatternType.PotentialStandstill;
                }

                if (!prev.IsHyper
                    && !note.IsHyper
                    && prev.SignificantMovementDirection == currentDirection
                    && calculateSpeed(note) <= calculateSpeed(next)
                    && next.DeltaPosition > note.HalfCatcherWidth)
                {
                    return PatternType.AcceleratingStream;
                }

                if (!prev.IsHyper
                    && !note.IsHyper
                    && ((prev.SignificantMovementDirection != currentDirection)
                        || (calculateSpeed(note) > calculateSpeed(next))
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

            // To simplify things, set ActionProbability to 0 for all narrow stacks
            if (next.DeltaPosition <= note.HalfCatcherWidth)
            {
                data.ActionProbability = 0;
            }

            switch (data.NotePattern)
            {
                case PatternType.BreakBeginningRequiringMovement:
                {
                    data.IsBreak = true;
                    data.ActionProbability = 0;
                    // Handle the action for speed at an earlier time in the speed evaluation - the case is already detected and stored
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

                case PatternType.HyperdashAfterBreak:
                {
                    data.ActionProbability = 1;
                    // Reset
                    data.LeftCatcherPosition = note.LeftNoteBorder;
                    data.RightCatcherPosition = note.RightNoteBorder;
                    break;
                }

                case PatternType.EdgedashAfterBreak:
                {
                    data.ActionProbability = 1;
                    data.BackwardCatcherPosition = data.FurthestForward(
                        note.Position - data.Directionize(note.HalfCatcherWidth),
                        next.Position - data.Directionize(note.HalfCatcherWidth + next.DeltaTime));
                    data.ForwardCatcherPosition = note.Position + data.Directionize(note.HalfCatcherWidth);
                    break;
                }

                case PatternType.StackAfterBreak:
                {
                    data.ActionProbability = 0;
                    data.BackwardCatcherPosition = data.FurthestForward(
                        note.Position - data.Directionize(note.HalfCatcherWidth),
                        next.Position - data.Directionize(note.HalfCatcherWidth + next.DeltaTime));
                    data.ForwardCatcherPosition = note.Position + data.Directionize(note.HalfCatcherWidth);

                    data.BackwardStandingPosition = next.Position - data.Directionize(note.HalfCatcherWidth);
                    data.ForwardStandingPosition = note.Position + data.Directionize(note.HalfCatcherWidth);
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
                    if (next.DeltaPosition <= 3 * note.CatcherWidth / 5.0)
                    {
                        data.NotePattern = PatternType.NarrowStack;
                        updateData(note, prev, next);
                        break;
                    }

                    data.LeftStandingPosition = Math.Max(note.Position - note.HalfCatcherWidth, next.Position - note.HalfCatcherWidth);
                    data.RightStandingPosition = Math.Min(note.Position + note.HalfCatcherWidth, next.Position + note.HalfCatcherWidth);

                    data.SkipToDirectionChange = true;

                    data.NotePattern = classify(note, prev, next);
                    updateData(note, prev, next);

                    break;
                }

                // first: if d_1 <= c/2, potential stack beginning, action probability 0/no jumps, set stand values
                // also c/2 < d_1 <= c
                // if x_2 > x_0 + c/2 or x_2 < x_0 - c/2, the pattern is normal, leave it as detected jumps or such continue on to the next note
                // if x_1 - c/2 <= x_2 <= x_1 + c/2, run stack detection
                case PatternType.PotentialStack:
                {
                    if (next.Position + note.HalfCatcherWidth < prevData.LeftStandingPosition || next.Position - note.HalfCatcherWidth > prevData.RightStandingPosition)
                    {
                        data.SkipToDirectionChange = true;
                        data.LeftStandingPosition = null;
                        data.RightStandingPosition = null;
                        data.IsStack = false;

                        data.NotePattern = classify(note, prev, next);
                        updateData(note, prev, next);

                        break;
                    }

                    data.LeftStandingPosition = Math.Max(note.Position - note.HalfCatcherWidth, next.Position - note.HalfCatcherWidth);
                    data.RightStandingPosition = Math.Min(note.Position + note.HalfCatcherWidth, next.Position + note.HalfCatcherWidth);

                    data.IsStack = true;

                    if (next.DeltaPosition / note.CatcherWidth >= millisecondsToCatcherStandingWidth(next.DeltaTime))
                    {
                        // wiggle
                        data.ActionProbability = 1;
                    }
                    else
                    {
                        // stand
                        data.ActionProbability = 0;
                    }

                    break;
                }

                case PatternType.StackContinuation:
                {
                    data.IsStack = true;

                    Debug.Assert(prevData.LeftStandingPosition != null, "prevData.LeftStandingPosition != null");
                    Debug.Assert(prevData.RightStandingPosition != null, "prevData.RightStandingPosition != null");

                    // Unchanged
                    data.LeftCatcherPosition = (double)prevData.LeftStandingPosition;
                    data.RightCatcherPosition = (double)prevData.RightStandingPosition;

                    data.LeftStandingPosition = prevData.LeftStandingPosition;
                    data.RightStandingPosition = prevData.RightStandingPosition;

                    double catcherStandingWidthBoundary = millisecondsToCatcherStandingWidth(next.DeltaTime);
                    bool isWigglingBetter = next.DeltaPosition / note.CatcherWidth >= catcherStandingWidthBoundary;

                    data.ActionProbability = isWigglingBetter ? 1 : 0;

                    break;
                }

                case PatternType.StackEnd:
                {
                    data.IsStack = false;
                    data.LeftStandingPosition = null;
                    data.RightStandingPosition = null;
                    data.SkipToDirectionChange = true;

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
                    data.NotePattern = classify(note, prev, next);
                    updateData(note, prev, next);

                    break;
                }

                // Direction changes
                case PatternType.JumpAfterHyperjump:
                {
                    data.ForwardCatcherPosition = next.Position + data.Directionize(note.HalfCatcherWidth + next.DeltaTime);
                    break;
                }

                case PatternType.Hyperjumps:
                {
                    data.ForwardCatcherPosition =
                        next.Position + data.Directionize(note.HalfCatcherWidth + next.DeltaTime * calculatePerfectHyperdashSpeed(next));
                    break;
                }

                case PatternType.HyperjumpAfterJump:
                {
                    // data.ForwardCatcherPosition = next.Position + data.Directionize(note.HalfCatcherWidth + calculatePrevToNextDistance(note, prev, next) / (next.DeltaTime - 1000.0 / 60.0) * next.DeltaTime);
                    data.ForwardCatcherPosition =
                        next.Position + data.Directionize(note.HalfCatcherWidth + calculatePrevToNextDistance(note, prev, next) / Math.Max(1, next.DeltaTime - 1000.0 / 60.0) * next.DeltaTime);

                    break;
                }

                case PatternType.Jumps:
                {
                    data.ForwardCatcherPosition = next.Position + data.Directionize(note.HalfCatcherWidth + next.DeltaTime);
                    break;
                }

                // Streams
                case PatternType.Hyperstream:
                {
                    data.ActionProbability = 0;
                    data.BackwardCatcherPosition = note.Position;
                    data.ForwardCatcherPosition = note.Position;
                    break;
                }

                case PatternType.PotentialStandstill:
                {
                    if (note.IsMovingRight)
                    {
                        data.ActionProbability = 1 - cdfWithNote(note.Position + note.HalfCatcherWidth - note.DeltaTime, prev);
                    }
                    else
                    {
                        data.ActionProbability = cdfWithNote(note.Position - note.HalfCatcherWidth + note.DeltaTime, prev);
                    }

                    data.BackwardCatcherPosition = note.Position - data.Directionize(note.HalfCatcherWidth);
                    data.ForwardCatcherPosition = data.FurthestBackward(prevForwardCatcherPosition + data.Directionize(note.DeltaTime), note.Position + data.Directionize(note.HalfCatcherWidth));
                    data.SpeedWeight = speed_bonus;
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
                        data.ActionProbability = Math.Abs(cdfWithNote(next.Position - note.HalfCatcherWidth - (note.DeltaTime + next.DeltaTime) / 2.0, prev) - cdfWithNote(note.Position + note.HalfCatcherWidth - note.DeltaTime, prev));
                    }
                    else
                    {
                        data.ActionProbability = Math.Abs(cdfWithNote(note.Position - note.HalfCatcherWidth + note.DeltaTime, prev) - cdfWithNote(next.Position + note.HalfCatcherWidth + (note.DeltaTime + next.DeltaTime) / 2.0, prev));
                    }

                    data.BackwardCatcherPosition = data.FurthestForward(next.Position - data.Directionize(note.HalfCatcherWidth + next.DeltaTime), note.Position - data.Directionize(note.HalfCatcherWidth));
                    data.ForwardCatcherPosition = data.FurthestBackward(prevForwardCatcherPosition + data.Directionize(note.DeltaTime), next.Position + data.Directionize(note.HalfCatcherWidth));

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
            double minimalDistance = calculateMinimalDistance(note, prev);
            double minimalVelocity = calculateMinimalHyperdashSpeed(note, prev, next);
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
                    if (data.ActionProbability == 1)
                    {
                        data.SkipToDirectionChange = true;
                        data.NotePattern = classify(note, prev, next);
                        double? precision = calculatePrecision(note, prev, next);
                        data.NotePattern = PatternType.StackContinuation;

                        return precision;
                    }

                    break;
                }

                case PatternType.JumpAfterHyperjump:
                {
                    if (next.DeltaPosition - next.DeltaTime >= note.HalfCatcherWidth)
                    {
                        return note.CatcherWidth / (2.0 * minimalVelocity);
                    }

                    double first = note.HalfCatcherWidth - next.DeltaPosition + nextToPrevDeltaTime;
                    double second = (data.Directionize(note.Position - prevForwardCatcherPosition) - note.HalfCatcherWidth) / minimalVelocity;
                    double third = first - second;

                    return third / 2.0;
                }

                case PatternType.Hyperjumps:
                {
                    double first = (note.HalfCatcherWidth - next.DeltaPosition) / calculatePerfectHyperdashSpeed(next);
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
                PatternType.StackContinuation => prevData.ActionProbability == 1 && data.ActionProbability == 0 ? note.CatcherWidth - next.DeltaPosition : null,
                _ => null
            };
        }

        /// <summary>
        /// Calculates the speed value for a given note.
        /// </summary>
        /// <param name="note"></param>
        /// <param name="prev"></param>
        /// <returns></returns>
        private static double calculateSpeed(CatchDifficultyHitObject note, CatchDifficultyHitObject prev)
        {
            CatchMovementData data = note.MovementData;
            CatchMovementData prevData = prev.MovementData;

            CatchDifficultyHitObject? prevGuaranteedAction = data.PreviousGuaranteedActionNote(0);
            CatchDifficultyHitObject? prevAmbiguousAction = data.PreviousActionNote(0);

            if (data.ActionProbability > 0)
            {
                if (data.ActionProbability < 1)
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

        private static double millisecondsToCatcherStandingWidth(double ms) => ms <= 188 ? 2.2 * 1e-5 * Math.Pow(ms, 2) - 8.3 * 1e-3 * ms + 1.35 : 0.567;

        /// <summary>
        /// Calculates the value of the CDF for the catcher position at the given note for the value x.
        /// </summary>
        /// <param name="x"></param>
        /// <param name="note"></param>
        /// <returns></returns>
        private static double cdfWithNote(double x, CatchDifficultyHitObject note) =>
            cdf(x, (note.MovementData.LeftCatcherPosition + note.MovementData.RightCatcherPosition) / 2.0,
                Math.Abs(note.MovementData.ForwardCatcherPosition - note.MovementData.BackwardCatcherPosition) / 6.0);

        /// <summary>
        /// Returns the value of the CDF with given mean and standard deviation at value x.
        /// </summary>
        /// <param name="x"></param>
        /// <param name="mean"></param>
        /// <param name="std"></param>
        /// <returns></returns>
        private static double cdf(double x, double mean, double std) => 0.5 * (1 + DifficultyCalculationUtils.Erf((x - mean) / (Math.Sqrt(2) * std)));

        /// <summary>
        /// Gets the catcher position of the last note closest to the current one.
        /// </summary>
        /// <param name="note"></param>
        /// <param name="prev"></param>
        /// <returns></returns>
        private static double getPrevForwardCatcherPosition(CatchDifficultyHitObject note, CatchDifficultyHitObject prev) =>
            note.IsMovingRight ? prev.MovementData.RightCatcherPosition : prev.MovementData.LeftCatcherPosition;

        private static double calculatePrevToNextDistance(CatchDifficultyHitObject note, CatchDifficultyHitObject prev, CatchDifficultyHitObject next) =>
            Math.Abs(note.MovementData.FurthestBackward(prev.MovementData.ForwardCatcherPosition + note.MovementData.Directionize(note.DeltaTime), note.ForwardNoteBorder) - next.Position);

        /// <summary>
        /// Calculates the minimal distance a catcher could travel between two notes.
        /// </summary>
        /// <param name="note"></param>
        /// <param name="prev"></param>
        /// <returns></returns>
        private static double calculateMinimalDistance(CatchDifficultyHitObject note, CatchDifficultyHitObject prev) =>
            Math.Abs(note.Position - note.MovementData.FurthestBackward(getPrevForwardCatcherPosition(note, prev), prev.Position + note.MovementData.Directionize(note.HalfCatcherWidth)));

        /// <summary>
        /// Calculates the simple speed between a note and the one before it.
        /// </summary>
        /// <param name="note">The current note.</param>
        /// <returns></returns>
        private static double calculateSpeed(CatchDifficultyHitObject note) => note.DeltaPosition / note.DeltaTime;

        /// <summary>
        /// Calculates the hyperdash speed between a note and the one before it, assuming that the catcher is perfectly positioned.
        /// </summary>
        /// <param name="note">The current note.</param>
        /// <returns></returns>
        private static double calculatePerfectHyperdashSpeed(CatchDifficultyHitObject note) => note.DeltaPosition / (Math.Max(note.DeltaTime - 1000.0 / 60.0, 1));

        /// <summary>
        /// Calculates the hyperdash speed between a note and the one before it, based on the expected player position.
        /// </summary>
        /// <param name="note">The current note.</param>
        /// <param name="prev">The previous note.</param>
        /// <param name="next">The next note.</param>
        /// <returns></returns>
        private static double calculateMinimalHyperdashSpeed(CatchDifficultyHitObject note, CatchDifficultyHitObject prev, CatchDifficultyHitObject next) =>
            calculateMinimalDistance(note, prev) / Math.Max(note.DeltaTime - 1000.0 / 60.0, 1);

        /// <summary>
        /// Calculates the average hyperdash speed between two notes.
        /// </summary>
        /// <param name="note"></param>
        /// <param name="prev"></param>
        /// <returns></returns>
        private static double calculateAverageHyperdashSpeed(CatchDifficultyHitObject note, CatchDifficultyHitObject prev)
        {
            CatchMovementData data = note.MovementData;
            CatchMovementData prevData = prev.MovementData;

            double left = Math.Max(prevData.LeftCatcherPosition, prev.LeftNoteBorder);
            double right = Math.Min(prevData.RightCatcherPosition, prev.RightNoteBorder);
            double average = (left + right) / 2.0;

            double distance = Math.Abs(note.Position - average);

            return distance / Math.Max(note.DeltaTime - 1000.0 / 60.0, 1);
        }
    }
}
