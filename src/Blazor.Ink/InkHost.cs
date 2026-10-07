using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Blazor.Ink;

public static class InkHost
{
    public static async Task<InkSession> RenderAsync<TComponent>(InkOptions? options = null,
        ParameterView? parameters = null, IServiceProvider? services = null, CancellationToken cancellationToken = default)
        where TComponent : IComponent
    {
        var session = new InkSession(options ?? new(), services, cancellationToken);
        await session.MountAsync<TComponent>(parameters ?? ParameterView.Empty);
        return session;
    }

    public static async Task<string> RenderToStringAsync<TComponent>(int columns = 80, ParameterView? parameters = null,
        IServiceProvider? services = null)
        where TComponent : IComponent
    {
        if (columns is < 1 or > Canvas.MaxDimension) throw new ArgumentOutOfRangeException(nameof(columns));
        await using var ownedServices = services is null ? new ServiceCollection().BuildServiceProvider() : null;
        var renderer = new TerminalRenderer(services ?? ownedServices!, columns);
        try { await renderer.MountAsync<TComponent>(parameters ?? ParameterView.Empty); }
        finally { await renderer.DisposeAsync(); }
        if (renderer.Error is { } error) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
        return renderer.Output;
    }
}
