using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ResponseSchemaHeader.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ResponseSchemaHeader
{
    internal class RequestCultureMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ResponseSchemaHeaderOptions _options;
        private readonly StringComparer _stringComparer;

        public RequestCultureMiddleware(RequestDelegate next, ResponseSchemaHeaderOptions options)
        {
            _next = next;
            _options = options;
            
			_stringComparer = options.CaseSensitive ? StringComparer.InvariantCulture : StringComparer.InvariantCultureIgnoreCase;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            string? responseSchema = context.Request.Headers[_options.HeaderName].FirstOrDefault();
            if (responseSchema == null)
            {
                await _next(context);
                return;
            }

            JArray schema = ValidateSchema(responseSchema);

            await context.Response.ModifyBodyAsync(() => _next(context), oldResponse => RemoveNonSchemaProperties(JToken.Parse(oldResponse), schema));
        }

		private static JArray ValidateSchema(string schema)
		{
			JToken schemaToken;
			try
			{
				schemaToken = JToken.Parse(schema, new JsonLoadSettings
				{
					DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error
				});
			}
			catch (JsonReaderException ex)
			{
				throw new ResponseSchemaHeaderException($"Response schema contains invalid JSON: {ex.Message}", ex);
			}

			if (schemaToken is not JArray)
			{
				throw new ResponseSchemaHeaderException("Response schema needs to be an array");
			}

			JArray schemaArray = (JArray)schemaToken;
			ValidateSchema(schemaArray);

			return schemaArray;

			static void ValidateSchema(JArray schema)
			{
				foreach (var item in schema)
				{
					if (item is JObject nestedSchema)
					{
						var properties = nestedSchema.Properties().ToList();
						if (properties.Count == 1 && properties[0].Value is JArray array)
						{
							ValidateSchema(array);
						}
						else
						{
							throw new ResponseSchemaHeaderException($"Object values in response schema can contain only one property with type of array. Unexpected token: \"{item}\"");
						}
					}
					else if (item.Type != JTokenType.String)
					{
						throw new ResponseSchemaHeaderException($"Response schema can contain only string and object values. Unexpected token: \"{item}\"");
					}
				}
			}
		}

		private string RemoveNonSchemaProperties(JToken fullModel, JArray schema)
		{
			if (fullModel is JArray array && array.Any())
			{
				foreach (JObject item in array)
				{
					ProcessItem(item, schema);
				}
			}
			else if (fullModel is JObject item)
			{
				ProcessItem(item, schema);
				fullModel = item;
			}

			return fullModel.ToString();

			void ProcessItem(JObject item, JArray schema)
			{
				List<string> neededProperties = schema
					.Select(p => p switch
					{
						JValue value => value.ToString(),
						JObject obj => obj.Properties().First().Name,
						_ => throw new NotImplementedException()
					})
					.ToList();

				List<string> duplicatingProperties = neededProperties.GroupBy(p => p, _stringComparer).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
				if (duplicatingProperties.Any())
				{
					throw new ResponseSchemaHeaderException($"Response schema contains duplicating properties: {string.Join(", ", duplicatingProperties)}");
				}

				IEnumerable<string> allProperties = item.Properties().Select(p => p.Name);
				List<string> propertiesToRemove = allProperties.Except(neededProperties, _stringComparer).ToList();

				propertiesToRemove.ForEach(p => item.Remove(p));

				foreach (var nestedSchema in schema.OfType<JObject>().Select(s => s.Properties().First()))
				{
					ProcessItem((JObject)item[nestedSchema.Name]!, (JArray)nestedSchema.Value);
				}
			}
		}
	}
}
