using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace Microsoft.AspNetCore.Builder
{
    public static class ApplicationBuilderExtensions
	{
		public static void UseResponseSchemaHeader(this IApplicationBuilder app) => UseResponseSchemaHeader(app, _ => { });

		public static void UseResponseSchemaHeader(this IApplicationBuilder app, Action<ResponseSchemaHeaderOptions> setupAction)
		{
			ResponseSchemaHeaderOptions options = new();
			setupAction(options);

			app.Use(async (context, next) =>
			{
				string? responseSchema = context.Request.Headers[options.HeaderName].FirstOrDefault();
				if (responseSchema == null)
				{
					await next();
					return;
				}

				JArray schemaJson = ValidateSchema(responseSchema);

				await ModifyResponseBody(context.Response, next, oldResponse => RemoveNonSchemaProperties(JToken.Parse(oldResponse), schemaJson));
			});

			static JArray ValidateSchema(string schema)
			{
				JToken schemaToken;
				try
                {
					schemaToken = JToken.Parse(schema);
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

			static string RemoveNonSchemaProperties(JToken fullModel, JArray schema)
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

                static void ProcessItem(JObject item, JArray schema)
                {
					List<string> neededProperties = schema
						.Select(p => p switch
						{
							JValue value => value.ToString(),
							JObject obj => obj.Properties().First().Name,
							_ => throw new NotImplementedException()
						})
						.ToList();

					List<string> allProperties = item.Properties().Select(p => p.Name).ToList();
                    List<string> propertiesToRemove = allProperties.Except(neededProperties).ToList();

					propertiesToRemove.ForEach(p => item.Remove(p));

                    foreach (var nestedSchema in schema.OfType<JObject>().Select(s => s.Properties().First()))
                    {
						ProcessItem((JObject)item[nestedSchema.Name]!, (JArray)nestedSchema.Value);
                    }
				}
            }

			static async Task ModifyResponseBody(HttpResponse response, Func<Task> next, Func<string, string> modifier)
			{
				// Set the response body to our stream, so we can read after the chain of middlewares have been called.
				Stream originBody = ReplaceBody(response);

				await next();

				string oldResponse;
				using (StreamReader streamReader = new(response.Body))
				{
					response.Body.Seek(0, SeekOrigin.Begin);
					oldResponse = await streamReader.ReadToEndAsync();
				}

				string newResponse = modifier(oldResponse);

				// Create a new stream with the modified body, and reset the content length to match the new stream
				response.Body = await new StringContent(newResponse).ReadAsStreamAsync();
				response.ContentLength = response.Body.Length;

				await ReturnBody(response, originBody);

				static Stream ReplaceBody(HttpResponse response)
				{
					Stream originBody = response.Body;
					response.Body = new MemoryStream();
					return originBody;
				}

				static async Task ReturnBody(HttpResponse response, Stream originBody)
				{
					response.Body.Seek(0, SeekOrigin.Begin);
					await response.Body.CopyToAsync(originBody);
					response.Body = originBody;
				}
			}
		}
    }
}
