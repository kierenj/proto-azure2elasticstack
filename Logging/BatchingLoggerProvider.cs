using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace RedRiver.SaffronCore.Logging.JsonFile
{
    public abstract class BatchingLoggerProvider
    {
        private readonly List<LogMessage> _currentBatch = new List<LogMessage>();
        private readonly TimeSpan _interval;
        private readonly int? _queueSize;
        private readonly int? _batchSize;
        private readonly IDisposable _optionsChangeToken;
        private readonly string _appName;
        private readonly string _environment;

        private BlockingCollection<LogMessage> _messageQueue;
        private Task _outputTask;
        private CancellationTokenSource _cancellationTokenSource;

        protected BatchingLoggerProvider(BatchingLoggerOptions loggerOptions)
        {
            _interval = loggerOptions.FlushPeriod;
            _batchSize = loggerOptions.BatchSize;
            _queueSize = loggerOptions.BackgroundQueueSize;
            Start();
        }

        protected abstract Task WriteMessagesAsync(IEnumerable<LogMessage> messages, CancellationToken token);

        long timeWaitingTicks = 0;
        long waits = 0;
        DateTime lastNote = DateTime.UtcNow;
        int minSize = int.MaxValue;
        int maxSize = int.MinValue;
        int written = 0;

        private async Task ProcessLogQueue(object state)
        {
            while (!_cancellationTokenSource.IsCancellationRequested)
            {
                if (_messageQueue.Count > maxSize) maxSize = _messageQueue.Count;
                if (_messageQueue.Count < minSize) minSize = _messageQueue.Count;
                if ((DateTime.UtcNow - lastNote).TotalSeconds >= 30)
                {
                    double tw;
                    lock (this)
                    {
                        tw = TimeSpan.FromTicks(timeWaitingTicks).TotalMilliseconds;
                    }
                    Console.WriteLine($"Log queue size = {_messageQueue.Count}.  Last 30 sec: min {minSize}, max {maxSize}, {written} written, time waiting {tw:0.000}ms (avg {tw / waits:0.000}ms)");
                    lastNote = DateTime.UtcNow;
                    minSize = int.MaxValue;
                    maxSize = int.MinValue;
                    written = 0;
                    waits = 0;
                    timeWaitingTicks = 0;
                }
                var limit = _batchSize ?? int.MaxValue;

                while (limit > 0 && _messageQueue.TryTake(out var message))
                {
                    _currentBatch.Add(message);
                    limit--;
                }

                if (_currentBatch.Count > 0)
                {
                    try
                    {
                        await WriteMessagesAsync(_currentBatch, _cancellationTokenSource.Token);
                        written += _currentBatch.Count;
                    }
                    catch
                    {
                        // ignored
                    }

                    _currentBatch.Clear();
                }
                else
                {
                    await IntervalAsync(_interval, _cancellationTokenSource.Token);
                }
            }
        }

        protected virtual Task IntervalAsync(TimeSpan interval, CancellationToken cancellationToken)
        {
            return Task.Delay(interval, cancellationToken);
        }

        internal void AddMessage(DateTimeOffset timestamp, string message)
        {
            if (!_messageQueue.IsAddingCompleted)
            {
                try
                {
                    var sw = Stopwatch.StartNew();
                    waits++;
                    _messageQueue.Add(new LogMessage { Message = message, Timestamp = timestamp }, _cancellationTokenSource.Token);
                    lock (this) { timeWaitingTicks += sw.ElapsedTicks; }
                }
                catch
                {
                    //cancellation token canceled or CompleteAdding called
                    Console.WriteLine("ERR: cancellation token canceled or CompleteAdding called");
                }
            }
        }

        private void Start()
        {
            _messageQueue = _queueSize == null ?
                new BlockingCollection<LogMessage>(new ConcurrentQueue<LogMessage>()) :
                new BlockingCollection<LogMessage>(new ConcurrentQueue<LogMessage>(), _queueSize.Value);

            _cancellationTokenSource = new CancellationTokenSource();
            _outputTask = Task.Factory.StartNew<Task>(
                ProcessLogQueue,
                null,
                TaskCreationOptions.LongRunning);
        }

        private void Stop()
        {
            _cancellationTokenSource.Cancel();
            _messageQueue.CompleteAdding();

            try
            {
                _outputTask.Wait(_interval);
            }
            catch (TaskCanceledException)
            {
            }
            catch (AggregateException ex) when (ex.InnerExceptions.Count == 1 && ex.InnerExceptions[0] is TaskCanceledException)
            {
            }
        }

        public BatchingLogger CreateLogger()
        {
            return new BatchingLogger(this);
        }
    }
}