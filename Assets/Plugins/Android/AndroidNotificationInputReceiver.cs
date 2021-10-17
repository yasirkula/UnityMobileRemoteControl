#if UNITY_EDITOR || UNITY_ANDROID
using UnityEngine;

public enum AndroidNotificationInputType
{
	VolumeDown,
	VolumeUp,
	LeftArrow,
	RightArrow,
	Spacebar
}

public class AndroidNotificationInputReceiver : AndroidJavaProxy
{
	private readonly System.Action<AndroidNotificationInputType> onInputReceived;

	public AndroidNotificationInputReceiver( System.Action<AndroidNotificationInputType> onInputReceived ) : base( "com.yasirkula.remotecontrol.NotificationInputCallback" )
	{
		this.onInputReceived = onInputReceived;
	}

	public void OnVolumeDownButtonClicked() { onInputReceived?.Invoke( AndroidNotificationInputType.VolumeDown ); }
	public void OnVolumeUpButtonClicked() { onInputReceived?.Invoke( AndroidNotificationInputType.VolumeUp ); }
	public void OnLeftArrowClicked() { onInputReceived?.Invoke( AndroidNotificationInputType.LeftArrow ); }
	public void OnRightArrowClicked() { onInputReceived?.Invoke( AndroidNotificationInputType.RightArrow ); }
	public void OnSpacebarClicked() { onInputReceived?.Invoke( AndroidNotificationInputType.Spacebar ); }
}
#endif