﻿using EntityFramework.FieldEncryption.DomainEvents.Abstract;

namespace EntityFramework.FieldEncryption.DomainEvents
{
    public sealed class ProductUpdatedEvent : DomainEvent
    {
        public Guid ProductId { get; set; }
        public string Name { get; set; }
        public decimal Price { get; set; }
    }
}