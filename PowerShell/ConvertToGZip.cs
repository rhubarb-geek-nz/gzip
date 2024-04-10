// Copyright (c) 2024 Roger Brown.
// Licensed under the MIT License.

using System;
using System.IO;
using System.IO.Compression;
using System.Management.Automation;

namespace RhubarbGeekNz.GZip
{
    internal class PSCmdletStream : Stream
    {
        readonly ConvertToGZip cmdlet;

        internal PSCmdletStream(ConvertToGZip cmdlet)
        {
            this.cmdlet = cmdlet;
        }

        public override bool CanRead => throw new NotImplementedException();

        public override bool CanSeek => throw new NotImplementedException();

        public override bool CanWrite => true;

        public override long Length => throw new NotImplementedException();

        public override long Position { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (count > 0)
            {
                cmdlet.isEmpty = false;
                byte[] buf = new byte[count];
                Buffer.BlockCopy(buffer, offset, buf, 0, buf.Length);
                cmdlet.WriteObject(buf);
            }
        }
    }

    [Cmdlet(VerbsData.ConvertTo, "GZip")]
    [OutputType(typeof(byte[]))]
    sealed public class ConvertToGZip : PSCmdlet
    {
        [Parameter(Mandatory = true, ValueFromPipeline = true, HelpMessage = "Plain text data to be compressed"), AllowNull, AllowEmptyCollection]
        public byte[] Value;
        private GZipStream gzipStream;
        internal bool isEmpty = true;

        protected override void BeginProcessing()
        {
            gzipStream = new GZipStream(new PSCmdletStream(this), CompressionMode.Compress);
        }

        protected override void ProcessRecord()
        {
            if (Value != null && Value.Length > 0)
            {
                gzipStream.Write(Value, 0, Value.Length);
            }
        }

        protected override void EndProcessing()
        {
            using (IDisposable disposable = gzipStream)
            {
                gzipStream = null;
            }

            if (isEmpty)
            {
                byte[] buf = new byte[Constants.EmptyEncoding.Length];
                Buffer.BlockCopy(Constants.EmptyEncoding, 0, buf, 0, buf.Length);
                WriteObject(buf);
            }
        }
    }
}
