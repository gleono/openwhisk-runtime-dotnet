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

using System.Text.Json.Nodes;

namespace Apache.OpenWhisk.Tests.Dotnet
{
    public class Environment
    {
        public JsonObject Main(JsonObject args)
        {
            var message = new JsonObject();
            message["api_host"] = JsonValue.Create(System.Environment.GetEnvironmentVariable("__OW_API_HOST"));
            message["api_key"] = JsonValue.Create(System.Environment.GetEnvironmentVariable("__OW_API_KEY"));
            message["namespace"] = JsonValue.Create(System.Environment.GetEnvironmentVariable("__OW_NAMESPACE"));
            message["action_name"] = JsonValue.Create(System.Environment.GetEnvironmentVariable("__OW_ACTION_NAME"));
            message["action_version"] = JsonValue.Create(System.Environment.GetEnvironmentVariable("__OW_ACTION_VERSION"));
            message["activation_id"] = JsonValue.Create(System.Environment.GetEnvironmentVariable("__OW_ACTIVATION_ID"));
            message["deadline"] = JsonValue.Create(System.Environment.GetEnvironmentVariable("__OW_DEADLINE"));
            return (message);
        }
    }
}
