using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Console;

namespace ByProxy {
    internal class Program {

        private enum StopReason {
            Restarting, StartupFailure, ShuttingDown
        }

        internal static async Task Main(string[] args) {
#if DEBUG
            // Check if invoked by EF Tools, used for building database migrations
            if (EF.IsDesignTime) {
                using (
                    var designTimeLoggerFactory = LoggerFactory.Create(builder => {
                        builder.AddConsole();
                        builder.SetMinimumLevel(LogLevel.Information);
                    })
                ) {
                    ILogger logger = designTimeLoggerFactory.CreateLogger("DesignTime");
                    logger.LogInformation("Scaffolding DbContext...");
                    var builder = Host.CreateApplicationBuilder(args);
                    builder.Services.AddSingleton<IProxyStateService, BootstrapProxyStateService>();
                    builder.Services.AddDbContext<ProxyDb>(ConfigureSQLiteWithoutConfigGuard);
                    await builder.Build().RunAsync();
                }
                return;
            }
#endif
            bool initialStart = true;
            StopReason stopReason = StopReason.Restarting;
            do {
                ProxyConfig proxyConfig;
                var builder = WebApplication.CreateBuilder(args);
                builder.Logging.ClearProviders();
                builder.Logging.AddLogFormatter();

                #region Load Startup Config
                using (
                    var bootstrapLoggerFactory = LoggerFactory.Create(builder => {
                        builder.AddConsole();
                        builder.SetMinimumLevel(LogLevel.Information);
                    })
                ) {
                    ILogger logger = bootstrapLoggerFactory.CreateLogger("Bootstrap");
                    logger.LogInformation("Building configuration...");
                    proxyConfig = await LoadConfig(logger, builder.Configuration.GetSection("Proxy"));
                    logger.LogInformation("Starting up...");
                }
                #endregion

                (stopReason, var errorMessage) = await RunApp(builder, proxyConfig, proxyConfig.AdminPort == 0);
                if (stopReason == StopReason.StartupFailure && initialStart == false) {
                    #region Revert Bad Unconfirmed Configuration
                    // Startup failed after soft restart. Revert startup config if not confirmed.
                    using (
                        var recoveryLoggerFactory = LoggerFactory.Create(builder => {
                            builder.AddConsole();
                            builder.SetMinimumLevel(LogLevel.Information);
                        })
                    ) {
                        ILogger logger = recoveryLoggerFactory.CreateLogger("Recovery");
                        logger.LogWarning("Startup interrupted due to error in configuration.");
                        var success = await RevertUnconfirmedStartupConfig(logger, errorMessage);
                        if (success) stopReason = StopReason.Restarting;
                    }
                    #endregion
                }
                initialStart = false;
            } while (stopReason == StopReason.Restarting);
        }

        private static void ConfigureSQLite(IServiceProvider sp, DbContextOptionsBuilder options) {
            ConfigureSQLiteWithoutConfigGuard(options);
            options.AddInterceptors(new ProtectConfigsInterceptor(sp.GetRequiredService<IProxyStateService>()));
        }

        private static void ConfigureSQLiteWithoutConfigGuard(DbContextOptionsBuilder options) {
            options.UseSqlite($"Data Source={Path.Combine(AppPaths.DataDir, "proxy.db")}",
                dbOptions => dbOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)
            );
        }

        private static async Task<ProxyConfig> LoadConfig(ILogger logger, IConfigurationSection envConfig) {
            var builder = new DbContextOptionsBuilder<ProxyDb>();
            ConfigureSQLiteWithoutConfigGuard(builder);
            await using var db = new ProxyDb(builder.Options, new BootstrapProxyStateService());
            await db.Database.MigrateAsync();
            var proxyConfig = await db.Configurations
                .AsNoTracking()
                .SelectLatestRunningConfig();
            if (proxyConfig == null) {
                logger.LogWarning("Running first start tasks...");
                logger.LogInformation("Generating initial CA...");
                Guid adminCertId;
                await using var transaction = await db.Database.BeginTransactionAsync();
                try {
                    var newCa = db.CertificateAuthorities.Add(new CACert {
                        Name = "CA-1"
                    });
                    await db.SaveChangesAsync();
                    using var newCaCert = Certificates.CreateCertificateAuthority("CA-1");
                    Certificates.ExportCertToDisk(newCa.Entity.Id, newCaCert);
                    using var caCert = Certificates.ImportCertFromDisk(newCa.Entity.Id);

                    logger.LogInformation("Generating initial admin cert...");
                    var newAdminCertEntry = db.IssuedCerts.Add(new IssuedCert {
                        IssuingCAId = newCa.Entity.Id,
                        Name = "localhost",
                    });
                    await db.SaveChangesAsync();
                    adminCertId = newAdminCertEntry.Entity.Id;
                    using var newAdminCert = Certificates.IssueServerCertFromCA(caCert, Certificates.LoopbackHosts);
                    Certificates.ExportCertToDisk(newAdminCertEntry.Entity.Id, newAdminCert);
                    await transaction.CommitAsync();
                } catch (Exception ex) {
                    throw new Exception($"Failed to initialize CA or admin cert: {ex.Message}", ex);
                }

                bool adminListenAny = envConfig.GetValue("Admin:ListenAny", true);
                int adminPort = envConfig.GetValue("Admin:Port", 8081);
                proxyConfig = new ProxyConfig() {
                    Revision = 1,
                    BasedOnRevision = 1,
                    AdminListenAny = adminListenAny,
                    AdminPort = adminPort,
                    AdminCertId = adminCertId,
                    UnmatchedStatus = 404
                };
                proxyConfig.Committed = true;
                proxyConfig.Confirmed = true;
                db.Configurations.Add(proxyConfig);
                await db.SaveChangesAsync();
            }
            return proxyConfig;
        }

