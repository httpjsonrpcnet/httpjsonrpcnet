using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace HttpJsonRpc
{
    internal static class OpenRpcDocumentGenerator
    {
        // Methods have already passed the inclusion filters. All generated state belongs to this call.
        internal static OpenRpcDocument Generate(JsonRpcOptions options, JsonRpcContext requestContext, JsonRpcMethod[] methods)
        {
            var methodTransformers = options.OpenRpc.MethodTransformers.ToArray();
            var documentTransformers = options.OpenRpc.DocumentTransformers.ToArray();
            var generator = new OpenRpcSchemaGenerator(options);
            var descriptions = new List<OpenRpcMethod>();
            foreach (var method in methods)
            {
                var parameters = new Dictionary<JsonRpcParameter, OpenRpcContentDescriptor>();
                foreach (var parameter in method.Parameters)
                {
                    parameters.Add(parameter, new OpenRpcContentDescriptor
                    {
                        Name = parameter.Name,
                        Description = parameter.Description,
                        Required = !parameter.Optional,
                        Schema = generator.GetUsageSchema(parameter.ClrType, OpenRpcSchemaUsageKind.Parameter,
                            parameter.PropertyInfo?.DeclaringType ?? method.MethodInfo.DeclaringType,
                            parameter.Name, parameter.PropertyInfo, parameter.ParameterInfo, method)
                    });
                }
                var resultSchema = generator.GetUsageSchema(method.MethodInfo.ReturnType, OpenRpcSchemaUsageKind.Result,
                    method.MethodInfo.DeclaringType, "result", method.MethodInfo, method.MethodInfo.ReturnParameter, method);
                var result = resultSchema == null ? null : new OpenRpcContentDescriptor { Name = "result", Schema = resultSchema };
                var description = new OpenRpcMethod
                {
                    Name = method.FullName, Version = method.ParentClass.Version, Description = method.Description,
                    Params = method.Parameters.Select(p => parameters[p]).Cast<object>().ToArray(), Result = result
                };
                var context = new OpenRpcMethodTransformerContext(requestContext, method, description,
                    new ReadOnlyDictionary<JsonRpcParameter, OpenRpcContentDescriptor>(parameters), result);
                foreach (var transformer in methodTransformers) transformer(context);
                descriptions.Add(description);
            }
            var info = options.OpenRpc.Info;
            var document = new OpenRpcDocument
            {
                Info = new OpenRpcInfo
                {
                    Title = info.Title, Version = info.Version, Description = info.Description, TermsOfService = info.TermsOfService,
                    Contact = info.Contact == null ? null : new OpenRpcContact { Name = info.Contact.Name, Email = info.Contact.Email, Url = info.Contact.Url },
                    License = info.License == null ? null : new OpenRpcLicense { Name = info.License.Name, Url = info.License.Url }
                },
                Methods = descriptions.ToArray(),
                Components = new OpenRpcComponents { Schemas = generator.Schemas }
            };
            var documentContext = new OpenRpcDocumentTransformerContext(requestContext, Array.AsReadOnly(methods.ToArray()), document);
            foreach (var transformer in documentTransformers) transformer(documentContext);
            return document;
        }
    }
}
