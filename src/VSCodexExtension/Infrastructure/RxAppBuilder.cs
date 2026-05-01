using System;
using System.Collections.Generic;
using System.Reactive.Concurrency;
using ReactiveUI;
using ReactiveUI.Builder;
using ReactiveUI.Extensions;
using VSCodexExtension.Services;
using VSCodexExtension.ViewModels;
using VSCodexExtension.Views;

namespace VSCodexExtension.Infrastructure
{
    public sealed class RxAppBuilder
    {
        private static bool _reactiveUiInitialized;
        private readonly Dictionary<Type, Func<RxAppContext, object>> _factories = new Dictionary<Type, Func<RxAppContext, object>>();
        private IServiceProvider _serviceProvider = Microsoft.VisualStudio.Shell.ServiceProvider.GlobalProvider;

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

            ReactiveUI.Builder.RxAppBuilder.CreateReactiveUIBuilder()
                .WithMainThreadScheduler(RxSchedulers.MainThreadScheduler, true)
                .WithTaskPoolScheduler(TaskPoolScheduler.Default, true)
                .WithCoreServices()
                .BuildApp();
            _reactiveUiInitialized = true;
            return this;
        }

        public RxAppBuilder RegisterSingleton<TService>(Func<RxAppContext, TService> factory) where TService : class
        {
            TService? instance = null;
            _factories[typeof(TService)] = context => instance ?? (instance = factory(context));
            return this;
        }

        public RxAppContext Build() => new RxAppContext(_serviceProvider, _factories);

        public static RxAppBuilder CreateVisualStudioDefault(IServiceProvider serviceProvider)
        {
            return new RxAppBuilder()
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
                .RegisterSingleton<CodexSdkJsonClient>(ctx => new CodexSdkJsonClient(ctx.Get<ISettingsStore>()))
                .RegisterSingleton<CodexCliClient>(ctx => new CodexCliClient(ctx.Get<ISettingsStore>()))
                .RegisterSingleton<ICodexOrchestrator>(ctx => new CodexOrchestrator(ctx.Get<CodexSdkJsonClient>(), ctx.Get<CodexCliClient>()))
                .RegisterSingleton<ITaskOrchestrationService>(ctx => new TaskOrchestrationService(ctx.Get<ISettingsStore>(), ctx.Get<ICodexOrchestrator>()));
        }
    }

    public sealed class RxAppContext
    {
        private readonly Dictionary<Type, Func<RxAppContext, object>> _factories;

        internal RxAppContext(IServiceProvider serviceProvider, Dictionary<Type, Func<RxAppContext, object>> factories)
        {
            ServiceProvider = serviceProvider;
            _factories = factories;
        }

        public IServiceProvider ServiceProvider { get; }

        public TService Get<TService>() where TService : class
        {
            if (_factories.TryGetValue(typeof(TService), out var factory))
            {
                return (TService)factory(this);
            }

            throw new InvalidOperationException("Service not registered in RxAppBuilder: " + typeof(TService).FullName);
        }

        public CodexToolWindowViewModel CreateToolWindowViewModel()
        {
            return new CodexToolWindowViewModel(
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
                Get<IModelAnalyticsService>());
        }

        public CodexToolWindowControl CreateToolWindowControl()
        {
            return new CodexToolWindowControl { DataContext = CreateToolWindowViewModel() };
        }
    }
}
