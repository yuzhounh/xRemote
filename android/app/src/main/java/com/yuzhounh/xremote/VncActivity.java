package com.yuzhounh.xremote;

import android.app.Activity;
import android.content.Context;
import android.content.Intent;
import android.graphics.Color;
import android.os.Bundle;
import android.view.Gravity;
import android.view.KeyEvent;
import android.view.WindowInsets;
import android.view.WindowInsetsController;
import android.view.WindowManager;
import android.view.inputmethod.InputMethodManager;
import android.widget.FrameLayout;
import android.widget.ImageButton;
import android.widget.LinearLayout;
import android.widget.TextView;
import com.gaurav.avnc.vnc.VncClient;
import java.lang.ref.WeakReference;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.TimeUnit;

public final class VncActivity extends Activity implements VncClient.Observer {
    private static volatile WeakReference<VncActivity> current=new WeakReference<>(null);
    private volatile VncClient client;
    private volatile VncConnectionBridge bridge;
    private volatile boolean closing;
    private volatile int frames;
    private final CountDownLatch released=new CountDownLatch(1);
    private VncView display;
    private TextView status;
    private Device device;
    private boolean previewOnly,previewSaved,previewFinishing,capturePending;
    private String username,password;
    static void disconnectCurrent(){VncActivity activity=current.get();if(activity!=null)activity.disconnect();}

