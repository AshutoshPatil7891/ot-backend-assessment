using OT.Assessment.App.Services;
using OT.Assessment.Common.Settings;

namespace OT.Assessment.UnitTests
{
    /// <summary>
    /// Tests how the publisher picks which of its RabbitMQ channels sends the next bet. Spreading the bets
    /// evenly over several channels is what lets the API publish many bets at the same time.
    /// </summary>
    public class PublishSlotTests
    {
        [Fact]
        public void WithOneChannel_AlwaysPicksTheFirstChannel()
        {
            var counter = 0;

            for (var i = 0; i < 50; i++)
            {
                Assert.Equal(0, PublishSlot.Next(ref counter, 1));
            }
        }

        [Fact]
        public void WithManyChannels_EveryChannelIsUsedTheSameNumberOfTimes()
        {
            var counter = 0;
            var uses = new int[16];

            for (var i = 0; i < 1600; i++)
            {
                uses[PublishSlot.Next(ref counter, 16)]++;
            }

            Assert.All(uses, count => Assert.Equal(100, count));
        }

        [Fact]
        public void AlwaysReturnsASlotInsideTheRange()
        {
            var counter = 0;

            for (var i = 0; i < 1000; i++)
            {
                var slot = PublishSlot.Next(ref counter, 7);
                Assert.InRange(slot, 0, 6);
            }
        }

        [Fact]
        public void WhenTheCounterWrapsAround_StillReturnsASlotInsideTheRange()
        {
            var counter = int.MaxValue - 5;

            for (var i = 0; i < 20; i++)
            {
                var slot = PublishSlot.Next(ref counter, 16);
                Assert.InRange(slot, 0, 15);
            }
        }

        [Fact]
        public void TheApiPublishesOverSixteenChannelsUnlessConfiguredOtherwise()
        {
            Assert.Equal(16, new RabbitMqSettings().PublishChannels);
        }
    }
}
