using System;

namespace RedRiver.SaffronCore.Logging.JsonFile
{
    public struct LogMessage
    {
        public DateTimeOffset Timestamp { get; set; }
        public string Message { get; set; }
    }
}