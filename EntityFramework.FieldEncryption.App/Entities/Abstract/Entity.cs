
using Microsoft.EntityFrameworkCore.ValueGeneration;

namespace EntityFramework.FieldEncryption.Entities.Abstract
{
    public abstract class Entity
    {
        private static readonly SequentialGuidValueGenerator _idFactory = new SequentialGuidValueGenerator();

        protected Entity() : this(NewId())
        {

        }

        protected Entity(Guid id)
        {
            Id = id;
        }

        public static Guid NewId() => _idFactory.Next(null);

        public Guid Id { get; private set; }
    }
}
