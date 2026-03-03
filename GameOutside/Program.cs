using System.Net;
using ChillyRoom.Functions.DBModel;
using ChillyRoom.Infra.ApiController;
using ChillyRoom.BuildingGame.Models;
using ChillyRoom.Games.BuildingGame.Services;
using GameOutside;
using GameOutside.DBContext;
using GameOutside.Repositories;
using GameOutside.Services;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;
using Unchase.Swashbuckle.AspNetCore.Extensions.Extensions;
using StackExchange.Redis;
using ChillyRoom.Infra.Analytics.Integration;
using System.Text.Encodings.Web;
using System.Threading.RateLimiting;
using AssistActivity.Models;
using ChillyRoom.Infra.Extensions;
using ChillyRoom.Infra.Logging;
using GenericPlayerService.Client;
using ChillyRoom.Infra.LiveMessage;
using GameOutside.Services.PlatformItemsService;
using GameOutside.Util;
using MailClient.Client;
using ImService.Client;
using Microsoft.AspNetCore.RateLimiting;
using ChillyRoom.Infra.OpenTelemetry;
using OpenTelemetry.Metrics;
using ChillyRoom.NotifyHub.Client;
using Microsoft.AspNetCore.HttpOverrides;
using GenericPlayerManagementService.Client;
using GameOutside.Services.KafkaConsumers;
using GameOutside.Models.Configs;
using GameOutside.Services.KafkaProducers;
using ChillyRoom.Infra.CensorService;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using AssistActivity.Services;
using GameOutside.Facade;
using ChillyRoom.BuildingGame.v1;
using Grpc.Net.Client.Configuration;
using ChillyRoom.Infra.PlatformDef.Config;
using ChillyRoom.Infra.PlatformDef.DBModel;

Log.Logger = new LoggerConfiguration().MinimumLevel.Override("Microsoft", LogEventLevel.Warning).MinimumLevel
    .Override("System", LogEventLevel.Warning).Enrich.FromLogContext().Enrich.WithClientIp().WriteTo
    .Console(new RenderedCompactJsonFormatter()).CreateBootstrapLogger();

