// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Dapper.Contrib.Extensions;
using Newtonsoft.Json;
using osu.Game.Beatmaps;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Rulesets;
using osu.Game.Scoring;
using osu.Server.Queues.ScoreStatisticsProcessor.Helpers;

namespace osu.Server.Queues.ScoreStatisticsProcessor.Models
{
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    [Serializable]
    [Table("scores")]
    public class SoloScore
    {
        [ExplicitKey]
        public ulong id { get; set; }

        public uint user_id { get; set; }

        public uint beatmap_id { get; set; }

        public ushort ruleset_id { get; set; }

        public bool has_replay { get; set; }
        public bool preserve { get; set; }
        public bool ranked { get; set; } = true;

        public ScoreRank rank { get; set; }

        public bool passed { get; set; } = true;

        public float accuracy { get; set; }

        public uint max_combo { get; set; }

        public uint total_score { get; set; }

        public SoloScoreData ScoreData = new SoloScoreData();

        public string data
        {
            get => JsonConvert.SerializeObject(ScoreData);
            set
            {
                var soloScoreData = JsonConvert.DeserializeObject<SoloScoreData>(value);
                if (soloScoreData != null)
                    ScoreData = soloScoreData;
            }
        }

        public double? pp { get; set; }

        public ulong? legacy_score_id { get; set; }
        public uint legacy_total_score { get; set; }

        public DateTimeOffset? started_at { get; set; }
        public DateTimeOffset ended_at { get; set; }

        public override string ToString() => $"score_id: {id} user_id: {user_id}";

        public ushort? build_id { get; set; }

        /// <summary>
        /// The beatmap that this score was set on.
        /// </summary>
        /// <remarks>
        /// Importantly, this is the <b>raw database row</b> corresponding to <see cref="beatmap_id"/>.
        /// This means that all properties pertaining to difficulty or ruleset in this model <b>will use the original beatmap data, excluding potential effects
        /// of ruleset conversion and active mods.</b>
        /// </remarks>
        [Computed]
        public Beatmap? beatmap { get; set; }

        [Computed]
        public bool is_legacy_score => legacy_score_id != null;

        public ScoreInfo ToScoreInfo()
        {
            var ruleset = LegacyRulesetHelper.GetRulesetFromLegacyId(ruleset_id);

            return new ScoreInfo
            {
                OnlineID = (long)id,
                LegacyOnlineID = (long?)legacy_score_id ?? -1,
                IsLegacyScore = is_legacy_score,
                User = new APIUser { Id = (int)user_id },
                BeatmapInfo = new BeatmapInfo
                {
                    OnlineID = (int)beatmap_id,
                    Ruleset = new RulesetInfo { OnlineID = beatmap!.playmode }
                },
                Ruleset = new RulesetInfo { OnlineID = ruleset_id },
                Passed = passed,
                TotalScore = total_score,
                // `TotalScoreWithoutMods` was added to the json after the `scores` table went live.
                // while we've gone through the effort of backpopulating old rows so that the value in theory should be present on all rows,
                // keep this nullable for the very first run of the process so that this properly dies
                // in case there is an oversight and there is in fact a row with this missing, rather than silently substituting in zero.
                TotalScoreWithoutMods = ScoreData.TotalScoreWithoutMods ?? throw new InvalidOperationException($"Score with ID {id} has missing `total_score_without_mods`."),
                LegacyTotalScore = legacy_total_score,
                Accuracy = accuracy,
                MaxCombo = (int)max_combo,
                Rank = rank,
                Statistics = ScoreData.Statistics,
                MaximumStatistics = ScoreData.MaximumStatistics,
                Date = ended_at,
                HasOnlineReplay = has_replay,
                Mods = ScoreData.Mods.Select(m => m.ToMod(ruleset)).ToArray(),
                PP = pp,
                Ranked = ranked,
            };
        }
    }
}
