package com.yuzhounh.xremote;

import android.app.Activity;
import android.content.Context;
import android.content.Intent;
import android.graphics.Color;
import android.os.Bundle;
import android.view.Gravity;
import android.view.KeyEvent;
import android.view.WindowManager;
import android.view.WindowInsets;
import android.view.WindowInsetsController;
import android.view.inputmethod.InputMethodManager;
import android.widget.ImageButton;
import android.widget.FrameLayout;
import android.widget.LinearLayout;
import android.widget.TextView;
import com.freerdp.freerdpcore.services.LibFreeRDP;
import java.lang.ref.WeakReference;
import java.util.ArrayList;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.atomic.AtomicInteger;

public final class RdpActivity extends Activity implements LibFreeRDP.Listener {
    private static volatile WeakReference<RdpActivity> current=new WeakReference<>(null);
    private volatile long instance;
    private volatile boolean closing,renderPaused=true;
    private final CountDownLatch ended=new CountDownLatch(1);
    private final CountDownLatch released=new CountDownLatch(1);
    private RdpView display;
    private TextView status;
    private boolean connectionStarted;
    private boolean previewOnly,previewSaved,connected,finishingPreview;
    private String previewFailure=L10n.text(R.string.ui_timed_out_waiting_for_the_desktop_image_please_connect);
    private long connectedAt;
    private volatile long lastGraphicsAt;
    private final Runnable capturePreview=this::captureDesktopPreview;
    private Device device;
    private String user,password;
    static void disconnectCurrent(){RdpActivity a=current.get();if(a!=null)a.disconnect();}
    @Override public void onCreate(Bundle saved){
        super.onCreate(saved);XRemoteApp app=(XRemoteApp)getApplication();
        String id=getIntent().getStringExtra("device");user=getIntent().getStringExtra("user");password=getIntent().getStringExtra("password");getIntent().removeExtra("password");
        if(id==null||user==null||password==null){startActivity(new Intent(this,MainActivity.class).addFlags(Intent.FLAG_ACTIVITY_CLEAR_TOP));finish();return;}
        device=Device.find(id);current=new WeakReference<>(this);getWindow().addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON);
        previewOnly=getIntent().getBooleanExtra("previewOnly",false);
        getWindow().setDecorFitsSystemWindows(false);
        getWindow().getAttributes().layoutInDisplayCutoutMode=WindowManager.LayoutParams.LAYOUT_IN_DISPLAY_CUTOUT_MODE_SHORT_EDGES;
        FrameLayout root=new FrameLayout(this);root.setBackgroundColor(Color.BLACK);
        display=new RdpView(this);root.addView(display,new FrameLayout.LayoutParams(-1,-1));
        status=Ui.text(this,device.name()+L10n.text(R.string.ui_connecting),14,Color.WHITE,false);status.setGravity(Gravity.CENTER);
        FrameLayout.LayoutParams statusLayout=new FrameLayout.LayoutParams(-2,-2,Gravity.CENTER);root.addView(status,statusLayout);
        LinearLayout bar=new LinearLayout(this);bar.setGravity(Gravity.CENTER);bar.setPadding(Ui.dp(this,4),0,Ui.dp(this,4),0);
        bar.setBackground(Ui.background(Color.argb(235,242,245,249),Ui.dp(this,18)));bar.setElevation(Ui.dp(this,4));
        sessionButton(bar,R.drawable.remote_zoom,L10n.text(R.string.ui_zoom_restore),()->display.toggleZoom());
        sessionButton(bar,R.drawable.remote_session,L10n.text(R.string.ui_session_menu),this::sessionMenu);
        sessionButton(bar,R.drawable.remote_keyboard,L10n.text(R.string.ui_show_hide_keyboard),this::toggleKeyboard);
        FrameLayout.LayoutParams barLayout=new FrameLayout.LayoutParams(-2,Ui.dp(this,48),Gravity.TOP|Gravity.CENTER_HORIZONTAL);barLayout.topMargin=Ui.dp(this,6);root.addView(bar,barLayout);
        root.setOnApplyWindowInsetsListener((v,insets)->{
            android.graphics.Insets safe=insets.getInsets(WindowInsets.Type.displayCutout());
            int bottom=Math.max(safe.bottom,insets.getInsets(WindowInsets.Type.ime()).bottom);
            root.setPadding(safe.left,safe.top,safe.right,bottom);display.keyboardVisible(insets.isVisible(WindowInsets.Type.ime()));return insets;
        });
        setContentView(root);root.requestApplyInsets();hideSystemBars();
        display.post(()->startConnection(display.getWidth(),display.getHeight()));
        if(previewOnly)display.postDelayed(()->{if(!isFinishing()&&!closing)finishPreview(RESULT_FIRST_USER);},30000);
    }
    private void sessionButton(LinearLayout bar,int icon,String label,Runnable action){
        ImageButton button=new ImageButton(this);button.setImageResource(icon);button.setContentDescription(label);button.setTooltipText(label);
        android.util.TypedValue background=new android.util.TypedValue();getTheme().resolveAttribute(android.R.attr.selectableItemBackgroundBorderless,background,true);button.setBackgroundResource(background.resourceId);
        button.setPadding(Ui.dp(this,11),Ui.dp(this,11),Ui.dp(this,11),Ui.dp(this,11));button.setOnClickListener(v->action.run());bar.addView(button,new LinearLayout.LayoutParams(Ui.dp(this,48),Ui.dp(this,48)));
    }
    private void hideSystemBars(){
        WindowInsetsController c=getWindow().getInsetsController();if(c!=null){c.setSystemBarsBehavior(WindowInsetsController.BEHAVIOR_SHOW_TRANSIENT_BARS_BY_SWIPE);c.hide(WindowInsets.Type.systemBars());}
    }
    @Override public void onWindowFocusChanged(boolean focused){super.onWindowFocusChanged(focused);if(focused)hideSystemBars();}
    private void toggleKeyboard(){
        display.requestFocus();WindowInsets insets=display.getRootWindowInsets();
        InputMethodManager ime=(InputMethodManager)getSystemService(Context.INPUT_METHOD_SERVICE);
        if(insets!=null&&insets.isVisible(WindowInsets.Type.ime()))ime.hideSoftInputFromWindow(display.getWindowToken(),0);
        else ime.showSoftInput(display,InputMethodManager.SHOW_IMPLICIT);
    }
    private void sessionMenu(){
        Ui.dialog(this).setTitle(device.name()).setItems(new String[]{L10n.text(R.string.ui_task_manager),L10n.text(R.string.ui_windows_start_menu),L10n.text(R.string.ui_restore_full_desktop),L10n.text(R.string.ui_disconnect_and_return_to_device_list)},(d,n)->{
            if(n==0){display.virtualKey(17,true);display.virtualKey(16,true);display.tapKey(27);display.virtualKey(16,false);display.virtualKey(17,false);}
            if(n==1){display.virtualKey(17,true);display.tapKey(27);display.virtualKey(17,false);}
            if(n==2)display.resetZoom();if(n==3)finish();
        }).setNegativeButton(L10n.text(R.string.ui_cancel),null).show();
    }
    private void startConnection(int width,int height){
        if(connectionStarted||closing||isFinishing())return;connectionStarted=true;
        int landscapeWidth=Math.max(320,Math.max(width,height)),landscapeHeight=Math.max(200,Math.min(width,height));
        float factor=Math.min(1f,1920f/landscapeWidth);int desktopWidth=Math.round(landscapeWidth*factor/2)*2,desktopHeight=Math.max(200,Math.round(landscapeHeight*factor/2)*2);
        new Thread(()->{
            try{
                LibFreeRDP.listener=this;instance=LibFreeRDP.freerdp_new(getApplicationContext());if(instance==0)throw new IllegalStateException();display.instance(instance);
                String domain="",name=user;int slash=user.indexOf('\\');if(slash>=0){domain=user.substring(0,slash);name=user.substring(slash+1);}
                if(device.protocol()!=Device.Protocol.RDP)throw new IllegalArgumentException(L10n.text(R.string.ui_this_device_uses_vnc));
                ArrayList<String> args=new ArrayList<>();args.add("xRemote");args.add("/v:"+device.connectHost()+":"+device.connectPort());args.add("/u:"+name);if(!domain.isEmpty())args.add("/d:"+domain);args.add("/p:"+password);
                args.add("/size:"+desktopWidth+"x"+desktopHeight);args.add("/bpp:32");args.add("/gdi:sw");args.add("/audio-mode:2");args.add("/kbd:unicode:on");args.add("/log-level:ERROR");args.add("/timeout:10000");
                // A disconnected session stays disconnected until the user connects again.
                args.add("-auto-reconnect");
                if(!LibFreeRDP.freerdp_parse_arguments(instance,args.toArray(new String[0]))||closing||!LibFreeRDP.freerdp_connect(instance))throw new IllegalStateException();
            }catch(Throwable e){failed(L10n.text(R.string.ui_connection_initialization_failed_return_to_the_device_l));}
        },"xremote-rdp-connect").start();
    }
    @Override public void onResume(){super.onResume();renderPaused=false;if(display!=null){display.refreshFrame();if(connected&&!previewSaved){display.removeCallbacks(capturePreview);display.postDelayed(capturePreview,500);}}}
    @Override public void onPause(){renderPaused=true;if(display!=null){display.removeCallbacks(capturePreview);if(device!=null&&!previewOnly)DesktopPreview.save(this,device,display.desktopPreview());}super.onPause();}
    @Override public void onDestroy(){disconnect();super.onDestroy();}
    private synchronized void disconnect(){
        if(closing)return;closing=true;password=null;long i=instance;instance=0;if(display!=null)display.instance(0);
        if(display!=null)display.removeCallbacks(capturePreview);
        if(i!=0)new Thread(()->{try{LibFreeRDP.freerdp_disconnect(i);if(ended.await(1800,TimeUnit.MILLISECONDS))LibFreeRDP.freerdp_free(i);}catch(Throwable ignored){}finally{if(LibFreeRDP.listener==this)LibFreeRDP.listener=null;released.countDown();}},"xremote-rdp-close").start();else released.countDown();
    }
    private void captureDesktopPreview(){
        if(closing||isFinishing()||renderPaused||previewSaved||!connected)return;
        long now=android.os.SystemClock.uptimeMillis(),elapsed=now-connectedAt;
        if(elapsed<6000||(now-lastGraphicsAt<800&&elapsed<12000)){display.postDelayed(capturePreview,500);return;}
        android.graphics.Bitmap image=display.desktopPreview();
        if(image==null){display.postDelayed(capturePreview,500);return;}
        previewSaved=true;
        DesktopPreview.save(this,device,image,success->{if(previewOnly&&!isFinishing()){if(!success)previewFailure=L10n.text(R.string.ui_failed_to_save_thumbnail);finishPreview(success?RESULT_OK:RESULT_FIRST_USER);}});
    }
    private void finishPreview(int result){
        if(finishingPreview||isFinishing())return;finishingPreview=true;disconnect();
        new Thread(()->{boolean ready=false;try{ready=released.await(5,TimeUnit.SECONDS);}catch(InterruptedException e){Thread.currentThread().interrupt();}int outcome=ready?result:RESULT_CANCELED;runOnUiThread(()->{setResult(outcome,new Intent().putExtra("previewError",previewFailure));finish();});},"xremote-preview-finish").start();
    }
    @Override public void connected(){runOnUiThread(()->{if(!closing){connected=true;connectedAt=android.os.SystemClock.uptimeMillis();status.setText(L10n.text(R.string.ui_generating_desktop_preview));if(!previewOnly)status.setVisibility(android.view.View.GONE);display.postDelayed(capturePreview,500);}});}
    @Override public void disconnected(){ended.countDown();runOnUiThread(()->{if(!closing){connected=false;if(previewOnly){finishPreview(RESULT_FIRST_USER);return;}status.setText(device.name()+L10n.text(R.string.ui_connection_closed_2));status.setVisibility(android.view.View.VISIBLE);}});}
    @Override public void failed(String error){
        ended.countDown();runOnUiThread(()->{if(isFinishing()||closing)return;if(previewOnly){finishPreview(RESULT_FIRST_USER);return;}String value=error==null?"":error;String message=value.contains("LOGON")||value.contains("AUTHENTICATION")?L10n.text(R.string.ui_the_username_or_password_was_not_verified_check_this_co):L10n.text(R.string.ui_connection_failed_confirm_the_device_is_online_and_retr);Ui.dialog(this).setTitle(device==null?L10n.text(R.string.ui_connection_failed):device.name()).setMessage(message).setPositiveButton(L10n.text(R.string.ui_return_to_devices),(d,w)->finish()).setCancelable(false).show();});
    }
    @Override public void size(int w,int h){if(!closing)display.resizeFrame(w,h);}
    @Override public void graphics(int x,int y,int w,int h){if(!closing&&!renderPaused){display.updateFrame(x,y,w,h);lastGraphicsAt=android.os.SystemClock.uptimeMillis();}}
    @Override public void pointer(int[] p,int w,int h,int x,int y){if(!closing)display.pointer(p,w,h,x,y);}
    @Override public boolean authenticate(StringBuilder u,StringBuilder d,StringBuilder p){return false;}
    @Override public int certificate(String cn,String subject,String fingerprint,boolean changed){
        if(closing)return 0;XRemoteApp app=(XRemoteApp)getApplication();
        try{if(fingerprint.equals(app.store.get("certificate."+device.id())))return 2;}catch(Exception ignored){}
        if(previewOnly){previewFailure=L10n.text(R.string.ui_manual_device_certificate_confirmation_required);return 0;}
        CountDownLatch answer=new CountDownLatch(1);AtomicInteger accepted=new AtomicInteger(0);
        runOnUiThread(()->{
            if(isFinishing()||closing){answer.countDown();return;}
            Ui.dialog(this).setTitle(changed?L10n.text(R.string.ui_device_certificate_changed):L10n.text(R.string.ui_confirm_remote_device))
                .setMessage(device.name()+L10n.text(R.string.ui_ncertificate_name)+cn+L10n.text(R.string.ui_n_nfingerprint_n)+fingerprint+L10n.text(R.string.ui_n_nverify_and_then_connect))
                .setNegativeButton(L10n.text(R.string.ui_cancel),(d,w)->answer.countDown())
                .setNeutralButton(L10n.text(R.string.ui_this_connection_only),(d,w)->{accepted.set(2);answer.countDown();})
                .setPositiveButton(L10n.text(R.string.ui_connect_and_remember),(d,w)->{try{app.store.put("certificate."+device.id(),fingerprint);}catch(Exception ignored){}accepted.set(2);answer.countDown();})
                .setOnCancelListener(d->answer.countDown()).show();
        });
        try{answer.await(180,TimeUnit.SECONDS);}catch(InterruptedException e){Thread.currentThread().interrupt();}return accepted.get();
    }
    @Override public boolean dispatchKeyEvent(KeyEvent event){if(display!=null&&event.getKeyCode()!=KeyEvent.KEYCODE_BACK&&event.getKeyCode()!=KeyEvent.KEYCODE_VOLUME_UP&&event.getKeyCode()!=KeyEvent.KEYCODE_VOLUME_DOWN&&display.hasFocus())return display.handleKey(event)||super.dispatchKeyEvent(event);return super.dispatchKeyEvent(event);}
}
