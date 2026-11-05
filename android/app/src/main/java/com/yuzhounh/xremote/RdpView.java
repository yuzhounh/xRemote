package com.yuzhounh.xremote;

import android.content.Context;
import android.graphics.Bitmap;
import android.graphics.Canvas;
import android.graphics.Color;
import android.graphics.Paint;
import android.graphics.RectF;
import android.text.Editable;
import android.text.SpannableStringBuilder;
import android.view.GestureDetector;
import android.view.KeyEvent;
import android.view.MotionEvent;
import android.view.ScaleGestureDetector;
import android.view.View;
import android.view.inputmethod.BaseInputConnection;
import android.view.inputmethod.EditorInfo;
import android.view.inputmethod.InputConnection;
import com.freerdp.freerdpcore.services.LibFreeRDP;

final class RdpView extends View {
    private final Object frameLock = new Object();
    private final Paint paint = new Paint(Paint.ANTI_ALIAS_FLAG | Paint.FILTER_BITMAP_FLAG);
    private Bitmap frame, pointer;
    private boolean frameReady;
    private final RectF display = new RectF();
    private final RectF cursorRect = new RectF();
    private float zoom=1, panX, panY, downX, downY, focusX, focusY;
    private int cursorX, cursorY, hotX, hotY;
    private boolean dragging, rightClick, multiTouch, keyboardVisible;
    private long instance;
    private final ScaleGestureDetector scale;
    private final GestureDetector gestures;
    RdpView(Context c) {
        super(c); setFocusable(true); setFocusableInTouchMode(true); setBackgroundColor(Color.rgb(18,25,37));
        setLayerType(LAYER_TYPE_SOFTWARE, null);
        scale = new ScaleGestureDetector(c, new ScaleGestureDetector.SimpleOnScaleGestureListener() {
            @Override public boolean onScale(ScaleGestureDetector d) { zoom=Math.max(1,Math.min(4,zoom*d.getScaleFactor()));invalidate();return true; }
        });
        gestures = new GestureDetector(c, new GestureDetector.SimpleOnGestureListener() {
            @Override public boolean onDown(MotionEvent e) { return true; }
            @Override public void onLongPress(MotionEvent e) {
                if (!multiTouch && !dragging) { rightClick=true;move(e.getX(),e.getY(),0x0800);mouse(0x2000|0x8000);mouse(0x2000); }
            }
        });
    }
    void instance(long i) { instance=i; }
    void resizeFrame(int w,int h) {
        if(w<=0||h<=0||w>8192||h>8192)return;
        synchronized(frameLock) { if(frame==null||frame.getWidth()!=w||frame.getHeight()!=h){frame=Bitmap.createBitmap(w,h,Bitmap.Config.ARGB_8888);frameReady=false;} }
        postInvalidate();
    }
    void updateFrame(int x,int y,int w,int h) {
        synchronized(frameLock) {
            if(frame==null||instance==0)return;
            int left=Math.max(0,x),top=Math.max(0,y),width=Math.min(w,frame.getWidth()-left),height=Math.min(h,frame.getHeight()-top);
            if(width>0&&height>0&&LibFreeRDP.freerdp_update_graphics(instance,frame,left,top,width,height))frameReady=true;
        }
        postInvalidateOnAnimation();
    }
    void refreshFrame() { synchronized(frameLock) { if(frame!=null)updateFrame(0,0,frame.getWidth(),frame.getHeight()); } }
    Bitmap desktopPreview(){
        synchronized(frameLock){
            if(frame==null||!frameReady)return null;
            int width=Math.min(480,frame.getWidth()),height=Math.max(1,Math.round((float)width*frame.getHeight()/frame.getWidth()));
            Bitmap image=Bitmap.createBitmap(width,height,Bitmap.Config.ARGB_8888);
            new Canvas(image).drawBitmap(frame,null,new RectF(0,0,width,height),paint);
            int[] colors=new int[512];int largest=0,samples=0;
            for(int y=0;y<14;y++)for(int x=0;x<24;x++){
                int pixel=image.getPixel(Math.min(width-1,(x*2+1)*width/48),Math.min(height-1,(y*2+1)*height/28));
                int bucket=((Color.red(pixel)>>5)<<6)|((Color.green(pixel)>>5)<<3)|(Color.blue(pixel)>>5);
                largest=Math.max(largest,++colors[bucket]);samples++;
            }
            // Do not replace a usable preview with the near-uniform frame during login.
            if(largest>samples*0.97f){image.recycle();return null;}
            return image;
        }
    }
    void pointer(int[] p,int w,int h,int hx,int hy) { synchronized(frameLock){if(p!=null&&w>0&&h>0&&p.length>=w*h){pointer=Bitmap.createBitmap(p,w,h,Bitmap.Config.ARGB_8888);hotX=hx;hotY=hy;}}postInvalidate(); }
    void resetZoom(){zoom=1;panX=panY=0;invalidate();}
    void toggleZoom(){if(zoom>1.01f)resetZoom();else{zoom=1.75f;invalidate();}}
    void keyboardVisible(boolean value){if(keyboardVisible!=value){keyboardVisible=value;invalidate();}}
    @Override protected void onDraw(Canvas canvas) {
        super.onDraw(canvas);
        synchronized(frameLock) {
            if(frame==null)return;
            float widthScale=(float)getWidth()/frame.getWidth();
            float s=(keyboardVisible?widthScale:Math.min(widthScale,(float)getHeight()/frame.getHeight()))*zoom;
            float w=frame.getWidth()*s,h=frame.getHeight()*s;
            panX=Math.max(-Math.max(0,(w-getWidth())/2),Math.min(Math.max(0,(w-getWidth())/2),panX));
            panY=Math.max(-Math.max(0,(h-getHeight())/2),Math.min(Math.max(0,(h-getHeight())/2),panY));
            display.set((getWidth()-w)/2+panX,(getHeight()-h)/2+panY,(getWidth()+w)/2+panX,(getHeight()+h)/2+panY);
            canvas.drawBitmap(frame,null,display,paint);
            if(pointer!=null){float x=display.left+(cursorX-hotX)*s,y=display.top+(cursorY-hotY)*s;cursorRect.set(x,y,x+pointer.getWidth()*s,y+pointer.getHeight()*s);canvas.drawBitmap(pointer,null,cursorRect,paint);}
        }
    }
    private void mouse(int flags){long i=instance;if(i!=0)LibFreeRDP.freerdp_send_cursor_event(i,cursorX,cursorY,flags);}
    private void move(float x,float y,int flags) {
        synchronized(frameLock){if(frame==null||display.width()<=0)return;cursorX=Math.max(0,Math.min(frame.getWidth()-1,(int)((x-display.left)*frame.getWidth()/display.width())));cursorY=Math.max(0,Math.min(frame.getHeight()-1,(int)((y-display.top)*frame.getHeight()/display.height())));}
        mouse(flags);invalidate();
    }
    @Override public boolean onTouchEvent(MotionEvent e) {
        scale.onTouchEvent(e);gestures.onTouchEvent(e);
        switch(e.getActionMasked()) {
            case MotionEvent.ACTION_DOWN -> { requestFocus();downX=e.getX();downY=e.getY();dragging=rightClick=multiTouch=false;move(e.getX(),e.getY(),0x0800); }
            case MotionEvent.ACTION_POINTER_DOWN -> { multiTouch=true;focusX=(e.getX(0)+e.getX(1))/2;focusY=(e.getY(0)+e.getY(1))/2;if(dragging){mouse(0x1000);dragging=false;} }
            case MotionEvent.ACTION_MOVE -> {
                if(e.getPointerCount()>1){float x=(e.getX(0)+e.getX(1))/2,y=(e.getY(0)+e.getY(1))/2;panX+=x-focusX;panY+=y-focusY;focusX=x;focusY=y;invalidate();}
                else if(!multiTouch&&!rightClick){if(!dragging&&Math.hypot(e.getX()-downX,e.getY()-downY)>14){dragging=true;mouse(0x1000|0x8000);}if(dragging)move(e.getX(),e.getY(),0x0800);}
            }
            case MotionEvent.ACTION_UP -> {if(dragging)mouse(0x1000);else if(!rightClick&&!multiTouch){move(e.getX(),e.getY(),0x0800);mouse(0x1000|0x8000);mouse(0x1000);}performClick();}
            case MotionEvent.ACTION_CANCEL -> {if(dragging)mouse(0x1000);dragging=false;}
        }
        return true;
    }
    @Override public boolean performClick(){super.performClick();return true;}
    void virtualKey(int key,boolean down){if(instance!=0)LibFreeRDP.freerdp_send_key_event(instance,key,down);}
    void tapKey(int key){virtualKey(key,true);virtualKey(key,false);}
    void text(CharSequence text){if(instance!=0)for(int n=0;n<text.length();n++){int c=text.charAt(n);LibFreeRDP.freerdp_send_unicodekey_event(instance,c,true);LibFreeRDP.freerdp_send_unicodekey_event(instance,c,false);}}
    @Override public boolean onCheckIsTextEditor(){return true;}
    @Override public InputConnection onCreateInputConnection(EditorInfo info){
        info.inputType=android.text.InputType.TYPE_CLASS_TEXT|android.text.InputType.TYPE_TEXT_FLAG_NO_SUGGESTIONS;
        info.imeOptions=EditorInfo.IME_FLAG_NO_EXTRACT_UI|EditorInfo.IME_ACTION_NONE;
        return new BaseInputConnection(this,false){
            private final Editable editable=new SpannableStringBuilder();
            private String composing="";
            @Override public Editable getEditable(){return editable;}
            @Override public boolean commitText(CharSequence value,int pos){text(value);composing="";editable.clear();return true;}
            @Override public boolean setComposingText(CharSequence value,int pos){composing=value.toString();return true;}
            @Override public boolean finishComposingText(){if(!composing.isEmpty()){text(composing);composing="";}return true;}
            @Override public boolean deleteSurroundingText(int before,int after){if(!composing.isEmpty()){composing="";return true;}for(int i=0;i<Math.min(before,30);i++)tapKey(8);return true;}
            @Override public boolean sendKeyEvent(KeyEvent e){return handleKey(e);}
        };
    }
    boolean handleKey(KeyEvent e){
        int code=e.getKeyCode(),vk=switch(code){case KeyEvent.KEYCODE_DEL->8;case KeyEvent.KEYCODE_ENTER->13;case KeyEvent.KEYCODE_TAB->9;case KeyEvent.KEYCODE_ESCAPE->27;case KeyEvent.KEYCODE_DPAD_LEFT->37;case KeyEvent.KEYCODE_DPAD_UP->38;case KeyEvent.KEYCODE_DPAD_RIGHT->39;case KeyEvent.KEYCODE_DPAD_DOWN->40;case KeyEvent.KEYCODE_CTRL_LEFT,KeyEvent.KEYCODE_CTRL_RIGHT->17;case KeyEvent.KEYCODE_SHIFT_LEFT,KeyEvent.KEYCODE_SHIFT_RIGHT->16;case KeyEvent.KEYCODE_ALT_LEFT,KeyEvent.KEYCODE_ALT_RIGHT->18;case KeyEvent.KEYCODE_FORWARD_DEL->46;default->0;};
        if(vk!=0){virtualKey(vk,e.getAction()==KeyEvent.ACTION_DOWN);return true;}
        if(e.isCtrlPressed()&&code>=KeyEvent.KEYCODE_A&&code<=KeyEvent.KEYCODE_Z){virtualKey(65+code-KeyEvent.KEYCODE_A,e.getAction()==KeyEvent.ACTION_DOWN);return true;}
        if(e.getAction()==KeyEvent.ACTION_DOWN&&e.getUnicodeChar()!=0){text(new String(Character.toChars(e.getUnicodeChar())));return true;}
        return e.getAction()==KeyEvent.ACTION_UP;
    }
    @Override public boolean onKeyDown(int key,KeyEvent e){return handleKey(e)||super.onKeyDown(key,e);}
    @Override public boolean onKeyUp(int key,KeyEvent e){return handleKey(e)||super.onKeyUp(key,e);}
}
