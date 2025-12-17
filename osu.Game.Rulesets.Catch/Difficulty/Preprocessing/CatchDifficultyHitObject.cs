// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Catch.Difficulty.Preprocessing.Data;
using osu.Game.Rulesets.Catch.Objects;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Objects;

namespace osu.Game.Rulesets.Catch.Difficulty.Preprocessing
{
    public class CatchDifficultyHitObject : DifficultyHitObject
    {
        public new PalpableCatchHitObject BaseObject => (PalpableCatchHitObject)base.BaseObject;

        public new PalpableCatchHitObject LastObject => (PalpableCatchHitObject)base.LastObject;

        private readonly IReadOnlyList<CatchDifficultyHitObject> noteDifficultyHitObjects;

        public readonly int NoteIndex;

        /// <summary>
        /// Whether this note is a Hyperdash.
        /// </summary>
        public bool IsHyper => BaseObject.HyperDash;

        /// <summary>
        /// The position of this note.
        /// </summary>
        public double Position;

        /// <summary>
        /// The distance between this note and the previous note.
        /// </summary>
        public double DeltaPosition;

        /// <summary>
        /// The left border of the note.
        /// </summary>
        public double LeftNoteBorder;

        /// <summary>
        /// The right border of the note.
        /// </summary>
        public double RightNoteBorder;

        /// <summary>
        /// The note border closest to the previous note.
        /// </summary>
        public double BackwardNoteBorder => IsMovingRight ? LeftNoteBorder : RightNoteBorder;

        /// <summary>
        /// The note border closest to the next note.
        /// </summary>
        public double ForwardNoteBorder => IsMovingRight ? RightNoteBorder : LeftNoteBorder;

        /// <summary>
        /// Whether this note is to the right of the previous note.
        /// </summary>
        /// <remarks>
        /// Difficulty calculation for each pattern is symmetric, with values having to be inverted depending on this property.
        /// </remarks>
        public bool IsMovingRight;

        /// <summary>
        /// The direction of movement between this note and the previous note.
        /// </summary>
        /// <remarks>
        /// If the distance is not deemed 'significant' enough (allowing for the catcher to catch both notes without any), this is set to None.
        /// </remarks>
        public MovementDirection SignificantMovementDirection(double catcherWidth, double clockRate) =>
            (Position - LastObject.EffectiveX / clockRate > catcherWidth / 2.0 || (Position > LastObject.EffectiveX / clockRate && LastObject.HyperDash))
                ? MovementDirection.Right
                : ((LastObject.EffectiveX / clockRate - Position > catcherWidth / 2.0 || (LastObject.EffectiveX / clockRate > Position && LastObject.HyperDash))
                    ? MovementDirection.Left
                    : MovementDirection.None);

        /// <summary>
        /// Movement data used in difficulty calculation.
        /// This is updated with meaningful values for each note by the available Preprocessors.
        /// </summary>
        public CatchMovementData MovementData;

        /// <summary>
        /// Reading data used in difficulty calculation.
        /// </summary>
        public CatchReadingData ReadingData;

        /// <summary>
        /// Data used only for GUI display - to be potentially removed in the future.
        /// </summary>
        public CatchDisplayData DisplayData;

        public CatchDifficultyHitObject(HitObject hitObject, HitObject lastObject, double clockRate,
                                        double normalizedCatcherWidth,
                                        List<DifficultyHitObject> objects,
                                        List<CatchDifficultyHitObject> noteObjects,
                                        int index)
            : base(hitObject, lastObject, clockRate, objects, index)
        {
            Position = BaseObject.EffectiveX / clockRate;
            LeftNoteBorder = Position - normalizedCatcherWidth / 2.0;
            RightNoteBorder = Position + normalizedCatcherWidth / 2.0;

            // Temporary hack to ensure DeltaPosition > 0
            if (noteObjects.Count >= 2)
            {
                CatchDifficultyHitObject prev = noteObjects[^1];
                CatchDifficultyHitObject prevPrev = noteObjects[^2];

                if (Position - prev.Position == 0)
                {
                    bool isMovingRight = prev.Position - prevPrev.Position > 0;

                    if (isMovingRight)
                    {
                        Position += 0.01;
                    }
                    else
                    {
                        Position -= 0.01;
                    }
                }
            }

            DeltaPosition = Math.Abs(Position - LastObject.EffectiveX / clockRate);

            if (noteObjects.Count >= 1)
            {
                CatchDifficultyHitObject prev = noteObjects[^1];
                IsMovingRight = Position > prev.Position;
            }
            else
            {
                IsMovingRight = Position >= LastObject.EffectiveX / clockRate;
            }

            noteDifficultyHitObjects = noteObjects;
            noteObjects.Add(this);

            NoteIndex = index;

            MovementData = new CatchMovementData(this, normalizedCatcherWidth, clockRate);
            ReadingData = new CatchReadingData();
            DisplayData = new CatchDisplayData();
        }

        public CatchDifficultyHitObject? PreviousNote(int backwardsIndex) => noteDifficultyHitObjects.ElementAtOrDefault(NoteIndex - (backwardsIndex + 1));

        public CatchDifficultyHitObject? NextNote(int forwardsIndex) => noteDifficultyHitObjects.ElementAtOrDefault(NoteIndex + (forwardsIndex + 1));
    }
}
