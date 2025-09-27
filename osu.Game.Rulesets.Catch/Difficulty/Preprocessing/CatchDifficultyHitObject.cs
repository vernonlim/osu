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

        public float Position => ((PalpableCatchHitObject)BaseObject).EffectiveX;

        public float NextPosition => next.EffectiveX;

        public float DeltaPosition => Math.Abs(Position - ((PalpableCatchHitObject)LastObject).EffectiveX);

        public float NextDeltaPosition => Math.Abs(next.EffectiveX - Position);

        public double NextDeltaTime => next.StartTime - BaseObject.StartTime;

        public double Velocity => DeltaPosition / (DeltaTime - 1000.0 / 60.0);
        public double NextVelocity => NextDeltaPosition / (NextDeltaTime - 1000.0 / 60.0);

        public float CatcherWidth;

        public float HalfCatcherWidth => CatcherWidth / 2;

        public bool IsMovingRight => Position >= ((PalpableCatchHitObject)LastObject).EffectiveX;

        public bool IsDirectionChange => IsMovingRight ? NextPosition < Position : NextPosition > Position;

        public float BackwardCatcherPosition;

        public float ForwardCatcherPosition;

        public bool IsBreak;

        public bool IsStack;

        public float LeftCatcherPosition => IsMovingRight ? BackwardCatcherPosition : ForwardCatcherPosition;

        public float RightCatcherPosition => IsMovingRight ? ForwardCatcherPosition : BackwardCatcherPosition;

        public float? LeftStandingPosition;

        public float? RightStandingPosition;

        public float ActionProbability;

        public CatchDifficultyHitObject(HitObject hitObject, HitObject lastObject, HitObject nextObject, double clockRate, float catcherWidth, List<DifficultyHitObject> objects, int index)
            : base(hitObject, lastObject, clockRate, objects, index)
        {
            this.next = (PalpableCatchHitObject)nextObject;
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
            LeftStandingPosition = null;
            RightStandingPosition = null;
            ActionProbability = 1;
        }

        private float directionize(float val) => IsMovingRight ? val : -val;

        private double directionize(double val) => IsMovingRight ? val : -val;

        private void enumerateCases()
        {
            // prevObject should never be null due to skipping this method for the 'first' object.
            Debug.Assert(prev != null, nameof(prev) + " != null");

            float prevBackwardCatcherPosition = IsMovingRight ? prev.LeftCatcherPosition : prev.RightCatcherPosition;
            float prevForwardCatcherPosition = IsMovingRight ? prev.RightCatcherPosition : prev.LeftCatcherPosition;

            double modifiedVelocity = Math.Abs((NextPosition - (prevForwardCatcherPosition + directionize(DeltaTime))) / (NextDeltaTime - 1000.0 / 60.0));

            if (IsDirectionChange)
            {
                if (prev.IsHyper && IsHyper)
                    ForwardCatcherPosition = NextPosition + directionize(HalfCatcherWidth + (float)(NextDeltaTime * NextVelocity));

                if (prev.IsHyper && !IsHyper)
                    ForwardCatcherPosition = NextPosition + directionize(HalfCatcherWidth + (float)NextDeltaTime);

                if (!prev.IsHyper && IsHyper)
                    ForwardCatcherPosition = NextPosition + directionize(HalfCatcherWidth + (float)(modifiedVelocity * NextDeltaTime));

                if (!prev.IsHyper && !IsHyper)
                    ForwardCatcherPosition = NextPosition + directionize(HalfCatcherWidth + (float)NextDeltaTime);
            }
        }
    }
}
