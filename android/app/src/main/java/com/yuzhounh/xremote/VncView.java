package com.yuzhounh.xremote;

import android.content.Context;
import android.graphics.Bitmap;
import android.graphics.RectF;
import android.opengl.GLES20;
import android.opengl.GLSurfaceView;
import android.text.Editable;
import android.text.SpannableStringBuilder;
import android.view.GestureDetector;
import android.view.KeyEvent;
import android.view.MotionEvent;
import android.view.ScaleGestureDetector;
import android.view.inputmethod.BaseInputConnection;
import android.view.inputmethod.EditorInfo;
import android.view.inputmethod.InputConnection;
import com.gaurav.avnc.vnc.VncClient;
import java.nio.ByteBuffer;
import java.nio.ByteOrder;
import java.nio.FloatBuffer;
import java.util.concurrent.atomic.AtomicBoolean;
import javax.microedition.khronos.egl.EGLConfig;
import javax.microedition.khronos.opengles.GL10;

/** GLES2 presentation of AVNC's framebuffer; follows AVNC 3.3.1 BGRA/Y-axis conventions. */
final class VncView extends GLSurfaceView implements GLSurfaceView.Renderer {
    private volatile VncClient client;
    private volatile int desktopWidth,desktopHeight;
    private final AtomicBoolean dirty=new AtomicBoolean(true);
    private final RectF image=new RectF();
    private volatile float zoom=1,panX,panY;
    private boolean keyboardVisible,multiTouch,dragging,rightClick;
    private float downX,downY,focusX,focusY;
    private int pointerX,pointerY,buttons;
    private int program,texture,viewWidth,viewHeight;
    private boolean textureReady;
    private final ScaleGestureDetector scale;
    private final GestureDetector gestures;
    private java.util.function.Consumer<Bitmap> capture;