        private static async Task<bool> RevertUnconfirmedStartupConfig(ILogger logger, string? reason) {
            try {
                var builder = new DbContextOptionsBuilder<ProxyDb>();
                ConfigureSQLiteWithoutConfigGuard(builder);
                await using var db = new ProxyDb(builder.Options, new BootstrapProxyStateService());

                logger.LogInformation("Checking for unconfirmed startup configuration...");
                var proxyConfig = await db.Configurations
                    .SelectLatestRunningConfig();

                if (proxyConfig == null) {
                    logger.LogError("Failed to load configuration.");
                    return false;
                }

                if (proxyConfig.Confirmed) {
                    logger.LogError("Startup config is confirmed, reversion canceled.");
                    return false;
                }

                proxyConfig.Reverted = true;
                proxyConfig.ReversionReason = $"Startup failed: {reason}" ?? "Startup failure";
                await db.SaveChangesAsync();
                return true;
            } catch (DbUpdateException ex) {
                logger.LogError($"Recovery failed: {ex.InnerException?.Message ?? ex.Message}");
                return false;
            } catch (Exception ex) {
                logger.LogError($"Recovery failed: {ex.Message}");
                return false;
            }
        }

        private static async Task<(StopReason, string? Error)> RunApp(WebApplicationBuilder builder, ProxyConfig config, bool disableAdminApp) {
            var runningConfig = config.AsRunningConfig();
            builder.Services.AddSingleton(runningConfig);

            builder.Services.AddSingleton<ProxyStateService>();
            builder.Services.AddSingleton<IProxyStateService>(sp => sp.GetRequiredService<ProxyStateService>());

            builder.Services.AddReverseProxy()
                .LoadFromMemory(runningConfig.Routes.ToList(), runningConfig.Clusters.ToList());

            builder.Configuration.AddJsonFile(Path.Combine(AppPaths.AppDir, "acme-providers.json"));
            builder.Services.AddHttpClient(HttpClientNames.AcmeClient, client => {
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                client.DefaultRequestHeaders.UserAgent.ParseAdd($"ByProxy/0.1 {HttpClientNames.AcmeClient}");
            });
            builder.Services.AddSingleton<AcmeClientService>();
            builder.Services.AddSingleton<AcmeChallengeIntercept>();

            builder.Services.AddSingleton<RequestLogger>();
            builder.Services.AddSingleton<ConditionalHttpsRedirect>();
            builder.Services.AddSingleton<NonClusterRouteHandler>();

            builder.Services.AddSingleton<ScriptCompilationService>();
            builder.Services.AddSingleton<CertificateService>();
            builder.Services.AddHostedService(sp => sp.GetRequiredService<CertificateService>());

            //builder.Logging.AddFilter("Microsoft.AspNetCore.Server.Kestrel", LogLevel.Debug);
            builder.WebHost.ConfigureKestrel((context, serverOptions) => {
                var certService = serverOptions.ApplicationServices.GetRequiredService<CertificateService>();
                if (!disableAdminApp) {
                    if (config.AdminListenAny) {
                        serverOptions.ListenAnyIP(config.AdminPort, listen => listen.UseHttps(httpsOptions => {
                            httpsOptions.ServerCertificateSelector = certService.AdminCertificateSelector;
                        }));
                    } else {
                        serverOptions.ListenLocalhost(config.AdminPort, listen => listen.UseHttps(httpsOptions => {
                            httpsOptions.ServerCertificateSelector = certService.AdminCertificateSelector;
                        }));
                    }
                }
                foreach (var port in runningConfig.HttpPorts) {
                    serverOptions.ListenAnyIP(port);
                }
                if (runningConfig.HttpsPorts.Any()) {
                    foreach (var port in runningConfig.HttpsPorts) {
                        serverOptions.ListenAnyIP(port, listen => listen.UseHttps(httpsOptions => {
                            httpsOptions.ServerCertificateSelector = certService.CertificateSelector;
                        }));
                    }
                }
            });

            builder.Services.AddSingleton<MatcherPolicy, PortMatcher>();

            builder.Services.AddDbContextFactory<ProxyDb>(ConfigureSQLite);
            builder.Services.AddDataProtection().PersistKeysToDbContext<ProxyDb>();

            builder.Services.AddSingleton<ConfigurationService>();
            builder.Services.AddHostedService(sp => sp.GetRequiredService<ConfigurationService>());

            if (!disableAdminApp) {
                builder.Services.AddAntiforgery(options => options.Cookie.Name = CookieNames.AdminCsrf);

                builder.Services.AddSingleton<BlazorSessionService>();
                builder.Services.AddScoped<AuthService>();
                builder.Services.AddSingleton<IAuthorizationMiddlewareResultHandler, AuthorizationMiddlewareResultHandler>();
                builder.Services.AddAuthorization(options => {
                    options.DefaultPolicy = new AuthorizationPolicyBuilder()
                        .RequireRole(AuthRoles.Admin)
                        .Build();
                });

                builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                    .AddCookie(options => {
                        options.ExpireTimeSpan = TimeSpan.FromDays(7);
                        options.SlidingExpiration = true;
                        options.Cookie.MaxAge = TimeSpan.FromDays(14);
                        options.Cookie.Name = CookieNames.AdminAuth;
                        options.Events.OnRedirectToLogin = context => {
                            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                            return Task.CompletedTask;
                        };
                        options.Events.OnRedirectToAccessDenied = ctx => {
                            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                            return Task.CompletedTask;
                        };
                    }
                );

                builder.Services.AddMemoryCache();
                builder.Services.AddSingleton<ITicketStore, CookieTicketStore>();
                builder.Services.AddOptions<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme)
                    .Configure<ITicketStore>((options, store) => options.SessionStore = store);

                builder.Services.AddHttpContextAccessor();
                builder.Services.AddCascadingAuthenticationState();

                builder.Services.AddLocalization();
                builder.Services.AddScoped<AdminAppStateService>();
                builder.Services.AddModalService();
                builder.Services.AddRazorComponents()
                    .AddInteractiveServerComponents();
            }

