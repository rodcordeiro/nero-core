using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Nero.Knowledge.Base.Mcp.Hosting;

namespace Nero.Knowledge.Base.Tests;

public class McpHostSmokeTests
{
    [Fact]
    public void BuildHost_RegistersHostedServices()
    {
        using var host = McpHost.Build();

        var hostedServices = host.Services.GetServices<IHostedService>().ToList();

        Assert.NotEmpty(hostedServices);
        Assert.Contains(hostedServices, service =>
            service.GetType().Name.Contains("McpServer", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Configure_DoesNotThrow()
    {
        var builder = Host.CreateApplicationBuilder();

        var exception = Record.Exception(() => McpHost.Configure(builder));

        Assert.Null(exception);

        using var host = builder.Build();
        Assert.NotNull(host.Services);
    }
}
