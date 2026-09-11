using AccessControlSystem.Application.Context.AccountContext.UseCases.Get;
using AccessControlSystem.Application.Context.AccountContext.UseCases.Get.Contracts;
using AccessControlSystem.Application.Context.AccountContext.UseCases.Get.Enums;
using AccessControlSystem.Application.SharedContext.UseCases;
using AccessControlSystem.Domain.Contexts.AccountContext.Enums;

namespace AccessControlSystem.Tests.Application.AccountContext.Get;

public class GetHandlerTests
{
    private readonly IRepository _repository = Substitute.For<IRepository>();
    private readonly Handler _handler;

    public GetHandlerTests()
    {
        _handler = new Handler(_repository);
    }

    [Fact]
    public async Task Requisicao_invalida_devolve_400_sem_consultar_o_banco()
    {
        var response = await _handler.HandleAsync(new Request(Size: 0, Page: 1));

        Assert.Equal(400, response.Status);
        await _repository.DidNotReceive()
            .GetOperatorList(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<EActiveFilter>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Devolve_200_com_a_pagina_e_os_metadados()
    {
        var items = new List<OperatorItem>
        {
            new(Guid.NewGuid(), "a@test.com", ERole.Admin, true),
            new(Guid.NewGuid(), "b@test.com", ERole.Operator, true),
        };
        _repository.GetOperatorList(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<EActiveFilter>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResponse<OperatorItem>(items, 2, 2, 5));

        var response = await _handler.HandleAsync(new Request(Size: 2, Page: 2));

        Assert.Equal(200, response.Status);
        Assert.Equal(2, response.Data!.Operators.Count);
        Assert.Equal(2, response.Data.Page);
        Assert.Equal(2, response.Data.Size);
        Assert.Equal(5, response.Data.Total);
        Assert.Equal(3, response.Data.TotalPages);   // ceil(5 / 2)
    }

    [Fact]
    public async Task Repassa_page_e_size_da_requisicao_pro_repositorio()
    {
        _repository.GetOperatorList(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<EActiveFilter>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResponse<OperatorItem>(new List<OperatorItem>(), 3, 15, 0));

        await _handler.HandleAsync(new Request(Size: 15, Page: 3));

        await _repository.Received(1)
            .GetOperatorList(Arg.Is(3), Arg.Is(15), Arg.Any<EActiveFilter>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Repassa_o_filtro_isActive_pro_repositorio()
    {
        _repository.GetOperatorList(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<EActiveFilter>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResponse<OperatorItem>(new List<OperatorItem>(), 1, 10, 0));

        await _handler.HandleAsync(new Request(IsActive: EActiveFilter.Inactive));

        await _repository.Received(1).GetOperatorList(
            Arg.Any<int>(), Arg.Any<int>(), Arg.Is(EActiveFilter.Inactive), Arg.Any<CancellationToken>());
    }
}