            var app = builder.Build();

            app.MapWhen(context => runningConfig.ProxyPorts.Contains(context.Connection.LocalPort), proxy => {
                proxy.UseMiddleware<RequestLogger>();

                proxy.UseRouting();

                proxy.UseMiddleware<AcmeChallengeIntercept>();

                proxy.UseEndpoints(endpoints => {
                    endpoints.MapReverseProxy(pipeline => {
                        pipeline.UseMiddleware<ConditionalHttpsRedirect>();
                        pipeline.UseMiddleware<NonClusterRouteHandler>();
                    });
                });

                var proxyState = proxy.ApplicationServices.GetRequiredService<ProxyStateService>();
                proxy.Run(async (context) => {
                    if (proxyState.RunningConfig.Tarpit) {
                        var tarpitTime = Random.Shared.Next(15_000, 30_000);
                        try {
                            await Task.Delay(tarpitTime, context.RequestAborted);
                        } catch { return; }
                    }
                    context.Response.StatusCode = proxyState.RunningConfig.UnmatchedStatus;
                    context.Response.ContentLength = 0;
                    return;
                });
            });

            if (!disableAdminApp) {
                app.MapWhen(context => context.Connection.LocalPort == config.AdminPort, admin => {
                    admin.UseForwardedHeaders(
                        new ForwardedHeadersOptions {
                            ForwardedHeaders = ForwardedHeaders.XForwardedFor
                            | ForwardedHeaders.XForwardedProto
                            | ForwardedHeaders.XForwardedHost
                        });

                    admin.UseStaticFiles();
                    admin.UseRouting();

                    admin.UseRequestLocalization(options => {
                        options.AddSupportedCultures([.. Languages.SupportedLanguages]);
                        options.AddSupportedUICultures([.. Languages.SupportedLanguages]);
                        options.SetDefaultCulture(Languages.SupportedLanguages[0]);

                        options.RequestCultureProviders
                            .OfType<CookieRequestCultureProvider>()
                            .First().CookieName = CookieNames.AdminCulture;

                        options.AddInitialRequestCultureProvider(new IdentityClaimRequestCultureProvider());
                    });

                    admin.UseAntiforgery();
                    admin.UseAuthorization();
                    admin.UseEndpoints(endpoints =>
                        endpoints.MapRazorComponents<App>()
                        .AddInteractiveServerRenderMode()
                    );
                });
            }

            if (!disableAdminApp) {
                var stoppingToken = app.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping;
                var restartToken = app.Services.GetRequiredService<ProxyStateService>().KestrelRestarting;
                var stopOrRestartSource = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, restartToken);
                try {
                    await app.RunAsync(stopOrRestartSource.Token);
                } catch (IOException ex) {
                    return (StopReason.StartupFailure, ex.Message);
                } catch (Exception ex) {
                    return (StopReason.StartupFailure, ex.Message);
                }
                stopOrRestartSource.Dispose();
                return (restartToken.IsCancellationRequested ? StopReason.Restarting : StopReason.ShuttingDown, null);
            } else {
                app.Run();
                return (StopReason.ShuttingDown, null);
            }
        }
    }
}