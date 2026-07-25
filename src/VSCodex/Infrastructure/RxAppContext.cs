// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.Threading;
using VSCodex.Services;
using VSCodex.ViewModels;
using VSCodex.Views;

namespace VSCodex.Infrastructure;

/// <summary>Provides the rx App Context implementation.</summary>
public sealed class RxAppContext
{
    /// <summary>Stores the factories.</summary>
    private readonly Dictionary<Type, Func<RxAppContext, object>> _factories;

    /// <summary>Initializes a new instance of the <see cref="RxAppContext"/> class.</summary>
    /// <param name="serviceProvider">The service Provider.</param>
    /// <param name="joinableTaskFactory">The joinable Task Factory.</param>
    /// <param name="factories">The factories.</param>
    internal RxAppContext(IServiceProvider serviceProvider, JoinableTaskFactory joinableTaskFactory, Dictionary<Type, Func<RxAppContext, object>> factories)
    {
        ServiceProvider = serviceProvider;
        JoinableTaskFactory = joinableTaskFactory;
        _factories = factories;
    }

    /// <summary>Gets the service Provider.</summary>
    public IServiceProvider ServiceProvider { get; }

    /// <summary>Gets the joinable Task Factory.</summary>
    public JoinableTaskFactory JoinableTaskFactory { get; }

    /// <summary>Gets the operation.</summary>
    /// <typeparam name="TService">The t Service type.</typeparam>
    /// <param name="fallbackFactories">Optional factories used when the requested service is not registered.</param>
    /// <returns>The get result.</returns>
    public TService Get<TService>(params Func<TService>[] fallbackFactories)
        where TService : class
    {
        if (_factories.TryGetValue(typeof(TService), out var factory))
        {
            return (TService)factory(this);
        }

        Func<TService>? fallbackFactory = fallbackFactories.FirstOrDefault();
        return fallbackFactory?.Invoke()
            ?? throw new InvalidOperationException($"Service not registered in RxAppBuilder: {typeof(TService).FullName}");
    }

    /// <summary>Creates tool Window View Model.</summary>
    /// <returns>The create Tool Window View Model result.</returns>
    public VSCodexToolWindowViewModel CreateToolWindowViewModel()
    {
        return new(
            Get<ISettingsStore>(),
            Get<IMemoryStore>(),
            Get<ISkillIndexService>(),
            Get<IMcpConfigService>(),
            Get<IMcpToolCatalogService>(),
            Get<IReactiveMemoryService>(),
            Get<IWorkspaceContextService>(),
            Get<ISessionStore>(),
            Get<ICodexOrchestrator>(),
            Get<ITaskOrchestrationService>(),
            Get<ICodingAssistantContextService>(),
            Get<IModelAnalyticsService>(),
            Get<ICodexEnvironmentService>(),
            Get<IVoiceInputService>(),
            Get<TimeProvider>(),
            JoinableTaskFactory);
    }

    /// <summary>Creates tool Window Control.</summary>
    /// <returns>The create Tool Window Control result.</returns>
    public VSCodexToolWindowControl CreateToolWindowControl() => new VSCodexToolWindowControl { DataContext = CreateToolWindowViewModel() };
}
