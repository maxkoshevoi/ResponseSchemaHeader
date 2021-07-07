using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace ResponseSchemaHeader
{
	public static class ApplicationBuilderExtensions
	{
		public static void UseResponseSchemaHeader(this IApplicationBuilder app)
		{
			app.Use(async (context, next) =>
			{
				string? responseSchema = context.Request.Headers["ResponseSchema"].FirstOrDefault();
				if (responseSchema == null)
				{
					await next();
					return;
				}

				await ModifyResponseBody(context.Response, next, oldResponse => RemoveNonSchemaProperties(oldResponse, responseSchema));
			});

			static string RemoveNonSchemaProperties(string fullModel, string schema)
			{
				JToken fullModelJson = JToken.Parse(fullModel);

				JArray schemaJson = JArray.Parse(schema);
				List<string> neededProperties = schemaJson.Select(p => ((JValue)p).Value!.ToString()!.ToLower()).ToList();

				if (fullModelJson is JArray array && array.Any())
				{
					var firstItem = array.First() as JObject;

					List<string> allProperties = firstItem!.Properties().Select(p => p.Name.ToLower()).ToList();
					List<string> propertiesToRemove = allProperties.Except(neededProperties).ToList();

					foreach (JObject item in array)
					{
						propertiesToRemove.ForEach(p => item.Remove(p));
					}
				}
				else if (fullModelJson is JObject item)
				{
					List<string> allProperties = item.Properties().Select(p => p.Name.ToLower()).ToList();
					List<string> propertiesToRemove = allProperties.Except(neededProperties).ToList();

					propertiesToRemove.ForEach(p => item.Remove(p));
					fullModelJson = item;
				}

				return fullModelJson.ToString();
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
