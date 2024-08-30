
namespace EntityFramework.FieldEncryption.Encryption
{
    public sealed class KeyManagementSettings
    {
        public int TimeToLiveMinutes { get; set; }
        public TimeSpan TimetoLive => TimeSpan.FromMinutes(TimeToLiveMinutes);
        public int FailureThreshold { get; set; }
        public int DurationOfCircuitBreakMinutes { get; set; }
        public TimeSpan DurationOfCircuitBreak => TimeSpan.FromMinutes(DurationOfCircuitBreakMinutes);
    }
}
