using EntityFramework.FieldEncryption.Entities.Abstract;

namespace EntityFramework.FieldEncryption.Entities
{
    public sealed class Statistics : Entity
    {
        public Statistics(string entityType, string operation, int count, double durationSeconds)
        {
            EntityType = entityType;
            Operation = operation;
            Count = count;
            DurationSeconds = durationSeconds;
            CompletedDate = DateTime.Now;
        }

        public string EntityType { get; private set; }
        public string Operation { get; private set; }
        public int Count { get; private set; }
        public double DurationSeconds { get; private set; }
        public DateTime CompletedDate { get; private set; }
    }
}
