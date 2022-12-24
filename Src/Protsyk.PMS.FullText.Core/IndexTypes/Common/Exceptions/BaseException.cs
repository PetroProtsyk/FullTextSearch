using System;

namespace Protsyk.PMS.FullText.Core;

public class BaseException : Exception
{
    public BaseException()
    {
    }

    public BaseException(string message)
        : base(message)
    {
    }
}
