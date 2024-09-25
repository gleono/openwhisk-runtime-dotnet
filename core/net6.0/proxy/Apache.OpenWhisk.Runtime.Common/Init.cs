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
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json.Linq;

namespace Apache.OpenWhisk.Runtime.Common
{
    public class Init
    {
        private readonly SemaphoreSlim _initSemaphoreSlim = new SemaphoreSlim(1, 1);

        public bool Initialized { get; private set; }
        private Type? Type { get; set; }
        private MethodInfo? Method { get; set; }
        private ConstructorInfo? Constructor { get; set; }
        private bool AwaitableMethod { get; set; }

        public Init()
        {
            Initialized = false;
            Type = null;
            Method = null;
            Constructor = null;
        }

        public async Task<Run?> HandleRequest(HttpContext httpContext)
        {
            await _initSemaphoreSlim.WaitAsync();
            try
            {
                if (Initialized)
                {
                    await httpContext.Response.WriteError("Cannot initialize the action more than once.");
                    Console.Error.WriteLine("Cannot initialize the action more than once.");
                    return new Run(Type, Method, Constructor, AwaitableMethod);
                }

                string body = await new StreamReader(httpContext.Request.Body).ReadToEndAsync();
                JObject inputObject = JObject.Parse(body);
                JToken? message;
                if (!inputObject.TryGetValue("value", out message) || message is not JObject)
                {
                    await httpContext.Response.WriteError("Missing main/no code to execute.");
                    return null;
                }

                JObject valueObj = (JObject)message;
                string main = valueObj.TryGetValue("main", out var mainToken) ? mainToken.ToString() : string.Empty;
                string code = valueObj.TryGetValue("code", out var codeToken) ? codeToken.ToString() : string.Empty;
                bool binary = valueObj.TryGetValue("binary", out var binaryToken) && binaryToken.ToObject<bool>();

                if (string.IsNullOrWhiteSpace(main) || string.IsNullOrWhiteSpace(code))
                {
                    await httpContext.Response.WriteError("Missing main/no code to execute.");
                    return null;
                }

                if (!binary)
                {
                    await httpContext.Response.WriteError("code must be binary (zip file).");
                    return null;
                }

                string[] mainParts = main.Split("::");
                if (mainParts.Length != 3)
                {
                    await httpContext.Response.WriteError("main required format is \"Assembly::Type::Function\".");
                    return null;
                }

                string tempPath = Path.Combine(Environment.CurrentDirectory, Guid.NewGuid().ToString());
                try
                {
                    using (MemoryStream stream = new MemoryStream(Convert.FromBase64String(code)))
                    {
                        using (ZipArchive archive = new ZipArchive(stream))
                        {
                            archive.ExtractToDirectory(tempPath);
                        }
                    }
                }
                catch (Exception)
                {
                    await httpContext.Response.WriteError("Unable to decompress package.");
                    return null;
                }

                Environment.CurrentDirectory = tempPath;

                string assemblyFile = $"{mainParts[0]}.dll";

                string assemblyPath = Path.Combine(tempPath, assemblyFile);

                if (!File.Exists(assemblyPath))
                {
                    await httpContext.Response.WriteError($"Unable to locate requested assembly (\"{assemblyFile}\").");
                    return null;
                }

                try
                {
                    JToken? envToken = valueObj["env"];
                    // Export init arguments as environment variables
                    if (envToken?.HasValues ?? false)
                    {
                        var dictEnv = envToken.ToObject<Dictionary<string, string?>>();
                        if (dictEnv != null) {
                            foreach (var (key, value) in dictEnv) {
                                // See https://docs.microsoft.com/en-us/dotnet/api/system.environment.setenvironmentvariable
                                // If entry.Value is null or the empty string, the variable is not set
                                Environment.SetEnvironmentVariable(key, value);
                            }
                        }
                    }

                    Assembly assembly = Assembly.LoadFrom(assemblyPath);
                    Type = assembly.GetType(mainParts[1]);
                    if (Type == null)
                    {
                        await httpContext.Response.WriteError($"Unable to locate requested type (\"{mainParts[1]}\").");
                        return null;
                    }
                    Method = Type.GetMethod(mainParts[2]);
                    Constructor = Type.GetConstructor(Type.EmptyTypes);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(ex.ToString());
                    await httpContext.Response.WriteError(ex.Message
#if DEBUG
                                                          + ", " + ex.StackTrace
#endif
                    );
                    return null;
                }

                if (Method == null)
                {
                    await httpContext.Response.WriteError($"Unable to locate requested method (\"{mainParts[2]}\").");
                    return null;
                }

                if (Constructor == null)
                {
                    await httpContext.Response.WriteError($"Unable to locate appropriate constructor for (\"{mainParts[1]}\").");
                    return null;
                }

                Initialized = true;

                AwaitableMethod = Method.ReturnType.GetMethod(nameof(Task.GetAwaiter)) != null;

                return new Run(Type, Method, Constructor, AwaitableMethod);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.StackTrace);
                await httpContext.Response.WriteError(ex.Message
#if DEBUG
                                                  + ", " + ex.StackTrace
#endif
                );
                Startup.WriteLogMarkers();
                return null;
            }
            finally
            {
                _initSemaphoreSlim.Release();
            }
        }
    }
}
