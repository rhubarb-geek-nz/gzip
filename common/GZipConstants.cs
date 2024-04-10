// Copyright (c) 2024 Roger Brown.
// Licensed under the MIT License.

using System.Runtime.InteropServices;

namespace RhubarbGeekNz.GZip
{
	internal class Constants
	{
		internal static readonly byte[] EmptyEncoding = {
			0x1f, 0x8b, 0x08, 0x00,
			0x00, 0x00, 0x00, 0x00,
			0x00,
			RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? (byte)0 : (byte)0x03,
			0x03, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
		};
	}
}
