using AccessControlSystem.Domain.Contexts.UserContext.ValueObjects;
using AccessControlSystem.Domain.SharedContext.Entities;
using AccessControlSystem.Domain.SharedContext.Exceptions;

namespace AccessControlSystem.Domain.Contexts.UserContext.Entities {
    public class User : Entity {

        protected User() {

        }

        public User(Name name, Telephone telephone, PasswordAccess password, Rg rg, Cpf cpf, string observation = "", DateTime? dateStart = null, DateTime? dateEnd = null) {

            if (!string.IsNullOrEmpty(observation) && observation.Length > 100) {
                throw new DomainException("Campo observação deve ter no máximo 100 caracteres");
            }
            if (dateEnd.HasValue && dateStart.HasValue && dateEnd < dateStart) {
                throw new DomainException("Data final não pode ser anterior à data inicial");
            }

            Name = name;
            Observation = observation;
            Telephone = telephone;
            Password = password;
            Rg = rg;
            Cpf = cpf;
            DateStart = dateStart;
            DateEnd = dateEnd;
        }

        public Name Name { get; private set; } = null!;
        public string Observation { get; private set; } = string.Empty;
        public Telephone Telephone { get; private set; } = null!;
        public DateTime? DateStart { get; private set; }
        public DateTime? DateEnd { get; private set; }
        public PasswordAccess Password { get; private set; } = null!;
        public Rg Rg { get; private set; } = null!;
        public Cpf Cpf { get; private set; } = null!;
        public bool IsActive { get; private set; } = true;

        public void UpdateName(Name name) {
            Name = name;
        }
        public void UpdateObservation(string observation) {
            if (!string.IsNullOrEmpty(observation) && observation.Length > 100) {
                throw new DomainException("Campo observação deve ter no máximo 100 caracteres");
            }
            Observation = observation;
        }

        public void UpdateTelephone(Telephone telephone) {
            Telephone = telephone;
        }

        public void UpdatePassword(PasswordAccess password) {
            Password = password;
        }

        public void UpdateDateStart(DateTime? dateStart) {
            if (dateStart.HasValue && DateEnd.HasValue && dateStart > DateEnd) {
                throw new DomainException("Data inicial não pode ser posterior à data final");
            }
            DateStart = dateStart;
        }

        public void UpdateDateEnd(DateTime? dateEnd) {
            if (dateEnd.HasValue && DateStart.HasValue && dateEnd < DateStart) {
                throw new DomainException("Data final não pode ser anterior à data inicial");
            }
            DateEnd = dateEnd;
        }

        public void UpdateRg(Rg rg) {
            Rg = rg;
        }

        public void UpdateCpf(Cpf cpf) {
            Cpf = cpf;
        }

        public void Active() {
            IsActive = true;
        }
        public void Deactivate() {
            IsActive = false;

        }
    }
}
