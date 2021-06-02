using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Flurl.Http;
using Flurl.Http.Configuration;

namespace Flurl.Serialization.TextJson
{
    /// <summary>
    /// ISerializer implementation that uses System.Text.Json.
    /// Default serializer used in calls to GetJsonAsync, PostJsonAsync, etc.
    /// </summary>
    public class TextJsonSerializer : ISerializer
    {
        readonly JsonSerializerOptions? _options;

        /// <summary>
        /// Initializes a new instance of the <see cref="TextJsonSerializer"/> class.
        /// </summary>
        /// <param name="options">The options.</param>
        public TextJsonSerializer(JsonSerializerOptions? options = null)
        {
            _options = options;
        }

        /// <summary>
        /// Serializes the specified object.
        /// </summary>
        /// <param name="obj">The object.</param>
        /// <returns></returns>
        public string Serialize(object obj)
        {
            return JsonSerializer.Serialize(obj, _options);
        }

        /// <summary>
        /// Deserializes the specified s.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="s">The s.</param>
        /// <returns></returns>
        public T Deserialize<T>(string s)
        {
            return JsonSerializer.Deserialize<T>(s, _options)!;
        }

        /// <summary>
        /// Deserializes the specified stream.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="stream">The stream.</param>
        /// <returns></returns>
        public T Deserialize<T>(Stream stream)
        {
            using var reader = new StreamReader(stream);
            return Deserialize<T>(reader.ReadToEnd());
        }
    }
}