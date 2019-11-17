using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Transport.Libuv;
using Microsoft.AspNetCore.Server.Kestrel.Transport.Libuv.Internal;
using Microsoft.AspNetCore.Server.Kestrel.Transport.Libuv.Internal.Networking;
using Microsoft.Extensions.Logging;
using System;
using System.Buffers;
using System.Buffers.Binary;
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
        Console.WriteLine("NIO: {0}", Thread.CurrentThread.ManagedThreadId);

        LibuvConnectionListener listener = new LibuvConnectionListener(libUv, transport, new IPEndPoint(IPAddress.Parse("0.0.0.0"), 2593));
        Console.WriteLine("Binding... ({0})", Thread.CurrentThread.ManagedThreadId);
        await listener.BindAsync();

        Console.WriteLine("Listening... ({0})", Thread.CurrentThread.ManagedThreadId);
        ConnectionContext connectionContext = await listener.AcceptAsync();
        Console.WriteLine("Accepted Connection from {0}", connectionContext.RemoteEndPoint);
        PipeReader reader = connectionContext.Transport.Input;

        while (true)
        {
          ReadResult readResult = await reader.ReadAsync();
          int packetId = readResult.Buffer.FirstSpan[0];
          Console.WriteLine($"[0x{packetId:X}] Processing Packet");
          int length = PacketLengths[packetId];
          int bodyLength = length - 1;
          int bodyStart = 1;
          if (length == 0)
          {
            length = BinaryPrimitives.ReadUInt16BigEndian(readResult.Buffer.FirstSpan.Slice(1, 2));
            bodyLength = length - 3;
            bodyStart = 3;
          }
          else if (length == 0xFFFF)
          {
            Console.WriteLine($"[0x{packetId:X}] Unknown Packet");
            throw new Exception($"[0x{packetId:X}] Unknown Packet");
          }
          Console.WriteLine($"[0x{packetId:X}] Found length {length}");
          Console.WriteLine($"Packet Data: {ByteArrayToString(readResult.Buffer.FirstSpan.ToArray()):X}");
          Memory<byte> mem = new Memory<byte>(readResult.Buffer.FirstSpan.Slice(bodyStart, bodyLength).ToArray());
          Console.WriteLine($"[0x{packetId:X}] Buffer length {mem.Length}");

          _ = uvThread.PostAsync((Tuple<ConnectionContext, Memory<byte>> t) =>
          {
            // stuff
            var (conn, mem) = t;
            // Do stuff wtih memOwner.Memory.Span;
            Console.WriteLine($"Packet ID: 0x{packetId:X} - Length: {length} - Data: 0x{ByteArrayToString(mem.ToArray())}");
          }, Tuple.Create(connectionContext, mem));

          reader.AdvanceTo(readResult.Buffer.GetPosition(length));
        }
      });

      // Manually putting something on the queue from another thread (or the main thread)
      uvThread.PostAsync<object>(_ =>
      {
        Console.WriteLine("Game: {0}", Thread.CurrentThread.ManagedThreadId);
      }, null);

      // Send an Initialization Request for Timers
      /*
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
      */

      Console.ReadLine();
    }

    public static int[] PacketLengths = {
              0x0068, 0x0005, 0x0007, 0x0000, 0x0002, 0x0005, 0x0005, 0x0007, // 0x00
              0x000e, 0x0005, 0x0007, 0x0007, 0x0000, 0x0003, 0x0000, 0x003d, // 0x08
              0x00d7, 0x0000, 0x0000, 0x000a, 0x0006, 0x0009, 0x0001, 0x0000, // 0x10
              0x0000, 0x0000, 0x0000, 0x0025, 0x0000, 0x0005, 0x0004, 0x0008, // 0x18
              0x0013, 0x0008, 0x0003, 0x001a, 0x0007, 0x0014, 0x0005, 0x0002, // 0x20
              0x0005, 0x0001, 0x0005, 0x0002, 0x0002, 0x0011, 0x000f, 0x000a, // 0x28
              0x0005, 0x0001, 0x0002, 0x0002, 0x000a, 0x028d, 0x0000, 0x0008, // 0x30
              0x0007, 0x0009, 0x0000, 0x0000, 0x0000, 0x0002, 0x0025, 0x0000, // 0x38
              0x00c9, 0x0000, 0x0000, 0x0229, 0x02c9, 0x0005, 0x0000, 0x000b, // 0x40
              0x0049, 0x005d, 0x0005, 0x0009, 0x0000, 0x0000, 0x0006, 0x0002, // 0x48
              0x0000, 0x0000, 0x0000, 0x0002, 0x000c, 0x0001, 0x000b, 0x006e, // 0x50
              0x006a, 0x0000, 0x0000, 0x0004, 0x0002, 0x0049, 0x0000, 0x0031, // 0x58
              0x0005, 0x0009, 0x000f, 0x000d, 0x0001, 0x0004, 0x0000, 0x0015, // 0x60
              0x0000, 0x0000, 0x0003, 0x0009, 0x0013, 0x0003, 0x000e, 0x0000, // 0x68
              0x001c, 0x0000, 0x0005, 0x0002, 0x0000, 0x0023, 0x0010, 0x0011, // 0x70
              0x0000, 0x0009, 0x0000, 0x0002, 0x0000, 0x000d, 0x0002, 0x0000, // 0x78
              0x003e, 0x0000, 0x0002, 0x0027, 0x0045, 0x0002, 0x0000, 0x0000, // 0x80
              0x0042, 0x0000, 0x0000, 0x0000, 0x000b, 0x0000, 0x0000, 0x0000, // 0x88
              0x0013, 0x0041, 0x0000, 0x0063, 0x0000, 0x0009, 0x0000, 0x0002, // 0x90
              0x0000, 0x001a, 0x0000, 0x0102, 0x0135, 0x0033, 0x0000, 0x0000, // 0x98
              0x0003, 0x0009, 0x0009, 0x0009, 0x0095, 0x0000, 0x0000, 0x0004, // 0xA0
              0x0000, 0x0000, 0x0005, 0x0000, 0x0000, 0x0000, 0x0000, 0x000d, // 0xA8
              0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0040, 0x0009, 0x0000, // 0xB0
              0x0000, 0x0003, 0x0006, 0x0009, 0x0003, 0x0000, 0x0000, 0x0000, // 0xB8
              0x0024, 0x0000, 0x0000, 0x0000, 0x0006, 0x00cb, 0x0001, 0x0031, // 0xC0
              0x0002, 0x0006, 0x0006, 0x0007, 0x0000, 0x0001, 0x0000, 0x004e, // 0xC8
              0x0000, 0x0002, 0x0019, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, // 0xD0
              0x0000, 0x010C, 0xFFFF, 0xFFFF, 0x0009, 0x0000, 0xFFFF, 0xFFFF, // 0xD8
              0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, // 0xE0
              0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0x0015, // 0xE8
              0x0000, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, // 0xF0
              0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, // 0xF8
        };
  }
}