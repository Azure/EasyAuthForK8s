using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;

namespace EasyAuthForK8s.Tests.Web.Helpers;

/// <summary>
/// Enables log messages to be evaluated in tests
/// Credit to Igor Kustov https://github.com/sirIrishman for starter code
/// </summary>
/// <typeparam name="T"></typeparam>
internal sealed class TestLogger : ILogger, IDisposable
{
    private object _lock = new object();
    private readonly List<LoggedMessage> _messages = new List<LoggedMessage>();

    public List<LoggedMessage> Messages
    {
        get
        {
            lock (_lock)
            {
                return _messages;
            }

        }
    }
    public void Dispose()
    {
    }

    public IDisposable BeginScope<TState>(TState state)
    {
        return this;
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return true;
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
    {
        string message = formatter(state, exception);
        lock (_lock)
        {
            _messages.Add(new LoggedMessage(logLevel, eventId, exception, message));
        }
    }

    public ILoggerFactory Factory()
    {
        return new TestLoggerFactory(this);
    }
    public sealed class LoggedMessage
    {
        public LogLevel LogLevel { get; }
        public EventId EventId { get; }
        public Exception Exception { get; }
        public string Message { get; }

        public LoggedMessage(LogLevel logLevel, EventId eventId, Exception exception, string message)
        {
            LogLevel = logLevel;
            EventId = eventId;
            Exception = exception;
            Message = message;
        }
    }
    public class TestLoggerFactory : ILoggerFactory
    {
        private TestLogger _logger;
        public TestLoggerFactory(TestLogger logger)
        {
            _logger = logger;
        }
        public ILogger CreateLogger(string name)
        {
            return _logger;
        }

        public void AddProvider(ILoggerProvider provider)
        {
        }

        public void Dispose()
        {
        }
    }
}

