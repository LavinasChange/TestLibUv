using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Transport.Libuv;
using Microsoft.AspNetCore.Server.Kestrel.Transport.Libuv.Internal;
using Microsoft.AspNetCore.Server.Kestrel.Transport.Libuv.Internal.Networking;
using Microsoft.Extensions.Logging;
using System;
using System.IO.Pipelines;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace TestLibUV
{
  class Program
  {
    public static LibuvFunctions libUv = new LibuvFunctions();

    public static string ByteArrayToString(byte[] ba) => BitConverter.ToString(ba).Replace("-", "");

    static void Main(string[] args)
    {
      Console.WriteLine("Main: {0}", Thread.CurrentThread.ManagedThreadId);

      // Use the factory
      LibuvTransportContext transport = new LibuvTransportContext
      {
        Options = new LibuvTransportOptions(),
        AppLifetime = new ApplicationLifetime(null),
        Log = new LibuvTrace(LoggerFactory.Create(builder =>
        {
          builder.AddConsole();
        }).CreateLogger("core"))
      };

      LibuvThread uvThread = new LibuvThread(libUv, transport);
      uvThread.StartAsync().Wait();

      Task.Run(async () =>
      {
        LibuvTransportContext transport = new LibuvTransportContext
        {
          Options = new LibuvTransportOptions(),
          AppLifetime = new ApplicationLifetime(null),
          Log = new LibuvTrace(LoggerFactory.Create(builder =>
          {
            builder.AddConsole();
          }).CreateLogger("core"))
        };

        LibuvConnectionListener listener = new LibuvConnectionListener(libUv, transport, new IPEndPoint(IPAddress.Parse("127.0.0.1"), 2593));
        Console.WriteLine("Binding...");
        await listener.BindAsync();

        Console.WriteLine("Listening...");
        ConnectionContext connectionContext = await listener.AcceptAsync();
        Console.WriteLine("Accepted Connection from {0}", connectionContext.RemoteEndPoint);
        PipeReader reader = connectionContext.Transport.Input;

        while (true)
        {
          ReadResult readResult = await reader.ReadAsync();
          if (readResult.Buffer.IsSingleSegment)
            Console.WriteLine(ByteArrayToString(readResult.Buffer.First.ToArray()));
          else foreach (var segment in readResult.Buffer)
              Console.WriteLine(ByteArrayToString(segment.ToArray()));
        }
      });

      // Manually putting something on the queue from another thread (or the main thread)
      uvThread.PostAsync<object>(_ =>
      {
        Console.WriteLine("Stuff ({0})", Thread.CurrentThread.ManagedThreadId);
      }, null);

      // Send an Initialization Request for Timers
      uvThread.PostAsync<object>(_ =>
      {
        UvTimerHandle timerHandle = new UvTimerHandle(null);
        timerHandle.Init(uvThread.Loop, (callback, handle) =>
        {
          Console.WriteLine("Closed ({0})", Thread.CurrentThread.ManagedThreadId);
        });

        Console.WriteLine("Timer Stuff {0}", Thread.CurrentThread.ManagedThreadId);
        int count = 10;

        void cb2(UvTimerHandle handle)
        {
          Console.WriteLine("Called!2 {0} ({1})", DateTime.Now, Thread.CurrentThread.ManagedThreadId);
          timerHandle.Start(cb2, 2000, 0);
        }

        void cb1(UvTimerHandle handle)
        {
          Console.WriteLine("Called!1 {0} ({1})", DateTime.Now, Thread.CurrentThread.ManagedThreadId);
          count--;
          if (count < 0)
            timerHandle.Start(cb2, 2000, 0);
          else
            timerHandle.Start(cb1, 1000, 0);
        }

        timerHandle.Start(cb1, 1000, 0);
      }, null);

      Console.ReadLine();
    }
  }
}
