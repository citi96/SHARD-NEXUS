using System.Text.Json;

namespace Shared.Network.Messages
{
    public class NetworkMessage
    {
        public MessageType Type { get; set; }
        public string PayloadJson { get; set; } = string.Empty;

        public static NetworkMessage Create<T>(MessageType type, T payload)
        {
            return new NetworkMessage
            {
                Type = type,
                PayloadJson = JsonSerializer.Serialize(payload)
            };
        }

        public T? DeserializePayload<T>()
        {
            if (string.IsNullOrEmpty(PayloadJson))
                return default;
                
            return JsonSerializer.Deserialize<T>(PayloadJson);
        }
        
        public string ToJson()
        {
            return JsonSerializer.Serialize(this);
        }
        
        public static NetworkMessage? FromJson(string json)
        {
            return JsonSerializer.Deserialize<NetworkMessage>(json);
        }
    }
}
