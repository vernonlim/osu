// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using Newtonsoft.Json;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Difficulty;

namespace osu.Game.Rulesets.Catch.Difficulty
{
    public class CatchDifficultyAttributes : DifficultyAttributes
    {
        /// <summary>
        /// The number of actions the player is expected to perform while playing the beatmap.
        /// </summary>
        [JsonProperty("total_actions")]
        public double TotalActions { get; set; }

        [JsonProperty("ar_factor")]
        public double ApproachRateFactor { get; set; }

        [JsonProperty("hidden_factor")]
        public double HiddenFactor { get; set; }

        // /// <summary>
        // /// Temporary debug property.
        // /// </summary>
        // [JsonProperty("precision_sr")]
        // public double PrecisionSR { get; set; }
        //
        // /// <summary>
        // /// Temporary debug property.
        // /// </summary>
        // [JsonProperty("speed_sr")]
        // public double SpeedSR { get; set; }

        [JsonProperty("sr_beginning_nerfed")]
        public double SRBeginningNerfed { get; set; }

        public override IEnumerable<(int attributeId, object value)> ToDatabaseAttributes()
        {
            foreach (var v in base.ToDatabaseAttributes())
                yield return v;

            // Todo: osu!catch should not output star rating in the 'aim' attribute.
            yield return (ATTRIB_ID_AIM, StarRating);
            yield return (ATTRIB_ID_TOTAL_ACTIONS, TotalActions);
        }

        public override void FromDatabaseAttributes(IReadOnlyDictionary<int, double> values, IBeatmapOnlineInfo onlineInfo)
        {
            base.FromDatabaseAttributes(values, onlineInfo);

            StarRating = values[ATTRIB_ID_AIM];
            TotalActions = values[ATTRIB_ID_TOTAL_ACTIONS];
        }
    }
}
