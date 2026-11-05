package com.freerdp.freerdpcore.services;

import android.content.Context;
import android.graphics.Bitmap;

/** Minimal Java ABI adapter for the pinned FreeRDP 3.32.1 Android client. */
public class LibFreeRDP {
    public interface Listener {
        void connected();
        void failed(String error);
        void disconnected();
        void size(int width, int height);
        void graphics(int x, int y, int width, int height);
        boolean authenticate(StringBuilder user, StringBuilder domain, StringBuilder password);
        int certificate(String commonName, String subject, String fingerprint, boolean changed);
        void pointer(int[] pixels, int width, int height, int hotX, int hotY);
    }
    public static volatile Listener listener;
    static {
        System.loadLibrary("freerdp-android");
        System.loadLibrary("freerdp-client3");
        System.loadLibrary("freerdp3");
        System.loadLibrary("winpr3");
    }
    public LibFreeRDP() { }
    public static native String freerdp_get_jni_version();
    public static native long freerdp_new(Context context);
    public static native void freerdp_free(long instance);
    public static native boolean freerdp_parse_arguments(long instance, String[] arguments);
    public static native boolean freerdp_connect(long instance);
    public static native boolean freerdp_disconnect(long instance);
    public static native boolean freerdp_update_graphics(long instance, Bitmap bitmap, int x, int y, int width, int height);
    public static native boolean freerdp_send_cursor_event(long instance, int x, int y, int flags);
    public static native boolean freerdp_send_key_event(long instance, int keycode, boolean down);
    public static native boolean freerdp_send_unicodekey_event(long instance, int codepoint, boolean down);
    public static native boolean freerdp_send_clipboard_data(long instance, String data);
    public static native String freerdp_get_last_error_string(long instance);

    private static void OnPreConnect(long i) { }
    private static void OnConnectionSuccess(long i) { Listener l=listener; if(l!=null)l.connected(); }
    private static void OnConnectionFailure(long i) { Listener l=listener; if(l!=null)l.failed(freerdp_get_last_error_string(i)); }
    private static void OnDisconnecting(long i) { }
    private static void OnDisconnected(long i) { Listener l=listener; if(l!=null)l.disconnected(); }
    private static void OnSettingsChanged(long i,int w,int h,int bpp) { Listener l=listener; if(l!=null)l.size(w,h); }
    private static void OnGraphicsResize(long i,int w,int h,int bpp) { Listener l=listener; if(l!=null)l.size(w,h); }
    private static void OnGraphicsUpdate(long i,int x,int y,int w,int h) { Listener l=listener; if(l!=null)l.graphics(x,y,w,h); }
    private static boolean OnAuthenticate(long i,StringBuilder u,StringBuilder d,StringBuilder p) { Listener l=listener;return l!=null&&l.authenticate(u,d,p); }
    private static boolean OnGatewayAuthenticate(long i,StringBuilder u,StringBuilder d,StringBuilder p) { return false; }
    private static int OnVerifyCertificateEx(long i,String host,long port,String cn,String subject,String issuer,String fp,long flags) {
        Listener l=listener;return l==null?0:l.certificate(cn,subject,fp,false);
    }
    private static int OnVerifyChangedCertificateEx(long i,String host,long port,String cn,String subject,String issuer,String fp,String oldSubject,String oldIssuer,String oldFp,long flags) {
        Listener l=listener;return l==null?0:l.certificate(cn,subject,fp,true);
    }
    private static boolean OnExperimentalFeature(long i,int feature) { return false; }
    private static void OnRemoteClipboardChanged(long i,String text) { }
    private static void OnRemoteClipboardImageChanged(long i,byte[] data) { }
    private static void OnPointerSet(long i,int[] pixels,int w,int h,int hotX,int hotY) { Listener l=listener;if(l!=null)l.pointer(pixels,w,h,hotX,hotY); }
    private static void OnPointerSetNull(long i) { }
    private static void OnPointerSetDefault(long i) { }
    private static void OnRailWindowUpdate(long i,long id,int w,int h,int[] pixels) { }
    private static void OnRailWindowMove(long i,long id,int x,int y,int w,int h) { }
    private static void OnRailWindowHide(long i,long id) { }
    private static void OnRailWindowDestroy(long i,long id) { }
    private static void OnRailSessionEnd(long i) { }
    private static void OnRailMonitoredDesktop(long i,long[] ids,long active) { }
}

