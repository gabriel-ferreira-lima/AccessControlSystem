using AccessControlSystem.Domain.SharedContext.Exceptions;
using AccessControlSystem.Domain.SharedContext.ValueObjects;
using System.Text.RegularExpressions;

namespace AccessControlSystem.Domain.Contexts.UserContext.ValueObjects {
    public partial class Rg : ValueObject {

        public const string RgPattern = @"^\d{1,2}\.?\d{3}\.?\d{3}[\-]?[\dXx]$";

        protected Rg() {

        }

        public Rg(string value = "", DateTime? validity = null) {

            if (!string.IsNullOrEmpty(value) && !RgRegex().IsMatch(value)) {
                throw new DomainException("RG inválido");
            }

            Value = value;
            Validity = validity;
        }

        public string Value { get; private set; } = string.Empty;
        public DateTime? Validity { get; private set; }


        [GeneratedRegex(RgPattern)]
        private static partial Regex RgRegex();
    }
}
