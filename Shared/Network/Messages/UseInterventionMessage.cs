namespace Shared.Network.Messages
{
    public class UseInterventionMessage
    {
        public string CardId { get; set; } = string.Empty;
        public int TargetId { get; set; }
    }
}
