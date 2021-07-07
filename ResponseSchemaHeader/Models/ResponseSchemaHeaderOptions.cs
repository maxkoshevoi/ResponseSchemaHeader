namespace Microsoft.AspNetCore.Builder
{
    public class ResponseSchemaHeaderOptions
    {
        public string HeaderName { get; set; } = "ResponseSchema";

        public bool CaseSensitive { get; } = true;

        public UnknownPropertyHandling UnknownPropertyHandling { get; } = UnknownPropertyHandling.Ignore;
    }

    public enum UnknownPropertyHandling
    {
        Ignore,
        Error
    }
}