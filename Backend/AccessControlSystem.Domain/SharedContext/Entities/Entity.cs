using System;
using System.Collections.Generic;
using System.Text;

namespace AccessControlSystem.Domain.SharedContext.Entities {
    public abstract class Entity : IEquatable<Entity> {

        public Guid Id { get; } = Guid.NewGuid();
        public bool Equals(Entity? other) {

            if (other == null) { 
                return false;
            }
            return Id == other.Id;
        }

        public override int GetHashCode() {
            return Id.GetHashCode();
        }
    }
}
