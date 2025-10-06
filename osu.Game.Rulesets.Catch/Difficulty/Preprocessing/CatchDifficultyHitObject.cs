// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Catch.Difficulty.Data;
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
        public double Position => BaseObject.EffectiveX;

        /// <summary>
        /// The width of the catcher.
        /// </summary>
        /// <remarks>
        /// Equivalent to the width of the notes.
        /// </remarks>
        public double CatcherWidth;

        /// <summary>
        /// Half of the catcher width.
        /// </summary>
        /// <remarks>
        /// Equivalent to the radius of each note.
        /// </remarks>
        public double HalfCatcherWidth => CatcherWidth / 2;

        /// <summary>
        /// The distance between this note and the previous note.
        /// </summary>
        public double DeltaPosition => Math.Abs(Position - LastObject.EffectiveX);

        /// <summary>
        /// The left border of the note.
        /// </summary>
        public double LeftNoteBorder => Position - HalfCatcherWidth;

        /// <summary>
        /// The right border of the note.
        /// </summary>
        public double RightNoteBorder => Position + HalfCatcherWidth;

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
        public bool IsMovingRight => Position >= LastObject.EffectiveX;

        /// <summary>
        /// The direction of movement between this note and the previous note.
        /// </summary>
        /// <remarks>
        /// If the distance is not deemed 'significant' enough (allowing for the catcher to catch both notes without any), this is set to None.
        /// </remarks>
        public MovementDirection SignificantMovementDirection => (Position - LastObject.EffectiveX > HalfCatcherWidth || (Position > LastObject.EffectiveX && LastObject.HyperDash))
            ? MovementDirection.Right
            : ((LastObject.EffectiveX - Position > HalfCatcherWidth || (LastObject.EffectiveX > Position && LastObject.HyperDash))
                ? MovementDirection.Left
                : MovementDirection.None);

        /// <summary>
        /// Movement data used in difficulty calculation.
        /// This is updated with meaningful values for each note by the available Preprocessors.
        /// </summary>
        public CatchMovementData MovementData;

        public CatchDifficultyHitObject(HitObject hitObject, HitObject lastObject, double clockRate,
                                        float catcherWidth,
                                        List<DifficultyHitObject> objects,
                                        List<CatchDifficultyHitObject> noteObjects,
                                        int index,
                                        List<CatchDifficultyHitObject> guaranteedActionNoteObjects,
                                        List<CatchDifficultyHitObject> ambiguousActionNoteObjects)
            : base(hitObject, lastObject, clockRate, objects, index)
        {
            CatcherWidth = catcherWidth;

            noteDifficultyHitObjects = noteObjects;
            noteObjects.Add(this);

            NoteIndex = index;

            MovementData = new CatchMovementData(this, guaranteedActionNoteObjects, ambiguousActionNoteObjects);
        }

        public CatchDifficultyHitObject? PreviousNote(int backwardsIndex) => noteDifficultyHitObjects.ElementAtOrDefault(NoteIndex - (backwardsIndex + 1));

        public CatchDifficultyHitObject? NextNote(int forwardsIndex) => noteDifficultyHitObjects.ElementAtOrDefault(NoteIndex + (forwardsIndex + 1));
    }
}
