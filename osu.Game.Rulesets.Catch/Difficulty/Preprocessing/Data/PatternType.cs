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

        // Breaks
        BreakBeginningRequiringMovement,
        BreakBeginningWithoutMovement,
        SingleNote,
        StackAfterBreak,
        HyperdashAfterBreak,
        EdgedashAfterBreak,

        // Stacks
        PotentialStack,
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
        Hyperstream,
        PotentialStandstill,
        ExtendedDirectionChange,
        AcceleratingStream,
        FreeStream,

        // Special Cases
        Hyperwalk,
        CurvedStack, // We are missing 4.5.3, that will be handled in the first/last note special case logic
        FreeStackAtPlayfieldBorder,

        LastNote,
        None,
    }
}
