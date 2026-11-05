package com.yuzhounh.xremote;

import android.app.Notification;
import android.app.NotificationChannel;
import android.app.NotificationManager;
import android.app.PendingIntent;
import android.app.Service;
import android.content.Intent;
import android.content.pm.ServiceInfo;
import android.os.Build;
import android.os.Handler;
import android.os.IBinder;
import android.os.Looper;

/** Exists only during the user-selected, bounded background grace period. */
public final class GraceService extends Service {
    private static final String CHANNEL="xremote.grace";
    private final Handler handler=new Handler(Looper.getMainLooper());
    @Override public int onStartCommand(Intent intent,int flags,int startId){
        XRemoteApp app=(XRemoteApp)getApplication();
        if(intent!=null&&"exit".equals(intent.getAction())){app.exitNow();return START_NOT_STICKY;}
        NotificationManager manager=getSystemService(NotificationManager.class);
        manager.createNotificationChannel(new NotificationChannel(CHANNEL,L10n.text(R.string.ui_briefly_keep_the_remote_connection),NotificationManager.IMPORTANCE_LOW));
        PendingIntent exit=PendingIntent.getService(this,1,new Intent(this,GraceService.class).setAction("exit"),PendingIntent.FLAG_IMMUTABLE|PendingIntent.FLAG_UPDATE_CURRENT);
        Notification notification=new Notification.Builder(this,CHANNEL).setSmallIcon(R.drawable.ic_xremote)
            .setContentTitle(L10n.text(R.string.ui_xremote_is_keeping_the_connection))
            .setContentText(app.graceSeconds()/60+L10n.text(R.string.ui_minutes_to_return_and_continue_otherwise_it_disconnects))
            .setContentIntent(exit).setOngoing(true).setOnlyAlertOnce(true).build();
        if(Build.VERSION.SDK_INT>=34)startForeground(42,notification,ServiceInfo.FOREGROUND_SERVICE_TYPE_SPECIAL_USE);
        else startForeground(42,notification);
        // startForegroundService() obliges us to call startForeground() first; returning early crashed the app.
        if(app.isForeground()){stopSelf();return START_NOT_STICKY;}
        handler.removeCallbacksAndMessages(null);
        handler.postDelayed(()->{if(!app.isForeground())app.exitNow();else stopSelf();},app.graceSeconds()*1000L);
        return START_NOT_STICKY;
    }
    @Override public void onTaskRemoved(Intent root){((XRemoteApp)getApplication()).exitNow();}
    @Override public void onDestroy(){handler.removeCallbacksAndMessages(null);stopForeground(STOP_FOREGROUND_REMOVE);super.onDestroy();}
    @Override public IBinder onBind(Intent intent){return null;}
}
