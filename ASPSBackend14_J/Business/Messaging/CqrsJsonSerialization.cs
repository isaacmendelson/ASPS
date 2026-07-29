using Common.Entities;
using Common.Enums;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Business.Messaging;

public static class CqrsJsonSerialization
{
    public static JsonSerializerSettings CreateSettings() => new()
    {
        TypeNameHandling = TypeNameHandling.None,
        ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
        Converters = { new DeviceAlertEntityConverter(), new UserDeviceConverter() }
    };

    private sealed class DeviceAlertEntityConverter : JsonConverter<DeviceAlertEntity>
    {
        public override bool CanWrite => false;

        public override DeviceAlertEntity? ReadJson(JsonReader reader, Type objectType,
            DeviceAlertEntity? existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return null;
            var json = JObject.Load(reader);
            var discriminator = json.Value<string>("AlertType");
            DeviceAlertEntity target = discriminator switch
            {
                "UrlAlert" => new UrlAlertEntity(),
                "TrackUrlAlert" => new TrackUrlAlertEntity(),
                "RemoteAccessAlert" => new RemoteAccessAlertEntity(),
                "TabClosedAlert" => new TabClosedAlertEntity(),
                "TabChangedAlert" => new TabChangedAlertEntity(),
                _ => throw new JsonSerializationException(
                    $"Device alert discriminator is not allowed: {discriminator}")
            };
            using var objectReader = json.CreateReader();
            serializer.Populate(objectReader, target);
            return target;
        }

        public override void WriteJson(JsonWriter writer, DeviceAlertEntity? value,
            JsonSerializer serializer) => throw new NotSupportedException();
    }

    private sealed class UserDeviceConverter : JsonConverter<UserDevice>
    {
        public override bool CanWrite => false;

        public override UserDevice? ReadJson(JsonReader reader, Type objectType,
            UserDevice? existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return null;
            var json = JObject.Load(reader);
            var rawValue = json.Value<int?>("DeviceType");
            var discriminator = rawValue.HasValue ? (DeviceType)rawValue.Value : (DeviceType?)null;
            UserDevice target = discriminator switch
            {
                DeviceType.MobilePhone => new SmartPhone(),
                _ => new PersonalComputer()
            };
            using var objectReader = json.CreateReader();
            serializer.Populate(objectReader, target);
            return target;
        }

        public override void WriteJson(JsonWriter writer, UserDevice? value,
            JsonSerializer serializer) => throw new NotSupportedException();
    }
}
