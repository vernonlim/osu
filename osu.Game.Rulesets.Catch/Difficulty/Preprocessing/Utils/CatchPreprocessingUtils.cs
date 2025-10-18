// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Diagnostics;
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
        public static double Cdf(double x, double mean, double std) => 0.5 * DifficultyCalculationUtils.Erfc((mean - x) / (std * Math.Sqrt(2)));

        /// <summary>
        /// Gets the catcher position of the last note closest to the current one.
        /// </summary>
        /// <param name="note"></param>
        /// <param name="prev"></param>
        /// <returns></returns>
        public static double GetPrevForwardCatcherPosition(CatchDifficultyHitObject note, CatchDifficultyHitObject prev) =>
            note.IsMovingRight ? prev.MovementData.RightCatcherPosition : prev.MovementData.LeftCatcherPosition;

        public static double GetPrevBackwardCatcherPosition(CatchDifficultyHitObject note, CatchDifficultyHitObject prev) =>
            note.IsMovingRight ? prev.MovementData.LeftCatcherPosition : prev.MovementData.RightCatcherPosition;

        /// <summary>
        /// Similar to MaximalDistance, but taking into account the maximal position the catcher can actually reach from
        /// the previous note, assuming it isn't a hyperdash.
        /// </summary>
        /// <param name="note"></param>
        /// <param name="prev"></param>
        /// <param name="next"></param>
        /// <returns></returns>
        public static double CalculateHighestDistance(CatchDifficultyHitObject note, CatchDifficultyHitObject prev, CatchDifficultyHitObject next) =>
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
        /// Calculates the maximal distance a catcher could travel between two notes.
        /// </summary>
        /// <param name="note"></param>
        /// <param name="prev"></param>
        /// <returns></returns>
        public static double CalculateMaximalDistance(CatchDifficultyHitObject note, CatchDifficultyHitObject prev) =>
            Math.Abs(note.Position - note.MovementData.FurthestForward(GetPrevBackwardCatcherPosition(note, prev), prev.Position - note.MovementData.Directionize(note.HalfCatcherWidth)));

        /// <summary>
        /// Calculates the simple speed between a note and the one before it.
        /// </summary>
        /// <param name="note">The current note.</param>
        /// <returns></returns>
        public static double CalculateSpeed(CatchDifficultyHitObject note) => note.DeltaPosition / note.DeltaTime;

        public static double CalculateSpeedFrom(CatchDifficultyHitObject note, double position) => Math.Abs(note.Position - position) / Math.Max(note.DeltaTime - note.FrameTime, 1);

        /// <summary>
        /// Calculates the hyperdash speed between a note and the one before it, assuming that the catcher is perfectly positioned.
        /// </summary>
        /// <param name="note">The current note.</param>
        /// <returns></returns>
        public static double CalculatePerfectHyperdashSpeed(CatchDifficultyHitObject note) => note.DeltaPosition / (Math.Max(note.DeltaTime - note.FrameTime, 1));

        /// <summary>
        /// Calculates the hyperdash speed between a note and the one before it, based on the expected player position.
        /// </summary>
        /// <param name="note">The current note.</param>
        /// <param name="prev">The previous note.</param>
        /// <returns></returns>
        public static double CalculateMinimalHyperdashSpeed(CatchDifficultyHitObject note, CatchDifficultyHitObject prev) =>
            CalculateMinimalDistance(note, prev) / Math.Max(note.DeltaTime - note.FrameTime, 1);

        /// <summary>
        /// Calculates the hyperdash speed between a note and the one before it, based on the expected player position.
        /// </summary>
        /// <param name="note">The current note.</param>
        /// <param name="prev">The previous note.</param>
        /// <returns></returns>
        public static double CalculateMaximalHyperdashSpeed(CatchDifficultyHitObject note, CatchDifficultyHitObject prev) =>
            CalculateMaximalDistance(note, prev) / Math.Max(note.DeltaTime - note.FrameTime, 1);

        /// <summary>
        /// Calculates the hyperdash speed between a note and the one before it, assuming the player starts from
        /// the right catcher position of the previous.
        /// </summary>
        /// <param name="note">The current note.</param>
        /// <param name="prev">The previous note.</param>
        /// <returns></returns>
        public static double CalculateExpectedHyperdashSpeed(CatchDifficultyHitObject note, CatchDifficultyHitObject prev) =>
            Math.Abs(note.Position - GetPrevForwardCatcherPosition(note, prev)) / Math.Max(note.DeltaTime - note.FrameTime, 1);

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

            return distance / Math.Max(note.DeltaTime - note.FrameTime, 1);
        }

        /// <summary>
        /// Calculates the probability that a direction change should instead be considered a standstill for the previous note.
        /// </summary>
        /// <param name="next"></param>
        /// <param name="velocity"></param>
        /// <returns></returns>
        public static double CalculateDirectionChangeWeight(CatchDifficultyHitObject next, double velocity)
        {
            double d2 = next.DeltaPosition;

            const double power = 0.5; //may be replaced by any number < 1.0
            double normalisedVelocity = Math.Pow(velocity, 0.5);

            return Math.Clamp(
                Math.Pow(Math.Min(d2, 3.0 / 5.0 * next.CatcherWidth) / (3.0 / 5.0 * next.CatcherWidth), (power / normalisedVelocity)),
                0.0, 1.0);
        }

        public static double CalculatePotentialStandstillEffectiveTime(CatchDifficultyHitObject note, CatchDifficultyHitObject next)
        {
            CatchMovementData data = note.MovementData;

            if (note.DeltaPosition <= note.HalfCatcherWidth)
            {
                double first = (-note.DeltaPosition - note.HalfCatcherWidth
                                + (note.CatcherWidth - 2 * next.DeltaPosition) / (2 * CalculatePerfectHyperdashSpeed(next)));

                double second = note.StartTime + next.StartTime;

                return (first + second) / 2.0;
            }
            else
            {
                double first = (-note.CatcherWidth + (note.CatcherWidth - 2 * next.DeltaPosition) / (2 * CalculateSpeedFrom(next, note.BackwardNoteBorder)));

                double second = note.StartTime + next.StartTime;

                return (first + second) / 2.0;
            }
        }

        public static double CalculatePrecisionCorrection(double distance, double? standingTime, double catcherWidth)
        {
            if (standingTime == null)
                return 2.0;

            const double precisionExponent = 2.0; // p
            const double timeExponent = 2.0;      // q

            double dRatio = distance / catcherWidth;
            double tRatio = (2.0 * standingTime.Value) / catcherWidth;

            double timeExp = Math.Exp(-Math.Pow(tRatio, timeExponent));

            double distanceEffect = Math.Max(0.0, 1.0 - Math.Pow(dRatio, precisionExponent));

            // 1 + (1 - e^{-t^q}) + e^{-t^q} * distanceEffect
            double value = 1.0 + (1.0 - timeExp) + timeExp * distanceEffect;

            return Math.Clamp(value, 1.0, 2.0);
        }

        public static double? CalculateCurvedStackProbability(CatchDifficultyHitObject note, CatchDifficultyHitObject prev, CatchDifficultyHitObject next, PatternType type)
        {
            CatchMovementData data = note.MovementData;
            CatchMovementData prevData = prev.MovementData;

            switch (type)
            {
                case PatternType.JumpAfterHyperjump:
                {
                    if (prevData.IsHyperWalk)
                    {
                        if ((data.Directionize(next.Position - note.Position) < next.DeltaTime - note.HalfCatcherWidth
                             && data.Directionize(next.Position - note.Position) > next.DeltaTime / 2.0 + note.HalfCatcherWidth)
                            || data.Directionize(next.Position - note.Position) < next.DeltaTime - note.HalfCatcherWidth)
                        {
                            return 1.0;
                        }

                        return 0.0;
                    }

                    if (data.Directionize(next.Position - note.Position) < next.DeltaTime - note.HalfCatcherWidth)
                    {
                        return 1.0;
                    }

                    return 0.0;
                }

                case PatternType.Jumps:
                {
                    double val1 = note.Position - data.Directionize(note.HalfCatcherWidth + note.DeltaTime);
                    double val2 = next.Position + data.Directionize(note.HalfCatcherWidth - note.DeltaTime - next.DeltaTime);
                    double val3 = note.Position - data.Directionize(note.HalfCatcherWidth + note.DeltaTime / 2.0);
                    double val4 = next.Position + data.Directionize(note.HalfCatcherWidth - (note.DeltaTime + next.DeltaTime) / 2.0);

                    // As these are symmetric (min and max for both) we don't need FurthestBackward/FurthestForward
                    double min1 = Math.Min(val1, val2);
                    double min2 = Math.Min(val3, val4);
                    double max1 = Math.Max(val1, val2);
                    double max2 = Math.Max(val3, val4);

                    bool distinct = Math.Max(min1, min2) < Math.Min(max1, max2);

                    if (distinct)
                    {
                        return Math.Max(1 - NormalCdfForNote(max1, prev) + NormalCdfForNote(min1, prev)
                            - NormalCdfForNote(max2, prev) + NormalCdfForNote(min2, prev), 0);
                    }

                    return Math.Max(1 - NormalCdfForNote(Math.Max(max1, max2), prev) + NormalCdfForNote(Math.Min(min1, min2), prev), 0);
                }

                case PatternType.HyperStream:
                {
                    return 0.0;
                }

                default:
                {
                    return null;
                }
            }
        }

        public static bool NoteWithinBelt(CatchDifficultyHitObject note, CatchDifficultyHitObject belt, PatternType type)
        {
            CatchDifficultyHitObject? beltPrevOrNull = belt.PreviousNote(0);
            Debug.Assert(beltPrevOrNull != null);

            CatchDifficultyHitObject beltPrev = beltPrevOrNull;

            CatchMovementData beltData = belt.MovementData;

            switch (type)
            {
                case PatternType.JumpAfterHyperjump:
                {
                    // I believe these are symmetric outside the gradient of x, i.e note.Position
                    double val1 = beltData.Directionize(note.Position - (belt.Position + note.HalfCatcherWidth)) + belt.StartTime;
                    double val2 = beltData.Directionize(note.Position - (belt.Position - note.HalfCatcherWidth)) + belt.StartTime;

                    double lower1 = Math.Min(val1, val2);
                    double higher1 = Math.Max(val1, val2);

                    bool bound1 = note.StartTime >= lower1 && note.StartTime <= higher1;

                    if (beltPrev.MovementData.IsHyperWalk)
                    {
                        double val3 = beltData.Directionize(2.0 * note.Position - 2.0 * (belt.Position + note.HalfCatcherWidth)) + belt.StartTime;
                        double val4 = beltData.Directionize(2.0 * note.Position - 2.0 * (belt.Position - note.HalfCatcherWidth)) + belt.StartTime;

                        double lower2 = Math.Min(val3, val4);
                        double higher2 = Math.Max(val3, val4);

                        bool bound2 = note.StartTime >= lower2 && note.StartTime <= higher2;

                        return bound1 || bound2;
                    }

                    return bound1;
                }

                case PatternType.Jumps:
                {
                    double prevBeltForward = GetPrevForwardCatcherPosition(belt, beltPrev);
                    double prevBeltBackward = GetPrevBackwardCatcherPosition(belt, beltPrev);

                    double val1 = beltData.Directionize(note.Position - (beltData.FurthestBackward(prevBeltForward, belt.ForwardNoteBorder) + beltData.Directionize(note.HalfCatcherWidth)))
                                  + belt.StartTime;
                    double val2 = beltData.Directionize(
                                      note.Position - (beltData.FurthestForward(prevBeltBackward + beltData.Directionize(belt.DeltaTime), belt.BackwardNoteBorder) - beltData.Directionize(note.HalfCatcherWidth)))
                                  + belt.StartTime;
                    double val3 = beltData.Directionize(2.0 * note.Position - 2.0 * (beltData.FurthestBackward(prevBeltForward, belt.ForwardNoteBorder) + beltData.Directionize(note.HalfCatcherWidth)))
                                  + belt.StartTime;
                    double val4 = beltData.Directionize(
                                      2.0 * note.Position - 2.0 * (beltData.FurthestForward(prevBeltBackward + beltData.Directionize(belt.DeltaTime), belt.BackwardNoteBorder) - beltData.Directionize(note.HalfCatcherWidth)))
                                  + belt.StartTime;

                    double lower1 = Math.Min(val1, val2);
                    double higher1 = Math.Max(val1, val2);

                    double lower2 = Math.Max(val3, val4);
                    double higher2 = Math.Min(val3, val4);

                    bool bound1 = note.StartTime >= lower1 && note.StartTime <= higher1;
                    bool bound2 = note.StartTime >= lower2 && note.StartTime <= higher2;

                    return bound1 || bound2;
                }

                default:
                {
                    return false;
                }
            }
        }
    }
}
