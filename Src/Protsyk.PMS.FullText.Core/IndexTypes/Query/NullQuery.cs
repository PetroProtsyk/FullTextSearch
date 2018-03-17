using System;

namespace Protsyk.PMS.FullText.Core
{
    /// <summary>
    /// Query that returns no results
    /// </summary>
    public class NullQuery : ISearchQuery
    {
        #region Fields
        public static readonly ISearchQuery Instance = new NullQuery();
        #endregion

        #region Methods
        private NullQuery() { }
        #endregion

        #region ISearchQuery

        public IMatch NextMatch() => null;

        public void Dispose() { }

        #endregion
    }
}
