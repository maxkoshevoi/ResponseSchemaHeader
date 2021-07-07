using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Dynamic;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class ApplicationBuilderExtensions
	{
        public static IServiceCollection AddResponseSchemaHeader(this IServiceCollection services) =>
			services.AddSingleton<IActionResultExecutor<ObjectResult>, ResponseEnvelopeResultExecutor>();

        internal class ResponseEnvelopeResultExecutor : ObjectResultExecutor
		{
			public ResponseEnvelopeResultExecutor(OutputFormatterSelector formatterSelector, IHttpResponseStreamWriterFactory writerFactory, ILoggerFactory loggerFactory, IOptions<MvcOptions> mvcOptions)
				: base(formatterSelector, writerFactory, loggerFactory, mvcOptions)
			{
			}

			public override Task ExecuteAsync(ActionContext context, ObjectResult result)
			{
				string? responseSchema = context.HttpContext.Request.Headers["ResponseSchema"].FirstOrDefault();
                TypeCode typeCode = Type.GetTypeCode(result.Value.GetType());
				if (typeCode == TypeCode.Object && responseSchema != null)
				{
					object newResponse = RemoveNonSchemaProperties(ToDynamic(result.Value), JArray.Parse(responseSchema));
					result.Value = newResponse;
                }

                return base.ExecuteAsync(context, result);
			}

			private static IDictionary<string, object?> RemoveNonSchemaProperties(IDictionary<string, object?> fullModel, JArray schema)
			{
				List<string> neededProperties = schema.Select(p => ((JValue)p).Value!.ToString()!.ToLower()).ToList();

				if (fullModel is JArray array && array.Any())
				{
					var firstItem = array.First() as JObject;

					List<string> allProperties = firstItem!.Properties().Select(p => p.Name.ToLower()).ToList();
					List<string> propertiesToRemove = allProperties.Except(neededProperties).ToList();

					foreach (JObject item in array)
					{
						propertiesToRemove.ForEach(p => item.Remove(p));
					}
				}
				else if (fullModel is JObject item)
				{
					List<string> allProperties = item.Properties().Select(p => p.Name.ToLower()).ToList();
					List<string> propertiesToRemove = allProperties.Except(neededProperties).ToList();

					propertiesToRemove.ForEach(p => item.Remove(p));
					fullModel = (dynamic)item;
				}

				return fullModel;
			}

			public static IDictionary<string, object?> ToDynamic(object value)
			{
				IDictionary<string, object?> expando = new ExpandoObject();
				var properties = TypeDescriptor.GetProperties(value.GetType());
				foreach (PropertyDescriptor prop in properties)
				{
					expando.Add(prop.Name, prop.GetValue(value));
				}
				return expando;
			}
		}
	}
}
