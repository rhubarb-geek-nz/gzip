// Copyright (c) 2024 Roger Brown.
// Licensed under the MIT License.

#if NETCOREAPP
#else
using Microsoft.VisualStudio.TestTools.UnitTesting;
#endif
using System;
using System.Collections.Generic;
using System.IO;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Reflection;
using System.Text;

namespace RhubarbGeekNz.GZip
{
    [TestClass]
    public class UnitTests
    {
        readonly InitialSessionState initialSessionState = InitialSessionState.CreateDefault();
        public UnitTests()
        {
            foreach (Type t in new Type[] {
                typeof(ConvertToGZip),
                typeof(ConvertFromGZip)
            })
            {
                CmdletAttribute ca = t.GetCustomAttribute<CmdletAttribute>();

                if (ca == null) throw new NullReferenceException();

                initialSessionState.Commands.Add(new SessionStateCmdletEntry($"{ca.VerbName}-{ca.NounName}", t, ca.HelpUri));
            }

            initialSessionState.Variables.Add(new SessionStateVariableEntry("ErrorActionPreference", ActionPreference.Stop, "Stop action"));
        }

        [TestMethod]
        public void TestHelloWorld()
        {
            byte[][] input =
            {
                Encoding.ASCII.GetBytes("Hello"),
                Encoding.ASCII.GetBytes(" "),
                Encoding.ASCII.GetBytes("World"),
            };

            List<byte[]> compressed = new List<byte[]>();

            using (PowerShell powerShell = PowerShell.Create(initialSessionState))
            {
                PSDataCollection<object> inputPipeline = new PSDataCollection<object>();

                foreach (var i in input)
                {
                    inputPipeline.Add(i);
                }

                powerShell.AddCommand("ConvertTo-GZip");

                PSDataCollection<object> outputPipeline = powerShell.Invoke(inputPipeline);

                Append(compressed, outputPipeline);
            }

            List<byte[]> uncompressed = new List<byte[]>();

            using (PowerShell powerShell = PowerShell.Create(initialSessionState))
            {
                PSDataCollection<object> inputPipeline = new PSDataCollection<object>();

                foreach (var i in compressed)
                {
                    inputPipeline.Add(i);
                }

                powerShell.AddCommand("ConvertFrom-GZip");

                PSDataCollection<object> outputPipeline = powerShell.Invoke(inputPipeline);

                Append(uncompressed, outputPipeline);
            }

            MemoryStream memoryStream = new MemoryStream();

            foreach (var i in uncompressed)
            {
                memoryStream.Write(i, 0, i.Length);
            }

            string result = Encoding.ASCII.GetString(memoryStream.ToArray());

            Assert.AreEqual("Hello World", result);
        }

        [TestMethod]
        public void TestRandom()
        {
            byte[][] input = new byte[64][];
            Random random = new Random();

            for (int i = 0; i < input.Length; i++)
            {
                input[i] = new byte[4096];
                random.NextBytes(input[i]);
            }

            List<byte[]> compressed = new List<byte[]>();

            using (PowerShell powerShell = PowerShell.Create(initialSessionState))
            {
                PSDataCollection<object> inputPipeline = new PSDataCollection<object>();

                foreach (var i in input)
                {
                    inputPipeline.Add(i);
                }

                powerShell.AddCommand("ConvertTo-GZip");

                PSDataCollection<object> outputPipeline = powerShell.Invoke(inputPipeline);

                Append(compressed, outputPipeline);
            }

            List<byte[]> uncompressed = new List<byte[]>();

            using (PowerShell powerShell = PowerShell.Create(initialSessionState))
            {
                PSDataCollection<object> inputPipeline = new PSDataCollection<object>();

                foreach (var i in compressed)
                {
                    inputPipeline.Add(i);
                }

                powerShell.AddCommand("ConvertFrom-GZip");

                PSDataCollection<object> outputPipeline = powerShell.Invoke(inputPipeline);

                Append(uncompressed, outputPipeline);
            }

            MemoryStream memoryStream = new MemoryStream();

            foreach (var i in uncompressed)
            {
                memoryStream.Write(i, 0, i.Length);
            }

            byte[] result = memoryStream.ToArray();

            memoryStream = new MemoryStream();

            foreach (var i in input)
            {
                memoryStream.Write(i, 0, i.Length);
            }

            byte[] original = memoryStream.ToArray();

            for (int i = 0; i < original.Length; i++)
            {
                if (original[i] != result[i])
                {
                    Assert.AreEqual(original[i], result[i]);
                }
            }
        }

