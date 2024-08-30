using EntityFramework.FieldEncryption.Encryption;
using EntityFramework.FieldEncryption.Entities.Abstract;

namespace EntityFramework.FieldEncryption.Entities
{
    public sealed class EncryptedFinancialEntity : Entity
    {
        private EncryptedFinancialEntity(string name,
            string taxIdentificationNumber,
            DateTime incorportationDate,
            EncryptedData<Dictionary<string, decimal>> amounts)
        {
            Name = name;
            TaxIdentificationNumber = taxIdentificationNumber;
            IncorportationDate = incorportationDate;
            Amounts = amounts;
        }

        public static EncryptedFinancialEntity Create(string name, string taxIdentificationNumber, DateTime incorportationDate, CompressionOptions options)
        {
            var amounts = new EncryptedData<Dictionary<string, decimal>>(new Dictionary<string, decimal>(), options);
            return new EncryptedFinancialEntity(name, taxIdentificationNumber, incorportationDate, amounts);
        }

        public string Name { get; private set; }
        public string TaxIdentificationNumber { get; private set; }
        public DateTime IncorportationDate { get; private set; }
        public EncryptedData<Dictionary<string, decimal>> Amounts { get; private set; }

        public void SetAmount(string key, decimal value)
        {
            Amounts.Data[key] = value;
        }
    }
}
