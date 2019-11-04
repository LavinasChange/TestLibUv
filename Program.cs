using Microsoft.AspNetCore.Server.Kestrel.Transport.Libuv.Internal.Networking;
using System;

namespace TestLibUV
{
    class Program
    {
        public static LibuvFunctions libUv = new LibuvFunctions();
        static void Main(string[] args)
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
