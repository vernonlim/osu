// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Catch.Difficulty.Preprocessing.Preprocessors;

namespace osu.Game.Rulesets.Catch.Difficulty.Preprocessing.Data
{
    public class CatchMovementData
    {
        /// <summary>
        /// The parent note object containing this data.
        /// </summary>
        public CatchDifficultyHitObject Note;

        /// <summary>
        /// The pattern type associated with this note.
        /// </summary>
        public PatternType NotePattern;

        /// <summary>
        /// The time at which the action associated with this note takes place.
        /// </summary>
        public double EffectiveTime;

        /// <summary>
        /// The key press associated with the action for this note, if it takes place.
        /// </summary>
        public MovementKey KeyPress;

        public MovementKey BackwardKeyPress => Note.IsMovingRight ? MovementKey.Left : MovementKey.Right;
        public MovementKey ForwardKeyPress => Note.IsMovingRight ? MovementKey.Right : MovementKey.Left;

        /// <summary>
        /// Is this note a HyperWalk?
        /// </summary>
        public bool IsHyperWalk;

        public CatchDifficultyHitObject? BeltBeginning;

        /// <summary>
        /// The leftmost position at the current time for which it is possible to catch both the previous note and the next note.
        /// </summary>
        public double LeftCatcherPosition;

        /// <summary>
        /// The rightmost position at the current time for which it is possible to catch both the previous note and the next note.
        /// </summary>
        public double RightCatcherPosition;

        /// <summary>
        /// The CatcherPosition closest to the previous note.
        /// </summary>
        public double BackwardCatcherPosition
        {
            get => Note.IsMovingRight ? LeftCatcherPosition : RightCatcherPosition;
            set
            {
                if (Note.IsMovingRight)
                {
                    LeftCatcherPosition = value;
                }
                else
                {
                    RightCatcherPosition = value;
                }
            }
        }

        /// <summary>
        /// The CatcherPosition furthest away from the previous note.
        /// </summary>
        public double ForwardCatcherPosition
        {
            get => Note.IsMovingRight ? RightCatcherPosition : LeftCatcherPosition;
            set
            {
                if (Note.IsMovingRight)
                {
                    RightCatcherPosition = value;
                }
                else
                {
                    LeftCatcherPosition = value;
                }
            }
        }

        /// <summary>
        /// The leftmost position the catcher is expected to stand within a stack.
        /// </summary>
        /// <remarks>
        /// Is null when not applicable.
        /// </remarks>
        public double? LeftStandingPosition;

        /// <summary>
        /// The rightmost position the catcher is expected to stand within a stack.
        /// </summary>
        /// <remarks>
        /// Is null when not applicable.
        /// </remarks>
        public double? RightStandingPosition;

        /// <summary>
        /// The StandingPosition closest to the previous note.
        /// </summary>
        public double? BackwardStandingPosition
        {
            get => Note.IsMovingRight ? LeftStandingPosition : RightStandingPosition;
            set
            {
                if (Note.IsMovingRight)
                {
                    LeftStandingPosition = value;
                }
                else
                {
                    RightStandingPosition = value;
                }
            }
        }

        /// <summary>
        /// The StandingPosition furthest away from the previous note.
        /// </summary>
        public double? ForwardStandingPosition
        {
            get => Note.IsMovingRight ? RightStandingPosition : LeftStandingPosition;
            set
            {
                if (Note.IsMovingRight)
                {
                    RightStandingPosition = value;
                }
                else
                {
                    LeftStandingPosition = value;
                }
            }
        }

        /// <summary>
        /// Is this note considered the start of a break?
        /// </summary>
        public bool IsBreak;

        /// <summary>
        ///  Is this note considered part of a stack?
        /// </summary>
        public bool IsStack;

        /// <summary>
        /// Whether the next note is in the opposite direction of the movement between this note and the previous.
        /// </summary>
        public bool IsDirectionChange;

        /// <summary>
        /// IsDirectionChange but including the case where the next note is at the same position of the current
        /// </summary>
        public bool IsDirectionChangeOrEqual;

        /// <summary>
        /// The likelihood of an action being performed.
        /// </summary>
        /// <remarks>
        /// An action is defined as a direction change or the independent releasing or pressing of the dash or movement keys.
        /// </remarks>
        public double ActionProbability;

        /// <summary>
        /// The time interval in which a chosen action leads to catching the next pattern.
        /// </summary>
        /// <remarks>
        /// Is null when considered infinite.
        /// </remarks>
        public double? NotePrecision;

        /// <summary>
        /// The width in pixels of the range allowing the catching of both the previous note and the next note.
        /// </summary>
        /// <remarks>
        /// Is null when the precision is not negligible.
        /// </remarks>
        public double? NoteAim;

        // For debug
        /// <summary>
        /// 1 divided by the time interval between this note and the last expected action.
        /// </summary>
        public double RawNoteSpeed;

        public double NoteSpeed;

        public double DirectionChangeWeight;
        public double PrecisionCorrection;
        public PatternType DisplayPattern;
        public double PartialLocalStarRating;
        public double LocalStarRating;

        /// <summary>
        /// Populates the class with default values which may be overwritten in <see cref="CatchMovementPreprocessor"/>.
        /// </summary>
        /// <param name="note"></param>
        public CatchMovementData(CatchDifficultyHitObject note)
        {
            Note = note;
            NotePattern = PatternType.None;
            EffectiveTime = note.StartTime;
            KeyPress = MovementKey.None;
            BeltBeginning = null;
            IsHyperWalk = false;
            IsBreak = false;
            IsStack = false;
            IsDirectionChange = false;
            IsDirectionChangeOrEqual = false;
            LeftCatcherPosition = note.Position - note.HalfCatcherWidth;
            RightCatcherPosition = note.Position + note.HalfCatcherWidth;
            LeftStandingPosition = null;
            RightStandingPosition = null;
            ActionProbability = 1;
            NotePrecision = null;
            NoteAim = null;
            RawNoteSpeed = 0;
            NoteSpeed = 0;

            DirectionChangeWeight = 1;
            PrecisionCorrection = 1;
            DisplayPattern = PatternType.None;
            PartialLocalStarRating = 0;
            LocalStarRating = 0;
        }

        /// <summary>
        /// Takes a displacement relative to the previous note (Note.IsMovingRight) and returns it as normal coordinates.
        /// </summary>
        /// <param name="movement">The relative displacement.</param>
        /// <returns>The normalized displacement.</returns>
        public double Directionize(double movement) => Note.IsMovingRight ? movement : -movement;

        /// <summary>
        /// Takes two positions and returns the one closest to the previous note.
        /// </summary>
        /// <param name="pos1"></param>
        /// <param name="pos2"></param>
        /// <returns></returns>
        public double FurthestBackward(double pos1, double pos2) => Note.IsMovingRight ? Math.Min(pos1, pos2) : Math.Max(pos1, pos2);

        /// <summary>
        /// Takes two positions and returns the one furthest from the previous note.
        /// </summary>
        /// <param name="pos1"></param>
        /// <param name="pos2"></param>
        /// <returns></returns>
        public double FurthestForward(double pos1, double pos2) => Note.IsMovingRight ? Math.Max(pos1, pos2) : Math.Min(pos1, pos2);

        // debug
        public double PrevToNextDistance;

        public double MinimalHyperdashSpeed;

        public double PerfectHyperdashSpeed;

        public double AverageHyperdashSpeed;
    }
}
