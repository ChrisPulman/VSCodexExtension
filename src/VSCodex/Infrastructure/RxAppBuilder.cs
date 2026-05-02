using System;
using System.Collections.Generic;
using System.Reactive.Concurrency;
using System.Windows;
using System.Windows.Threading;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using ReactiveUI.Builder;
using VSCodex.Services;
using VSCodex.ViewModels;
using VSCodex.Views;

namespace VSCodex.Infrastructure;

public sealed class RxAppBuilder
{
    private static bool _reactiveUiInitialized;
    private readonly Dictionary<Type, Func<RxAppContext, object>> _factories = new Dictionary<Type, Func<RxAppContext, object>>();
    private IServiceProvider _serviceProvider = Microsoft.VisualStudio.Shell.ServiceProvider.GlobalProvider;
    private JoinableTaskFactory _joinableTaskFactory = new JoinableTaskFactory(ThreadHelper.JoinableTaskContext);

    public RxAppBuilder UseVisualStudioServiceProvider(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? Microsoft.VisualStudio.Shell.ServiceProvider.GlobalProvider;
        return this;
    }

    public RxAppBuilder UseReactiveUiSchedulers()
    {
        if (_reactiveUiInitialized)
        {
            return this;
        }

        var dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
        var mainThreadScheduler = new DispatcherScheduler(dispatcher);

        ReactiveUI.Builder.RxAppBuilder.CreateReactiveUIBuilder()
            .WithMainThreadScheduler(mainThreadScheduler, true)
            .WithTaskPoolScheduler(TaskPoolScheduler.Default, true)
            .WithCoreServices()
            .BuildApp();
        _reactiveUiInitialized = true;
        return this;
    }

    public RxAppBuilder UseJoinableTaskFactory(JoinableTaskFactory joinableTaskFactory)
    {
        _joinableTaskFactory = joinableTaskFactory ?? _joinableTaskFactory;
        return this;
    }

    public RxAppBuilder RegisterSingleton<TService>(Func<RxAppContext, TService> factory) where TService : class
    {
        TService? instance = null;
        _factories[typeof(TService)] = context => (instance ??= factory(context));
        return this;
    }

    public RxAppContext Build() => new RxAppContext(_serviceProvider, _joinableTaskFactory, _factories);

    public static RxAppBuilder CreateVisualStudioDefault(IServiceProvider serviceProvider, JoinableTaskFactory? joinableTaskFactory = null)
    {
        var builder = new RxAppBuilder()
            .UseVisualStudioServiceProvider(serviceProvider)
            .UseReactiveUiSchedulers()
            .RegisterSingleton<ISettingsStore>(_ => new SettingsStore())
            .RegisterSingleton<IMemoryStore>(_ => new MemoryStore())
            .RegisterSingleton<ISkillIndexService>(_ => new SkillIndexService())
            .RegisterSingleton<IMcpConfigService>(_ => new McpConfigService())
            .RegisterSingleton<IMcpToolCatalogService>(ctx => new McpToolCatalogService(ctx.Get<IMcpConfigService>()))
            .RegisterSingleton<IWorkspaceContextService>(ctx => new WorkspaceContextService(ctx.ServiceProvider))
            .RegisterSingleton<ISessionStore>(_ => new SessionStore())
            .RegisterSingleton<ICodingAssistantContextService>(ctx => new CodingAssistantContextService(ctx.ServiceProvider, ctx.Get<IWorkspaceContextService>()))
            .RegisterSingleton<IModelAnalyticsService>(_ => new ModelAnalyticsService())
            .RegisterSingleton<ICodexEnvironmentService>(_ => new CodexEnvironmentService())
            .RegisterSingleton<CodexSdkJsonClient>(ctx => new CodexSdkJsonClient(ctx.Get<ISettingsStore>()))
            .RegisterSingleton<CodexCliClient>(ctx => new CodexCliClient(ctx.Get<ISettingsStore>()))
            .RegisterSingleton<ICodexOrchestrator>(ctx => new CodexOrchestrator(ctx.Get<CodexSdkJsonClient>(), ctx.Get<CodexCliClient>()))
            .RegisterSingleton<ITaskOrchestrationService>(ctx => new TaskOrchestrationService(ctx.Get<ISettingsStore>(), ctx.Get<ICodexOrchestrator>()));
        return joinableTaskFactory == null ? builder : builder.UseJoinableTaskFactory(joinableTaskFactory);
    }
}

public sealed class RxAppContext
{
    private readonly Dictionary<Type, Func<RxAppContext, object>> _factories;

    internal RxAppContext(IServiceProvider serviceProvider, JoinableTaskFactory joinableTaskFactory, Dictionary<Type, Func<RxAppContext, object>> factories)
    {
        ServiceProvider = serviceProvider;
        JoinableTaskFactory = joinableTaskFactory;
        _factories = factories;
    }

    public IServiceProvider ServiceProvider { get; }

    public JoinableTaskFactory JoinableTaskFactory { get; }

    public TService Get<TService>() where TService : class
    {
        if (_factories.TryGetValue(typeof(TService), out var factory))
        {
            return (TService)factory(this);
        }

        throw new InvalidOperationException("Service not registered in RxAppBuilder: " + typeof(TService).FullName);
    }

    public VSCodexToolWindowViewModel CreateToolWindowViewModel()
    {
        return new VSCodexToolWindowViewModel(
            Get<ISettingsStore>(),
            Get<IMemoryStore>(),
            Get<ISkillIndexService>(),
            Get<IMcpConfigService>(),
            Get<IMcpToolCatalogService>(),
            Get<IWorkspaceContextService>(),
            Get<ISessionStore>(),
            Get<ICodexOrchestrator>(),
            Get<ITaskOrchestrationService>(),
            Get<ICodingAssistantContextService>(),
            Get<IModelAnalyticsService>(),
            Get<ICodexEnvironmentService>(),
            JoinableTaskFactory);
    }

    public VSCodexToolWindowControl CreateToolWindowControl() => new VSCodexToolWindowControl { DataContext = CreateToolWindowViewModel() };
}
