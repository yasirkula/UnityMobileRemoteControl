using System;
using System.Net.Sockets;
using System.Text;

public static class ExtensionFunctions
{
	public static int ReadBytes( this NetworkStream stream, ref byte[] buffer, int bufferSize )
	{
		if( buffer == null || buffer.Length < bufferSize )
			buffer = new byte[bufferSize];

		return stream.Read( buffer, 0, bufferSize );
	}

	public static string ReadString( this NetworkStream stream, ref byte[] buffer, int bufferSize )
	{
		if( buffer == null || buffer.Length < bufferSize )
			buffer = new byte[bufferSize];

		int bytesRead = stream.Read( buffer, 0, bufferSize );
		return Encoding.UTF8.GetString( buffer, 0, bytesRead );
	}

	public static void WriteString( this NetworkStream stream, string value )
	{
		byte[] bytesToSend = Encoding.UTF8.GetBytes( value );
		stream.Write( bytesToSend, 0, bytesToSend.Length );
	}

	public static long ReadLong( this NetworkStream stream, ref byte[] buffer )
	{
		if( buffer == null || buffer.Length < 8 )
			buffer = new byte[8];

		stream.Read( buffer, 0, 8 );
		if( BitConverter.IsLittleEndian )
			Array.Reverse( buffer, 0, 8 );

		return BitConverter.ToInt64( buffer, 0 );
	}

	public static void WriteLong( this NetworkStream stream, long value )
	{
		byte[] bytesToSend = BitConverter.GetBytes( value );
		if( BitConverter.IsLittleEndian )
			Array.Reverse( bytesToSend );

		stream.Write( bytesToSend, 0, bytesToSend.Length );
	}
}