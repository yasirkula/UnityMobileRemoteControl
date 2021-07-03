using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.UI;

public class RemoteOpBroadcaster : MonoBehaviour
{
#pragma warning disable 0649
	[SerializeField]
	private Dropdown networkTargets;

	[SerializeField]
	private GameObject[] controls;

	[SerializeField]
	private Text volumeText;

	[SerializeField]
	private float volumeCheckInterval = 5f;

	[SerializeField]
	private Button toggleTouchpadButton;

	[SerializeField]
	private GameObject touchpadRoot;

	[SerializeField]
	private Color touchpadButtonActiveColor;
	private Color touchpadButtonInactiveColor;

#pragma warning disable 0618
	[SerializeField]
	private NetworkDiscovery networkDiscovery;
#pragma warning restore 0618
#pragma warning restore 0649

	private readonly List<Dropdown.OptionData> networkTargetNames = new List<Dropdown.OptionData>( 4 );
	private readonly List<string> networkTargetIPs = new List<string>( 4 );

	private string m_connectedIP;
	private string ConnectedIP
	{
		get { return m_connectedIP; }
		set
		{
			if( m_connectedIP != value )
			{
				m_connectedIP = value;
				Debug.Log( "Connected to IP: " + ConnectedIP );
				CheckVolume();
			}
		}
	}

	private int m_volume;
	private int Volume
	{
		get { return m_volume; }
		set
		{
			m_volume = value;
			volumeText.text = value.ToString();
		}
	}

	private void Start()
	{
		for( int i = 0; i < controls.Length; i++ )
			controls[i].SetActive( false );

		touchpadButtonInactiveColor = toggleTouchpadButton.image.color;

		networkTargets.onValueChanged.AddListener( ( value ) => ConnectedIP = ( value >= 0 && value < networkTargetIPs.Count ) ? networkTargetIPs[value] : null );

		toggleTouchpadButton.onClick.AddListener( () =>
		{
			touchpadRoot.SetActive( !touchpadRoot.activeSelf );
			toggleTouchpadButton.image.color = touchpadRoot.activeSelf ? touchpadButtonActiveColor : touchpadButtonInactiveColor;
		} );

		StartCoroutine( CheckNetworkTargetsRegularlyCoroutine() );
		StartCoroutine( CheckVolumeRegularlyCoroutine() );

		networkDiscovery.Initialize();
		networkDiscovery.StartAsClient();

		Screen.sleepTimeout = SleepTimeout.NeverSleep;
	}

	private void OnApplicationQuit()
	{
		if( networkDiscovery.running )
			networkDiscovery.StopBroadcast();
	}

	public void IncrementVolume( int delta )
	{
		SetVolume( Volume + delta );
	}

	private void SetVolume( int value )
	{
		if( string.IsNullOrEmpty( ConnectedIP ) )
			return;

		Volume = Mathf.Clamp( value, 0, 100 );
		SendOp( new RemoteOp( RemoteOpType.ChangeVolume, Volume.ToString() ) );
	}

	private void CheckVolume()
	{
		SendOp( new RemoteOp( RemoteOpType.CheckVolume, null ) );
	}

	public void TriggerKeyPress( string key )
	{
		SendOp( new RemoteOp( RemoteOpType.TriggerKey, key ) );
	}

	public void TriggerMouseMovement( BaseEventData eventData )
	{
		Vector2 delta = ( (PointerEventData) eventData ).delta;
		SendOp( new RemoteOp( RemoteOpType.TriggerMouseMovement, JsonUtility.ToJson( delta, false ) ) );
	}

	public void OnTouchpadClick( BaseEventData eventData )
	{
		if( !( (PointerEventData) eventData ).dragging )
		{
			TriggerMouseButtonDown( 0 );
			TriggerMouseButtonUp( 0 );
		}
	}

	public void TriggerMouseButtonDown( int button )
	{
		SendOp( new RemoteOp( RemoteOpType.TriggerMouseButtonDown, button.ToString() ) );
	}

	public void TriggerMouseButtonUp( int button )
	{
		SendOp( new RemoteOp( RemoteOpType.TriggerMouseButtonUp, button.ToString() ) );
	}

	private void SendOp( RemoteOp op )
	{
		if( string.IsNullOrEmpty( ConnectedIP ) )
			return;

		try
		{
			using( TcpClient client = new TcpClient( ConnectedIP, networkDiscovery.broadcastPort ) )
			{
				NetworkStream stream = client.GetStream();
				byte[] bytesToSend = Encoding.UTF8.GetBytes( JsonUtility.ToJson( op, false ) );

				// Send request
				stream.Write( bytesToSend, 0, bytesToSend.Length );

				switch( op.Type )
				{
					case RemoteOpType.CheckVolume:
					{
						// Retrieve volume value
						byte[] buffer = new byte[client.ReceiveBufferSize];
						int bytesRead = stream.Read( buffer, 0, client.ReceiveBufferSize );
						Volume = int.Parse( Encoding.UTF8.GetString( buffer, 0, bytesRead ) );

						break;
					}
				}
			}
		}
		catch( SocketException ) { }
		catch( Exception e )
		{
			Debug.LogException( e );
		}
	}

	private IEnumerator CheckNetworkTargetsRegularlyCoroutine()
	{
		yield return null;

		while( true )
		{
			networkTargetNames.Clear();
			networkTargetIPs.Clear();

			foreach( var target in networkDiscovery.broadcastsReceived )
			{
				networkTargetNames.Add( new Dropdown.OptionData( BytesToString( target.Value.broadcastData ) ) );
				networkTargetIPs.Add( target.Key.Replace( "::ffff:", "" ) );
			}

			if( networkTargetNames.Count == 0 )
			{
				networkTargets.interactable = false;
				networkTargets.value = 0;
				networkTargets.options = new List<Dropdown.OptionData>( 1 ) { new Dropdown.OptionData( "Scanning network..." ) };

				ConnectedIP = null;

				for( int i = 0; i < controls.Length; i++ )
					controls[i].SetActive( false );
			}
			else
			{
				networkTargets.interactable = true;
				networkTargets.options = networkTargetNames;

				ConnectedIP = ( networkTargets.value >= 0 && networkTargets.value < networkTargetIPs.Count ) ? networkTargetIPs[networkTargets.value] : null;

				for( int i = 0; i < controls.Length; i++ )
					controls[i].SetActive( true );
			}

			yield return new WaitForSeconds( networkDiscovery.broadcastInterval * 0.001f );
		}
	}

	private IEnumerator CheckVolumeRegularlyCoroutine()
	{
		yield return null;

		while( true )
		{
			CheckVolume();
			yield return new WaitForSeconds( volumeCheckInterval );
		}
	}

	private string BytesToString( byte[] bytes )
	{
		char[] chars = new char[bytes.Length / sizeof( char )];
		Buffer.BlockCopy( bytes, 0, chars, 0, bytes.Length );
		return new string( chars );
	}
}