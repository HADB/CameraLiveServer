using System;
using CameraLiveServer;

namespace CameraLiveServer
{
    class Program
    {
        static void Main(string[] args)
        {
            var server = new ImageStreamingServer(1280, 960);
            server.Start(8888);
            Console.ReadLine();
        }
    }
}
