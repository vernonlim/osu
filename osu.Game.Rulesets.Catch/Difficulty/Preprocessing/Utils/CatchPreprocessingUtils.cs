// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Catch.Difficulty.Preprocessing.Data;
using osu.Game.Rulesets.Difficulty.Utils;

namespace osu.Game.Rulesets.Catch.Difficulty.Preprocessing.Utils
{
    public static class CatchPreprocessingUtils
    {
        public static double MillisecondsToCatcherStandingWidth(double ms) => ms <= 188 ? 2.2 * 1e-5 * Math.Pow(ms, 2) - 8.3 * 1e-3 * ms + 1.35 : 0.567;

        /// <summary>
        /// Calculates the value of the CDF for the catcher position at the given note for the value x.
        /// </summary>
        /// <param name="x"></param>
        /// <param name="note"></param>
        /// <returns></returns>
        public static double NormalCdfForNote(double x, CatchDifficultyHitObject note) =>
            Cdf(x, (note.MovementData.LeftCatcherPosition + note.MovementData.RightCatcherPosition) / 2.0,
                Math.Abs(note.MovementData.ForwardCatcherPosition - note.MovementData.BackwardCatcherPosition) / 6.0);

        /// <summary>
        /// Returns the value of the CDF with given mean and standard deviation at value x.
        /// </summary>
        /// <param name="x"></param>
        /// <param name="mean"></param>
        /// <param name="std"></param>
        /// <returns></returns>
        public static double Cdf(double x, double mean, double std) => 0.5 * (1 + DifficultyCalculationUtils.Erf((x - mean) / (Math.Sqrt(2) * std)));

        /// <summary>
        /// Gets the catcher position of the last note closest to the current one.
        /// </summary>
        /// <param name="note"></param>
        /// <param name="prev"></param>
        /// <returns></returns>
        public static double GetPrevForwardCatcherPosition(CatchDifficultyHitObject note, CatchDifficultyHitObject prev) =>
            note.IsMovingRight ? prev.MovementData.RightCatcherPosition : prev.MovementData.LeftCatcherPosition;

        public static double CalculatePrevToNextDistance(CatchDifficultyHitObject note, CatchDifficultyHitObject prev, CatchDifficultyHitObject next) =>
            Math.Abs(note.MovementData.FurthestBackward(prev.MovementData.ForwardCatcherPosition + note.MovementData.Directionize(note.DeltaTime), note.ForwardNoteBorder) - next.Position);

        /// <summary>
        /// Calculates the minimal distance a catcher could travel between two notes.
        /// </summary>
        /// <param name="note"></param>
        /// <param name="prev"></param>
        /// <returns></returns>
        public static double CalculateMinimalDistance(CatchDifficultyHitObject note, CatchDifficultyHitObject prev) =>
            Math.Abs(note.Position - note.MovementData.FurthestBackward(GetPrevForwardCatcherPosition(note, prev), prev.Position + note.MovementData.Directionize(note.HalfCatcherWidth)));

        /// <summary>
        /// Calculates the simple speed between a note and the one before it.
        /// </summary>
        /// <param name="note">The current note.</param>
        /// <returns></returns>
        public static double CalculateSpeed(CatchDifficultyHitObject note) => note.DeltaPosition / note.DeltaTime;

        /// <summary>
        /// Calculates the hyperdash speed between a note and the one before it, assuming that the catcher is perfectly positioned.
        /// </summary>
        /// <param name="note">The current note.</param>
        /// <returns></returns>
        public static double CalculatePerfectHyperdashSpeed(CatchDifficultyHitObject note) => note.DeltaPosition / (Math.Max(note.DeltaTime - 1000.0 / 60.0, 1));

        /// <summary>
        /// Calculates the hyperdash speed between a note and the one before it, based on the expected player position.
        /// </summary>
        /// <param name="note">The current note.</param>
        /// <param name="prev">The previous note.</param>
        /// <param name="next">The next note.</param>
        /// <returns></returns>
        public static double CalculateMinimalHyperdashSpeed(CatchDifficultyHitObject note, CatchDifficultyHitObject prev, CatchDifficultyHitObject next) =>
            CalculateMinimalDistance(note, prev) / Math.Max(note.DeltaTime - 1000.0 / 60.0, 1);

        /// <summary>
        /// Calculates the average hyperdash speed between two notes.
        /// </summary>
        /// <param name="note"></param>
        /// <param name="prev"></param>
        /// <returns></returns>
        public static double CalculateAverageHyperdashSpeed(CatchDifficultyHitObject note, CatchDifficultyHitObject prev)
        {
            _ = note.MovementData;
            CatchMovementData prevData = prev.MovementData;

            double left = Math.Max(prevData.LeftCatcherPosition, prev.LeftNoteBorder);
            double right = Math.Min(prevData.RightCatcherPosition, prev.RightNoteBorder);
            double average = (left + right) / 2.0;

            double distance = Math.Abs(note.Position - average);

            return distance / Math.Max(note.DeltaTime - 1000.0 / 60.0, 1);
        }
    }
}
