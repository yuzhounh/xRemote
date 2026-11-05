/* SPDX-License-Identifier: GPL-3.0-or-later
 * Java ABI adapter for the pinned AVNC 3.3.1 native-vnc library.
 * Protocol, authentication, decoding and framebuffer locking remain in AVNC/LibVNCClient.
 */
package com.gaurav.avnc.vnc;

import java.io.IOException;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.locks.ReentrantReadWriteLock;

public final class VncClient {
    public interface Observer {
        void size(int width,int height);
        void frame();
        void cursor(int x,int y);
    }
    static { System.loadLibrary("native-vnc"); initLibrary(); }
    private final Observer observer;
    private final ReentrantReadWriteLock state=new ReentrantReadWriteLock();
    private final ExecutorService input=Executors.newSingleThreadExecutor(r->{Thread t=new Thread(r,"xremote-vnc-input");t.setDaemon(true);return t;});
    private long pointer;
    private String username,password;
    private volatile boolean connected,stopping;

    public VncClient(String username,String password,Observer observer) {
        this.username=username;this.password=password;this.observer=observer;
        pointer=nativeClientCreate();
        if(pointer==0)throw new IllegalStateException("VNC Initialization failed");
    }
    // Called only on the session worker. The local bridge supplies cancellation/EOF.
    public void connect(String host,int port) throws IOException {
        state.readLock().lock();
        try {
            if(stopping)return;
            nativeConfigure(pointer,2,false,7,false); // Mac advertises standard VNC password authentication.
            if(!nativeInit(pointer,host,port))throw new IOException("VNC Authentication or connection failed; check the screen-sharing password");
            connected=true;
        } finally { state.readLock().unlock(); }
    }
    public void receive() throws IOException {
        state.readLock().lock();
        try { if(!stopping&&connected&&!nativeProcessServerMessage(pointer))throw new IOException("VNC Connection closed"); }
        finally { state.readLock().unlock(); }
    }
    public boolean isStopping(){return stopping;}
    public void stop(){stopping=true;connected=false;input.shutdownNow();}
    // Never invoked on the UI thread, and only after the receive loop has returned.
    public void cleanup() {
        stop();state.writeLock().lock();
        try {if(pointer!=0){nativeCleanup(pointer);pointer=0;}username="";password="";}
        finally {state.writeLock().unlock();}
    }
    private void send(Runnable action) {
        if(stopping)return;
        try {input.execute(()->{state.readLock().lock();try{if(connected&&!stopping&&pointer!=0)action.run();}finally{state.readLock().unlock();}});}
        catch(java.util.concurrent.RejectedExecutionException ignored){}
    }
    public void key(int keysym,boolean down){send(()->nativeSendKeyEvent(pointer,keysym,0,down));}
    public void pointer(int x,int y,int buttons){send(()->nativeSendPointerEvent(pointer,x,y,buttons));}
    public void text(String text){
        send(()->text.codePoints().forEach(code->{int sym=code<=255?code:0x01000000|code;nativeSendKeyEvent(pointer,sym,0,true);nativeSendKeyEvent(pointer,sym,0,false);}));
    }
    public boolean uploadTexture() {
        if(stopping||!connected||!state.readLock().tryLock())return false;
        try {if(stopping||!connected||pointer==0)return false;nativeUploadFrameTexture(pointer);return true;}
        finally {state.readLock().unlock();}
    }
    private String cbGetPassword(){return password==null?"":password;}
    private UserCredential cbGetCredential(){return new UserCredential(username==null?"":username,password==null?"":password);}
    private boolean cbVerifyServerCertificate(byte[] der){return false;}
    private void cbGotXCutText(byte[] bytes,boolean utf8){} // Clipboard is not synchronized automatically.
    private void cbFinishedFrameBufferUpdate(){if(!stopping)observer.frame();}
    private void cbFramebufferSizeChanged(int width,int height){if(!stopping)observer.size(width,height);}
    private void cbBell(){}
    private void cbHandleCursorPos(int x,int y){if(!stopping)observer.cursor(x,y);}
    private void cbHandleCursorInfo(int w,int h,int hotX,int hotY){}
    private static native void initLibrary();
    private native long nativeClientCreate();
    private native void nativeConfigure(long p,int security,boolean cursor,int quality,boolean raw);
    private native boolean nativeInit(long p,String host,int port);
    private native boolean nativeProcessServerMessage(long p);
    private native boolean nativeSendKeyEvent(long p,int key,int xt,boolean down);
    private native boolean nativeSendPointerEvent(long p,int x,int y,int mask);
    private native void nativeUploadFrameTexture(long p);
    private native void nativeCleanup(long p);
}
