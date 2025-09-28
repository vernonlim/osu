// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Rulesets.Catch.Objects;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Objects;

namespace osu.Game.Rulesets.Catch.Difficulty.Preprocessing
{
    public enum PatternType
    {
        BreakBeginningRequiringMovement,
        JumpAfterHyperjump,
        Hyperjumps,
        HyperjumpAfterJump,
        Jumps,
        LastNote,
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
        private PalpableCatchHitObject prevBaseObject => (PalpableCatchHitObject)LastObject;

        public bool IsHyper => ((PalpableCatchHitObject)BaseObject).HyperDash;

        public double Position => ((PalpableCatchHitObject)BaseObject).EffectiveX;

        public double DeltaPosition => Math.Abs(Position - prevBaseObject.EffectiveX);

        public bool IsMovingRight => Position >= ((PalpableCatchHitObject)LastObject).EffectiveX;

        public MovementDirection Direction =>
            (Position - prevBaseObject.EffectiveX > HalfCatcherWidth) || (Position > prevBaseObject.EffectiveX && prevBaseObject.HyperDash)
                ? MovementDirection.Right
                : ((prevBaseObject.EffectiveX - Position > HalfCatcherWidth || (prevBaseObject.EffectiveX > Position && prevBaseObject.HyperDash))
                    ? MovementDirection.Left
                    : MovementDirection.None);

        public double DefaultHyperdashSpeed => DeltaPosition / (DeltaTime - 1000.0 / 60.0);

        // Methods used for generalizing directional code
        private double directionize(double val) => IsMovingRight ? val : -val;

        private double furthestForward(double val1, double val2) => IsMovingRight ? Math.Max(val1, val2) : Math.Min(val1, val2);

        private double furthestBackward(double val1, double val2) => IsMovingRight ? Math.Min(val1, val2) : Math.Max(val1, val2);

        // Initialized in constructor
        public double CatcherWidth;

        public double HalfCatcherWidth => CatcherWidth / 2;

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

        public double ActionProbability;

        // Initialized in second pass
        public bool IsDirectionChange;

        public double HyperdashSpeed;

        public PatternType NoteType;

        private bool isProcessed = false;

        public CatchDifficultyHitObject(HitObject hitObject, HitObject lastObject, double clockRate, float catcherWidth, List<DifficultyHitObject> objects, int index)
            : base(hitObject, lastObject, clockRate, objects, index)
        {
            CatcherWidth = catcherWidth;

            initializeBasicProperties();
        }

        private void initializeBasicProperties()
        {
            IsBreak = false;
            IsStack = false;
            BackwardCatcherPosition = IsMovingRight ? Position - HalfCatcherWidth : Position + HalfCatcherWidth;
            ForwardCatcherPosition = IsMovingRight ? Position + HalfCatcherWidth : Position - HalfCatcherWidth;
            BackwardStandingPosition = null;
            ForwardStandingPosition = null;
            ActionProbability = 1;
        }

        public void FinishInitialization()
        {
            if (isProcessed) return;

            CatchDifficultyHitObject? prev = Previous(0) as CatchDifficultyHitObject;
            CatchDifficultyHitObject? next = Next(0) as CatchDifficultyHitObject;

            if (prev == null)
            {
                IsBreak = true;
                NoteType = PatternType.BreakBeginningRequiringMovement;
                return;
            }

            if (next == null)
            {
                ActionProbability = 0;
                NoteType = PatternType.LastNote;
                return;
            }

            initializeRelationshipProperties(prev, next);

            NoteType = classifyNote(prev, next);

            updateProperties(prev, next);

            isProcessed = true;
        }

        private void initializeRelationshipProperties(CatchDifficultyHitObject prev, CatchDifficultyHitObject next)
        {
            IsDirectionChange = IsMovingRight ? next.Position < Position : next.Position > Position;
            HyperdashSpeed =
                Math.Abs(Position - 0.5 * (Math.Max(prev.LeftCatcherPosition, prev.Position - HalfCatcherWidth)) + Math.Min(prev.RightCatcherPosition, Position + HalfCatcherWidth))
                / (DeltaTime - 1000.0 / 60.0);
        }

        private PatternType classifyNote(CatchDifficultyHitObject prev, CatchDifficultyHitObject next)
        {
            if (IsDirectionChange)
            {
                if (prev.IsHyper && !IsHyper)
                    return PatternType.JumpAfterHyperjump;

                if (prev.IsHyper && IsHyper)
                    return PatternType.Hyperjumps;

                if (!prev.IsHyper && IsHyper)
                    return PatternType.HyperjumpAfterJump;

                if (!prev.IsHyper && !IsHyper)
                    return PatternType.Jumps;
            }

            return PatternType.None;
        }

        private void updateProperties(CatchDifficultyHitObject prev, CatchDifficultyHitObject next)
        {
            double prevForwardCatcherPosition = IsMovingRight ? prev.RightCatcherPosition : prev.LeftCatcherPosition;

            double modifiedVelocity = Math.Abs((next.Position - (prevForwardCatcherPosition + directionize(DeltaTime))) / (next.DeltaTime - 1000.0 / 60.0));

            switch (NoteType)
            {
                case PatternType.JumpAfterHyperjump:
                    ForwardCatcherPosition = next.Position + directionize(HalfCatcherWidth + next.DeltaTime);
                    break;

                case PatternType.Hyperjumps:
                    ForwardCatcherPosition = next.Position + directionize(HalfCatcherWidth + next.DeltaTime * next.DefaultHyperdashSpeed);
                    break;

                case PatternType.HyperjumpAfterJump:
                    ForwardCatcherPosition = next.Position + directionize(HalfCatcherWidth + modifiedVelocity * next.DeltaTime);
                    break;

                case PatternType.Jumps:
                    ForwardCatcherPosition = next.Position + directionize(HalfCatcherWidth + next.DeltaTime);
                    break;

                case PatternType.None:
                    break;
            }
        }

        private double calculatePrecision()
        {
            // calculate here

            return 0;
        }
    }
}
