// Copyright (c) 2024 Roger Brown.
// Licensed under the MIT License.

using System.IO.Compression;

namespace RhubarbGeekNz.GZip
{
    internal class EmptyStreamMonitor : Stream
    {
        private readonly Stream stream;
        internal bool isEmpty = true;

        internal EmptyStreamMonitor(Stream stream)
        {
            this.stream = stream;
        }

        public override bool CanRead => stream.CanRead;

        public override bool CanSeek => stream.CanSeek;

        public override bool CanWrite => stream.CanWrite;

        public override long Length => stream.Length;

        public override long Position
        {
            get => stream.Position;
            set { stream.Position = value; }
        }

        public override void Flush() => stream.Flush();

        public override int Read(byte[] buffer, int offset, int count) => stream.Read(buffer, offset, count);

        public override long Seek(long offset, SeekOrigin origin) => stream.Seek(offset, origin);

        public override void SetLength(long value) => stream.SetLength(value);

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (isEmpty && count > 0)
            {
                isEmpty = false;
            }

            stream.Write(buffer, offset, count);
        }
    }

    internal class Tool
    {
        private CompressionMode mode;
        static readonly string DOT_GZ = ".gz";

        internal Tool(CompressionMode m)
        {
            mode = m;
        }

        internal void Run(string[] args)
        {
            bool anyFiles = false;

            foreach (string arg in args)
            {
                bool isFilename = true;

                switch (arg)
                {
                    case "-d":
                    case "--decompress":
                    case "--uncompress":
                        if (mode != CompressionMode.Decompress)
                        {
                            mode = CompressionMode.Decompress;
                            isFilename = false;
                        }
                        break;
                }

                if (isFilename)
                {
                    anyFiles = true;
                    string deleteFile = null;

                    try
                    {
                        if ((mode == CompressionMode.Decompress) ^ arg.EndsWith(DOT_GZ))
                        {
                            throw new Exception($"File {arg} has wrong extension for {mode}");
                        }

                        using (var inputStream = File.OpenRead(arg))
                        {
                            string target = mode == CompressionMode.Decompress ? arg.Substring(0, arg.Length - DOT_GZ.Length) : arg + DOT_GZ;

                            using (var outputStream = File.OpenWrite(target))
                            {
                                deleteFile = target;

                                CopyTo(inputStream, outputStream);
                            }
                        }

                        deleteFile = arg;
                    }
                    finally
                    {
                        if (deleteFile != null)
                        {
                            File.Delete(deleteFile);
                        }
                    }
                }
            }

            if (!anyFiles)
            {
                using (var inputStream = Console.OpenStandardInput())
                {
                    using (var outputStream = Console.OpenStandardOutput())
                    {
                        CopyTo(inputStream, outputStream);
                    }
                }
            }
        }

        internal void CopyTo(Stream inputStream, Stream outputStream)
        {
            if (mode == CompressionMode.Compress)
            {
                var empty = new EmptyStreamMonitor(outputStream);

                using (var gzipStream = new GZipStream(empty, mode))
                {
                    inputStream.CopyTo(gzipStream);

                    if (empty.isEmpty)
                    {
                        outputStream.Write(Constants.EmptyEncoding);
                    }
                }
            }
            else
            {
                using (var gzipStream = new GZipStream(inputStream, mode))
                {
                    gzipStream.CopyTo(outputStream);
                }
            }
        }
    }
}
