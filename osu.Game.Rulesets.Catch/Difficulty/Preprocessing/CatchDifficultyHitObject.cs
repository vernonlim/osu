// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using osu.Game.Rulesets.Catch.Objects;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Objects;

namespace osu.Game.Rulesets.Catch.Difficulty.Preprocessing
{
    public enum PatternType
    {
        JumpAfterHyperjump,
        Hyperjumps,
        HyperjumpAfterJump,
        Jumps,
        None,
    }

    public enum MovementDirection
    {
        Right,
        Left,
        None
    }

    public class CatchDifficultyHitObject : DifficultyHitObject
    {
        private readonly PalpableCatchHitObject next;

        private readonly CatchDifficultyHitObject? prev;

        private PalpableCatchHitObject prevBaseObject => (PalpableCatchHitObject)LastObject;

        public bool IsHyper => ((PalpableCatchHitObject)BaseObject).HyperDash;

        public double Position => ((PalpableCatchHitObject)BaseObject).EffectiveX;

        public double NextPosition => next.EffectiveX;

        public double DeltaPosition => Math.Abs(Position - prevBaseObject.EffectiveX);

        public double NextDeltaPosition => Math.Abs(next.EffectiveX - Position);

        public double NextDeltaTime => next.StartTime - BaseObject.StartTime;

        public double CatcherSpeed => DeltaPosition / (DeltaTime - 1000.0 / 60.0);
        public double NextCatcherSpeed => NextDeltaPosition / (NextDeltaTime - 1000.0 / 60.0);

        public double CatcherWidth;

        public double HalfCatcherWidth => CatcherWidth / 2;

        public bool IsMovingRight => Position >= ((PalpableCatchHitObject)LastObject).EffectiveX;

        public bool IsDirectionChange => IsMovingRight ? NextPosition < Position : NextPosition > Position;

        public MovementDirection Direction =>
            (Position - prevBaseObject.EffectiveX > HalfCatcherWidth) || (Position > prevBaseObject.EffectiveX && prevBaseObject.HyperDash)
                ? MovementDirection.Right
                : ((prevBaseObject.EffectiveX - Position > HalfCatcherWidth || (prevBaseObject.EffectiveX > Position && prevBaseObject.HyperDash))
                    ? MovementDirection.Left
                    : MovementDirection.None);

        private double directionize(double val) => IsMovingRight ? val : -val;

        private double furthestForward(double val1, double val2) => IsMovingRight ? Math.Max(val1, val2) : Math.Min(val1, val2);

        private double furthestBackward(double val1, double val2) => IsMovingRight ? Math.Min(val1, val2) : Math.Max(val1, val2);

        public double BackwardCatcherPosition;

        public double ForwardCatcherPosition;

        public bool IsBreak;

        public bool IsStack;

        public double LeftCatcherPosition => IsMovingRight ? BackwardCatcherPosition : ForwardCatcherPosition;

        public double RightCatcherPosition => IsMovingRight ? ForwardCatcherPosition : BackwardCatcherPosition;

        public double? BackwardStandingPosition;

        public double? ForwardStandingPosition;

        public double? LeftStandingPosition => IsMovingRight ? BackwardStandingPosition : ForwardStandingPosition;

        public double? RightStandingPosition => IsMovingRight ? ForwardStandingPosition : BackwardStandingPosition;

        public PatternType NoteType;

        public double ActionProbability;

        public double Precision;

        public double Speed;

        public CatchDifficultyHitObject(HitObject hitObject, HitObject lastObject, HitObject nextObject, double clockRate, float catcherWidth, List<DifficultyHitObject> objects, int index)
            : base(hitObject, lastObject, clockRate, objects, index)
        {
            next = (PalpableCatchHitObject)nextObject;
            CatcherWidth = catcherWidth;

            initializeVariables();

            DifficultyHitObject prevHitObject = Previous(0);

            if (prevHitObject is CatchDifficultyHitObject difficultyHitObject)
            {
                prev = difficultyHitObject;
            }
            else
            {
                prev = null;

                // If this is the 'first' object, set break=1 and leave other values at default.
                IsBreak = true;
                return;
            }

            NoteType = classifyNote();

            updateVariables();
        }

        private void initializeVariables()
        {
            IsBreak = false;
            IsStack = false;
            BackwardCatcherPosition = IsMovingRight ? Position - HalfCatcherWidth : Position + HalfCatcherWidth;
            ForwardCatcherPosition = IsMovingRight ? Position + HalfCatcherWidth : Position - HalfCatcherWidth;
            BackwardStandingPosition = null;
            ForwardStandingPosition = null;
            ActionProbability = 1;
        }

        private PatternType classifyNote()
        {
            Debug.Assert(prev != null, nameof(prev) + " != null");

            if (IsDirectionChange)
            {
                if (prev.IsHyper && IsHyper)
                    return PatternType.HyperjumpAfterJump;

                if (prev.IsHyper && !IsHyper)
                    return PatternType.Hyperjumps;

                if (!prev.IsHyper && IsHyper)
                    return PatternType.JumpAfterHyperjump;

                if (!prev.IsHyper && !IsHyper)
                    return PatternType.Jumps;
            }

            return PatternType.None;
        }

        private void updateVariables()
        {
            Debug.Assert(prev != null, nameof(prev) + " != null");

            double prevBackwardCatcherPosition = IsMovingRight ? prev.LeftCatcherPosition : prev.RightCatcherPosition;
            double prevForwardCatcherPosition = IsMovingRight ? prev.RightCatcherPosition : prev.LeftCatcherPosition;

            double modifiedVelocity = Math.Abs((NextPosition - (prevForwardCatcherPosition + directionize(DeltaTime))) / (NextDeltaTime - 1000.0 / 60.0));

            switch (NoteType)
            {
                case PatternType.HyperjumpAfterJump:
                    ForwardCatcherPosition = NextPosition + directionize(HalfCatcherWidth + NextDeltaTime * NextCatcherSpeed);
                    break;

                case PatternType.Hyperjumps:
                    ForwardCatcherPosition = NextPosition + directionize(HalfCatcherWidth + NextDeltaTime);
                    break;

                case PatternType.JumpAfterHyperjump:
                    ForwardCatcherPosition = NextPosition + directionize(HalfCatcherWidth + modifiedVelocity * NextDeltaTime);
                    break;

                case PatternType.Jumps:
                    ForwardCatcherPosition = NextPosition + directionize(HalfCatcherWidth + NextDeltaTime);
                    break;

                case PatternType.None:
                    break;
            }
        }
    }
}
