// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using osu.Game.Rulesets.Difficulty.Preprocessing;

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
        private const double speed_bonus = 1.2;

        /// <summary>
        /// Processes a list of <see cref="CatchDifficultyHitObject"/>s and populates their corresponding <see cref="CatchMovementData"/>s.
        /// </summary>
        /// <param name="hitObjects"></param>
        public static void ProcessAndAssign(List<DifficultyHitObject> hitObjects)
        {
            // Special handling for the first and last objects of the map, as they lack a previous or future object
            CatchDifficultyHitObject first = (CatchDifficultyHitObject)hitObjects[0];
            first.MovementData.NotePattern = PatternType.FirstNote;
            updateInitialData(first, (CatchDifficultyHitObject)hitObjects[0]);


            CatchDifficultyHitObject last = (CatchDifficultyHitObject)hitObjects[^1];
            last.MovementData.NotePattern = PatternType.LastNote;
            last.MovementData.IsDirectionChange = false; // There is no next note to change direction to.
            // As above

            for (int i = 1; i < hitObjects.Count - 1; i++)
            {
                CatchDifficultyHitObject note = (CatchDifficultyHitObject)hitObjects[i];
                CatchDifficultyHitObject prev = (CatchDifficultyHitObject)hitObjects[i - 1];
                CatchDifficultyHitObject next = (CatchDifficultyHitObject)hitObjects[i + 1];

                CatchMovementData data = note.MovementData;

                updateInitialData(note, next);

                // I would make classify modify the data imperatively, but I think some flexibility is needed for some cases here
                data.NotePattern = classify(note, prev, next);

                updateData(note, prev, next);

                data.NotePrecision = calculatePrecision(note, prev, next);
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
            if (next.DeltaPosition < next.DeltaTime - 2 * note.CatcherWidth
                && next.DeltaTime > 200)
            {
                return next.DeltaPosition > note.CatcherWidth
                    ? PatternType.BreakBeginningRequiringMovement
                    : PatternType.BreakBeginningWithoutMovement;
            }

            if (next.DeltaPosition < next.DeltaTime - 2 * note.CatcherWidth
                && prevData.IsBreak)
            {
                return PatternType.SingleNote;
            }

            if (prevData.IsBreak
                && note.DeltaPosition > note.CatcherWidth
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

            // Stacks
            if (next.DeltaPosition <= note.CatcherWidth
                && (note.DeltaPosition > note.CatcherWidth || prevData.BackwardStandingPosition is not null)
                && (prevData.IsBreak || (note.DeltaPosition != 0 && data.IsDirectionChangeOrEqual)))
            {
                return PatternType.PotentialStackBeginning;
            }

            if (prevData.BackwardStandingPosition is not null
                && next.DeltaPosition <= note.HalfCatcherWidth
                && Math.Abs(next.Position - prev.Position) <= note.CatcherWidth)
            {
                return PatternType.NarrowStack;
            }

            if (prevData.BackwardStandingPosition is not null
                && (note.HalfCatcherWidth < next.DeltaPosition && next.DeltaPosition <= note.CatcherWidth)
                && data.IsDirectionChangeOrEqual)
            {
                return PatternType.PotentialStack;
            }

            if (prevData.IsStack
                && (prevData.LeftStandingPosition <= note.Position)
                && (prevData.RightStandingPosition >= note.Position))
            {
                return PatternType.StackContinuation;
            }

            if (prevData.IsStack
                && (note.Position < prevData.LeftStandingPosition || note.Position > prevData.RightStandingPosition))
            {
                return PatternType.StackEnd;
            }

            return PatternType.None;
        }

        /// <summary>
        /// Attempts to classify a note as a direction change.
        /// </summary>
        /// <param name="note">The current note.</param>
        /// <param name="prev">The previous note.</param>
        /// <param name="next">The next note.</param>
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
                    && calculateSpeed(note) <= calculateSpeed(next))
                {
                    return PatternType.AcceleratingStream;
                }

                if (!prev.IsHyper
                    && !note.IsHyper
                    && ((prev.SignificantMovementDirection != currentDirection)
                        || (calculateSpeed(note) > calculateSpeed(next))))
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

            double directionChangeVelocity = Math.Abs((next.Position - (prevForwardCatcherPosition + data.Directionize(note.DeltaTime))) / (next.DeltaTime - 1000.0 / 60.0));

            switch (data.NotePattern)
            {
                case PatternType.BreakBeginningRequiringMovement:
                {
                    data.IsBreak = true;
                    data.ActionProbability = 0;
                    // TODO: Handle weird new object thing
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
                    // default values, but we overwrite them to be sure
                    data.ActionProbability = 1;
                    data.LeftCatcherPosition = note.LeftNoteBorder;
                    data.RightCatcherPosition = note.RightNoteBorder;
                    break;
                }

                case PatternType.EdgedashAfterBreak:
                {
                    data.ActionProbability = 1;
                    data.LeftCatcherPosition = data.FurthestForward(
                        note.Position - data.Directionize(note.HalfCatcherWidth),
                        next.Position - data.Directionize(note.HalfCatcherWidth + next.DeltaTime));
                    data.RightCatcherPosition = note.Position + data.Directionize(note.HalfCatcherWidth);
                    break;
                }

                case PatternType.StackAfterBreak:
                {
                    data.ActionProbability = 0;
                    data.LeftCatcherPosition = data.FurthestForward(
                        note.Position - data.Directionize(note.HalfCatcherWidth),
                        next.Position - data.Directionize(note.HalfCatcherWidth + next.DeltaTime));
                    data.RightCatcherPosition = note.Position + data.Directionize(note.HalfCatcherWidth);
                    data.BackwardStandingPosition = next.Position - data.Directionize(note.HalfCatcherWidth);
                    data.ForwardStandingPosition = note.Position + data.Directionize(note.HalfCatcherWidth);
                    break;
                }

                case PatternType.PotentialStackBeginning:
                {
                    data.BackwardStandingPosition = next.Position - data.Directionize(note.CatcherWidth);
                    data.ForwardStandingPosition = note.Position + data.Directionize(note.CatcherWidth);

                    PatternType directionChangeType = classifyAsDirectionChange(note, prev);

                    if (directionChangeType != PatternType.None)
                    {
                        data.NotePattern = directionChangeType;
                    }
                    else
                    {
                        Console.WriteLine("This shouldn't happen!");
                    }

                    // Re-run this assuming it's a direction change
                    updateData(note, prev, next);

                    break;
                }

                case PatternType.NarrowStack:
                {
                    data.ActionProbability = 0;
                    data.IsStack = true;
                    data.BackwardCatcherPosition = next.Position - data.Directionize(note.HalfCatcherWidth);
                    data.ForwardCatcherPosition = note.Position + data.Directionize(note.HalfCatcherWidth);
                    data.BackwardStandingPosition = next.Position - data.Directionize(note.HalfCatcherWidth);
                    data.ForwardStandingPosition = note.Position + data.Directionize(note.HalfCatcherWidth);
                    break;
                }

                case PatternType.PotentialStack:
                {
                    data.IsStack = true;
                    data.BackwardStandingPosition = next.Position - data.Directionize(note.HalfCatcherWidth);
                    data.ForwardStandingPosition = note.Position + data.Directionize(note.HalfCatcherWidth);

                    // TODO: Local SR estimation
                    data.ActionProbability = 0;

                    break;
                }

                case PatternType.StackContinuation:
                {
                    // TODO: Decaying q
                    data.ActionProbability = 0;

                    Debug.Assert(prevData.LeftStandingPosition != null, "prevData.LeftStandingPosition != null");
                    Debug.Assert(prevData.RightStandingPosition != null, "prevData.RightStandingPosition != null");

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
                    data.SkipToDirectionChange = true;

                    prevData.BackwardCatcherPosition = prev.Position;
                    prevData.ForwardCatcherPosition = prev.Position + data.Directionize(note.HalfCatcherWidth);

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
                        next.Position + data.Directionize(note.HalfCatcherWidth + next.DeltaTime * calculatePerfectHyperdashSpeed(note));
                    break;
                }

                case PatternType.HyperjumpAfterJump:
                {
                    data.ForwardCatcherPosition = next.Position + data.Directionize(note.HalfCatcherWidth + directionChangeVelocity * next.DeltaTime);
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
                    data.LeftCatcherPosition = note.Position;
                    data.RightCatcherPosition = note.Position;
                    break;
                }

                case PatternType.PotentialStandstill:
                {
                    // TODO: Replace with probability
                    data.ActionProbability = 0;
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
                    // TODO: replace with probability
                    data.ActionProbability = 0;

                    data.BackwardStandingPosition = data.FurthestForward(next.Position - data.Directionize(note.HalfCatcherWidth + next.DeltaTime), note.Position - data.Directionize(note.HalfCatcherWidth));
                    data.ForwardCatcherPosition = data.FurthestBackward(prevForwardCatcherPosition + data.Directionize(note.DeltaTime), next.Position + data.Directionize(note.HalfCatcherWidth));

                    break;
                }

                case PatternType.FreeStream:
                {
                    data.ActionProbability = 0;

                    data.LeftCatcherPosition = note.LeftNoteBorder;
                    data.RightCatcherPosition = note.RightNoteBorder;

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

            double expectedDistance = Math.Abs(note.Position - (prevData.LeftCatcherPosition + prevData.RightCatcherPosition) / 2.0);
            double prevForwardCatcherPosition = note.IsMovingRight ? prevData.RightCatcherPosition : prevData.LeftCatcherPosition;

            switch (data.NotePattern)
            {
                case PatternType.JumpAfterHyperjump:
                {
                    if (next.DeltaTime + next.DeltaPosition > note.HalfCatcherWidth)
                    {
                        return (next.DeltaTime - next.DeltaPosition + note.HalfCatcherWidth)
                               / (2 * calculateExpectedHyperdashSpeed(note, prev));
                    }

                    return (note.DeltaTime + next.DeltaTime + note.HalfCatcherWidth - next.DeltaPosition -
                            (expectedDistance - note.HalfCatcherWidth) / calculateExpectedHyperdashSpeed(note, prev)) / 2.0;
                }

                case PatternType.Hyperjumps:
                {
                    return (2 * note.DeltaTime + note.CatcherWidth / calculatePerfectHyperdashSpeed(next) - (2 * expectedDistance - note.CatcherWidth) / calculateExpectedHyperdashSpeed(note, prev));
                }

                case PatternType.HyperjumpAfterJump:
                {
                    // far right or far left
                    double farPosition = prevForwardCatcherPosition + data.Directionize(note.DeltaTime);

                    return Math.Abs(farPosition - note.BackwardNoteBorder) + note.HalfCatcherWidth / (2 * calculateExpectedHyperdashSpeed(note, prev));
                }

                case PatternType.Jumps:
                {
                    return next.DeltaPosition + note.HalfCatcherWidth + next.DeltaTime;
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
        /// <returns></returns>
        private static double calculateExpectedHyperdashSpeed(CatchDifficultyHitObject note, CatchDifficultyHitObject prev) =>
            Math.Abs((note.Position - 0.5 * (Math.Max(prev.MovementData.LeftCatcherPosition, prev.LeftNoteBorder) + Math.Min(prev.MovementData.RightCatcherPosition, prev.RightNoteBorder)))
                     / Math.Max(note.DeltaTime - 1000.0 / 60.0, 1));
    }
}
