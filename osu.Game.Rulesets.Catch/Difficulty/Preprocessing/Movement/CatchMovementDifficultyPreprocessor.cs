// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Rulesets.Difficulty.Preprocessing;

namespace osu.Game.Rulesets.Catch.Difficulty.Preprocessing.Movement
{
    /// <summary>
    /// Utility class that calculates Movement-related properties.
    /// </summary>
    public class CatchMovementDifficultyPreprocessor
    {
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
            // Add any extra value calculations here (specifically precision, aim, etc)

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

                data.NotePattern = classify(note, prev, next);

                updateData(note, prev, next);
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
            CatchMovementData nextData = next.MovementData;

            double prevForwardCatcherPosition = note.IsMovingRight ? prevData.RightCatcherPosition : prevData.LeftCatcherPosition;

            double modifiedVelocity = Math.Abs((next.Position - (prevForwardCatcherPosition + data.Directionize(note.DeltaTime))) / (next.DeltaTime - 1000.0 / 60.0));

            switch (data.NotePattern)
            {
                case PatternType.JumpAfterHyperjump:
                    data.ForwardCatcherPosition = next.Position + data.Directionize(note.HalfCatcherWidth + next.DeltaTime);
                    break;

                case PatternType.Hyperjumps:
                    data.ForwardCatcherPosition =
                        next.Position + data.Directionize(note.HalfCatcherWidth + next.DeltaTime * calculatePerfectHyperdashSpeed(note));
                    break;

                case PatternType.HyperjumpAfterJump:
                    data.ForwardCatcherPosition = next.Position + data.Directionize(note.HalfCatcherWidth + modifiedVelocity * next.DeltaTime);
                    break;

                case PatternType.Jumps:
                    data.ForwardCatcherPosition = next.Position + data.Directionize(note.HalfCatcherWidth + next.DeltaTime);
                    break;

                case PatternType.None:
                    break;
            }
        }

        private static double calculatePerfectHyperdashSpeed(CatchDifficultyHitObject note) => note.DeltaPosition / (note.DeltaTime - 1000.0 / 60.0);
    }
}
