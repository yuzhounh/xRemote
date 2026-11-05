package com.yuzhounh.xremote;

import java.io.Closeable;
import java.io.IOException;
import java.net.InetAddress;
import java.net.InetSocketAddress;
import java.net.ServerSocket;
import java.net.Socket;

/** Owns both sockets so closing a session interrupts native reads without freeing an active JNI pointer. */
final class VncConnectionBridge implements Closeable {
    private final ServerSocket listener;
    private volatile Socket client,upstream;
    private volatile boolean closed;
    VncConnectionBridge(String host,int port)throws IOException {
        listener=new ServerSocket(0,1,InetAddress.getByName("127.0.0.1"));
        Thread accept=new Thread(()->{
            try{
                client=listener.accept();client.setTcpNoDelay(true);
                if(closed){close();return;}
                upstream=new Socket();upstream.connect(new InetSocketAddress(host,port),5000);upstream.setTcpNoDelay(true);
                if(closed){close();return;}
                Thread reverse=new Thread(()->copy(upstream,client),"xremote-vnc-in");reverse.setDaemon(true);reverse.start();
                copy(client,upstream);
            }catch(IOException ignored){close();}
        },"xremote-vnc-bridge");accept.setDaemon(true);accept.start();
    }
    int port(){return listener.getLocalPort();}
    private void copy(Socket source,Socket target){
        try{source.getInputStream().transferTo(target.getOutputStream());}catch(IOException ignored){}finally{close();}
    }
    @Override public void close(){
        closed=true;
        for(Closeable value:new Closeable[]{listener,client,upstream})if(value!=null)try{value.close();}catch(IOException ignored){}
    }
}
