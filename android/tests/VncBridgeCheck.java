package com.yuzhounh.xremote;

import java.net.InetAddress;
import java.net.ServerSocket;
import java.net.Socket;
import java.nio.charset.StandardCharsets;
import java.util.concurrent.Executors;
import java.util.concurrent.TimeUnit;

/** Exercises cancellation of a blocked native-side read without requiring a Mac password. */
public final class VncBridgeCheck {
    public static void main(String[] args)throws Exception {
        var worker=Executors.newSingleThreadExecutor();
        try(var upstream=new ServerSocket(0,1,InetAddress.getByName("127.0.0.1"));
            var bridge=new VncConnectionBridge("127.0.0.1",upstream.getLocalPort());
            var nativeSide=new Socket("127.0.0.1",bridge.port());
            var server=upstream.accept()){
            server.setSoTimeout(2000);nativeSide.setSoTimeout(2000);
            byte[] banner="RFB 003.889\n".getBytes(StandardCharsets.US_ASCII);
            server.getOutputStream().write(banner);
            if(!java.util.Arrays.equals(nativeSide.getInputStream().readNBytes(12),banner))throw new AssertionError("Server data changed");
            nativeSide.getOutputStream().write(123);
            if(server.getInputStream().read()!=123)throw new AssertionError("Client input not forwarded");
            var blocked=worker.submit(()->nativeSide.getInputStream().read());
            bridge.close();
            if(blocked.get(2,TimeUnit.SECONDS)!=-1)throw new AssertionError("Cancellation did not produce EOF");
            System.out.println("PASS VNC data/input forwarding and blocked-read cancellation");
        }finally{worker.shutdownNow();}
    }
}
