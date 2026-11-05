package com.yuzhounh.xremote;

import android.app.Activity;
import android.app.Application;
import android.content.Intent;
import android.os.Bundle;
import android.os.Handler;
import android.os.Looper;
import android.os.Process;
import java.lang.ref.WeakReference;

public final class XRemoteApp extends Application implements Application.ActivityLifecycleCallbacks {
    SecureStore store;
    TunnelController tunnel;
    private final Handler main = new Handler(Looper.getMainLooper());
    private int visible;
    private int generation;
    private boolean externalPicker;
    private boolean exiting;
    private WeakReference<Activity> last = new WeakReference<>(null);
    @Override public void onCreate() {
        super.onCreate(); L10n.initialize(this); Theme.initialize(this); store = new SecureStore(this); tunnel = new TunnelController(store);
        registerActivityLifecycleCallbacks(this);
    }
    void externalPicker(boolean value) { externalPicker = value; }
    void ensureTunnel() { if(tunnel.isClosed()) tunnel=new TunnelController(store); }
    int graceSeconds() { return getSharedPreferences("ui",MODE_PRIVATE).getInt("graceSeconds",120); }
    void graceSeconds(int value) { getSharedPreferences("ui",MODE_PRIVATE).edit().putInt("graceSeconds",value).commit(); }
    boolean isForeground() { return visible>0; }
    void exitNow() {
        if(exiting)return;exiting=true;stopService(new Intent(this,GraceService.class));
        Activity a=last.get();if(a!=null)a.finishAndRemoveTask();
        Thread cleanup=new Thread(()->{RdpActivity.disconnectCurrent();VncActivity.disconnectCurrent();tunnel.close();main.post(()->Process.killProcess(Process.myPid()));},"xremote-exit");
        cleanup.setDaemon(true);cleanup.start();main.postDelayed(()->Process.killProcess(Process.myPid()),1500);
    }
    @Override public void onActivityStarted(Activity a) {
        visible++; generation++; last = new WeakReference<>(a); Theme.applySystemBars(a);
        stopService(new Intent(this,GraceService.class));ensureTunnel();tunnel.start();
    }
    @Override public void onActivityStopped(Activity a) {
        visible = Math.max(0, visible - 1); if (visible != 0 || a.isChangingConfigurations()) return;
        int token = ++generation;
        main.postDelayed(() -> {
            if (visible != 0 || token != generation) return;
            if(exiting)return;
            if(externalPicker&&!tunnel.isActive())return;
            if(!tunnel.isActive()){exitNow();return;}
            try { startForegroundService(new Intent(this,GraceService.class)); }
            catch(RuntimeException e) { exitNow(); }
        }, 180);
    }
    @Override public void onActivityCreated(Activity a, Bundle b) { }
    @Override public void onActivityResumed(Activity a) { }
    @Override public void onActivityPaused(Activity a) { }
    @Override public void onActivitySaveInstanceState(Activity a, Bundle b) { }
    @Override public void onActivityDestroyed(Activity a) { }
}
