// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Rulesets.Osu.Difficulty
{
    public record OsuDifficultyConstants
    {
        public static OsuDifficultyConstants Default { get; } = new OsuDifficultyConstants();

        public double AimPerformanceScale { get; init; } = 1.0;
        public double SpeedPerformanceScale { get; init; } = 1.0;
        public double AccuracyPerformanceScale { get; init; } = 1.0;
        public double FlashlightPerformanceScale { get; init; } = 1.0;
        public double ReadingPerformanceScale { get; init; } = 1.0;
        public double TotalPerformanceScale { get; init; } = 1.0;

        public double AimSkillStrainScale { get; init; } = 1.0;
        public double SpeedSkillStrainScale { get; init; } = 1.0;
        public double FlashlightSkillStrainScale { get; init; } = 1.0;
        public double ReadingSkillStrainScale { get; init; } = 1.0;

        public double AimWideAngleBonusScale { get; init; } = 1.5;
        public double AimAcuteAngleScale { get; init; } = 2.3;
        public double AimSliderBonusScale { get; init; } = 1.5;
        public double AimVelocityChangeBonusScale { get; init; } = 0.75;
        public double AimWiggleBonusScale { get; init; } = 1.02;
        public double AimHighBpmBonusBase { get; init; } = 0.15;

        public double FlashlightMaxOpacityBonusScale { get; init; } = 0.4;
        public double FlashlightHiddenBonusScale { get; init; } = 0.2;
        public double FlashlightMinVelocityScale { get; init; } = 0.5;
        public double FlashlightSliderBonusScale { get; init; } = 1.3;
        public double FlashlightMinAngleScale { get; init; } = 0.2;

        public int RhythmHistoryTimeMax { get; init; } = 5 * 1000; // 5 seconds
        public int RhythmHistoryObjectsMax { get; init; } = 32;
        public double RhythmOverallScale { get; init; } = 1.0;
        public double RhythmRatioScale { get; init; } = 30.0;

        public double SpeedSingleSpacingThreshold { get; init; } = 125;
        public double SpeedMinBonusBpm { get; init; } = 200;
        public double SpeedBalancingFactor { get; init; } = 40;
        public double SpeedDistanceScale { get; init; } = 0.8;
        public double SpeedHighBpmBonusBase { get; init; } = 0.3;

        public double ReadingWindowSize { get; init; } = 3000;
        public double ReadingDistanceInfluenceThreshold { get; init; } = 150;
        public double ReadingHiddenMultiplier { get; init; } = 0.28;
        public double ReadingDensityMultiplier { get; init; } = 2.4;
        public double ReadingDensityDifficultyBase { get; init; } = 2.5;
        public double ReadingPreemptBalancingFactor { get; init; } = 140000;
        public double ReadingPreemptStartingPoint { get; init; } = 500;
        public double ReadingMinimumAngleRelevancyTime { get; init; } = 2000;
        public double ReadingMaximumAngleRelevancyTime { get; init; } = 200;
        public double ReadingReducedDifficultyBaseLine { get; init; } = 0.0;
        public double ReadingReducedDifficultyDuration { get; init; } = 60 * 1000;

        public double CognitionPerformanceExponent { get; init; } = 2.0;

    }
}
