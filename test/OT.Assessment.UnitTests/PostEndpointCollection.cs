namespace OT.Assessment.UnitTests
{
    /// <summary>
    /// Test classes that call the POST endpoint share the metric counters (they are global),
    /// so they are put in one collection and run one after another instead of in parallel.
    /// </summary>
    [CollectionDefinition(Name)]
    public class PostEndpointCollection
    {
        public const string Name = "POST endpoint (shared metric counters)";
    }
}
