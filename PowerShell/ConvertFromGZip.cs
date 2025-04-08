// Copyright (c) 2024 Roger Brown.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Management.Automation;
using System.Threading;

namespace RhubarbGeekNz.GZip
{
    internal class PipeState : IDisposable
    {
        internal AutoResetEvent writeEvent = new AutoResetEvent(false), readEvent = new AutoResetEvent(false);
        internal readonly List<byte[]> readList = new List<byte[]>();
        internal int readOffset;
        internal List<byte[]> writeList = new List<byte[]>();
        internal bool readClosed, readBlocked;
        internal Exception exception;

        public void Dispose()
        {
            writeEvent.Dispose();
            readEvent.Dispose();
        }

        internal bool IsBlocked()
        {
            bool value;

            lock (this)
            {
                value = readBlocked;
            }

            return value;
        }
    }

    internal class PipeWriter : Stream
    {
        readonly PipeState state;

        internal PipeWriter(PipeState pipe)
        {
            state = pipe;
        }

        public override bool CanRead => false;

        public override bool CanSeek => throw new NotImplementedException();

        public override bool CanWrite => true;

        public override long Length => throw new NotImplementedException();

        public override long Position { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public override void Flush()
        {
            throw new NotImplementedException();
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
            lock (state)
            {
                byte[] ba = new byte[count];
                Buffer.BlockCopy(buffer, offset, ba, 0, count);
                state.writeList.Add(ba);
            }
        }
    }

    internal class PipeReader : Stream
    {
        readonly PipeState state;

        internal PipeReader(PipeState pipe)
        {
            state = pipe;
        }

        public override bool CanRead => true;

        public override bool CanSeek => throw new NotImplementedException();

        public override bool CanWrite => throw new NotImplementedException();

        public override long Length => throw new NotImplementedException();

        public override long Position { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public override void Flush()
        {
            throw new NotImplementedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int result = 0;

            while (count > 0)
            {
                bool readBlocked = false;

                lock (state)
                {
                    state.readBlocked = false;

                    while (count > 0)
                    {
                        if (state.readList.Count == 0)
                        {
                            if (result == 0)
                            {
                                state.readBlocked = true;
                                readBlocked = true;
                            }
                            else
                            {
                                count = 0;
                            }

                            break;
                        }
                        else
                        {
                            byte[] ba = state.readList[0];
                            int length = ba.Length - state.readOffset;

                            if (length > count)
                            {
                                length = count;
                            }

                            Buffer.BlockCopy(ba, state.readOffset, buffer, offset, length);

                            state.readOffset += length;
                            offset += length;
                            result += length;
                            count -= length;

                            if (state.readOffset == ba.Length)
                            {
                                state.readOffset = 0;
                                state.readList.Remove(ba);
                            }
                        }
                    }
                }

                if (readBlocked)
                {
                    if (state.readClosed)
                    {
                        break;
                    }

                    state.writeEvent.Set();
                    state.readEvent.WaitOne();
                }
            }

            return result;
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
            throw new NotImplementedException();
        }
    }

    internal class PipeTask
    {
        readonly PipeState state;

        internal PipeTask(PipeState pipe)
        {
            state = pipe;
        }

        internal void Run()
        {
            Exception exception = null;

            try
            {
                try
                {
                    using (var gzip = new GZipStream(new PipeReader(state), CompressionMode.Decompress))
                    {
                        using (var write = new PipeWriter(state))
                        {
                            gzip.CopyTo(write);
                        }
                    }
                }
                catch (Exception ex)
                {
                    exception = ex;
                }
            }
            finally
            {
                lock (state)
                {
                    state.exception = exception;
                    state.readBlocked = true;
                    state.readClosed = true;
                    state.writeEvent.Set();
                }
            }
        }
    }

    [Cmdlet(VerbsData.ConvertFrom, "GZip")]
    [OutputType(typeof(byte[]))]
    sealed public class ConvertFromGZip : PSCmdlet, IDisposable
    {
        [Parameter(Mandatory = true, ValueFromPipeline = true, HelpMessage = "GZip data to be decoded"), AllowNull, AllowEmptyCollection]
        public byte[] Value;
        PipeState state;
        Thread thread;
        private const string ERROR_ID_DATA = "DataError";
        private const string ERROR_ID_STATE = "StateError";

        protected override void BeginProcessing()
        {
            state = new PipeState();
            thread = new Thread(() => new PipeTask(state).Run());
            thread.Start();
        }

        protected override void ProcessRecord()
        {
            if (state == null)
            {
                WriteError(new ErrorRecord(new Exception("State Error"), ERROR_ID_STATE, ErrorCategory.ObjectNotFound, null));
            }
            else
            {
                if (Value != null && Value.Length > 0)
                {
                    lock (state)
                    {
                        if (state.readClosed)
                        {
                            WriteError(new ErrorRecord(new Exception("Data Error"), ERROR_ID_DATA, ErrorCategory.InvalidData, null));
                        }
                        else
                        {
                            state.readList.Add(Value);
                            state.readEvent.Set();
                        }
                    }
                }

                while (!state.IsBlocked())
                {
                    state.writeEvent.WaitOne();
                }

                lock (state)
                {
                    foreach (byte[] ba in state.writeList)
                    {
                        WriteObject(ba);
                    }

                    state.writeList.Clear();

                    Exception exception = state.exception;
                    state.exception = null;

                    if (exception != null)
                    {
                        WriteError(new ErrorRecord(exception, ERROR_ID_DATA, ErrorCategory.InvalidData, null));
                    }
                }
            }
        }

        protected override void EndProcessing()
        {
            using (PipeState state = this.state)
            {
                this.state = null;

                if (state != null)
                {
                    lock (state)
                    {
                        state.readClosed = true;
                        state.readEvent.Set();
                    }
                }

                Thread thread = this.thread;
                this.thread = null;

                if (thread != null)
                {
                    thread.Join();
                }

                if (state != null)
                {
                    List<byte[]> writeList = state.writeList;
                    state.writeList = null;

                    foreach (byte[] ba in writeList)
                    {
                        WriteObject(ba);
                    }

                    Exception exception = state.exception;
                    state.exception = null;

                    if (exception != null)
                    {
                        WriteError(new ErrorRecord(exception, ERROR_ID_DATA, ErrorCategory.InvalidData, null));
                    }
                }
            }
        }

        public void Dispose()
        {
            using (PipeState state = this.state)
            {
                this.state = null;

                if (state != null)
                {
                    lock (state)
                    {
                        state.readClosed = true;
                        state.readList.Clear();
                        state.readEvent.Set();
                    }
                }

                Thread thread = this.thread;
                this.thread = null;

                if (thread != null)
                {
                    thread.Join();
                }
            }
        }
    }
}
