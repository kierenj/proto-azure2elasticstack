using System;
using System.Collections.Generic;
using System.Linq;
using azure2elasticstack;

namespace RedRiver.SaffronCore.Logging.JsonFile
{
    public class BatchingLogger
    {
        private readonly BatchingLoggerProvider _provider;

        public BatchingLogger(BatchingLoggerProvider loggerProvider)
        {
            _provider = loggerProvider;
        }

        public void LogAsJson(IDictionary<string, object> obj)
        {
            var timestamp = DateTime.UtcNow;
            var entryJson = obj.ToCondensedJsonString();

            _provider.AddMessage(timestamp, entryJson);
        }
    }
}