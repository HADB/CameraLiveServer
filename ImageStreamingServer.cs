using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Windows;
using AForge.Imaging.Filters;
using AForge.Video;
using AForge.Video.DirectShow;
using log4net;

namespace CameraLiveServer
{
    public class ImageStreamingServer : IDisposable
    {
        private ILog Logger = LogManager.GetLogger("CameraLiveServer");
        private List<Socket> _Clients;
        private Thread _Thread;

        private byte[] buffer;

        private int _Width = 1920;
        private int _Height = 1080;
        private bool hasNewFrame = false;
        private DateTime lastFrameTime = DateTime.Now;
        private DateTime lastFpsTime = DateTime.Now;
        private int fps = 0;

        public ImageStreamingServer(int width, int height)
        {
            _Clients = new List<Socket>();
            _Thread = null;
            _Width = width;
            _Height = height;
            var videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);

            var source = new VideoCaptureDevice(videoDevices[0].MonikerString);
            var videoCapabilities = source.VideoCapabilities;
            foreach (VideoCapabilities vc in videoCapabilities)
            {
                if (vc.FrameSize.Width == width && vc.FrameSize.Height == height)
                {
                    source.VideoResolution = vc;
                }
            }
            source.NewFrame += (object sender, NewFrameEventArgs e) => NewFrameEventHandler(e);
            source.Start();
        }

        private void NewFrameEventHandler(NewFrameEventArgs e)
        {
            if (DateTime.Now - lastFrameTime > TimeSpan.FromMilliseconds(25))
            {
                using (var ms = new MemoryStream())
                {
                    var mirror = new Mirror(false, true);
                    mirror.ApplyInPlace(e.Frame);
                    e.Frame.Save(ms, ImageFormat.Jpeg);
                    buffer = ms.ToArray();
                    hasNewFrame = true;
                    lastFrameTime = DateTime.Now;
                }
            }
        }

        public bool IsRunning { get { return (_Thread != null && _Thread.IsAlive); } }

        public void Start(int port)
        {

            lock (this)
            {
                _Thread = new Thread(new ParameterizedThreadStart(ServerThread));
                _Thread.IsBackground = true;
                _Thread.Start(port);
            }
        }

        public void Stop()
        {

            if (IsRunning)
            {
                try
                {
                    _Thread.Join();
                    _Thread.Abort();
                }
                finally
                {

                    lock (_Clients)
                    {
                        foreach (var s in _Clients)
                        {
                            try
                            {
                                s.Close();
                            }
                            catch { }
                        }
                        _Clients.Clear();
                    }
                    _Thread = null;
                }
            }
        }

        private void ServerThread(object state)
        {

            try
            {
                Socket Server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                Server.Bind(new IPEndPoint(IPAddress.Any, (int)state));
                Server.Listen(100);

                Logger.Info(string.Format("Server started on port {0}.", state));

                foreach (Socket client in Server.IncommingConnectoins())
                {
                    ThreadPool.QueueUserWorkItem(new WaitCallback(ClientThread), client);
                }

            }
            catch (Exception ex)
            {
                Logger.Error(ex.Message, ex);
            }

            Stop();
        }

        private void ClientThread(object client)
        {
            var socket = (Socket)client;

            Logger.Info(string.Format("New client from {0}", socket.RemoteEndPoint.ToString()));

            lock (_Clients)
            {
                _Clients.Add(socket);
            }

            try
            {
                using (var mjpegWriter = new MjpegWriter(new NetworkStream(socket, true)))
                {
                    mjpegWriter.WriteHeader();
                    while (true)
                    {
                        if (hasNewFrame)
                        {
                            using (var ms = new MemoryStream(buffer))
                            {
                                mjpegWriter.Write(ms);
                                hasNewFrame = false;
                                if (DateTime.Now - lastFpsTime < TimeSpan.FromSeconds(1))
                                {
                                    fps += 1;
                                }
                                else
                                {
                                    lastFpsTime = DateTime.Now;
                                    Logger.Info("FPS: " + fps);
                                    fps = 0;
                                }
                            }
                        }

                        Thread.Sleep(10);
                    }

                }
            }
            catch (Exception ex)
            {
                // Logger.Error(ex.Message, ex);
            }
            finally
            {
                lock (_Clients)
                {
                    _Clients.Remove(socket);
                }
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
