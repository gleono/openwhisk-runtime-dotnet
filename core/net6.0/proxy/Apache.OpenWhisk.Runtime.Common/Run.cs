/*
 * Licensed to the Apache Software Foundation (ASF) under one or more
 * contributor license agreements.  See the NOTICE file distributed with
 * this work for additional information regarding copyright ownership.
 * The ASF licenses this file to You under the Apache License, Version 2.0
 * (the "License"); you may not use this file except in compliance with
 * the License.  You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Apache.OpenWhisk.Runtime.Common
{
    public class Run
    {
        private readonly Type _type;
        private readonly MethodInfo _method;
        private readonly ConstructorInfo _constructor;
        private readonly bool _awaitableMethod;

        public Run(Type type, MethodInfo method, ConstructorInfo constructor, bool awaitableMethod)
        {
            _type = type;
            _method = method;
            _constructor = constructor;
            _awaitableMethod = awaitableMethod;
        }

        public async Task HandleRequestAsync(HttpContext httpContext)
        {
            if (_type == null || _method == null || _constructor == null)
            {
                await httpContext.Response.WriteErrorAsync("Cannot invoke an uninitialized action.");
                return;
            }

            try
            { 
                using JsonDocument document = await JsonDocument.ParseAsync(httpContext.Request.Body);

                JsonElement inputObject = document.RootElement;
                JsonElement valueElement = inputObject.GetProperty("value");
                JsonNode? valueNode = valueElement.ValueKind switch {
                    JsonValueKind.Array => JsonArray.Create(valueElement),
                    JsonValueKind.Object => JsonObject.Create(valueElement),
                    _ => JsonValue.Create(valueElement)
                };
                
                await LoadEnvironmentVariablesAsync(inputObject);

                object owObject = _constructor.Invoke(Array.Empty<object>());
                
                try
                {
                    JsonNode? output = null;
                    if(_awaitableMethod)
                    {
                        var parameters = new object?[] { valueNode, httpContext.RequestAborted };
                        output = (JsonNode?) await (dynamic?) _method.Invoke(owObject, parameters);
                    }
                    else
                    {
                        var parameters = new object?[] { valueNode };
                        output = (JsonNode?) _method.Invoke(owObject, parameters);
                    }

                    if (output == null)
                    {
                        await httpContext.Response.WriteErrorAsync("The action returned null");
                        Console.Error.WriteLine("The action returned null");
                        return;
                    }

                    await httpContext.Response.WriteResponseAsync(200, output.ToString());
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(ex.StackTrace);
                    await httpContext.Response.WriteErrorAsync(ex.Message
#if DEBUG
                                                          + ", " + ex.StackTrace
#endif
                    );
                }
            }
            finally
            {
                Startup.WriteLogMarkers();
            }
        }

        private async ValueTask LoadEnvironmentVariablesAsync(JsonElement input)
        {
            foreach (var property in input.EnumerateObject())
            {
                if (!property.NameEquals("value"))
                {
                    string envKey = $"__OW_{property.Name.ToUpperInvariant()}";
                    string? envVal = property.Value.ToString();

                    try
                    {
                        Environment.SetEnvironmentVariable(envKey, envVal);
                        //Console.WriteLine($"Set environment variable \"{envKey}\" to \"{envVal}\".");
                    }
                    catch (Exception)
                    {
                        await Console.Error.WriteLineAsync(
                            $"Unable to set environment variable for the \"{property.Name}\" token.");
                    }
                }
            }
        }
    }
}
