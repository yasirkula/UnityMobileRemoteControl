#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
using System;
using System.Runtime.InteropServices;

public class SystemVolumePlugin
{
	[UnmanagedFunctionPointer( CallingConvention.Cdecl )]
	public delegate void LogDelegate( string str );

	[DllImport( "SystemVolumePlugin" )]
	public static extern float GetVolume();

	[DllImport( "SystemVolumePlugin" )]
	public static extern int SetVolume( float volume );

	[DllImport( "SystemVolumePlugin" )]
	public static extern int InitializeVolume();

	[DllImport( "SystemVolumePlugin" )]
	public static extern void SetLoggingCallback( IntPtr func );
}
#endif