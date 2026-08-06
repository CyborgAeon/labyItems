namespace Microsoft.Extensions.Logging
{
    public interface ILogger
    {
    }

    public interface ILogger<out TCategoryName> : ILogger
    {
    }

    public static class LoggerExtensions
    {
        public static void LogWarning(this ILogger logger, string message, params object[] args)
        {
        }

        public static void LogWarning(this ILogger logger, Exception exception, string message, params object[] args)
        {
        }

        public static void LogInformation(this ILogger logger, string message, params object[] args)
        {
        }

        public static void LogError(this ILogger logger, string message, params object[] args)
        {
        }

        public static void LogError(this ILogger logger, Exception exception, string message, params object[] args)
        {
        }
    }
}

namespace Microsoft.Extensions.Logging.Abstractions
{
    using Microsoft.Extensions.Logging;

    public sealed class NullLogger<T> : ILogger<T>
    {
        public static NullLogger<T> Instance { get; } = new();
    }
}
