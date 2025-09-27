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
    public class CatchDifficultyHitObject : DifficultyHitObject
    {
        private readonly PalpableCatchHitObject next;

        private readonly CatchDifficultyHitObject? prev;

        public bool IsHyper => ((PalpableCatchHitObject)BaseObject).HyperDash;

        public double Position => ((PalpableCatchHitObject)BaseObject).EffectiveX;

        public double NextPosition => next.EffectiveX;

        public double DeltaPosition => Math.Abs(Position - ((PalpableCatchHitObject)LastObject).EffectiveX);

        public double NextDeltaPosition => Math.Abs(next.EffectiveX - Position);

        public double NextDeltaTime => next.StartTime - BaseObject.StartTime;

        public double Speed => DeltaPosition / (DeltaTime - 1000.0 / 60.0);
        public double NextSpeed => NextDeltaPosition / (NextDeltaTime - 1000.0 / 60.0);

        public double CatcherWidth;

        public double HalfCatcherWidth => CatcherWidth / 2;

        public bool IsMovingRight => Position >= ((PalpableCatchHitObject)LastObject).EffectiveX;

        public bool IsDirectionChange => IsMovingRight ? NextPosition < Position : NextPosition > Position;

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

            enumerateCases();
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

        private double directionize(double val) => IsMovingRight ? val : -val;

        private double furthestForward(double val1, double val2) => IsMovingRight ? Math.Max(val1, val2) : Math.Min(val1, val2);

        private double furthestBackward(double val1, double val2) => IsMovingRight ? Math.Min(val1, val2) : Math.Max(val1, val2);

        private void enumerateCases()
        {
            // prevObject should never be null due to skipping this method for the 'first' object.
            Debug.Assert(prev != null, nameof(prev) + " != null");

            // Cases 4.3.1 to 4.3.4
            ForwardCatcherPosition = getDirectionChangePosition();
        }

        private double getDirectionChangePosition()
        {
            double prevBackwardCatcherPosition = IsMovingRight ? prev.LeftCatcherPosition : prev.RightCatcherPosition;
            double prevForwardCatcherPosition = IsMovingRight ? prev.RightCatcherPosition : prev.LeftCatcherPosition;

            double modifiedVelocity = Math.Abs((NextPosition - (prevForwardCatcherPosition + directionize(DeltaTime))) / (NextDeltaTime - 1000.0 / 60.0));

            if (IsDirectionChange)
            {
                if (prev.IsHyper && IsHyper)
                    return NextPosition + directionize(HalfCatcherWidth + NextDeltaTime * NextSpeed);

                if (prev.IsHyper && !IsHyper)
                    return NextPosition + directionize(HalfCatcherWidth + NextDeltaTime);

                if (!prev.IsHyper && IsHyper)
                    return NextPosition + directionize(HalfCatcherWidth + modifiedVelocity * NextDeltaTime);

                if (!prev.IsHyper && !IsHyper)
                    return NextPosition + directionize(HalfCatcherWidth + NextDeltaTime);
            }

            return ForwardCatcherPosition;
        }
    }
}
