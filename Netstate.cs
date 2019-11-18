using System;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Transport.Libuv;
using Microsoft.AspNetCore.Server.Kestrel.Transport.Libuv.Internal;
using Microsoft.AspNetCore.Server.Kestrel.Transport.Libuv.Internal.Networking;
using System.Buffers;
using System.Buffers.Binary;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace Tela
{
  public class NetState
  {
    public static int Count { get; private set; }
    public Task ReceiveTask { get; private set; }
    public ConnectionContext Connection { get; private set; }

    public NetState(ConnectionContext connection)
    {
      Connection = connection;
      Console.WriteLine("New NetState for {0}", Connection.RemoteEndPoint);
    }

    public static Task StartAsync(ConnectionContext connection)
    {
      NetState ns = new NetState(connection);
      ns.ReceiveTask = Task.Run(async () =>
      {
        Console.WriteLine("NetState reader... ({0})", Thread.CurrentThread.ManagedThreadId);
        PipeReader reader = connection.Transport.Input;

        while (true)
        {
          ReadResult readResult = await reader.ReadAsync();
          int packetId = readResult.Buffer.FirstSpan[0];
          Console.WriteLine($"[0x{packetId:X}] Processing Packet");
          int length = PacketHandlers.PacketLengths[packetId];
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

          ReadOnlySequence<byte> seq = new ReadOnlySequence<byte>(readResult.Buffer.FirstSpan.Slice(bodyStart, bodyLength).ToArray());
          _ = Program.UvThread.PostAsync((Tuple<ConnectionContext, int, ReadOnlySequence<byte>> t) =>
          {
            var (conn, packetId, mem) = t;
            PacketHandlers.GetHandler(packetId).OnReceive(ns, new PacketReader(seq));
          }, Tuple.Create(connection, packetId, seq));

          reader.AdvanceTo(readResult.Buffer.GetPosition(length));
        }
      });
      Count += 1;
      return ns.ReceiveTask;
    }

    public void Send(ReadOnlyMemory<byte> data)
    {
      _ = Connection.Transport.Output.WriteAsync(data);
    }

    public void Dispose()
    {
      ReceiveTask.Dispose();
      Connection.Abort();
      Count -= 1;
    }
  }
}
