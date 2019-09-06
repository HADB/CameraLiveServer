using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Windows;
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

        private int _Type = 0;
        private int _Width = 1920;
        private int _Height = 1080;

        public ImageStreamingServer(int type, int width, int height)
        {
            _Clients = new List<Socket>();
            _Thread = null;
            _Type = type;
            _Width = width;
            _Height = height;

            if (type == 0)
            {
                var videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);

                var source = new VideoCaptureDevice(videoDevices[0].MonikerString);
                var videoCapabilities = source.VideoCapabilities;
                foreach (VideoCapabilities vc in videoCapabilities)
                {
                    if (vc.FrameSize.Width == width)
                    {
                        source.VideoResolution = vc;
                    }
                }
                source.NewFrame += (object sender, NewFrameEventArgs e) => NewFrameEventHandler(e);
                source.Start();
            }
        }

        private void NewFrameEventHandler(NewFrameEventArgs e)
        {
            using (var ms = new MemoryStream())
            {
                e.Frame.Save(ms, ImageFormat.Jpeg);
                buffer = ms.ToArray();
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
                Server.Listen(10);

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

                    if (_Type == 0)
                    {

                        while (true)
                        {
                            using (var ms = new MemoryStream(buffer))
                            {
                                mjpegWriter.Write(ms);
                            }
                        }
                    }
                    else
                    {
                        var screenSize = new Size((int)SystemParameters.PrimaryScreenWidth, (int)SystemParameters.PrimaryScreenHeight);
                        var srcImage = new Bitmap(screenSize.Width, screenSize.Height);
                        var srcGraphics = Graphics.FromImage(srcImage);

                        var dstImage = srcImage;
                        var dstGraphics = srcGraphics;

                        var needScale = _Width != screenSize.Width;

                        if (needScale)
                        {
                            dstImage = new Bitmap(_Width, _Height);
                            dstGraphics = Graphics.FromImage(dstImage);
                        }

                        var srcRect = new Rectangle(new Point(0, 0), screenSize);
                        var dstRect = new Rectangle(new Point(0, 0), new Size(_Width, _Height));


                        while (true)
                        {
                            using (var ms = new MemoryStream())
                            {
                                srcGraphics.CopyFromScreen(0, 0, 0, 0, screenSize);
                                if (needScale)
                                {
                                    dstGraphics.DrawImage(srcImage, dstRect, srcRect, GraphicsUnit.Pixel);
                                }
                                ms.SetLength(0);
                                dstImage.Save(ms, ImageFormat.Jpeg);
                                mjpegWriter.Write(ms);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex.Message, ex);
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
