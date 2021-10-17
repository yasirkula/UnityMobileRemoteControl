package com.yasirkula.remotecontrol;

import android.app.Notification;
import android.app.NotificationChannel;
import android.app.NotificationManager;
import android.app.PendingIntent;
import android.app.Service;
import android.content.Intent;
import android.os.Binder;
import android.os.Build;
import android.os.IBinder;
import android.util.Log;
import android.widget.RemoteViews;

public class NotificationService extends Service
{
	public static class NotificationBinder extends Binder
	{
	}

	private static final String NOTIFICATION_CHANNEL_ID = "MOBILE_REMOTE_CONTROL";
	private static final int NOTIFICATION_ID = 365641;

	private static RemoteViews notificationRemoteView;
	private static PendingIntent notificationClickIntent, volumeDownIntent, volumeUpIntent, leftArrowIntent, rightArrowIntent, spacebarIntent;

	public static Class unityActivityClass;
	public static boolean dismissibleNotifications = true;

	private final IBinder mBinder = new NotificationBinder();

	@Override
	public void onCreate()
	{
		super.onCreate();

		NotificationManager notificationManager = (NotificationManager) getSystemService( NOTIFICATION_SERVICE );
		Notification.Builder notificationBuilder;

		if( notificationClickIntent == null )
		{
			notificationClickIntent = PendingIntent.getActivity( this, 0, new Intent( this, unityActivityClass ).setFlags( Intent.FLAG_ACTIVITY_NEW_TASK ), 0 );

			volumeDownIntent = PendingIntent.getBroadcast( this, 0, new Intent( this, NotificationInputReceiver.class ).setAction( NotificationInputReceiver.VOLUME_DOWN_ACTION ), 0 );
			volumeUpIntent = PendingIntent.getBroadcast( this, 0, new Intent( this, NotificationInputReceiver.class ).setAction( NotificationInputReceiver.VOLUME_UP_ACTION ), 0 );
			leftArrowIntent = PendingIntent.getBroadcast( this, 0, new Intent( this, NotificationInputReceiver.class ).setAction( NotificationInputReceiver.LEFT_ARROW_ACTION ), 0 );
			rightArrowIntent = PendingIntent.getBroadcast( this, 0, new Intent( this, NotificationInputReceiver.class ).setAction( NotificationInputReceiver.RIGHT_ARROW_ACTION ), 0 );
			spacebarIntent = PendingIntent.getBroadcast( this, 0, new Intent( this, NotificationInputReceiver.class ).setAction( NotificationInputReceiver.SPACEBAR_ACTION ), 0 );
		}

		if( Build.VERSION.SDK_INT < 24 )
		{
			// Custom notifications aren't supported on old versions

			notificationBuilder = new Notification.Builder( this )
					.addAction( R.drawable.volume_down_icon_small, "Volume-", volumeDownIntent )
					.addAction( R.drawable.spacebar_icon_small, "Space", spacebarIntent )
					.addAction( R.drawable.volume_up_icon_small, "Volume+", volumeUpIntent );

			// Show the action buttons in the compact (unexpanded) notification when possible
			if( Build.VERSION.SDK_INT >= 21 )
				notificationBuilder.setStyle( new Notification.MediaStyle().setShowActionsInCompactView( 0, 1, 2 ) ).setContentText( "" );
			else
				notificationBuilder.setContentText( "Expand to access quick actions" );
		}
		else
		{
			// Use custom notification layout
			// Credit: https://thecodeprogram.com/android-build-your-own-custom-notification

			if( notificationRemoteView == null )
			{
				notificationRemoteView = new RemoteViews( getPackageName(), R.layout.notification_custom_layout );
				notificationRemoteView.setOnClickPendingIntent( R.id.volumeDownButton, volumeDownIntent );
				notificationRemoteView.setOnClickPendingIntent( R.id.volumeUpButton, volumeUpIntent );
				notificationRemoteView.setOnClickPendingIntent( R.id.leftArrowButton, leftArrowIntent );
				notificationRemoteView.setOnClickPendingIntent( R.id.rightArrowButton, rightArrowIntent );
				notificationRemoteView.setOnClickPendingIntent( R.id.spacebarButton, spacebarIntent );
			}

			if( Build.VERSION.SDK_INT < 26 )
				notificationBuilder = new Notification.Builder( this );
			else
			{
				NotificationChannel notificationChannel = new NotificationChannel( NOTIFICATION_CHANNEL_ID, "Unity Mobile Remote Control", NotificationManager.IMPORTANCE_LOW );
				notificationChannel.setDescription( "Unity Mobile Remote Control" );
				notificationChannel.setLockscreenVisibility( Notification.VISIBILITY_PUBLIC );
				notificationManager.createNotificationChannel( notificationChannel );

				notificationBuilder = new Notification.Builder( this, NOTIFICATION_CHANNEL_ID );
			}

			notificationBuilder.setCustomContentView( notificationRemoteView ).setContentText( "" );
		}

		if( Build.VERSION.SDK_INT >= 21 )
			notificationBuilder.setCategory( Notification.EXTRA_MEDIA_SESSION ).setVisibility( Notification.VISIBILITY_PUBLIC );

		Notification notification = notificationBuilder.setSmallIcon( getApplicationInfo().icon )
				.setContentTitle( "Quick actions" )
				.setContentIntent( notificationClickIntent )
				.setOngoing( !dismissibleNotifications ).build();

		startForeground( NOTIFICATION_ID, notification );

		// Credit: https://stackoverflow.com/a/35721582/2373034
		if( dismissibleNotifications )
			stopForeground( false );
	}

	@Override
	public IBinder onBind( Intent intent )
	{
		return mBinder;
	}
}