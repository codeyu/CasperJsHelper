using System;

namespace CasperJsHelper
{
    public class CasperJsException : Exception
    {
        public int ErrorCode
        {
            get;
            private set;
        }

        public CasperJsException(int errCode, string message)
            : base(string.Format("CasperJs exit code {0}: {1}", errCode, message))
        {
            ErrorCode = errCode;
        }
    }
}
