using System;

namespace Protsyk.PMS.FullText.Core;

public interface IFullTextQueryCompiler : IDisposable
{
    ISearchQuery Compile(string query);

    ITermMatcher CompilePattern(string pattern);
}
