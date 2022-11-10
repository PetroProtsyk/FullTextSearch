namespace Protsyk.PMS.FullText.Core
{
    /// <summary>
    /// Name and the instance of the in memory index
    /// </summary>
    public class InMemoryIndexName : IIndexName
    {
        internal InMemoryIndex Index { get; } = new InMemoryIndex();
    }
}
