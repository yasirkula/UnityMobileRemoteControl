﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
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

#pragma warning disable 0618
	[SerializeField]
	private NetworkDiscovery networkDiscovery;
#pragma warning restore 0618

	[SerializeField]
	private GameObject[] controls;

	[Header( "Volume Controls" )]
	[SerializeField]
	private Text volumeText;

	[SerializeField]
	private float volumeCheckInterval = 5f;

	[Header( "Mouse Controls" )]
	[SerializeField]
	private Button toggleTouchpadButton;

	[SerializeField]
	private GameObject touchpadRoot;

	[SerializeField]
	private Color touchpadButtonActiveColor;
	private Color touchpadButtonInactiveColor;

	[SerializeField, Tooltip( "The maximum delay between pointer down and pointer up events for the pointer up to be recognized as mouse click" )]
	private float touchpadClickMaxHoldTime = 0.5f;

	[Header( "- Mouse Drag Gesture" )]
	[SerializeField, Tooltip( "Quickly double tapping and then moving the pointer on the touchpad will perform mouse drag operation. This is the maximum delay between double taps for this gesture to be recognized" )]
	private float touchpadMouseDragMaxPointerDelay = 0.25f;
	[SerializeField, Tooltip( "Maximum distance between the first tap's pointer release and the second tap's pointer down for these two taps to be accepted as a double tap for mouse drag gesture" )]
	private float touchpadMouseDragMaxDistanceToSecondTap = 50f;

	private float touchpadPointerDownTime, touchpadPointerUpTime;
	private Vector2 touchpadPointerUpPosition;
	private Coroutine touchpadDelayedMouseUpCoroutine;
	private bool touchpadPerformingMouseDragGesture;

	[Header( "- Mouse Streaming" )]
	[SerializeField]
	private float mouseScreenshotRefreshInterval = 0.1f;
	[SerializeField]
	private RawImage mouseScreenshotDisplay;

	private Texture2D mouseScreenshot;
	private Coroutine mouseScreenshotCoroutine;

	[Header( "Keyboard Controls" )]
	[SerializeField]
	private Button toggleKeyboardButton;
	private TouchScreenKeyboard keyboard;
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
				Debug.Log( "Connected to IP: " + m_connectedIP );
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

			if( mouseScreenshotCoroutine != null )
				StopCoroutine( mouseScreenshotCoroutine );
			if( touchpadRoot.activeSelf )
				mouseScreenshotCoroutine = StartCoroutine( GetMouseScreenshotRegularlyCoroutine() );
		} );

		toggleKeyboardButton.onClick.AddListener( () =>
		{
			if( keyboard == null && TouchScreenKeyboard.isSupported )
			{
				toggleKeyboardButton.image.color = touchpadButtonActiveColor;
				StartCoroutine( ShowTouchScreenKeyboardCoroutine() );
			}
		} );

		StartCoroutine( CheckNetworkTargetsRegularlyCoroutine() );
		StartCoroutine( CheckVolumeRegularlyCoroutine() );

		networkDiscovery.Initialize();
		networkDiscovery.StartAsClient();

		Screen.sleepTimeout = SleepTimeout.NeverSleep;

		if( TouchScreenKeyboard.isSupported )
			TouchScreenKeyboard.hideInput = true;
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
		SendOp( new RemoteOp( RemoteOpType.TriggerMouseMovement, JsonUtility.ToJson( new MouseDelta( Mathf.RoundToInt( delta.x ), Mathf.RoundToInt( delta.y ) ), false ) ) );
	}

	// ABOUT MOUSE DRAG GESTURE
	// Quickly double tapping and then moving the pointer on the touchpad will perform a mouse drag operation. To support this gesture, on pointer release,
	// we must execute only mouse down event and delay mouse up event for a short duration. If user presses the touchpad a second time during that delay,
	// then we must cancel the pending mouse up event and rather consider the second touch as a mouse drag operation (mouse drag gesture). To summarize:
	// First pointer release (assuming this was a click, i.e. 'isClick == true'):
	// - Execute mouse down event
	// - Execute a mouse up event after 'touchpadMouseDragMaxPointerDelay' seconds ('touchpadDelayedMouseUpCoroutine')
	// Consecutive pointer down:
	// - If the delay between this pointer down and previous pointer up was shorter than 'touchpadMouseDragMaxPointerDelay' seconds and the distance between
	//   these two taps was shorter than 'touchpadMouseDragMaxDistanceToSecondTap' units, consider this a mouse drag gesture ('touchpadExecutingMouseDragGesture = true')
	//   and cancel the pending mouse up event ('touchpadDelayedMouseUpCoroutine')
	// Release of the consecutive pointer (when 'touchpadExecutingMouseDragGesture == true'):
	// - If this was a click (i.e. 'isClick == true'), user wasn't trying to perform a mouse drag gesture but rather double clicking the mouse. First,
	//   execute a mouse up event because we had canceled the pending mouse up event. Then, perform mouse down and mouse up events in quick succession to
	//   execute the second mouse click
	// - If this wasn't a click, user was indeed performing a mouse drag gesture. In this case, simply release the mouse by executing a mouse up event
	public void OnTouchpadPointerDown( BaseEventData eventData )
	{
		touchpadPointerDownTime = Time.time;

		if( ( Time.time - touchpadPointerUpTime ) <= touchpadMouseDragMaxPointerDelay && Vector2.Distance( ( (PointerEventData) eventData ).position, touchpadPointerUpPosition ) <= touchpadMouseDragMaxDistanceToSecondTap && touchpadDelayedMouseUpCoroutine != null )
		{
			touchpadPerformingMouseDragGesture = true;

			// One may think that the 'touchpadDelayedMouseUpCoroutine != null' check is unnecessary but in the very rare case that the touch happens after
			// exactly 'touchpadMouseDragMaxPointerDelay' seconds and the coroutine is continued prior to this pointer down function, then the mouse up event
			// will be executed by the coroutine and we shouldn't perform mouse drag gesture. Hence the 'touchpadDelayedMouseUpCoroutine != null' condition
			StopCoroutine( touchpadDelayedMouseUpCoroutine );
			touchpadDelayedMouseUpCoroutine = null;
		}
	}

	public void OnTouchpadPointerUp( BaseEventData eventData )
	{
		bool isClick = !( (PointerEventData) eventData ).dragging && ( Time.time - touchpadPointerDownTime ) <= touchpadClickMaxHoldTime;
		touchpadPointerUpTime = 0f;

		if( !touchpadPerformingMouseDragGesture ) // First pointer release
		{
			if( isClick )
			{
				touchpadPointerUpTime = Time.time;
				touchpadPointerUpPosition = ( (PointerEventData) eventData ).position;

				if( touchpadDelayedMouseUpCoroutine != null )
					StopCoroutine( touchpadDelayedMouseUpCoroutine );

				TriggerMouseButtonDown( 0 );
				touchpadDelayedMouseUpCoroutine = StartCoroutine( PerformDelayedMouseUpCoroutine() );
			}
		}
		else // Release of the consecutive pointer that was executing mouse drag gesture
		{
			// Reset the gesture
			touchpadPerformingMouseDragGesture = false;

			TriggerMouseButtonUp( 0 );

			// This was not a drag gesture but rather a double tap, click the mouse a second time
			if( isClick )
			{
				TriggerMouseButtonDown( 0 );
				TriggerMouseButtonUp( 0 );
			}
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
					case RemoteOpType.RequestMouseScreenshot:
					{
						MemoryStream memoryStream = new MemoryStream( client.ReceiveBufferSize );
						stream.CopyTo( memoryStream, client.ReceiveBufferSize );

						// When LoadImage fails, it might still return true for some reason and the Texture will become an 8x8 question mark
						mouseScreenshotDisplay.enabled = mouseScreenshot.LoadImage( memoryStream.ToArray(), false ) && ( mouseScreenshot.width != 8 || mouseScreenshot.height != 8 );

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

	private IEnumerator ShowTouchScreenKeyboardCoroutine()
	{
		string previousText = "                                                  "; // Original text isn't blank so that we can detect backspace input in the beginning, as well
		keyboard = TouchScreenKeyboard.Open( previousText, TouchScreenKeyboardType.Default, false, true, false, false, "", 0 );

		while( keyboard.active )
		{
			string newText = keyboard.text;
			if( newText != previousText )
			{
				int commonPrefixLength = 0, upperBound = Mathf.Min( previousText.Length, newText.Length );
				while( commonPrefixLength < upperBound && previousText[commonPrefixLength] == newText[commonPrefixLength] )
					commonPrefixLength++;

				int backspaceCount = previousText.Length - commonPrefixLength;
				string typedText = commonPrefixLength < newText.Length ? newText.Substring( commonPrefixLength ) : "";
				previousText = newText;

				SendOp( new RemoteOp( RemoteOpType.TriggerKeyboardInput, JsonUtility.ToJson( new KeyboardInput( backspaceCount, typedText ), false ) ) );
			}

			yield return null;
		}

		keyboard = null;
		toggleKeyboardButton.image.color = touchpadButtonInactiveColor;
	}

	private IEnumerator GetMouseScreenshotRegularlyCoroutine()
	{
		yield return null;

		if( mouseScreenshot == null )
		{
			mouseScreenshot = new Texture2D( 2, 2, TextureFormat.RGB24, false );
			mouseScreenshotDisplay.texture = mouseScreenshot;
		}

		while( true )
		{
			SendOp( new RemoteOp( RemoteOpType.RequestMouseScreenshot, null ) );
			yield return new WaitForSeconds( mouseScreenshotRefreshInterval );
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

	private IEnumerator PerformDelayedMouseUpCoroutine()
	{
		yield return new WaitForSeconds( touchpadMouseDragMaxPointerDelay );

		TriggerMouseButtonUp( 0 );
		touchpadDelayedMouseUpCoroutine = null;
	}

	private string BytesToString( byte[] bytes )
	{
		char[] chars = new char[bytes.Length / sizeof( char )];
		Buffer.BlockCopy( bytes, 0, chars, 0, bytes.Length );
		return new string( chars );
	}
}