using System;
using CameraLiveServer;

namespace CameraLiveServer
{
    class Program
    {
        static void Main(string[] args)
        {
            var server = new ImageStreamingServer(0, 1280, 720);
            server.Start(8888);
            Console.ReadLine();
        }
    }
}