        [TestMethod]
        public void TestRandomPipeline()
        {
            byte[][] input = new byte[64][];
            Random random = new Random();

            for (int i = 0; i < input.Length; i++)
            {
                input[i] = new byte[4096];
                random.NextBytes(input[i]);
            }

            List<byte[]> uncompressed = new List<byte[]>();

            using (PowerShell powerShell = PowerShell.Create(initialSessionState))
            {
                PSDataCollection<object> inputPipeline = new PSDataCollection<object>();

                foreach (var i in input)
                {
                    inputPipeline.Add(i);
                }

                powerShell.AddCommand("ConvertTo-GZip").AddCommand("ConvertFrom-GZip");

                PSDataCollection<object> outputPipeline = powerShell.Invoke(inputPipeline);

                Append(uncompressed, outputPipeline);
            }

            MemoryStream memoryStream = new MemoryStream();

            foreach (var i in uncompressed)
            {
                memoryStream.Write(i, 0, i.Length);
            }

            byte[] result = memoryStream.ToArray();

            memoryStream = new MemoryStream();

            foreach (var i in input)
            {
                memoryStream.Write(i, 0, i.Length);
            }

            byte[] original = memoryStream.ToArray();

            for (int i = 0; i < original.Length; i++)
            {
                if (original[i] != result[i])
                {
                    Assert.AreEqual(original[i], result[i]);
                }
            }
        }

        [TestMethod]
        public void TestException()
        {
            bool caught = false;

            try
            {
                using (PowerShell powerShell = PowerShell.Create(initialSessionState))
                {
                    PSDataCollection<object> inputPipeline = new PSDataCollection<object>() { { new byte[4] } };

                    powerShell.AddCommand("ConvertFrom-GZip");

                    powerShell.Invoke(inputPipeline);
                }

            }
            catch (ActionPreferenceStopException)
            {
                caught = true;
            }

            Assert.IsTrue(caught, "should have thrown exception");
        }

        [TestMethod]
        public void TestEmptyArray()
        {
            List<byte[]> compressed = new List<byte[]>();

            using (PowerShell powerShell = PowerShell.Create(initialSessionState))
            {
                PSDataCollection<object> inputPipeline = new PSDataCollection<object>() { { new byte[0] } };

                powerShell.AddCommand("ConvertTo-GZip");

                PSDataCollection<object> outputPipeline = powerShell.Invoke(inputPipeline);

                Append(compressed, outputPipeline);
            }

            MemoryStream memoryStream = new MemoryStream();

            foreach (var i in compressed)
            {
                memoryStream.Write(i, 0, i.Length);
            }

            byte[] result = memoryStream.ToArray();

            Assert.AreEqual(20, result.Length);
        }


        [TestMethod]
        public void TestNoInput()
        {
            List<byte[]> compressed = new List<byte[]>();

            using (PowerShell powerShell = PowerShell.Create(initialSessionState))
            {
                PSDataCollection<object> inputPipeline = new PSDataCollection<object>() { null };

                powerShell.AddCommand("ConvertTo-GZip");

                PSDataCollection<object> outputPipeline = powerShell.Invoke(inputPipeline);

                Append(compressed, outputPipeline);
            }

            MemoryStream memoryStream = new MemoryStream();

            foreach (var i in compressed)
            {
                memoryStream.Write(i, 0, i.Length);
            }

            byte[] result = memoryStream.ToArray();

            Assert.AreEqual(20, result.Length);
        }

        [TestMethod]
        public void TestNoInputEndToEnd()
        {
            List<byte[]> uncompressed = new List<byte[]>();

            using (PowerShell powerShell = PowerShell.Create(initialSessionState))
            {
                PSDataCollection<object> inputPipeline = new PSDataCollection<object>() { null };

                powerShell.AddCommand("ConvertTo-GZip").AddCommand("ConvertFrom-GZip");

                PSDataCollection<object> outputPipeline = powerShell.Invoke(inputPipeline);

                Append(uncompressed, outputPipeline);
            }

            Assert.AreEqual(0, uncompressed.Count);
        }

        private void Append(List<byte[]> compressed, object o)
        {
            if (o is PSObject)
            {
                PSObject p = (PSObject)o;

                Append(compressed, p.BaseObject);
            }
            else
            {
                if (o is byte[])
                {
                    compressed.Add((byte[])o);
                }
                else
                {
                    IEnumerable<object> e = (IEnumerable<object>)o;

                    foreach (var i in e)
                    {
                        Append(compressed, i);
                    }
                }
            }
        }
    }
}
