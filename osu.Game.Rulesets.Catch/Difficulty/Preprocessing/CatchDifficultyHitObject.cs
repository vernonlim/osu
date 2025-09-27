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

        public double Velocity => DeltaPosition / (DeltaTime - 1000.0 / 60.0) * 0.001;
        public double NextVelocity => NextDeltaPosition / (NextDeltaTime - 1000.0 / 60.0) * 0.001;

        public float CatcherWidth;

        public float HalfCatcherWidth => CatcherWidth / 2;

        public readonly float LeftNoteBorder;

        public readonly float RightNoteBorder;

        public bool IsBreak;

        public bool IsStack;

        public float LeftCatcherPosition;

        public float RightCatcherPosition;

        public float? LeftStandingPosition;

        public float? RightStandingPosition;

        public float ActionProbability;

        public CatchDifficultyHitObject(HitObject hitObject, HitObject lastObject, HitObject nextObject, double clockRate, float catcherWidth, List<DifficultyHitObject> objects, int index)
            : base(hitObject, lastObject, clockRate, objects, index)
        {
            this.next = (PalpableCatchHitObject)nextObject;
            CatcherWidth = catcherWidth;
            LeftNoteBorder = Position - (HalfCatcherWidth);
            RightNoteBorder = Position + (HalfCatcherWidth);

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

            clampPositions();
        }

        private void initializeVariables()
        {
            IsBreak = false;
            IsStack = false;
            LeftCatcherPosition = LeftNoteBorder;
            RightCatcherPosition = RightNoteBorder;
            LeftStandingPosition = null;
            RightStandingPosition = null;
            ActionProbability = 1;
        }

        private void clampPositions()
        {
            LeftCatcherPosition = float.Clamp(LeftCatcherPosition, 0, 512);
            RightCatcherPosition = float.Clamp(RightCatcherPosition, 0, 512);
        }

        private void enumerateCases()
        {
            // prevObject should never be null due to skipping this method for the 'first' object.
            Debug.Assert(prev != null, nameof(prev) + " != null");

            // Cases 4.3.1 - 4.3.4
            if (Position > prev.Position && NextPosition < Position)
            {
                if (prev.IsHyper)
                {
                    if (IsHyper)
                    {
                        RightCatcherPosition = NextPosition + HalfCatcherWidth + (float)(NextDeltaTime * NextVelocity);
                        return;
                    }
                    else
                    {
                        RightCatcherPosition = NextPosition + HalfCatcherWidth + (float)NextDeltaTime;
                        return;
                    }
                }
                else
                {
                    if (IsHyper)
                    {
                        double modifiedVelocity = Math.Abs((NextPosition - (prev.RightCatcherPosition + DeltaTime)) / (NextDeltaTime - 1000.0 / 60.0));

                        RightCatcherPosition = NextPosition + HalfCatcherWidth + (float)(modifiedVelocity * NextDeltaTime);
                        return;
                    }
                    else
                    {
                        RightCatcherPosition = NextPosition + HalfCatcherWidth + (float)NextDeltaTime;
                        return;
                    }
                }
            }
            else if (Position < prev.Position && NextPosition > Position)
            {
                if (prev.IsHyper)
                {
                    if (IsHyper)
                    {
                        LeftCatcherPosition = NextPosition - HalfCatcherWidth - (float)(NextDeltaTime * NextVelocity);
                        return;
                    }
                    else
                    {
                        LeftCatcherPosition = NextPosition - HalfCatcherWidth - (float)NextDeltaTime;
                        return;
                    }
                }
                else
                {
                    if (IsHyper)
                    {
                        double modifiedVelocity = Math.Abs((NextPosition - (prev.LeftCatcherPosition - DeltaTime)) / (NextDeltaTime - 1000.0 / 60.0));

                        LeftCatcherPosition = NextPosition - HalfCatcherWidth - (float)(modifiedVelocity * NextDeltaTime);
                        return;
                    }
                    else
                    {
                        LeftCatcherPosition = NextPosition - HalfCatcherWidth - (float)NextDeltaTime;
                        return;
                    }
                }
            }
        }
    }
}
