package com.yasirkula.remotecontrol;

import android.content.BroadcastReceiver;
import android.content.Context;
import android.content.Intent;
import android.util.Log;

public class NotificationInputReceiver extends BroadcastReceiver
{
	public static final String VOLUME_DOWN_ACTION = "REMOTE_CONTROL_VOLUME_DOWN";
	public static final String VOLUME_UP_ACTION = "REMOTE_CONTROL_VOLUME_UP";
	public static final String LEFT_ARROW_ACTION = "REMOTE_CONTROL_LEFT_ARROW";
	public static final String RIGHT_ARROW_ACTION = "REMOTE_CONTROL_RIGHT_ARROW";
	public static final String SPACEBAR_ACTION = "REMOTE_CONTROL_SPACEBAR";

	public static NotificationInputCallback inputCallback;

	@Override
	public void onReceive( Context context, Intent intent )
	{
		if( inputCallback == null )
			Log.e( "Unity", "inputCallback is reset!" );
		else
		{
			String action = intent.getAction();
			if( action != null )
			{
				switch( action )
				{
					case VOLUME_DOWN_ACTION:
						inputCallback.OnVolumeDownButtonClicked();
						break;
					case VOLUME_UP_ACTION:
						inputCallback.OnVolumeUpButtonClicked();
						break;
					case LEFT_ARROW_ACTION:
						inputCallback.OnLeftArrowClicked();
						break;
					case RIGHT_ARROW_ACTION:
						inputCallback.OnRightArrowClicked();
						break;
					case SPACEBAR_ACTION:
						inputCallback.OnSpacebarClicked();
						break;
				}
			}
		}
	}
}