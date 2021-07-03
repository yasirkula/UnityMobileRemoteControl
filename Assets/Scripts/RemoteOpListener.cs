using System;
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

					if( op.Type != RemoteOpType.TriggerMouseMovement ) // Mouse movement generates too many logs, skipping them might be better than logging them
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
							int volume = int.Parse( op.Data );
							Debug.Log( "Setting volume: " + volume );
							Volume = volume;

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
								case "space": SystemKeyboardPlugin.TriggerKey( SystemKeyboardPlugin.ScanCodeShort.SPACE, false ); break;
								default: Debug.LogWarning( "Unknown key: " + key ); break;
							}

							break;
						}
						case RemoteOpType.TriggerMouseMovement:
						{
							Vector2 delta = JsonUtility.FromJson<Vector2>( op.Data );
							SystemMousePlugin.MoveCursor( Mathf.RoundToInt( delta.x ), Mathf.RoundToInt( -delta.y ) );

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