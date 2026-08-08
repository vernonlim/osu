// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

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
        /// The original pattern assigned to the note before reclassification.
        /// </summary>
        public PatternType OriginalPattern;

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

        public bool BeltHasAction;

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
        /// Number of wiggle notes in the stack in a row.
        /// </summary>
        public int StackWiggleCount;

        /// <summary>
        /// Whether the next note is in the opposite direction of the movement between this note and the previous.
        /// </summary>
        public bool IsDirectionChange;

        /// <summary>
        /// The likelihood of an action being performed.
        /// </summary>
        /// <remarks>
        /// An action is defined as a direction change or the independent releasing or pressing of the dash or movement keys.
        /// </remarks>
        public double ActionProbability;

        /// <summary>
        /// Is this a real action or one applied by the 0* pattern fix?
        /// </summary>
        public bool IsRealAction;

        /// <summary>
        /// The time interval in which a chosen action leads to catching the next pattern.
        /// </summary>
        /// <remarks>
        /// Is null when considered infinite.
        /// </remarks>
        public double? NotePrecision;

        public double? OriginalPrecision;

        /// <summary>
        /// Precision Strain of the note
        /// </summary>
        public double RawPrecisionStrain;

        /// <summary>
        /// Precision Strain of the note + previous note
        /// </summary>
        public double PrecisionStrain;

        /// <summary>
        /// Speed value for actions with the same direction.
        /// </summary>
        public double BurstSpeed;

        /// <summary>
        /// Speed value for actions with the same direction, but for the second previous action.
        /// </summary>
        public double ConsistencySpeed;

        /// <summary>
        /// Speed value for actions with any direction.
        /// </summary>
        public double SnapSpeed;

        /// <summary>
        /// Whether "Future Precision" was utilized.
        /// </summary>
        public bool FuturePrecisionUtilized;

        /// <summary>
        /// The precision between this note and the note after the next note.
        /// </summary>
        public double? FuturePrecision;

        /// <summary>
        /// A static bonus based on distance to prevent patterns from being 0*.
        /// </summary>
        public double DistanceBonus;

        /// <summary>
        /// Populates the class with default values which may be overwritten.
        /// </summary>
        /// <param name="note"></param>
        /// <param name="normalizedCatcherWidth"></param>
        /// <param name="clockRate"></param>
        public CatchMovementData(CatchDifficultyHitObject note, double normalizedCatcherWidth, double clockRate)
        {
            Note = note;
            NotePattern = PatternType.None;
            OriginalPattern = PatternType.None;
            EffectiveTime = note.StartTime;
            KeyPress = MovementKey.None;
            BeltBeginning = null;
            BeltHasAction = false;
            IsHyperWalk = false;
            IsBreak = false;
            IsStack = false;
            StackWiggleCount = 0;
            IsDirectionChange = false;
            LeftCatcherPosition = note.Position - normalizedCatcherWidth / 2.0;
            RightCatcherPosition = note.Position + normalizedCatcherWidth / 2.0;
            LeftStandingPosition = null;
            RightStandingPosition = null;
            ActionProbability = 1;
            IsRealAction = true;
            NotePrecision = null;
            OriginalPrecision = null;
            RawPrecisionStrain = 0;
            PrecisionStrain = 0;
            FuturePrecisionUtilized = false;
            FuturePrecision = null;
            DistanceBonus = 0;
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
    }
}