// ServerConfigTool.ReloadFromFile();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseCommonSerilog();
    builder.Host.UseNacosConfig("Nacos", logAction: log => log.AddSerilog(Log.Logger));

    builder.Services.AddHttpContextAccessor();
    builder.Services.AddInfraOpenTelemetry().WithMetrics(b => b.AddRuntimeInstrumentation());
    builder.Services.AddHealthChecks();
    builder.Services.AddCommonHttpLogging();
    if (!builder.Environment.IsDevelopment() && !Consts.IsConsumer)
    {
        builder.Services.AddDelayedShutdown();
    }

    builder.Services.AddGatewayAuthorization(false);
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor;
        options.KnownProxies.Clear();
        options.KnownNetworks.Clear();
        options.ForwardLimit = 3;
    });

    var db = Environment.GetEnvironmentVariable("DB_DATABASE") ?? "building-game";
    builder.Services.AddDbContextPool<BuildingGameDB>(options =>
    {
        options.UseCommonCockroachConfig(db, applicationName: "GameOutside",
                minPoolSize: 64,
                maxPoolSize: 64,
                connetionStringModifier: options =>
                {
                    options.ServerCompatibilityMode = Npgsql.ServerCompatibilityMode.NoTypeLoading;
                    options.MaxAutoPrepare = 128;
                    // options.AutoPrepareMinUsages = 5;
                    options.ConnectionLifetime = 600 * 3;
                    options.ConnectionIdleLifetime = 600 * 3;
                    options.CommandTimeout = 10;
                    options.Timeout = 5;
                },
                Debug: builder.Environment.IsDevelopment())
            .EnableThreadSafetyChecks(false);
    });
    builder.Services.AddPlatformKafkaConfig(builder.Configuration);
    builder.Services.AddPlatformRepositories<BuildingGameDB>(new AddPlatformAllOptions { IncludePaidOrder = true, PaidOrderWithShard = true });

    builder.Services.AddRateLimiter(ro =>
    {
        ro.AddConcurrencyLimiter("global-request-limit", options =>
        {
            options.PermitLimit = 80;
            options.QueueLimit = 25;
            options.QueueProcessingOrder = QueueProcessingOrder.NewestFirst;
        });
        ro.RejectionStatusCode = (int)HttpStatusCode.BadRequest;
        ro.OnRejected = async (context, rateLimit) =>
        {
            Log.Logger.Information("Concurrency limit hit!");
            context.HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.HttpContext.Response.WriteAsync(PoolExhaustedResponse.Singleton);
        };
    });

    // GenericPlayerAPI.GenericPlayerAPIClient playerClient
    // // 通过uid获得用户下的pid
    // var reply = playerClient.GetMyPlayers(new GetMyPlayersRequest()
    // {
    //     Uid = args.ToUidList[i]
    // });

    builder.Services.AddPlayerClient(builder.Configuration);
    builder.Services.AddGenericPlayerManagementClient(builder.Configuration);
    builder.Services.AddPlayerBanner();
    builder.Services.AddHostedService<PlayerPunishmentConsumer>();
    builder.Services.AddCensorClient(builder.Configuration);

    // private readonly MessagingAPI.MessagingAPIClient _imClient;
    // rpc SendMessageOnBehalfOfUser (SendMessageRequest) returns (SendMessageReply);
    // rpc SetOnlineStatus (SetOnlineStatusRequest) returns (SetOnlineStatusReply);
    // rpc SetMasterStatus (SetMasterStatusRequest) returns (SetMasterStatusReply);
    // rpc QueryUserRelationships (QueryUserRelationshipsRequest) returns (QueryUserRelationshipsReply);
    builder.Services.AddImClient(builder.Configuration);

    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v2", new()
        {
            Title = "GameOutside",
            Version = "v1",
            Description = "GameOutside service",
        });
        c.DocumentFilter<CustomModelDocumentFilter<ErrorKind>>();
        c.SchemaFilter<DictionaryNumericKeySchemaFilter>();
        c.AddEnumsWithValuesFixFilters();
    });

    builder.Services.AddAnalyticService(builder.Configuration.GetSection("Analytics"));
    builder.Services.AddLiveMessageHandler(builder.Configuration.GetSection("LiveMessage"));
    builder.Services.AddMailClient(builder.Configuration);

    builder.Services.AddMemoryCache();

    if (builder.Environment.IsDevelopment())
    {
        builder.Services.AddDistributedMemoryCache();
    }
    else
    {
        builder.Services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = builder.Configuration.GetConnectionString("Redis");
            options.InstanceName = "building-game:";
        });
    }

    builder.Services.AddKeyedSingleton<IConnectionMultiplexer>("GlobalCache",
        ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("GlobalCache")!)
    );

    builder.Services.AddSingleton<IConnectionMultiplexer>(
        ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("Redis")!));
    builder.Services.AddSingleton<IRedisScriptService, RedisScriptService>();
    builder.Services.AddSingleton<ServerConfigService>();
    builder.Services.AddSingleton<CacheManager>();
    builder.Services.AddSingleton<MessageService>();

    builder.Services.AddScoped<IUserRankRepository, UserRankRepository>();
    builder.Services.AddScoped<IUserDivisionRepository, UserDivisionRepository>();
    builder.Services.AddScoped<IUserEndlessRankRepository, UserEndlessRankRepository>();
    builder.Services.AddScoped<ISeasonInfoRepository, SeasonInfoRepository>();
    builder.Services.AddScoped<ISeasonRefreshedHistoryRepository, SeasonRefreshedHistoryRepository>();
    builder.Services.AddSingleton<IUserRankGroupRepository, UserRankGroupRepository>();
    builder.Services.AddScoped<IUserInfoRepository, UserInfoRepository>();
    builder.Services.AddScoped<IGameRepository, GameRepository>();
    builder.Services.AddScoped<IUserAssetRepository, UserAssetRepository>();
    builder.Services.AddScoped<IUserCardRepository, UserCardRepository>();
    builder.Services.AddScoped<IUserItemRepository, UserItemRepository>();
    builder.Services.AddScoped<IActivityRepository, ActivityRepository>();
    builder.Services.AddScoped<IBattlePassRepository, BattlePassRepository>();
    builder.Services.AddScoped<IIapPackageRepository, IapPackageRepository>();
    builder.Services.AddScoped<IServerDataRepository, ServerDataRepository>();
    builder.Services.AddScoped<IInvitationCodeRepository, InvitationCodeRepository>();
    builder.Services.AddScoped<IUserAchievementRepository, UserAchievementRepository>();
    builder.Services.AddScoped<IPlatformItemRepository, PlatformItemRepository>();
    builder.Services.AddScoped<IAssistActivityRepository, AssistActivityRepository>();

    builder.Services.AddScoped<UserRankService>();
    builder.Services.AddScoped<DivisionService>();
    builder.Services.AddScoped<GameService>();
    builder.Services.AddScoped<UserEndlessRankService>();
    builder.Services.AddScoped<SeasonService>();
    builder.Services.AddScoped<UserInfoService>();
    builder.Services.AddScoped<UserAssetService>();
    builder.Services.AddScoped<UserCardService>();
    builder.Services.AddScoped<UserItemService>();
    builder.Services.AddScoped<ActivityService>();
    builder.Services.AddScoped<BattlePassService>();
    builder.Services.AddScoped<ExportGameDataService>();
    builder.Services.AddScoped<IapPackageService>();
    builder.Services.AddScoped<ServerDataService>();
    builder.Services.AddScoped<InvitationCodeService>();
    builder.Services.AddScoped<UserAchievementService>();
    builder.Services.AddScoped<AssistActivityManagementService>();

    builder.Services.AddScoped<FriendModule>();
    builder.Services.AddScoped<PlayerModule>();
    builder.Services.AddScoped<LeaderboardModule>();
    builder.Services.Configure<ServerConfigService.GameDataTable>(builder.Configuration.GetSection("GameDataTable"));

    if (Consts.IsConsumer)
    {
        builder.Services.AddHostedService<AcceptAttachmentsHandler>();
        builder.Services.AddHostedService<GiftCodeEventHandler>();
        builder.Services.AddHostedService<PayEventHandler>();
        builder.Services.AddHostedService<RefreshUserDivisionConsumer>();
        builder.Services.AddHostedService<RefreshWorldRankConsumer>();
    }

    builder.Services.AddSingleton<RefreshUserDivisionProducer>();
    builder.Services.AddSingleton<RefreshWorldRankProducer>();
    builder.Services.AddSingleton<AntiCheatService>();
    builder.Services.AddScoped<DanmakuService>();

    builder.Services.AddHostedService<InitHostedService>();

    builder.Services.AddNotifyHubClient();

    builder.Services.AddControllers()
        .ConfigureApiBehaviorOptions(options => { options.SuppressModelStateInvalidFilter = true; }).AddJsonOptions(
            options => { options.JsonSerializerOptions.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping; });

    builder.Services.Configure<PlatformKafkaConfig>(builder.Configuration.GetSection("PlatformKafkaConfig"));
    builder.Services.Configure<KafkaConfig>(builder.Configuration.GetSection("KafkaConfig"));
    builder.Services.Configure<AssistActivityConfig>(builder.Configuration.GetSection("AssistConfig"));
    builder.Services.Configure<DanmakuConfig>(builder.Configuration.GetSection("DanmakuConfig"));
    builder.Services.AddScoped<PlatformItemsService>();

    builder.Services.AddRetryHttpClient();
    builder.Services.Configure<ApiDelayMiddlewareOptions>(
        builder.Configuration.GetSection("ApiDelayMiddleware"));

    // CORS 配置
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader());
    });

    if (!Consts.IsConsumer || Consts.Region == "cn-sz")
    {
        builder.Services.AddGrpc(o =>
        {
            // See https://github.com/grpc/grpc-dotnet/issues/1834
            o.IgnoreUnknownServices = true;
        });
        builder.Services.AddCommonGrpcLogging(true);
    }

    var globalGameoutsideConfig = new GlobalGameoutsideConfig();
    builder.Configuration.GetSection("GlobalGameoutside").Bind(globalGameoutsideConfig);
    foreach (var kv in globalGameoutsideConfig.RemoteGameoutside.Where(kv => kv.Key != Consts.Region))
    {
        builder.Services.AddGrpcClient<Group.GroupClient>(kv.Key, o =>
        {
            o.Address = new Uri(kv.Value);
        }).ConfigureChannel(o =>
        {
            var httpHandler = new SocketsHttpHandler();
            httpHandler.EnableMultipleHttp2Connections = true;
            httpHandler.SslOptions = new System.Net.Security.SslClientAuthenticationOptions
            {
                RemoteCertificateValidationCallback = delegate { return true; },
            };
            o.HttpHandler = httpHandler;

            o.Credentials = Grpc.Core.ChannelCredentials.SecureSsl;
            o.ServiceConfig ??= new ServiceConfig();
            o.ServiceConfig.LoadBalancingConfigs.Add(new RoundRobinConfig());
        });
    }

    if (builder.Environment.IsDevelopment())
    {
        builder.Services.Configure<HostOptions>(options =>
        {
            options.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore;
        });
    }

    var app = builder.Build();

    // 初始化 Redis 脚本
    var redisScriptService = app.Services.GetRequiredService<IRedisScriptService>();
    await redisScriptService.GetUserGroupAllocationScriptAsync();

    app.UseSwagger();
    if (app.Environment.IsDevelopment())
    {
        app.UseSwaggerUI(c => c.SwaggerEndpoint("v2/swagger.json", "GameOutside"));
        app.UseDeveloperExceptionPage();
    }

    app.UseHealthChecks("/healthz");
    app.UseForwardedHeaders();
    app.UseRateLimiter();
    app.UseRouting();
    app.UseMiddleware<ApiDelayMiddleware>();
    app.UseCors();
    app.UseMiddleware<LogPlayerMiddleware>();
    app.UseCommonHttpLogging(true);
    app.UseMiddleware<CatchPoolExhaustedExceptionMiddleware>();
    app.UseAuthentication();
    app.UseAuthorization();

    app.UseEndpoints(endpoints =>
    {
        endpoints.MapPrometheusScrapingEndpoint();
        if (!Consts.IsConsumer || Consts.Region == "cn-sz")
        {
            endpoints.MapGrpcService<LeaderboardGmService>();
            endpoints.MapGrpcService<GroupService>();
            endpoints.MapGrpcService<PlayerGameSaveManager>();
        }
        endpoints.MapControllers().RequireRateLimiting("global-request-limit");
    });

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Host terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

// 用于集成测试的 Program 类声明
public partial class Program { }

public class BuildingGameDbFactory : IDesignTimeDbContextFactory<BuildingGameDB>
{
    public BuildingGameDB CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<BuildingGameDB>();
        optionsBuilder.UseCommonCockroachConfig("building-game", applicationName: "GameOutside");

        return new BuildingGameDB(optionsBuilder.Options);
    }
}

public class GlobalGameoutsideConfig
{
    public IDictionary<string, string> RemoteGameoutside { get; set; } = new Dictionary<string, string>();
}
