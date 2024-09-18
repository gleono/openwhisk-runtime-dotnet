using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Apache.OpenWhisk.Runtime.Common
{
    /// <summary>
    /// Initialization message as described in:
    /// https://github.com/apache/openwhisk/blob/master/docs/actions-new.md#initialization
    /// </summary>
    public struct InitMessage 
    {
        public InitValue Value {get; init; }

        [JsonIgnore]
        public static JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
    }

    public struct InitValue 
    {
        /// <summary>
        /// Name of the action.
        /// </summary>
        public string? Name { get; init; }

        /// <summary>
        /// Name of the funcion to execute.
        /// </summary>
        public string? Main { get; init; }

        /// <summary>
        /// Base64 encoded string for binary functions.
        /// </summary>
        public string? Code { get; init; }

        /// <summary>
        /// false if code <see cref="Code"/> is in plain text,
        /// true if it is base64 encoded.
        /// </summary>
        public bool Binary { get; init;}

        /// <summary>
        /// Map of key-value pairs of properties to export to the
        /// environment.
        /// </summary>
        public IReadOnlyDictionary<string, string>? Env { get; init; }
    }

    
}