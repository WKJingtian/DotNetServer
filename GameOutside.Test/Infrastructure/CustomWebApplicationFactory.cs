using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using StackExchange.Redis;

namespace GameOutside.Test.Infrastructure;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Environment.SetEnvironmentVariable("DB_HOST", "10.10.0.56");
        Environment.SetEnvironmentVariable("DB_USER", "building-game");
        Environment.SetEnvironmentVariable("DB_PORT", "32277");
        Environment.SetEnvironmentVariable("DB_PASSWORD", "");
        Environment.SetEnvironmentVariable("DB_DATABASE", "building-game");

        var baseDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "GameOutside", "DBKeys"));
        Environment.SetEnvironmentVariable("CLIENT_CERT_KEY_PATH", Path.Combine(baseDir, "client.building-game.key"));
        Environment.SetEnvironmentVariable("CLIENT_CERT_PATH", Path.Combine(baseDir, "client.building-game.crt"));
        Environment.SetEnvironmentVariable("CA_CERT_PATH", Path.Combine(baseDir, "ca.crt"));

        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Test");
        Environment.SetEnvironmentVariable("REGION", "cn-sz");

        builder.ConfigureAppConfiguration((context, config) =>
        {
            // 清除现有配置
            config.Sources.Clear();

            // 添加测试配置
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Redis"] = "10.10.0.56:32545,abortConnect=false",
                ["ConnectionStrings:GlobalCache"] = "10.10.0.56:32545,abortConnect=false",
                ["ConnectionStrings:Player"] = "https://192.168.2.16",
                ["ConnectionStrings:Im"] = "https://192.168.2.17",
                ["ConnectionStrings:Mail"] = "https://192.168.2.16",
                ["DB_DATABASE"] = "building-game",
                ["MockContext:PlayerShard"] = "1051",
                ["PlatformKafkaConfig:SendMailsV2Topic"] = "building-game.mail.send-mails-v2",
                ["PlatformKafkaConfig:MailAcceptAttachmentsV2Topic"] = "building-game.mail.mail-accept-attachment-v2",
                ["PlatformKafkaConfig:MailAcceptAttachmentsV2DlqTopic"] = "building-game.mail.mail-accept-attachment-v2-dlq",
                ["PlatformKafkaConfig:Brokers"] = "10.10.0.56:31115"
            });
        });

        builder.ConfigureServices(services =>
        {
            // 替换 ServerConfigService 为测试版本
            services.RemoveAll<ServerConfigService>();
            services.AddSingleton<ServerConfigService, TestableServerConfigService>();

            // 为 GlobalCache 添加单独的连接
            services.AddKeyedSingleton<IConnectionMultiplexer>("GlobalCache", (provider, key) =>
                provider.GetRequiredService<IConnectionMultiplexer>());

            // 移除托管服务以防止启动时的副作用
            services.RemoveAll<IHostedService>();

            // 设置测试环境
            builder.UseEnvironment("Test");
        });
    }
}
