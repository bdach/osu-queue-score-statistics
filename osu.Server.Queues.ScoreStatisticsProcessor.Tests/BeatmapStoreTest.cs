// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Threading.Tasks;
using Dapper;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Difficulty;
using osu.Server.Queues.ScoreStatisticsProcessor.Stores;
using Xunit;

namespace osu.Server.Queues.ScoreStatisticsProcessor.Tests
{
    public class BeatmapStoreTest : DatabaseTest
    {
        [Fact]
        public async Task TestCachePurgingOnBeatmapUpdate()
        {
            // add a beatmap and difficulty attributes for it
            var beatmap = AddBeatmap();
            AddBeatmapAttributes<OsuDifficultyAttributes>(beatmap.beatmap_id, attr =>
            {
                attr.StarRating = 3.2;
            });

            // fetch the difficulty attributes via beatmap store (which has caching)
            DifficultyAttributes refetchedAttributes;
            using (var connection = Processor.GetDatabaseConnection())
                refetchedAttributes = await BeatmapStore.GetDifficultyAttributesAsync(beatmap, new OsuRuleset(), [], connection);

            Assert.Equal(3.2f, refetchedAttributes.StarRating);

            using (var connection = Processor.GetDatabaseConnection())
            {
                // simulating insertion into and updates in `bss_process_queue`
                await connection.ExecuteAsync("INSERT INTO `bss_process_queue` (`beatmapset_id`) VALUES (@BeatmapSetId)", new { BeatmapSetId = beatmap.beatmapset_id });

                // https://github.com/peppy/osu-web-10/blob/c909557094210053f169c6d5dacef97d92e82864/www/web/update_bss_queue.php#L29
                await connection.ExecuteAsync("UPDATE `bss_process_queue` SET `status` = 1 WHERE `beatmapset_id` = @BeatmapSetId", new { BeatmapSetId = beatmap.beatmapset_id });
                // https://github.com/peppy/osu-web-10/blob/c909557094210053f169c6d5dacef97d92e82864/www/web/update_bss_queue.php#L53
                await connection.ExecuteAsync("UPDATE `bss_process_queue` SET `status` = 2 WHERE `beatmapset_id` = @BeatmapSetId", new { BeatmapSetId = beatmap.beatmapset_id });

                // at this point the beatmap is queued for processing onto `BeatmapProcessor`:
                // https://github.com/peppy/osu-web-10/blob/c909557094210053f169c6d5dacef97d92e82864/www/web/update_bss_queue.php#L56-L58
            }

            // delay 15sec to make sure any theoretical `BeatmapStatusWatcher` callback can fire and invalidate the cache
            await Task.Delay(15000);

            // query the difficulty attributes again to re-populate the cache
            using (var connection = Processor.GetDatabaseConnection())
                refetchedAttributes = await BeatmapStore.GetDifficultyAttributesAsync(beatmap, new OsuRuleset(), [], connection);

            Assert.Equal(3.2f, refetchedAttributes.StarRating);

            using (var connection = Processor.GetDatabaseConnection())
            {
                // simulate the actual processing of the beatmap in `BeatmapProcessor` by changing SR
                await connection.ExecuteAsync("UPDATE `osu_beatmap_difficulty_attribs` SET `value` = 4.3 WHERE `beatmap_id` = @BeatmapId AND `attrib_id` = 11", new { BeatmapId = beatmap.beatmap_id });

                // uncommenting the line below makes the test pass
                // await connection.ExecuteAsync("INSERT INTO `bss_process_queue` (`beatmapset_id`) VALUES (@BeatmapSetId)", new { BeatmapSetId = beatmap.beatmapset_id });
            }

            // delay 15sec to make sure any theoretical `BeatmapStatusWatcher` callback can fire and invalidate the cache
            await Task.Delay(15000);

            using (var connection = Processor.GetDatabaseConnection())
                refetchedAttributes = await BeatmapStore.GetDifficultyAttributesAsync(beatmap, new OsuRuleset(), [], connection);

            Assert.Equal(4.3f, refetchedAttributes.StarRating);
        }
    }
}
