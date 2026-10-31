using System;
using System.Collections.Generic;
using System.Text;

namespace AccessControlSystem.Domain.SharedContext.Exceptions {
    public class DomainException : Exception {
        public DomainException(string message) : base(message) {
        }
    }
}
