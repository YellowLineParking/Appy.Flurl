using System.Text.Json;
using Flurl.Http.Configuration;

namespace Flurl.Serialization.TextJson
{
    public static class HttpSettingsSerializerExtensions
    {
        public static ClientFlurlHttpSettings WithTextJsonSerializer(this ClientFlurlHttpSettings httpSettings, JsonSerializerOptions? options = null)
        {
            httpSettings.JsonSerializer = new TextJsonSerializer(options);
            
            return httpSettings;
        }
    }
}