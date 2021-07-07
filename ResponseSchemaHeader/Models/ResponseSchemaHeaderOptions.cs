namespace Microsoft.AspNetCore.Builder
{
    public class ResponseSchemaHeaderOptions
    {
        public string HeaderName { get; set; } = "ResponseSchema";

        public bool CaseSensitive { get; set; } = false;
    }
}