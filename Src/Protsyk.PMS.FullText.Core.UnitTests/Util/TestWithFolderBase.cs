using System;
using System.IO;

namespace Protsyk.PMS.FullText.Core.UnitTests
{
    public abstract class TestWithFolderBase : IDisposable
    {
        protected string TestFolder;

        public TestWithFolderBase()
        {
            TestFolder = Path.Combine(Path.GetTempPath(), "PMS_FullText_Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(TestFolder);
        }

        public void Dispose()
        {
            Directory.Delete(TestFolder, true);
        }
    }
}
