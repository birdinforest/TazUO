// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.Diagnostics;
using System.Text;

namespace ClassicUO.Utility.Logging
{
    public class Log
    {
        private static Logger _logger;

        public static void Start(LogTypes logTypes, LogFile logFile = null)
        {
            _logger = _logger ?? new Logger
            {
                LogTypes = logTypes
            };

            _logger.Start(logFile);
        }

        public static void Stop()
        {
            _logger?.Stop();
            _logger = null;
        }

        public static void Resume(LogTypes logTypes)
        {
            if (_logger != null)
                _logger.LogTypes = logTypes;
        }

        public static void Pause()
        {
            if (_logger != null)
                _logger.LogTypes = LogTypes.None;
        }

        [Conditional("DEBUG")]
        public static void Debug(string text) => _logger?.Message(LogTypes.Debug, text);

        public static void Info(string text) => _logger?.Message(LogTypes.Info, text);

        public static void Trace(string text) => _logger?.Message(LogTypes.Trace, text);

        [Conditional("DEBUG")]
        public static void TraceDebug(string text) => Trace(text);

        public static void Warn(string text) => _logger?.Message(LogTypes.Warning, text);

        [Conditional("DEBUG")]
        public static void WarnDebug(string text) => Warn(text);

        public static void Error(string text) => _logger?.Message(LogTypes.Error, text);

        [Conditional("DEBUG")]
        public static void ErrorDebug(string text) => Error(text);

        public static void Panic(string text) => _logger?.Message(LogTypes.Error, text);

        public static void NewLine() => _logger?.NewLine();

        public static void Clear() => _logger?.Clear();

        public static void PushIndent() => _logger?.PushIndent();

        public static void PopIndent() => _logger?.PopIndent();

        /// <summary>
        /// Logs the call stack for debugging purposes
        /// </summary>
        /// <param name="methodName">Name of the method calling this function</param>
        public static void LogCallStack(string methodName)
        {
            StackTrace stackTrace = new StackTrace(true);
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"{methodName}() called from:");

            for (int i = 1; i < Math.Min(stackTrace.FrameCount, 6); i++) // Skip frame 0 (this method), show up to 5 frames
            {
                StackFrame frame = stackTrace.GetFrame(i);
                if (frame != null)
                {
                    string callerMethodName = frame.GetMethod()?.Name ?? "Unknown";
                    string callerClassName = frame.GetMethod()?.DeclaringType?.Name ?? "Unknown";
                    int lineNumber = frame.GetFileLineNumber();
                    sb.AppendLine($"  [{i}] {callerClassName}.{callerMethodName}() at line {lineNumber}");
                }
            }

            Trace(sb.ToString());
        }
    }
}
