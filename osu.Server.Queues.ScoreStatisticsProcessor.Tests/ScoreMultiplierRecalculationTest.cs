// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Threading.Tasks;
using osu.Server.Queues.ScoreStatisticsProcessor.Commands.Maintenance;
using osu.Server.Queues.ScoreStatisticsProcessor.Models;
using Xunit;

namespace osu.Server.Queues.ScoreStatisticsProcessor.Tests
{
    public class ScoreMultiplierRecalculationTest : DatabaseTest
    {
        private readonly Beatmap beatmap;

        public ScoreMultiplierRecalculationTest()
        {
            beatmap = AddBeatmap();
        }

        [Fact]
        public async Task TestSkeleton()
        {
            using var conn = Processor.GetDatabaseConnection();

            InsertScore(conn, new ScoreItem(
                new SoloScore
                {
                    // ...
                },
                new ProcessHistory()));

            var populateCommand = new PopulateTotalScoreWithoutModsCommand();
            await populateCommand.OnExecuteAsync(CancellationToken);

            // ...

            var recalculateCommand = new RecalculateModMultipliersCommand();
            await recalculateCommand.OnExecuteAsync(CancellationToken);

            // ...
        }
    }
}