    VncView(Context context){
        super(context);setEGLContextClientVersion(2);setPreserveEGLContextOnPause(true);
        setRenderer(this);setRenderMode(RENDERMODE_WHEN_DIRTY);setFocusable(true);setFocusableInTouchMode(true);
        scale=new ScaleGestureDetector(context,new ScaleGestureDetector.SimpleOnScaleGestureListener(){
            @Override public boolean onScale(ScaleGestureDetector d){zoom=Math.max(1,Math.min(4,zoom*d.getScaleFactor()));requestRender();return true;}
        });
        gestures=new GestureDetector(context,new GestureDetector.SimpleOnGestureListener(){
            @Override public boolean onDown(MotionEvent e){return true;}
            @Override public void onLongPress(MotionEvent e){if(!multiTouch&&!dragging){rightClick=true;move(e.getX(),e.getY());sendPointer(4);sendPointer(0);}}
        });
    }
    void client(VncClient value){client=value;dirty.set(true);requestRender();}
    void size(int w,int h){if(w>0&&h>0&&w<=16384&&h<=16384){desktopWidth=w;desktopHeight=h;dirty.set(true);requestRender();}}
    void updated(){dirty.set(true);requestRender();}
    void keyboardVisible(boolean value){keyboardVisible=value;requestRender();}
    void toggleZoom(){if(zoom>1.01f)resetZoom();else{zoom=1.75f;requestRender();}}
    void resetZoom(){zoom=1;panX=panY=0;requestRender();}
    void releaseInput(){sendPointer(0);for(int key:new int[]{0xffe1,0xffe2,0xffe3,0xffe4,0xffe9,0xffea,0xffeb,0xffec})key(key,false);}
    void key(int key,boolean down){VncClient c=client;if(c!=null)c.key(key,down);}
    void tap(int key){key(key,true);key(key,false);}
    void shortcut(int modifier,int key){key(modifier,true);tap(key);key(modifier,false);}
    void text(CharSequence value){
        VncClient c=client;if(c==null)return;
        StringBuilder run=new StringBuilder();
        value.toString().codePoints().forEach(code->{if(code==10||code==13||code==9){if(run.length()>0){c.text(run.toString());run.setLength(0);}tap(code==9?0xff09:0xff0d);}else run.appendCodePoint(code);});
        if(run.length()>0)c.text(run.toString());
    }
    private synchronized RectF layoutImage(){
        int w=desktopWidth,h=desktopHeight;if(w<=0||h<=0||getWidth()==0)return new RectF();
        float fit=(float)getWidth()/w;if(!keyboardVisible)fit=Math.min(fit,(float)getHeight()/h);
        float width=w*fit*zoom,height=h*fit*zoom;
        panX=Math.max(-Math.max(0,(width-getWidth())/2),Math.min(Math.max(0,(width-getWidth())/2),panX));
        panY=Math.max(-Math.max(0,(height-getHeight())/2),Math.min(Math.max(0,(height-getHeight())/2),panY));
        image.set((getWidth()-width)/2+panX,(getHeight()-height)/2+panY,(getWidth()+width)/2+panX,(getHeight()+height)/2+panY);
        return new RectF(image);
    }
    private void move(float x,float y){RectF rect=layoutImage();if(rect.width()<=0)return;pointerX=Math.max(0,Math.min(desktopWidth-1,(int)((x-rect.left)*desktopWidth/rect.width())));pointerY=Math.max(0,Math.min(desktopHeight-1,(int)((y-rect.top)*desktopHeight/rect.height())));sendPointer(buttons);}
    private void sendPointer(int mask){buttons=mask;VncClient c=client;if(c!=null)c.pointer(pointerX,pointerY,mask);}
    @Override public boolean onTouchEvent(MotionEvent e){
        scale.onTouchEvent(e);gestures.onTouchEvent(e);
        switch(e.getActionMasked()){
            case MotionEvent.ACTION_DOWN->{requestFocus();downX=e.getX();downY=e.getY();dragging=rightClick=multiTouch=false;move(downX,downY);}
            case MotionEvent.ACTION_POINTER_DOWN->{multiTouch=true;sendPointer(0);dragging=false;focusX=(e.getX(0)+e.getX(1))/2;focusY=(e.getY(0)+e.getY(1))/2;}
            case MotionEvent.ACTION_MOVE->{if(e.getPointerCount()>1){float x=(e.getX(0)+e.getX(1))/2,y=(e.getY(0)+e.getY(1))/2;panX+=x-focusX;panY+=y-focusY;focusX=x;focusY=y;requestRender();}else if(!multiTouch&&!rightClick){if(!dragging&&Math.hypot(e.getX()-downX,e.getY()-downY)>14){dragging=true;sendPointer(1);}if(dragging)move(e.getX(),e.getY());}}
            case MotionEvent.ACTION_UP->{if(dragging)sendPointer(0);else if(!multiTouch&&!rightClick){move(e.getX(),e.getY());sendPointer(1);sendPointer(0);}performClick();}
            case MotionEvent.ACTION_CANCEL->{sendPointer(0);dragging=false;}
        }return true;
    }
    @Override public boolean performClick(){super.performClick();return true;}
    @Override public boolean onCheckIsTextEditor(){return true;}
    @Override public InputConnection onCreateInputConnection(EditorInfo info){
        info.inputType=android.text.InputType.TYPE_CLASS_TEXT|android.text.InputType.TYPE_TEXT_FLAG_NO_SUGGESTIONS;info.imeOptions=EditorInfo.IME_FLAG_NO_EXTRACT_UI|EditorInfo.IME_ACTION_NONE;
        return new BaseInputConnection(this,false){
            private final Editable editable=new SpannableStringBuilder();
            @Override public Editable getEditable(){return editable;}
            @Override public boolean commitText(CharSequence value,int pos){text(value);editable.clear();return true;}
            @Override public boolean setComposingText(CharSequence value,int pos){editable.replace(0,editable.length(),value);return true;}
            @Override public boolean finishComposingText(){editable.clear();return true;}
            @Override public boolean deleteSurroundingText(int before,int after){if(editable.length()>0){editable.clear();return true;}for(int i=0;i<Math.min(before,30);i++)tap(0xff08);for(int i=0;i<Math.min(after,30);i++)tap(0xffff);return true;}
            @Override public boolean sendKeyEvent(KeyEvent event){return handleKey(event);}
        };
    }
    boolean handleKey(KeyEvent e){
        int code=e.getKeyCode();int key=switch(code){case KeyEvent.KEYCODE_DEL->0xff08;case KeyEvent.KEYCODE_FORWARD_DEL->0xffff;case KeyEvent.KEYCODE_ENTER->0xff0d;case KeyEvent.KEYCODE_TAB->0xff09;case KeyEvent.KEYCODE_ESCAPE->0xff1b;case KeyEvent.KEYCODE_DPAD_LEFT->0xff51;case KeyEvent.KEYCODE_DPAD_UP->0xff52;case KeyEvent.KEYCODE_DPAD_RIGHT->0xff53;case KeyEvent.KEYCODE_DPAD_DOWN->0xff54;case KeyEvent.KEYCODE_SHIFT_LEFT->0xffe1;case KeyEvent.KEYCODE_SHIFT_RIGHT->0xffe2;case KeyEvent.KEYCODE_CTRL_LEFT->0xffe3;case KeyEvent.KEYCODE_CTRL_RIGHT->0xffe4;case KeyEvent.KEYCODE_ALT_LEFT->0xffe9;case KeyEvent.KEYCODE_ALT_RIGHT->0xffea;case KeyEvent.KEYCODE_META_LEFT->0xffeb;case KeyEvent.KEYCODE_META_RIGHT->0xffec;default->0;};
        if(key!=0){key(key,e.getAction()==KeyEvent.ACTION_DOWN);return true;}
        if((e.isCtrlPressed()||e.isAltPressed()||e.isMetaPressed())&&code>=KeyEvent.KEYCODE_A&&code<=KeyEvent.KEYCODE_Z){key('a'+code-KeyEvent.KEYCODE_A,e.getAction()==KeyEvent.ACTION_DOWN);return true;}
        if(e.getAction()==KeyEvent.ACTION_MULTIPLE&&e.getCharacters()!=null){text(e.getCharacters());return true;}
        if(e.getAction()==KeyEvent.ACTION_DOWN&&e.getUnicodeChar()!=0){text(new String(Character.toChars(e.getUnicodeChar())));return true;}return e.getAction()==KeyEvent.ACTION_UP;
    }
    @Override public void onSurfaceCreated(GL10 gl,EGLConfig config){
        String vertex="attribute vec2 aPosition;attribute vec2 aTex;varying vec2 tex;void main(){gl_Position=vec4(aPosition,0.,1.);tex=aTex;}";
        String fragment="precision mediump float;varying vec2 tex;uniform sampler2D image;void main(){gl_FragColor=vec4(texture2D(image,tex).bgr,1.);}";
        program=GLES20.glCreateProgram();GLES20.glAttachShader(program,shader(GLES20.GL_VERTEX_SHADER,vertex));GLES20.glAttachShader(program,shader(GLES20.GL_FRAGMENT_SHADER,fragment));GLES20.glLinkProgram(program);
        int[] values=new int[1];GLES20.glGenTextures(1,values,0);texture=values[0];GLES20.glBindTexture(GLES20.GL_TEXTURE_2D,texture);GLES20.glTexParameteri(GLES20.GL_TEXTURE_2D,GLES20.GL_TEXTURE_MIN_FILTER,GLES20.GL_LINEAR);GLES20.glTexParameteri(GLES20.GL_TEXTURE_2D,GLES20.GL_TEXTURE_MAG_FILTER,GLES20.GL_LINEAR);GLES20.glTexParameteri(GLES20.GL_TEXTURE_2D,GLES20.GL_TEXTURE_WRAP_S,GLES20.GL_CLAMP_TO_EDGE);GLES20.glTexParameteri(GLES20.GL_TEXTURE_2D,GLES20.GL_TEXTURE_WRAP_T,GLES20.GL_CLAMP_TO_EDGE);GLES20.glDisable(GLES20.GL_BLEND);textureReady=false;dirty.set(true);
    }
    private int shader(int kind,String source){int shader=GLES20.glCreateShader(kind);GLES20.glShaderSource(shader,source);GLES20.glCompileShader(shader);return shader;}
    @Override public void onSurfaceChanged(GL10 gl,int w,int h){viewWidth=w;viewHeight=h;GLES20.glViewport(0,0,w,h);}
    @Override public void onDrawFrame(GL10 gl){
        GLES20.glClearColor(0,0,0,1);GLES20.glClear(GLES20.GL_COLOR_BUFFER_BIT);VncClient c=client;if(c==null||c.isStopping()||desktopWidth<=0||viewWidth==0)return;
        GLES20.glBindTexture(GLES20.GL_TEXTURE_2D,texture);if(dirty.getAndSet(false))textureReady=c.uploadTexture();if(!textureReady)return;
        RectF rect=layoutImage();float left=rect.left*2/viewWidth-1,right=rect.right*2/viewWidth-1,top=1-rect.top*2/viewHeight,bottom=1-rect.bottom*2/viewHeight;
        float[] vertices={left,top,0,0,right,top,1,0,left,bottom,0,1,right,bottom,1,1};FloatBuffer buffer=ByteBuffer.allocateDirect(vertices.length*4).order(ByteOrder.nativeOrder()).asFloatBuffer();buffer.put(vertices).position(0);
        GLES20.glUseProgram(program);int position=GLES20.glGetAttribLocation(program,"aPosition"),tex=GLES20.glGetAttribLocation(program,"aTex");GLES20.glEnableVertexAttribArray(position);GLES20.glEnableVertexAttribArray(tex);GLES20.glVertexAttribPointer(position,2,GLES20.GL_FLOAT,false,16,buffer);buffer.position(2);GLES20.glVertexAttribPointer(tex,2,GLES20.GL_FLOAT,false,16,buffer);GLES20.glUniform1i(GLES20.glGetUniformLocation(program,"image"),0);GLES20.glDrawArrays(GLES20.GL_TRIANGLE_STRIP,0,4);
        if(capture!=null){var callback=capture;capture=null;Bitmap bitmap=readPreview();post(()->callback.accept(bitmap));}
    }
    void capture(java.util.function.Consumer<Bitmap> callback){queueEvent(()->capture=callback);requestRender();}
    private Bitmap readPreview(){
        if(viewWidth<=0||viewHeight<=0)return null;
        ByteBuffer pixels=ByteBuffer.allocateDirect(viewWidth*viewHeight*4);GLES20.glReadPixels(0,0,viewWidth,viewHeight,GLES20.GL_RGBA,GLES20.GL_UNSIGNED_BYTE,pixels);
        int width=Math.min(480,viewWidth),height=Math.max(1,width*viewHeight/viewWidth);int[] colors=new int[width*height];
        for(int y=0;y<height;y++)for(int x=0;x<width;x++){int index=((viewHeight-1-y*viewHeight/height)*viewWidth+x*viewWidth/width)*4;colors[y*width+x]=0xff000000|((pixels.get(index)&255)<<16)|((pixels.get(index+1)&255)<<8)|(pixels.get(index+2)&255);}
        boolean varied=false;for(int i=0;i<colors.length;i+=Math.max(1,colors.length/100))if(colors[i]!=colors[0]){varied=true;break;}return varied?Bitmap.createBitmap(colors,width,height,Bitmap.Config.ARGB_8888):null;
    }
}
