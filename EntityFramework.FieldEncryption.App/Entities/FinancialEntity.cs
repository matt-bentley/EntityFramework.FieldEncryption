using EntityFramework.FieldEncryption.Entities.Abstract;

namespace EntityFramework.FieldEncryption.Entities
{
    public sealed class FinancialEntity : Entity
    {
        private FinancialEntity(string name, 
            string taxIdentificationNumber, 
            DateTime incorportationDate, 
            Dictionary<string, decimal> amounts)
        {
            Name = name;
            TaxIdentificationNumber = taxIdentificationNumber;
            IncorportationDate = incorportationDate;
            Amounts = amounts;
        }

        public static FinancialEntity Create(string name, string taxIdentificationNumber, DateTime incorportationDate)
        {
            return new FinancialEntity(name, taxIdentificationNumber, incorportationDate, new Dictionary<string, decimal>());
        }

        public string Name { get; private set; }
        public string TaxIdentificationNumber { get; private set; }
        public DateTime IncorportationDate { get; private set; }
        public Dictionary<string, decimal> Amounts { get; private set; }

        public void SetAmount(string key, decimal value)
        {
            Amounts[key] = value;
        }
    }
}
