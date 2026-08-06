using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Nero.Knowledge.Base.Mcp.Application.Services.Operations;
using Nero.Knowledge.Base.Mcp.Hosting;

if (KnowledgeCliCommandRunner.IsCommand(args))
{
    using var host = McpHost.Build([]);
    var runner = host.Services.GetRequiredService<KnowledgeCliCommandRunner>();
    return await runner.ExecuteAsync(args, Console.Out);
}

var builder = Host.CreateApplicationBuilder(args);
McpHost.Configure(builder);
await builder.Build().RunAsync();

return 0;
