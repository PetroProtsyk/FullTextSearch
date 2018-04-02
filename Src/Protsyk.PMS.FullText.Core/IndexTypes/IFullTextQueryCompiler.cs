using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Protsyk.PMS.FullText.Core
{
    public interface IFullTextQueryCompiler : IDisposable
    {
        ISearchQuery Compile(string query);
    }
}
