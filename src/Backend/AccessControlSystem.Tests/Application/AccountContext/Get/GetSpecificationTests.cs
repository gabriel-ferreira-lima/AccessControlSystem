using AccessControlSystem.Application.Context.AccountContext.UseCases.Get;
using AccessControlSystem.Application.Context.AccountContext.UseCases.Get.Enums;

namespace AccessControlSystem.Tests.Application.AccountContext.Get;

public class GetSpecificationTests
{
    [Fact]
    public void Page_e_size_dentro_do_limite_passa()
    {
        Assert.True(Specification.Ensure(new Request(Size: 10, Page: 1)).IsValid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Page_menor_que_1_falha(int page)
    {
        Assert.False(Specification.Ensure(new Request(Size: 10, Page: page)).IsValid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Size_menor_que_1_falha(int size)
    {
        Assert.False(Specification.Ensure(new Request(Size: size, Page: 1)).IsValid);
    }

    [Fact]
    public void Size_acima_de_100_falha()
    {
        Assert.False(Specification.Ensure(new Request(Size: 101, Page: 1)).IsValid);
    }

    [Fact]
    public void Size_no_limite_de_100_passa()
    {
        Assert.True(Specification.Ensure(new Request(Size: 100, Page: 1)).IsValid);
    }

    [Theory]
    [InlineData(EActiveFilter.Inactive)]
    [InlineData(EActiveFilter.Active)]
    [InlineData(EActiveFilter.All)]
    public void IsActive_com_valor_definido_passa(EActiveFilter filtro)
    {
        Assert.True(Specification.Ensure(new Request(IsActive: filtro)).IsValid);
    }

    [Fact]
    public void IsActive_fora_do_enum_falha()
    {
        Assert.False(Specification.Ensure(new Request(IsActive: (EActiveFilter)99)).IsValid);
    }
}
