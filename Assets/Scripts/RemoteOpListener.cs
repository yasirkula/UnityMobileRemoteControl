﻿using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;

public class RemoteOpListener : MonoBehaviour
{
#pragma warning disable 0649
#pragma warning disable 0618
	[SerializeField]
	private NetworkDiscovery networkDiscovery;
#pragma warning restore 0618

#pragma warning disable 0414
	[Header( "Mouse Screenshot" )]
	[SerializeField]
	private Vector2Int mouseScreenshotRenderArea = new Vector2Int( 128, 128 );
	[SerializeField]
	private Vector2Int mouseScreenshotResolution = new Vector2Int( 128, 128 );
	[SerializeField, Range( 0, 100 )]
	private int mouseScreenshotQuality = 50;
#pragma warning restore 0414
#pragma warning restore 0649

	private TcpListener listener;
	private Thread thread;

	private int Volume
	{
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
		get { return Mathf.Clamp( Mathf.RoundToInt( SystemVolumePlugin.GetVolume() * 100f ), 0, 100 ); }
		set { SystemVolumePlugin.SetVolume( value * 0.01f ); }
#else
		get { return 0; }
		set { }
#endif
	}

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
	private void Awake()
	{
		SystemVolumePlugin.LogDelegate logDelegate = ( log ) => Debug.LogErrorFormat( "SystemVolumePlugin: {0}", log );
		SystemVolumePlugin.SetLoggingCallback( Marshal.GetFunctionPointerForDelegate( logDelegate ) );
		if( SystemVolumePlugin.InitializeVolume() != 0 )
			Debug.LogError( "Couldn't initialize SystemVolumePlugin" );
	}
#endif

	private void Start()
	{
		listener = new TcpListener( GetLocalIPAddress(), networkDiscovery.broadcastPort );
		listener.Start();

		thread = new Thread( ListenOpsThread );
		thread.Start();

		networkDiscovery.broadcastData = SystemInfo.deviceName;
		networkDiscovery.Initialize();
		networkDiscovery.StartAsServer();
	}

	private void OnApplicationQuit()
	{
		if( networkDiscovery.running )
			networkDiscovery.StopBroadcast();

		if( thread != null )
		{
			thread.Abort();
			thread = null;
		}

		if( listener != null )
		{
			listener.Stop();
			listener = null;
		}
	}

	// Source: https://stackoverflow.com/a/27376368/2373034
	private IPAddress GetLocalIPAddress()
	{
		using( Socket socket = new Socket( AddressFamily.InterNetwork, SocketType.Dgram, 0 ) )
		{
			socket.Connect( "8.8.8.8", 65530 );
			IPEndPoint endPoint = socket.LocalEndPoint as IPEndPoint;
			return IPAddress.Parse( endPoint.Address.ToString() );
		}
	}

	private void ListenOpsThread()
	{
		Debug.Log( "Listening for operations..." );

		while( true )
		{
			using( TcpClient client = listener.AcceptTcpClient() )
			{
				NetworkStream stream = client.GetStream();

				byte[] buffer = new byte[client.ReceiveBufferSize];
				int bytesRead = stream.Read( buffer, 0, client.ReceiveBufferSize );

				string dataReceived = Encoding.UTF8.GetString( buffer, 0, bytesRead );

				try
				{
					RemoteOp op = JsonUtility.FromJson<RemoteOp>( dataReceived );

					// Some operations occur too often that not logging them might be better
					if( op.Type != RemoteOpType.CheckVolume && op.Type != RemoteOpType.TriggerMouseMovement && op.Type != RemoteOpType.TriggerMouseWheel && op.Type != RemoteOpType.RequestMouseScreenshot )
						Debug.Log( "Operation: " + dataReceived );

					switch( op.Type )
					{
						case RemoteOpType.CheckVolume:
						{
							byte[] bytesToSend = Encoding.UTF8.GetBytes( Volume.ToString() );
							stream.Write( bytesToSend, 0, bytesToSend.Length );

							break;
						}
						case RemoteOpType.ChangeVolume:
						{
							Volume = int.Parse( op.Data );
							break;
						}
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
						case RemoteOpType.TriggerKey:
						{
							string key = op.Data;
							switch( key )
							{
								case "left": SystemKeyboardPlugin.TriggerKey( SystemKeyboardPlugin.ScanCodeShort.LEFT, true ); break;
								case "right": SystemKeyboardPlugin.TriggerKey( SystemKeyboardPlugin.ScanCodeShort.RIGHT, true ); break;
								case "down": SystemKeyboardPlugin.TriggerKey( SystemKeyboardPlugin.ScanCodeShort.DOWN, true ); break;
								case "up": SystemKeyboardPlugin.TriggerKey( SystemKeyboardPlugin.ScanCodeShort.UP, true ); break;
								case "space": SystemKeyboardPlugin.TriggerKey( SystemKeyboardPlugin.ScanCodeShort.SPACE ); break;
								default: Debug.LogWarning( "Unknown key: " + key ); break;
							}

							break;
						}
						case RemoteOpType.TriggerKeyboardInput:
						{
							KeyboardInput input = JsonUtility.FromJson<KeyboardInput>( op.Data );
							for( int i = 0; i < input.backspace; i++ )
								SystemKeyboardPlugin.TriggerKey( SystemKeyboardPlugin.ScanCodeShort.BACK );
							for( int i = 0; i < input.text.Length; i++ )
								SystemKeyboardPlugin.TriggerUnicodeCharacter( input.text[i] );

							break;
						}
						case RemoteOpType.TriggerMouseMovement:
						{
							MouseDelta delta = JsonUtility.FromJson<MouseDelta>( op.Data );
							SystemMousePlugin.MoveCursor( delta.x, -delta.y );

							break;
						}
						case RemoteOpType.TriggerMouseButtonDown:
						{
							string button = op.Data;
							switch( button )
							{
								case "0": SystemMousePlugin.MouseEvent( SystemMousePlugin.MouseEventFlags.LeftDown ); break;
								case "1": SystemMousePlugin.MouseEvent( SystemMousePlugin.MouseEventFlags.RightDown ); break;
								case "2": SystemMousePlugin.MouseEvent( SystemMousePlugin.MouseEventFlags.MiddleDown ); break;
							}

							break;
						}
						case RemoteOpType.TriggerMouseButtonUp:
						{
							string button = op.Data;
							switch( button )
							{
								case "0": SystemMousePlugin.MouseEvent( SystemMousePlugin.MouseEventFlags.LeftUp ); break;
								case "1": SystemMousePlugin.MouseEvent( SystemMousePlugin.MouseEventFlags.RightUp ); break;
								case "2": SystemMousePlugin.MouseEvent( SystemMousePlugin.MouseEventFlags.MiddleUp ); break;
							}

							break;
						}
						case RemoteOpType.TriggerMouseWheel:
						{
							int delta = int.Parse( op.Data );
							SystemMousePlugin.MouseScrollWheel( delta );

							break;
						}
						case RemoteOpType.RequestMouseScreenshot:
						{
							if( !SystemScreenshotPlugin.StreamBitmap( mouseScreenshotRenderArea.x, mouseScreenshotRenderArea.y, mouseScreenshotResolution.x, mouseScreenshotResolution.y, mouseScreenshotQuality, stream ) )
							{
								// Always send some data back
								stream.Write( new byte[4], 0, 4 );
							}

							break;
						}
#endif
					}
				}
				catch( Exception e )
				{
					Debug.LogException( e );
				}
			}
		}
	}
}