    @Override public void onCreate(Bundle saved){
        super.onCreate(saved);
        String id=getIntent().getStringExtra("device");username=getIntent().getStringExtra("user");password=getIntent().getStringExtra("password");getIntent().removeExtra("password");
        if(id==null||password==null){finish();return;}device=Device.find(id);if(device.protocol()!=Device.Protocol.VNC){finish();return;}
        if(username==null)username="";previewOnly=getIntent().getBooleanExtra("previewOnly",false);current=new WeakReference<>(this);
        getWindow().addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON);getWindow().setDecorFitsSystemWindows(false);
        getWindow().getAttributes().layoutInDisplayCutoutMode=WindowManager.LayoutParams.LAYOUT_IN_DISPLAY_CUTOUT_MODE_SHORT_EDGES;
        FrameLayout root=new FrameLayout(this);root.setBackgroundColor(Color.BLACK);
        display=new VncView(this);root.addView(display,new FrameLayout.LayoutParams(-1,-1));
        status=Ui.text(this,device.name()+L10n.text(R.string.ui_vnc_connecting),14,Color.WHITE,false);status.setGravity(Gravity.CENTER);root.addView(status,new FrameLayout.LayoutParams(-2,-2,Gravity.CENTER));
        LinearLayout bar=new LinearLayout(this);bar.setGravity(Gravity.CENTER);bar.setPadding(Ui.dp(this,4),0,Ui.dp(this,4),0);bar.setBackground(Ui.background(Color.argb(235,242,245,249),Ui.dp(this,18)));bar.setElevation(Ui.dp(this,4));
        button(bar,R.drawable.remote_zoom,L10n.text(R.string.ui_zoom_restore),()->display.toggleZoom());button(bar,R.drawable.remote_session,L10n.text(R.string.ui_mac_session_menu),this::sessionMenu);button(bar,R.drawable.remote_keyboard,L10n.text(R.string.ui_show_hide_keyboard),this::toggleKeyboard);
        FrameLayout.LayoutParams barLayout=new FrameLayout.LayoutParams(-2,Ui.dp(this,48),Gravity.TOP|Gravity.CENTER_HORIZONTAL);barLayout.topMargin=Ui.dp(this,6);root.addView(bar,barLayout);
        root.setOnApplyWindowInsetsListener((view,insets)->{var safe=insets.getInsets(WindowInsets.Type.displayCutout());root.setPadding(safe.left,safe.top,safe.right,Math.max(safe.bottom,insets.getInsets(WindowInsets.Type.ime()).bottom));display.keyboardVisible(insets.isVisible(WindowInsets.Type.ime()));return insets;});
        setContentView(root);root.requestApplyInsets();hideBars();display.post(this::startConnection);
        display.postDelayed(()->{if(!closing&&frames==0)failed(L10n.text(R.string.ui_no_desktop_image_received_yet_please_ensure_mac_is_awak));},20000);
        if(previewOnly)display.postDelayed(()->{if(!isFinishing()&&!closing)finishPreview(RESULT_FIRST_USER);},30000);
    }
    private void button(LinearLayout bar,int icon,String description,Runnable action){
        ImageButton button=new ImageButton(this);button.setImageResource(icon);button.setContentDescription(description);button.setTooltipText(description);android.util.TypedValue background=new android.util.TypedValue();getTheme().resolveAttribute(android.R.attr.selectableItemBackgroundBorderless,background,true);button.setBackgroundResource(background.resourceId);button.setPadding(Ui.dp(this,11),Ui.dp(this,11),Ui.dp(this,11),Ui.dp(this,11));button.setOnClickListener(v->action.run());bar.addView(button,new LinearLayout.LayoutParams(Ui.dp(this,48),Ui.dp(this,48)));
    }
    private void hideBars(){var controller=getWindow().getInsetsController();if(controller!=null){controller.setSystemBarsBehavior(WindowInsetsController.BEHAVIOR_SHOW_TRANSIENT_BARS_BY_SWIPE);controller.hide(WindowInsets.Type.systemBars());}}
    @Override public void onWindowFocusChanged(boolean focused){super.onWindowFocusChanged(focused);if(focused)hideBars();}
    private void toggleKeyboard(){display.requestFocus();var ime=(InputMethodManager)getSystemService(Context.INPUT_METHOD_SERVICE);WindowInsets insets=display.getRootWindowInsets();if(insets!=null&&insets.isVisible(WindowInsets.Type.ime()))ime.hideSoftInputFromWindow(display.getWindowToken(),0);else ime.showSoftInput(display,InputMethodManager.SHOW_IMPLICIT);}
    private void sessionMenu(){
        Ui.dialog(this).setTitle(device.name()).setItems(new String[]{L10n.text(R.string.ui_command_a_select_all),L10n.text(R.string.ui_command_c_copy),L10n.text(R.string.ui_command_v_paste),L10n.text(R.string.ui_command_tab_switch_app),"Option+Backspace","Esc",L10n.text(R.string.ui_restore_full_desktop),L10n.text(R.string.ui_disconnect_and_return_to_device_list)},(dialog,n)->{
            if(n<=3)display.shortcut(0xffeb,n==0?'a':n==1?'c':n==2?'v':0xff09);
            if(n==4)display.shortcut(0xffe9,0xff08);if(n==5)display.tap(0xff1b);if(n==6)display.resetZoom();if(n==7)finish();
        }).setNegativeButton(L10n.text(R.string.ui_cancel),null).show();
    }
    private void startConnection(){
        final String user=username,secret=password;username=null;password=null;
        new Thread(()->{
            VncClient owned=null;
            try{
                if(closing)return;
                bridge=new VncConnectionBridge(device.connectHost(),device.connectPort());
                if(closing){bridge.close();return;}
                owned=new VncClient(user,secret,this);client=owned;display.client(owned);
                if(closing){owned.stop();return;}
                owned.connect("127.0.0.1",bridge.port());
                runOnUiThread(()->{if(!closing){if(frames>0)status.setVisibility(android.view.View.GONE);else status.setText(L10n.text(R.string.ui_connected_receiving_mac_desktop));display.updated();}});
                while(!closing&&!owned.isStopping())owned.receive();
            }catch(Throwable error){if(!closing)failed(error instanceof java.io.IOException?error.getMessage():L10n.text(R.string.ui_vnc_initialization_failed_please_go_back_and_retry));}
            finally{if(owned!=null)owned.cleanup();if(bridge!=null)bridge.close();client=null;released.countDown();}
        },"xremote-vnc-session").start();
    }
    @Override public void size(int width,int height){if(!closing)display.size(width,height);}
    @Override public void frame(){
        if(closing)return;frames++;display.updated();
        runOnUiThread(()->{if(!closing){status.setVisibility(android.view.View.GONE);if(!previewSaved&&!capturePending){capturePending=true;display.postDelayed(this::capturePreview,1500);}}});
    }
    @Override public void cursor(int x,int y){}
    private void capturePreview(){
        if(closing||isFinishing()){capturePending=false;return;}
        display.capture(bitmap->{capturePending=false;if(closing||isFinishing())return;if(bitmap==null)return;DesktopPreview.save(this,device,bitmap,success->{if(success)previewSaved=true;if(previewOnly&&success)finishPreview(RESULT_OK);});});
    }
    private void failed(String message){
        runOnUiThread(()->{if(isFinishing()||closing)return;disconnect();if(previewOnly){finishPreview(RESULT_FIRST_USER);return;}Ui.dialog(this).setTitle(device.name()+" · VNC").setMessage(message==null?L10n.text(R.string.ui_connection_failed_check_mac_screen_sharing_and_password):message).setPositiveButton(L10n.text(R.string.ui_return_to_devices),(d,n)->finish()).setCancelable(false).show();});
    }
    private void finishPreview(int result){
        if(previewFinishing||isFinishing())return;previewFinishing=true;disconnect();new Thread(()->{boolean done=false;try{done=released.await(5,TimeUnit.SECONDS);}catch(InterruptedException e){Thread.currentThread().interrupt();}int answer=done?result:RESULT_CANCELED;runOnUiThread(()->{setResult(answer,new Intent().putExtra("previewError",L10n.text(R.string.ui_vnc_preview_could_not_complete_connect_manually_and_ret)));finish();});},"xremote-vnc-preview-close").start();
    }
    private synchronized void disconnect(){
        if(closing)return;closing=true;username=null;password=null;if(display!=null){display.releaseInput();display.client(null);}VncClient c=client;if(c!=null)c.stop();VncConnectionBridge b=bridge;if(b!=null)b.close();
    }
    @Override public void onResume(){super.onResume();if(display!=null)display.onResume();}
    @Override public void onPause(){if(display!=null){display.releaseInput();display.onPause();}super.onPause();}
    @Override public void onDestroy(){disconnect();super.onDestroy();}
    @Override public boolean dispatchKeyEvent(KeyEvent event){if(display!=null&&event.getKeyCode()!=KeyEvent.KEYCODE_BACK&&event.getKeyCode()!=KeyEvent.KEYCODE_VOLUME_UP&&event.getKeyCode()!=KeyEvent.KEYCODE_VOLUME_DOWN&&display.hasFocus())return display.handleKey(event)||super.dispatchKeyEvent(event);return super.dispatchKeyEvent(event);}
}
