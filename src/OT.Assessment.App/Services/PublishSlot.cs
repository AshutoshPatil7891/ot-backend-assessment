namespace OT.Assessment.App.Services
{
    /// <summary>
    /// Picks which of the publisher's channels sends the next bet: one after the other, so every channel
    /// gets the same share of the bets. The counter is converted to an unsigned number first, so the
    /// result stays inside the range even after the counter wraps around.
    /// </summary>
    public static class PublishSlot
    {
        public static int Next(ref int counter, int size)
        {
            return (int)((uint)Interlocked.Increment(ref counter) % (uint)size);
        }
    }
}
