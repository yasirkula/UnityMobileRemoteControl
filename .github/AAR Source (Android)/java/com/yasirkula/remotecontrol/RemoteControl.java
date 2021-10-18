package com.yasirkula.remotecontrol;

import android.content.ActivityNotFoundException;
import android.content.ComponentName;
import android.content.Context;
import android.content.Intent;
import android.content.ServiceConnection;
import android.net.Uri;
import android.os.IBinder;
import android.util.Log;
import android.webkit.MimeTypeMap;
import android.widget.Toast;

import java.io.File;
import java.util.Locale;

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

	public static void OpenFile( Context context, String filePath )
	{
		Log.d( "Unity", "Opening file: " + filePath );

		File file = new File( filePath );
		Uri fileUri = file.exists() ? RemoteControlContentProvider.getUriForFile( context, "com.yasirkula.remotecontrol.contentproviderauth", file ) : Uri.parse( filePath );

		Intent intent = new Intent( Intent.ACTION_VIEW );
		intent.setFlags( Intent.FLAG_ACTIVITY_NEW_TASK | Intent.FLAG_GRANT_READ_URI_PERMISSION );
		intent.setData( fileUri );

		int extensionStart = filePath.lastIndexOf( '.' );
		if( extensionStart >= 0 && extensionStart < filePath.length() - 1 )
		{
			String mime = MimeTypeMap.getSingleton().getMimeTypeFromExtension( filePath.substring( extensionStart + 1 ).toLowerCase( Locale.ENGLISH ) );
			if( mime != null && mime.length() > 0 )
			{
				Log.d( "Unity", "Determined mime type: " + mime );
				intent.setDataAndType( fileUri, mime );
			}
		}

		try
		{
			context.startActivity( Intent.createChooser( intent, "Open with" ) );
		}
		catch( ActivityNotFoundException ex )
		{
			Toast.makeText( context, "No apps can open this file.", Toast.LENGTH_SHORT ).show();
		}
	}
}