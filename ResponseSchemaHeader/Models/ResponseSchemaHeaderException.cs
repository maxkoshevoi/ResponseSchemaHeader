using System;

namespace Microsoft.AspNetCore.Builder
{
    internal class ResponseSchemaHeaderException : Exception
    {
        public ResponseSchemaHeaderException(string message)
            : base(message)
        {
        }

        public ResponseSchemaHeaderException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}