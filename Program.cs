using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Transport.Libuv;
using Microsoft.AspNetCore.Server.Kestrel.Transport.Libuv.Internal;
using Microsoft.AspNetCore.Server.Kestrel.Transport.Libuv.Internal.Networking;
using Microsoft.Extensions.Logging;
using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Net;
using System.Threading.Tasks;

namespace TestLibUV
{
    class Program
    {
        public static LibuvFunctions libUv = new LibuvFunctions();

        public static string ByteArrayToString(byte[] ba) => BitConverter.ToString(ba).Replace("-", "");

        static void Main(string[] args)
        {
            UvLoopHandle loopHandle = new UvLoopHandle(null);
            loopHandle.Init(libUv);

            Task.Run(async () =>
            {
                LibuvTransportContext transport = new LibuvTransportContext()
                {
                    Options = new LibuvTransportOptions(),
                    AppLifetime = new ApplicationLifetime(null),
                    Log = new LibuvTrace(LoggerFactory.Create(builder =>
                    {
                        builder.AddConsole();
                    }).CreateLogger("core")),
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

            Console.ReadLine();
        }

        static void Main2(string[] args)
        {
            UvLoopHandle loopHandle = new UvLoopHandle(null);
            loopHandle.Init(libUv);

            UvTimerHandle timerHandle = new UvTimerHandle(null);
            timerHandle.Init(loopHandle, (callback, handle) =>
            {
                Console.WriteLine("Closed");
            });

            int count = 10;

            void cb2(UvTimerHandle handle)
            {
                Console.WriteLine("Called!2 {0}", DateTime.Now);
                timerHandle.Start(cb2, 2000, 0);
            }

            void cb1(UvTimerHandle handle)
            {
                Console.WriteLine("Called!1 {0}", DateTime.Now);
                count--;
                if (count < 0)
                    timerHandle.Start(cb2, 2000, 0);
                else
                    timerHandle.Start(cb1, 1000, 0);
            }

            timerHandle.Start(cb1, 1000, 0);

            /*bool sw = true;*/

            while (true)
            {
                loopHandle.Run(1);
/*                if (sw && count >= 10)
                {
                    Console.WriteLine("Switching!");
                    sw = false;
                    timerHandle.Start((handle) =>
                    {
                        Console.WriteLine("Called!2");
                    }, 10000, 10000);
                }*/
            }
        }
    }
}
