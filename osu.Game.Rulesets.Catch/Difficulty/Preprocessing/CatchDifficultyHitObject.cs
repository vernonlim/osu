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
        public const double NORMALIZED_HALF_CATCHER_WIDTH = 41.0f;
        private const double absolute_player_positioning_error = 16.0f;

        /// <summary>
        /// Normalized position of <see cref="BaseObject"/>.
        /// </summary>
        public readonly double NormalizedPosition;

        /// <summary>
        /// Normalized position of <see cref="LastObject"/>.
        /// </summary>
        public readonly double LastNormalizedPosition;

        /// <summary>
        /// Normalized position of the player required to catch <see cref="BaseObject"/>, assuming the player moves as little as possible.
        /// </summary>
        public double PlayerPosition { get; private set; }

        /// <summary>
        /// Normalized position of the player after catching <see cref="LastObject"/>.
        /// </summary>
        public double LastPlayerPosition { get; private set; }

        /// <summary>
        /// Normalized distance between <see cref="LastPlayerPosition"/> and <see cref="PlayerPosition"/>.
        /// </summary>
        /// <remarks>
        /// The sign of the value indicates the direction of the movement: negative is left and positive is right.
        /// </remarks>
        public double DistanceMoved { get; private set; }

        /// <summary>
        /// Normalized distance the player has to move from <see cref="LastPlayerPosition"/> in order to catch <see cref="BaseObject"/> at its <see cref="NormalizedPosition"/>.
        /// </summary>
        /// <remarks>
        /// The sign of the value indicates the direction of the movement: negative is left and positive is right.
        /// </remarks>
        public double ExactDistanceMoved { get; private set; }

        /// <summary>
        /// Milliseconds elapsed since the start time of the previous <see cref="CatchDifficultyHitObject"/>, with a minimum of 40ms.
        /// </summary>
        public readonly double StrainTime;

        private readonly double clockRate;

        public new PalpableCatchHitObject BaseObject => (PalpableCatchHitObject)base.BaseObject;

        public new PalpableCatchHitObject LastObject => (PalpableCatchHitObject)base.LastObject;

        private readonly IReadOnlyList<CatchDifficultyHitObject> noteDifficultyHitObjects;

        public readonly int NoteIndex;

        /// <summary>
        /// The minimum frame time the game is assumed to have.
        /// </summary>
        public double FrameTime;

        /// <summary>
        /// Whether this note is a Hyperdash.
        /// </summary>
        public bool IsHyper => BaseObject.HyperDash;

        /// <summary>
        /// The position of this note.
        /// </summary>
        public double Position;

        /// <summary>
        /// Normalized playfield width>
        /// </summary>
        public double PlayfieldWidth;

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
        public double HalfCatcherWidth => CatcherWidth / 2.0;

        /// <summary>
        /// The distance between this note and the previous note.
        /// </summary>
        public double DeltaPosition => Math.Abs(Position - LastObject.EffectiveX / clockRate);

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
        public bool IsMovingRight;

        /// <summary>
        /// The direction of movement between this note and the previous note.
        /// </summary>
        /// <remarks>
        /// If the distance is not deemed 'significant' enough (allowing for the catcher to catch both notes without any), this is set to None.
        /// </remarks>
        public MovementDirection SignificantMovementDirection =>
            (Position - LastObject.EffectiveX / clockRate > HalfCatcherWidth || (Position > LastObject.EffectiveX / clockRate && LastObject.HyperDash))
                ? MovementDirection.Right
                : ((LastObject.EffectiveX / clockRate - Position > HalfCatcherWidth || (LastObject.EffectiveX / clockRate > Position && LastObject.HyperDash))
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

        public CatchDifficultyHitObject(HitObject hitObject, HitObject lastObject, double clockRate,
                                        float catcherWidth,
                                        List<DifficultyHitObject> objects,
                                        List<CatchDifficultyHitObject> noteObjects,
                                        int index)
            : base(hitObject, lastObject, clockRate, objects, index)
        {
            this.clockRate = clockRate;

            Position = BaseObject.EffectiveX / clockRate;
            PlayfieldWidth = 512.0 / clockRate;

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

            if (noteObjects.Count >= 1)
            {
                CatchDifficultyHitObject prev = noteObjects[^1];
                IsMovingRight = Position > prev.Position;
            }
            else
            {
                IsMovingRight = Position >= LastObject.EffectiveX / clockRate;
            }

            CatcherWidth = catcherWidth / clockRate;

            FrameTime = 1000.0 / 60.0 / clockRate;

            noteDifficultyHitObjects = noteObjects;
            noteObjects.Add(this);

            NoteIndex = index;

            MovementData = new CatchMovementData(this);
            ReadingData = new CatchReadingData();

            // We will scale everything by this factor, so we can assume a uniform CircleSize among beatmaps.
            double scalingFactor = NORMALIZED_HALF_CATCHER_WIDTH / HalfCatcherWidth;

            NormalizedPosition = BaseObject.EffectiveX * scalingFactor;
            LastNormalizedPosition = LastObject.EffectiveX * scalingFactor;

            // Every strain interval is hard capped at the equivalent of 375 BPM streaming speed as a safety measure
            StrainTime = Math.Max(40, DeltaTime);

            setMovementState();
        }

        private void setMovementState()
        {
            LastPlayerPosition = Index == 0 ? LastNormalizedPosition : ((CatchDifficultyHitObject)Previous(0)).PlayerPosition;

            PlayerPosition = Math.Clamp(
                LastPlayerPosition,
                NormalizedPosition - (NORMALIZED_HALF_CATCHER_WIDTH - absolute_player_positioning_error),
                NormalizedPosition + (NORMALIZED_HALF_CATCHER_WIDTH - absolute_player_positioning_error)
            );

            DistanceMoved = PlayerPosition - LastPlayerPosition;

            // For the exact position we consider that the catcher is in the correct position for both objects
            ExactDistanceMoved = NormalizedPosition - LastPlayerPosition;

            // After a hyperdash we ARE in the correct position. Always!
            if (LastObject.HyperDash)
                PlayerPosition = NormalizedPosition;
        }

        public CatchDifficultyHitObject? PreviousNote(int backwardsIndex) => noteDifficultyHitObjects.ElementAtOrDefault(NoteIndex - (backwardsIndex + 1));

        public CatchDifficultyHitObject? NextNote(int forwardsIndex) => noteDifficultyHitObjects.ElementAtOrDefault(NoteIndex + (forwardsIndex + 1));
    }
}
