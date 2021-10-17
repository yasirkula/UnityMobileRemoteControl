package com.yasirkula.remotecontrol;

import android.content.ComponentName;
import android.content.Context;
import android.content.Intent;
import android.content.ServiceConnection;
import android.os.IBinder;

public class RemoteControl
{
	private static ServiceConnection notificationServiceConnection = new ServiceConnection()
	{
		@Override
		public void onServiceConnected( ComponentName className, IBinder binder )
		{
		}

		@Override
		public void onServiceDisconnected( ComponentName className )
		{
		}
	};

	public static void ShowNotification( Context context, NotificationInputCallback inputCallback )
	{
		NotificationService.unityActivityClass = context.getClass();
		NotificationInputReceiver.inputCallback = inputCallback;

		Intent serviceIntent = new Intent( context, NotificationService.class );
		try
		{
			context.bindService( serviceIntent, notificationServiceConnection, Context.BIND_AUTO_CREATE );
		}
		catch( IllegalArgumentException e )
		{
		}
	}

	public static void HideNotification( Context context )
	{
		try
		{
			context.unbindService( notificationServiceConnection );
		}
		catch( IllegalArgumentException e )
		{
		}
	}
}