using AccessControlSystem.Domain.SharedContext.Exceptions;
using AccessControlSystem.Domain.SharedContext.ValueObjects;

namespace AccessControlSystem.Domain.Contexts.UserContext.ValueObjects {
    public class Cpf : ValueObject{

        protected Cpf() {

        }

        public Cpf(string value = "", DateTime? validity = null) {

            if (!string.IsNullOrEmpty(value) && !IsValid(value)) {
                throw new DomainException("CPF inválido");
            }

            if (!string.IsNullOrEmpty(value)) {
                value = Format(value);
 
            }

            Value = value;
            Validity = validity;
        }
        public string Value { get; private set; } = string.Empty;
        public DateTime? Validity { get; private set; }


        public static bool IsValid(string cpf) {
            if (string.IsNullOrWhiteSpace(cpf))
                return false;

            cpf = new string(cpf.Where(char.IsDigit).ToArray());

            if (cpf.Length != 11)
                return false;

            if (cpf.Distinct().Count() == 1)
                return false;

            int[] digits = cpf.Select(c => c - '0').ToArray();

            int soma1 = 0;
            for (int i = 0; i < 9; i++)
                soma1 += digits[i] * (10 - i);

            int resto1 = soma1 % 11;
            int dv1 = resto1 < 2 ? 0 : 11 - resto1;

            if (dv1 != digits[9])
                return false;

            int soma2 = 0;
            for (int i = 0; i < 10; i++)
                soma2 += digits[i] * (11 - i);

            int resto2 = soma2 % 11;
            int dv2 = resto2 < 2 ? 0 : 11 - resto2;

            return dv2 == digits[10];
        }

        public static string Format(string cpf) {
            cpf = new string(cpf.Where(char.IsDigit).ToArray());

            if (cpf.Length != 11)
                throw new DomainException("CPF deve conter 11 dígitos.");

            return Convert.ToUInt64(cpf).ToString(@"000\.000\.000\-00");
        }
    }
}
