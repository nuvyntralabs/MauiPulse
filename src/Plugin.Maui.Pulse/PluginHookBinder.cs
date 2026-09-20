using System.Linq.Expressions;
using System.Reflection;

namespace Plugin.Maui.Pulse;

sealed class PluginHookBinder
{
    public IReadOnlyList<BoundPlugin> Bind(IServiceProvider services)
    {
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        var bound = new List<BoundPlugin>();
        foreach (var hook in PluginHookCatalog.Hooks)
        {
            var loaded = AssemblyLoaded(assemblies, hook);
            var type = FindInterface(assemblies, hook);
            object? instance = null;
            if (type is not null)
            {
                try
                {
                    instance = services.GetService(type);
                }
                catch (Exception)
                {
                    instance = null;
                }
            }

            var state = instance is not null
                ? "quiet"
                : type is not null
                    ? "not_registered"
                    : loaded
                        ? "not_registered"
                        : "not_installed";
            bound.Add(new BoundPlugin(hook, type, instance, state));
        }

        return bound;
    }

    public IReadOnlyList<IDisposable> Subscribe(IReadOnlyList<BoundPlugin> plugins, Action<PluginHook, string, object?> onEvent)
    {
        var subscriptions = new List<IDisposable>();
        foreach (var plugin in plugins)
        {
            if (plugin.Instance is null || plugin.Type is null)
                continue;

            foreach (var eventName in plugin.Hook.EventNames)
            {
                var eventInfo = plugin.Type.GetEvent(eventName)
                    ?? plugin.Instance.GetType().GetEvent(eventName);
                if (eventInfo?.EventHandlerType is null)
                    continue;

                var hook = plugin.Hook;
                var handler = CreateHandler(eventInfo.EventHandlerType, (_, args) => onEvent(hook, eventName, args));
                eventInfo.AddEventHandler(plugin.Instance, handler);
                subscriptions.Add(new EventSubscription(eventInfo, plugin.Instance, handler));
            }
        }

        return subscriptions;
    }

    public static string Summarize(object? args)
    {
        if (args is null)
            return "";

        var type = args.GetType();
        var current = type.GetProperty("Current")?.GetValue(args);
        if (current is not null)
        {
            if (ReadBool(current, "IsCaptivePortal") == true)
                return "captive portal";
            if (ReadBool(current, "HasInternet") == false)
                return "offline";
            return current.ToString() ?? "";
        }

        var session = type.GetProperty("Session")?.GetValue(args);
        var sessionId = session?.GetType().GetProperty("SessionId")?.GetValue(session)?.ToString();
        if (!string.IsNullOrWhiteSpace(sessionId))
            return sessionId.Length <= 8 ? sessionId : sessionId[..8];

        var collection = type.GetProperty("Collection")?.GetValue(args)?.ToString();
        var entity = type.GetProperty("EntityId")?.GetValue(args)?.ToString();
        if (!string.IsNullOrWhiteSpace(collection) && !string.IsNullOrWhiteSpace(entity))
            return $"{collection}#{entity}";

        return type.GetProperty("Result")?.GetValue(args)?.ToString()
            ?? type.GetProperty("Status")?.GetValue(args)?.ToString()
            ?? args.ToString()
            ?? "";
    }

    public static string? ReadSessionId(object? args)
    {
        var session = args?.GetType().GetProperty("Session")?.GetValue(args);
        return session?.GetType().GetProperty("SessionId")?.GetValue(session)?.ToString();
    }

    static bool AssemblyLoaded(IEnumerable<Assembly> assemblies, PluginHook hook) =>
        assemblies.Any(assembly => hook.AssemblyHints.Any(hint =>
            string.Equals(assembly.GetName().Name, hint, StringComparison.OrdinalIgnoreCase)));

    static Type? FindInterface(IEnumerable<Assembly> assemblies, PluginHook hook)
    {
        foreach (var name in hook.InterfaceNames)
        {
            foreach (var assembly in assemblies)
            {
                var type = assembly.GetType(name, throwOnError: false, ignoreCase: false);
                if (type is not null)
                    return type;
            }
        }

        return null;
    }

    static bool? ReadBool(object target, string property)
    {
        var value = target.GetType().GetProperty(property)?.GetValue(target);
        return value is bool flag ? flag : null;
    }

    static Delegate CreateHandler(Type handlerType, Action<object?, object?> callback)
    {
        var invoke = handlerType.GetMethod("Invoke") ?? throw new InvalidOperationException(handlerType.FullName);
        var parameters = invoke.GetParameters();
        var sender = Expression.Parameter(parameters[0].ParameterType, "sender");
        var args = Expression.Parameter(parameters[1].ParameterType, "args");
        var body = Expression.Invoke(
            Expression.Constant(callback),
            Expression.Convert(sender, typeof(object)),
            Expression.Convert(args, typeof(object)));
        return Expression.Lambda(handlerType, body, sender, args).Compile();
    }

    sealed class EventSubscription(EventInfo eventInfo, object target, Delegate handler) : IDisposable
    {
        public void Dispose() => eventInfo.RemoveEventHandler(target, handler);
    }
}

sealed record BoundPlugin(PluginHook Hook, Type? Type, object? Instance, string State);
