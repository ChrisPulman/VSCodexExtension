// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Reactive.Concurrency;
using System.Windows;
using System.Windows.Threading;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using ReactiveUI.Builder;
using VSCodex.Services;

namespace VSCodex.Infrastructure;

/// <summary>Provides the rx App Builder implementation.</summary>
public sealed class RxAppBuilder
{
    /// <summary>Stores the factories.</summary>
    private readonly Dictionary<Type, Func<RxAppContext, object>> _factories = new();

    /// <summary>Stores the service Provider.</summary>
    private IServiceProvider _serviceProvider = Microsoft.VisualStudio.Shell.ServiceProvider.GlobalProvider;

    /// <summary>Stores the joinable Task Factory.</summary>
    private JoinableTaskFactory _joinableTaskFactory = new(ThreadHelper.JoinableTaskContext);

    /// <summary>Creates the default Visual Studio application context.</summary>
    /// <param name="serviceProvider">The Visual Studio service provider.</param>
    /// <returns>A configured application builder.</returns>
    public static RxAppBuilder CreateVisualStudioDefault(IServiceProvider serviceProvider) => CreateVisualStudioDefault(serviceProvider, null);

    /// <summary>Creates the default Visual Studio application context.</summary>
    /// <param name="serviceProvider">The Visual Studio service provider.</param>
    /// <param name="joinableTaskFactory">The optional joinable task factory.</param>
    /// <returns>A configured application builder.</returns>
    public static RxAppBuilder CreateVisualStudioDefault(IServiceProvider serviceProvider, JoinableTaskFactory? joinableTaskFactory)
    {
        var builder = new RxAppBuilder()
            .UseVisualStudioServiceProvider(serviceProvider)
            .UseReactiveUiSchedulers()
            .RegisterSingleton(_ => TimeProvider.System)
            .RegisterSingleton<ISettingsStore>(_ => new SettingsStore())
            .RegisterSingleton<IMemoryStore>(_ => new MemoryStore())
            .RegisterSingleton<ISkillIndexService>(_ => new SkillIndexService())
            .RegisterSingleton<IMcpConfigService>(_ => new McpConfigService())
            .RegisterSingleton<IMcpToolCatalogService>(ctx => new McpToolCatalogService(ctx.Get<IMcpConfigService>(), ctx.Get<TimeProvider>()))
            .RegisterSingleton<IReactiveMemoryService>(ctx => new ReactiveMemoryService(ctx.Get<IMcpConfigService>(), ctx.Get<IMcpToolCatalogService>(), ctx.Get<TimeProvider>()))
            .RegisterSingleton<IWorkspaceContextService>(ctx => new WorkspaceContextService(ctx.ServiceProvider))
            .RegisterSingleton<ISessionStore>(ctx => new SessionStore(ctx.Get<TimeProvider>()))
            .RegisterSingleton<ICodingAssistantContextService>(ctx => new CodingAssistantContextService(ctx.ServiceProvider, ctx.Get<IWorkspaceContextService>()))
            .RegisterSingleton<IModelAnalyticsService>(_ => new ModelAnalyticsService())
            .RegisterSingleton<ICodexEnvironmentService>(_ => new CodexEnvironmentService())
            .RegisterSingleton<IVoiceInputService>(_ => new VoiceInputService())
            .RegisterSingleton(ctx => new CodexSdkJsonClient(ctx.Get<ISettingsStore>()))
            .RegisterSingleton(ctx => new CodexCliClient(ctx.Get<ISettingsStore>()))
            .RegisterSingleton<ICodexOrchestrator>(ctx => new CodexOrchestrator(ctx.Get<CodexSdkJsonClient>(), ctx.Get<CodexCliClient>()))
            .RegisterSingleton<ITaskOrchestrationService>(ctx => new TaskOrchestrationService(ctx.Get<ISettingsStore>(), ctx.Get<ICodexOrchestrator>()));
        return joinableTaskFactory is null ? builder : builder.UseJoinableTaskFactory(joinableTaskFactory);
    }

    /// <summary>Performs the use Visual Studio Service Provider operation.</summary>
    /// <param name="serviceProvider">The service Provider.</param>
    /// <returns>The use Visual Studio Service Provider result.</returns>
    public RxAppBuilder UseVisualStudioServiceProvider(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? Microsoft.VisualStudio.Shell.ServiceProvider.GlobalProvider;
        return this;
    }

    /// <summary>Performs the use Reactive Ui Schedulers operation.</summary>
    /// <returns>The use Reactive Ui Schedulers result.</returns>
    public RxAppBuilder UseReactiveUiSchedulers()
    {
        ReactiveUiInitialization.Ensure();
        return this;
    }

    /// <summary>Performs the use Joinable Task Factory operation.</summary>
    /// <param name="joinableTaskFactory">The joinable Task Factory.</param>
    /// <returns>The use Joinable Task Factory result.</returns>
    public RxAppBuilder UseJoinableTaskFactory(JoinableTaskFactory joinableTaskFactory)
    {
        _joinableTaskFactory = joinableTaskFactory ?? _joinableTaskFactory;
        return this;
    }

    /// <summary>Performs the register Singleton operation.</summary>
    /// <typeparam name="TService">The t Service type.</typeparam>
    /// <param name="factory">The factory.</param>
    /// <returns>The register Singleton result.</returns>
    public RxAppBuilder RegisterSingleton<TService>(Func<RxAppContext, TService> factory)
        where TService : class
    {
        TService? instance = null;
        _factories[typeof(TService)] = context => instance ??= factory(context);
        return this;
    }

    /// <summary>Builds the operation.</summary>
    /// <returns>The build result.</returns>
    public RxAppContext Build() => new(_serviceProvider, _joinableTaskFactory, _factories);

    /// <summary>Initializes ReactiveUI schedulers once for the process.</summary>
    private static class ReactiveUiInitialization
    {
        /// <summary>Synchronizes process-wide scheduler initialization.</summary>
        private static readonly object InitializationLock = new();

        /// <summary>Indicates whether the schedulers have been initialized.</summary>
        private static bool _isInitialized;

        /// <summary>Initializes the configured ReactiveUI schedulers when required.</summary>
        internal static void Ensure()
        {
            lock (InitializationLock)
            {
                if (_isInitialized)
                {
                    return;
                }

                var dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
                var mainThreadScheduler = new DispatcherScheduler(dispatcher);

                _ = ReactiveUI.Builder.RxAppBuilder.CreateReactiveUIBuilder()
                    .WithMainThreadScheduler(mainThreadScheduler)
                    .WithTaskPoolScheduler(TaskPoolScheduler.Default)
                    .WithCoreServices()
                    .BuildApp();
                _isInitialized = true;
            }
        }
    }
}
