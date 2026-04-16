using Common.Infrastructure.Tasks;
using Common.Interfaces;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Composition;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Business.DomainEvents
{
    [Export(typeof(IASPSMessagePublisher)), Shared]
    [Export(typeof(IBackgroundTask))]
    internal class DomainMessagePublisher : IASPSMessagePublisher, IBackgroundTask
    {
        private readonly BlockingCollection<IASPSMessage> messages = new BlockingCollection<IASPSMessage>();

        private readonly Dictionary<Type, IASPSMessageHandler> handlers;
        private Task worker;
        private CancellationTokenSource cancellationTokenSource;

        [ImportingConstructor]
        public DomainMessagePublisher([ImportMany]IEnumerable<IASPSMessageHandler> handlers)
        {
            this.handlers = new Dictionary<Type, IASPSMessageHandler>();
            foreach (var h in handlers)
            {
                foreach (var t in h.GetHandleableMessageTypes())
                {
                    this.handlers.Add(t, h);
                }
            }
        }

        public void Start()
        {
            this.cancellationTokenSource = new CancellationTokenSource();
            this.worker = LongRunningTask.Run(this.DoWork);
        }

        public void Stop()
        {
            this.cancellationTokenSource.Cancel();
            this.worker.Wait(1000);
            this.messages.Dispose();
        }

        public void Publish(IASPSMessage message)
        {
            this.messages.Add(message);
            
        }


        public void DoWork()
        {
            while (!this.cancellationTokenSource.IsCancellationRequested)
            {
                try
                {
                    IASPSMessage message = this.messages.Take(this.cancellationTokenSource.Token);
                    if (this.handlers.TryGetValue(message.GetType(), out IASPSMessageHandler handler))
                    {
                        try
                        {
                            handler.Handle(message);
                        }
                        catch (Exception x)
                        {
                            NLog.LogManager.GetCurrentClassLogger().Error(x, "Domain message handler failed.");
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }
}
