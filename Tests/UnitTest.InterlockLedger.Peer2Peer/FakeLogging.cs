/******************************************************************************************************************************
 *
 *      Copyright (c) 2017-2018 InterlockLedger Network
 *
 ******************************************************************************************************************************/

using Microsoft.Extensions.Logging;
using System;

#pragma warning disable S3881 // "IDisposable" should be implemented correctly

namespace UnitTest.InterlockLedger.Peer2Peer
{
    public class FakeLogging : ILoggerFactory, ILogger
    {
        public string LastLog { get; private set; }
        void ILoggerFactory.AddProvider(ILoggerProvider provider) {
            // Nothing to emulate here
        }

        IDisposable ILogger.BeginScope<TState>(TState state) => this;

        ILogger ILoggerFactory.CreateLogger(string categoryName) => this;

        void IDisposable.Dispose() {
            // Nothing to dispose.
        }

        bool ILogger.IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        void ILogger.Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
            => LastLog = $"{logLevel}: {formatter(state, exception)}";
    }
}