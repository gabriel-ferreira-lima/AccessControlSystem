using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationParts;

namespace AccessControlSystem.Tests.Api.Support;

/// <summary>
/// Controller de PROVA: não tem nenhum atributo de acesso (nem <c>[Authorize]</c> nem <c>[AllowAnonymous]</c>).
/// Serve para provar que a Api fecha por padrão uma rota "esquecida" e, na fase F, que a verificação de
/// cobertura de rotas aponta uma rota fora do catálogo. Só existe no projeto de testes e só é carregado
/// por <see cref="ApiFactory.WithOpenProbeEndpoint"/>; a Api de produção nunca o enxerga.
/// </summary>
[ApiController]
[Route("__probe/open")]
public sealed class OpenProbeController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok();
}

/// <summary>Parte de aplicação restrita ao <see cref="OpenProbeController"/> (não carrega o resto do assembly de testes).</summary>
public sealed class OpenProbeApplicationPart : ApplicationPart, IApplicationPartTypeProvider
{
    public override string Name => nameof(OpenProbeApplicationPart);

    public IEnumerable<TypeInfo> Types => [typeof(OpenProbeController).GetTypeInfo()];
}
