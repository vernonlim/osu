// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Rulesets.Catch.Difficulty.Preprocessing.Data
{
    /// <summary>
    /// A type of pattern corresponding to a certain case or situation in gameplay.
    /// </summary>
    public enum PatternType
    {
        FirstNote,

        // Stacks
        PotentialStack,
        PotentialStackAfterJumpAfterHyperjump,
        PotentialStackBeginning,
        NarrowStack,
        StackContinuation,
        StackEnd,

        // Direction Changes
        JumpAfterHyperjump,
        Hyperjumps,
        HyperjumpAfterJump,
        Jumps,

        // Streams
        HyperStream,
        PotentialStandstill,
        ExtendedDirectionChange,
        AcceleratingStream,
        FreeStream,

        // Special Cases
        HyperWalk,

        LastNote,
        None,

        // Intentional variant of None
        Ignored,
    }
}
