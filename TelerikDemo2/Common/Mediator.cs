using System.Collections.Concurrent;

namespace TelerikDemo2.Common;

public interface IRequest<TResponse> { }

public interface IRequestHandler<in TRequest, TResponse> where TRequest : IRequest<TResponse>
{
    Task<TResponse> HandleAsync(TRequest request, CancellationToken ct);
}

// Pages send requests through this and never reference handler classes.
public interface IDispatcher
{
    Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken ct = default);
}

internal sealed class Dispatcher(IServiceProvider services) : IDispatcher
{
    private static readonly ConcurrentDictionary<Type, object> Invokers = new();

    public Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var invoker = (Invoker<TResponse>)Invokers.GetOrAdd(
            request.GetType(),
            type => Activator.CreateInstance(typeof(Invoker<,>).MakeGenericType(type, typeof(TResponse)))!);

        return invoker.InvokeAsync(request, services, ct);
    }

    private abstract class Invoker<TResponse>
    {
        public abstract Task<TResponse> InvokeAsync(IRequest<TResponse> request, IServiceProvider sp, CancellationToken ct);
    }

    private sealed class Invoker<TRequest, TResponse> : Invoker<TResponse> where TRequest : IRequest<TResponse>
    {
        public override Task<TResponse> InvokeAsync(IRequest<TResponse> request, IServiceProvider sp, CancellationToken ct) =>
            sp.GetRequiredService<IRequestHandler<TRequest, TResponse>>().HandleAsync((TRequest)request, ct);
    }
}

public static class MediatorExtensions
{
    public static IServiceCollection AddRequestHandlers(this IServiceCollection services, System.Reflection.Assembly assembly)
    {
        services.AddScoped<IDispatcher, Dispatcher>();

        var handlerInterface = typeof(IRequestHandler<,>);
        var handlers = assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false })
            .SelectMany(t => t.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == handlerInterface)
                .Select(i => (Service: i, Implementation: t)));

        foreach (var (service, implementation) in handlers)
        {
            services.AddScoped(service, implementation);
        }
        return services;
    }
}
