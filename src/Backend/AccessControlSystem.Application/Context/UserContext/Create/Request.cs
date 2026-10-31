using AccessControlSystem.Application.SharedContext.UseCases.Contracts;

namespace AccessControlSystem.Application.Context.UserContext.Create {
    public record Request (
        string Name, 
        string Telephone = "", 
        string Password = "", 
        string Rg = "", 
        string Cpf = "", 
        string Observation = "",
        DateTime? CpfValidity = null,
        DateTime? RgValidity = null,
        DateTime? DateStart = null, 
        DateTime? DateEnd = null
        ): IRequest{
    }
}
