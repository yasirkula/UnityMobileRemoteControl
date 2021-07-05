// This is the source code of SystemScreenshotPlugin.dll. Before deciding to compile this DLL, I've tried the following steps:
// - I've copied System.Drawing.dll from "C:\Windows\Microsoft.NET\Framework64\v2.0.50727" to this Unity project. It caused the following error:
//   'error CS1703: Multiple assemblies with equivalent identity have been imported'
// - I've copied System.Drawing.dll from Unity's installment directory. It caused the following issues:
//   a) System.Drawing.Bitmap wasn't recognized in Unity when target platform is set to anything other than Standalone Windows (I'm not talking
//      about a build error, I'm talking about using System.Drawing.Bitmap while working inside Unity editor)
//   b) In an unknown rare circumstance, Graphics.CopyFromScreen thrown 'A null reference or invalid value was found [GDI+ status: InvalidParameter]'
//      which seems to be Mono related (my assumption is that System.Drawing.dll inside Unity's installment directory is a Mono library)
// 
// Thus, I've decided to create a .NET project in Visual Studio, paste the following code inside it and generate SystemScreenshotPlugin.dll from that
// project. This way, the code works inside Unity editor regardless of the target platform and it uses .NET's System.Drawing.dll, not Mono's one

#if false
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

public static class SystemScreenshotPlugin
{
	private static ImageCodecInfo jpegEncoder;

	public static bool StreamBitmap( int renderAreaWidth, int renderAreaHeight, int bitmapWidth, int bitmapHeight, int quality, Stream targetStream )
	{
		try
		{
			if( jpegEncoder == null )
			{
				foreach( ImageCodecInfo codec in ImageCodecInfo.GetImageEncoders() )
				{
					if( codec.FormatID == ImageFormat.Jpeg.Guid )
					{
						jpegEncoder = codec;
						break;
					}
				}
			}

			EncoderParameters encoderParams = new EncoderParameters( 1 );
			encoderParams.Param[0] = new EncoderParameter( Encoder.Quality, quality );

			CURSORINFO cursorInfo;
			cursorInfo.cbSize = Marshal.SizeOf( typeof( CURSORINFO ) );

			if( !GetCursorInfo( out cursorInfo ) )
				return false;

			using( Bitmap bitmap = CaptureBitmap( cursorInfo.ptScreenPos.x - renderAreaWidth / 2, cursorInfo.ptScreenPos.y - renderAreaHeight / 2, renderAreaWidth, renderAreaHeight ) )
			{
				if( bitmap == null )
					return false;

				if( renderAreaWidth == bitmapWidth && renderAreaHeight == bitmapHeight )
					bitmap.Save( targetStream, jpegEncoder, encoderParams );
				else
				{
					using( Bitmap downscaledBitmap = new Bitmap( bitmap, bitmapWidth, bitmapHeight ) )
						downscaledBitmap.Save( targetStream, jpegEncoder, encoderParams );
				}
			}

			return true;
		}
		catch( Exception e )
		{
			Console.WriteLine( e );
			return false;
		}
	}

	public static Bitmap CaptureBitmap( int x, int y, int width, int height )
	{
		Bitmap bitmap = null;
		try
		{
			bitmap = new Bitmap( width, height, PixelFormat.Format24bppRgb );
			using( Graphics g = Graphics.FromImage( bitmap ) )
			{
				g.CopyFromScreen( x, y, 0, 0, bitmap.Size, CopyPixelOperation.SourceCopy );

				CURSORINFO cursorInfo;
				cursorInfo.cbSize = Marshal.SizeOf( typeof( CURSORINFO ) );

				if( GetCursorInfo( out cursorInfo ) && cursorInfo.flags == CURSOR_SHOWING )
				{
					IntPtr iconPointer = CopyIcon( cursorInfo.hCursor );
					ICONINFO iconInfo;
					if( GetIconInfo( iconPointer, out iconInfo ) )
					{
						// Calculate the correct position of the cursor
						int iconX = width / 2 - iconInfo.xHotspot;
						int iconY = height / 2 - iconInfo.yHotspot;

						// Draw the cursor icon on top of the captured screen image
						DrawIcon( g.GetHdc(), iconX, iconY, cursorInfo.hCursor );

						// Release the handle created by call to g.GetHdc()
						g.ReleaseHdc();
					}
				}
			}

			return bitmap;
		}
		catch( Exception e )
		{
			Console.WriteLine( e );

			if( bitmap != null )
				bitmap.Dispose();

			return null;
		}
	}

	public const int CURSOR_SHOWING = 0x00000001;

	[StructLayout( LayoutKind.Sequential )]
	public struct ICONINFO
	{
		public bool fIcon;
		public int xHotspot;
		public int yHotspot;
		public IntPtr hbmMask;
		public IntPtr hbmColor;
	}

	[StructLayout( LayoutKind.Sequential )]
	public struct POINT
	{
		public int x;
		public int y;
	}

	[StructLayout( LayoutKind.Sequential )]
	public struct CURSORINFO
	{
		public int cbSize;
		public int flags;
		public IntPtr hCursor;
		public POINT ptScreenPos;
	}

	[DllImport( "user32.dll" )]
	public static extern bool GetCursorInfo( out CURSORINFO pci );

	[DllImport( "user32.dll" )]
	public static extern IntPtr CopyIcon( IntPtr hIcon );

	[DllImport( "user32.dll" )]
	public static extern bool DrawIcon( IntPtr hdc, int x, int y, IntPtr hIcon );

	[DllImport( "user32.dll" )]
	public static extern bool GetIconInfo( IntPtr hIcon, out ICONINFO piconinfo );
}
#endif