using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;

namespace CameraLiveServer
{
    /// <summary>
    /// Provides a stream writer that can be used to write images as MJPEG 
    /// or (Motion JPEG) to any stream.
    /// </summary>
    public class MjpegWriter : IDisposable
    {
        public MjpegWriter(Stream stream) : this(stream, "--boundary") { }

        public MjpegWriter(Stream stream, string boundary)
        {
            this.Stream = stream;
            this.Boundary = boundary;
        }

        public string Boundary { get; private set; }

        public Stream Stream { get; private set; }

        public void WriteHeader()
        {
            Write("HTTP/1.1 200 OK\r\n" +
                  "Content-Type: multipart/x-mixed-replace; boundary=" +
                  this.Boundary +
                  "\r\n");
            this.Stream.Flush();
        }

        public void Write(MemoryStream imageStream)
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine();
            sb.AppendLine(this.Boundary);
            sb.AppendLine("Content-Type: image/jpeg");
            sb.AppendLine("Content-Length: " + imageStream.Length.ToString());
            sb.AppendLine();

            Write(sb.ToString());
            imageStream.WriteTo(this.Stream);
            Write("\r\n");

            this.Stream.Flush();
        }

        private void Write(string text)
        {
            byte[] data = BytesOf(text);
            this.Stream.Write(data, 0, data.Length);
        }

        private static byte[] BytesOf(string text)
        {
            return Encoding.ASCII.GetBytes(text);
        }

        #region IDisposable Members

        public void Dispose()
        {
            try
            {
                if (this.Stream != null)
                {
                    this.Stream.Dispose();
                }
            }
            finally
            {
                this.Stream = null;
            }
        }

        #endregion
    }
}
