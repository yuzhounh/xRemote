package com.yuzhounh.xremote;

import android.content.Context;
import android.graphics.Bitmap;
import android.graphics.BitmapFactory;
import android.util.AtomicFile;
import java.io.File;
import java.io.FileOutputStream;
import java.util.concurrent.ConcurrentHashMap;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;

final class DesktopPreview {
    private static final ConcurrentHashMap<String,Bitmap> images=new ConcurrentHashMap<>();
    private static final ExecutorService writer=Executors.newSingleThreadExecutor();
    private static File file(Context context,Device device){return new File(new File(context.getFilesDir(),"desktop-previews"),device.id()+".jpg");}
    static Bitmap load(Context context,Device device){
        Bitmap image=images.get(device.id());
        if(image==null){image=BitmapFactory.decodeFile(file(context,device).getPath());if(image!=null)images.put(device.id(),image);}
        return image;
    }
    static void save(Context context,Device device,Bitmap image){
        save(context,device,image,null);
    }
    static void save(Context context,Device device,Bitmap image,java.util.function.Consumer<Boolean> completion){
        if(image==null)return;
        images.put(device.id(),image);
        File target=file(context,device);
        writer.execute(()->{
            AtomicFile output=new AtomicFile(target);FileOutputStream stream=null;boolean saved=false;
            try{
                File directory=target.getParentFile();
                if(directory==null||(!directory.isDirectory()&&!directory.mkdirs()))throw new java.io.IOException("Preview directory unavailable");
                stream=output.startWrite();
                if(!image.compress(Bitmap.CompressFormat.JPEG,78,stream))throw new java.io.IOException("Preview compression failed");
                output.finishWrite(stream);
                saved=true;
            }catch(Exception ignored){if(stream!=null)output.failWrite(stream);}
            if(completion!=null){boolean success=saved;new android.os.Handler(android.os.Looper.getMainLooper()).post(()->completion.accept(success));}
        });
    }
}
