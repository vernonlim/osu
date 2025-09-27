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
        private readonly PalpableCatchHitObject nextObject;

        private readonly CatchDifficultyHitObject? prevObject;

        public bool IsHyper => ((PalpableCatchHitObject)BaseObject).HyperDash;

        public float Position => ((PalpableCatchHitObject)BaseObject).EffectiveX;

        public float NextPosition => nextObject.EffectiveX;

        public float DeltaPosition => Math.Abs(Position - ((PalpableCatchHitObject)LastObject).EffectiveX);

        public float NextDeltaPosition => Math.Abs(nextObject.EffectiveX - Position);

        public double NextDeltaTime => nextObject.StartTime - BaseObject.StartTime;

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
            this.nextObject = (PalpableCatchHitObject)nextObject;
            CatcherWidth = catcherWidth;
            LeftNoteBorder = Position - (HalfCatcherWidth);
            RightNoteBorder = Position + (HalfCatcherWidth);

            initializeVariables();

            DifficultyHitObject prevHitObject = Previous(0);

            if (prevHitObject is CatchDifficultyHitObject difficultyHitObject)
            {
                prevObject = difficultyHitObject;
            }
            else
            {
                prevObject = null;

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
            LeftCatcherPosition = LeftNoteBorder;
            RightCatcherPosition = RightNoteBorder;
            LeftStandingPosition = null;
            RightStandingPosition = null;
            ActionProbability = 1;
        }

        private void enumerateCases()
        {
            // prevObject should never be null due to skipping this method for the 'first' object.
            Debug.Assert(prevObject != null, nameof(prevObject) + " != null");
        }
    }
}
