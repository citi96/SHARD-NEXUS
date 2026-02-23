namespace Shared.Network.Messages
{
    public class RoundResultMessage
    {
        public int WinnerId { get; set; }
        public string Reason { get; set; } = string.Empty;
    }
}
