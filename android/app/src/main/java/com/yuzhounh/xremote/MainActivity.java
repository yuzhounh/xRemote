package com.yuzhounh.xremote;

import android.app.Activity;
import android.app.AlertDialog;
import android.content.Intent;
import android.graphics.Color;
import android.graphics.Bitmap;
import android.os.Bundle;
import android.text.InputType;
import android.text.TextUtils;
import android.view.Gravity;
import android.view.MotionEvent;
import android.view.View;
import android.view.ViewGroup;
import android.widget.Button;
import android.widget.FrameLayout;
import android.widget.ImageButton;
import android.widget.CheckBox;
import android.widget.EditText;
import android.widget.ImageView;
import android.widget.LinearLayout;
import android.widget.ScrollView;
import android.widget.TextView;
import android.widget.Toast;
import com.freerdp.freerdpcore.services.LibFreeRDP;
import org.json.JSONObject;
import java.io.InputStream;

public final class MainActivity extends Activity implements TunnelController.Listener {
    private XRemoteApp app;
    private TextView network, title, emptyTitle, emptyBody;
    private LinearLayout headerBar, searchWrap, deviceList, emptyBox;private EditText searchInput;private boolean searchOpen;
    private final LinearLayout[] deviceItems=new LinearLayout[Device.ALL.length];
    private View fab;
    private int view;
    private String query="";
    private float headerFraction;
    private boolean headerCollapsed;
    private android.animation.ValueAnimator headerAnim;
    private int previewEnd=Device.ALL.length;
    private final TextView[] status=new TextView[Device.ALL.length];
    private final LinearLayout[] deviceCards=new LinearLayout[Device.ALL.length];
    private final ImageView[] previews=new ImageView[Device.ALL.length];
    private FrameLayout overview;
    private TextView detailStatus,detailAccount;
    private ImageView detailPreview;
    private TextView detailPreviewLabel;
    private LinearLayout hardwareContent,storageContent;
    private Button hardwareRefresh;
    private int hardwareGeneration;
    private int selectedDevice=-1;
    private ScrollView menuScroll;
    private int menuScrollY;
    private boolean helpVisible;
    private final android.window.OnBackInvokedCallback backToDevices=()->{if(helpVisible)showMenu();else showOverview();};
    private static final int IMPORT=410;
    private static final int IMPORT_CONTROL=411;
    private static final int PREVIEW_CAPTURE=412;
    private int previewNext=-1,previewCompleted,previewSkipped;
    private String previewFailures="";
    private ControlClient control;
    private String pendingControlDevice;
    private boolean deviceInfoRefreshing;
    private final int[] reachable=new int[Device.ALL.length];
    private record DeviceUsage(int remoteConnections, boolean local) {
        String text() {
            if (remoteConnections > 0) return L10n.text(R.string.ui_in_remote_use) + remoteConnections + ")";
            return L10n.text(local ? R.string.ui_in_local_use : R.string.ui_no_active_remote_connections);
        }
    }
    private final DeviceUsage[] usage=new DeviceUsage[Device.ALL.length];
    private final boolean[] querying=new boolean[Device.ALL.length];
    private final android.os.Handler controlTimer=new android.os.Handler(android.os.Looper.getMainLooper());
    private final Runnable pollControl=new Runnable(){public void run(){refreshUsage();controlTimer.postDelayed(this,10000);}};
    @Override public void onCreate(Bundle state){
        super.onCreate(state);app=(XRemoteApp)getApplication();loadAliases();
        Theme.applySystemBars(this);
        control=new ControlClient(app.store);if(state!=null){pendingControlDevice=state.getString("pendingControlDevice");view=state.getInt("view");}
        buildOverview();
        if(state!=null){previewNext=state.getInt("previewNext",-1);previewEnd=state.getInt("previewEnd",Device.ALL.length);previewCompleted=state.getInt("previewCompleted");previewSkipped=state.getInt("previewSkipped");previewFailures=state.getString("previewFailures","");int selected=state.getInt("selectedDevice",-1);if(selected>=0&&selected<Device.ALL.length)showDeviceDetails(selected);else if(state.getBoolean("menuVisible")){showMenu(state.getInt("menuScrollY"));if(state.getBoolean("helpVisible"))showHelp();}}
    }
    private void buildOverview(){
        FrameLayout root=new FrameLayout(this);overview=root;Ui.page(root);
        LinearLayout column=new LinearLayout(this);column.setOrientation(LinearLayout.VERTICAL);root.addView(column,new FrameLayout.LayoutParams(-1,-1));
        headerBar=new LinearLayout(this);headerBar.setGravity(Gravity.CENTER_VERTICAL);headerBar.setPadding(Ui.dp(this,16),0,Ui.dp(this,8),0);
        LinearLayout labels=new LinearLayout(this);labels.setOrientation(LinearLayout.VERTICAL);
        title=Ui.text(this,viewName(),18,Ui.ink(this),true);network=Ui.text(this,L10n.text(R.string.ui_preparing),11,Ui.muted(this),false);network.setSingleLine(true);network.setEllipsize(TextUtils.TruncateAt.END);labels.addView(title);labels.addView(network);headerBar.addView(labels,Ui.weight());
        ImageButton menu=Ui.iconButton(this,XIcon.MENU,Ui.ink(this),L10n.text(R.string.ui_menu));menu.setOnClickListener(v->showMenu());headerBar.addView(menu);
        column.addView(headerBar,new LinearLayout.LayoutParams(-1,Ui.dp(this,60)));
        searchWrap=new LinearLayout(this);searchWrap.setPadding(Ui.dp(this,16),0,Ui.dp(this,16),Ui.dp(this,8));
        LinearLayout box=new LinearLayout(this);box.setGravity(Gravity.CENTER_VERTICAL);box.setPadding(Ui.dp(this,14),0,Ui.dp(this,14),0);box.setBackground(Ui.background(Theme.isDark(this)?Color.argb(220,0x10,0x17,0x26):Color.argb(220,255,255,255),Ui.dp(this,24)));
        box.addView(Ui.icon(this,XIcon.SEARCH,Ui.muted(this),22));
        EditText search=new EditText(this);searchInput=search;search.setText(query);search.setHint(L10n.text(R.string.ui_search_devices));search.setSingleLine(true);search.setTextSize(14);search.setTextColor(Ui.ink(this));search.setHintTextColor(Ui.muted(this));search.setBackground(null);search.setPadding(Ui.dp(this,10),0,0,0);search.setImeOptions(android.view.inputmethod.EditorInfo.IME_ACTION_SEARCH);
        search.addTextChangedListener(new android.text.TextWatcher(){public void beforeTextChanged(CharSequence s,int a,int b,int c){}public void onTextChanged(CharSequence s,int a,int b,int c){query=s.toString();applyFilter();}public void afterTextChanged(android.text.Editable s){}});
        box.addView(search,Ui.weight());ImageButton closeSearch=Ui.iconButton(this,XIcon.CLOSE,Ui.muted(this),L10n.text(R.string.ui_close_search));closeSearch.setOnClickListener(v->{searchOpen=false;search.setText("");((android.view.inputmethod.InputMethodManager)getSystemService(INPUT_METHOD_SERVICE)).hideSoftInputFromWindow(search.getWindowToken(),0);applyFilter();});box.addView(closeSearch,new LinearLayout.LayoutParams(Ui.dp(this,40),Ui.dp(this,40)));searchWrap.addView(box,new LinearLayout.LayoutParams(-1,Ui.dp(this,46)));column.addView(searchWrap,new LinearLayout.LayoutParams(-1,Ui.dp(this,54)));
        LinearLayout panel=new LinearLayout(this);
        PullScroll scroll=new PullScroll(this);scroll.setFillViewport(true);scroll.refresh=()->{app.tunnel.refresh();refreshUsage();};
        scroll.setOnScrollChangeListener((v,x,y,ox,oy)->{if(y>oy&&y>Ui.dp(this,24))collapseHeader(true);else if(y<oy)collapseHeader(false);});
        deviceList=new LinearLayout(this);deviceList.setOrientation(LinearLayout.VERTICAL);deviceList.setPadding(Ui.dp(this,4),Ui.dp(this,4),Ui.dp(this,4),Ui.dp(this,4));
        for(int i=0;i<Device.ALL.length;i++){
            final int index=i;final Device d=Device.ALL[i];
            LinearLayout item=new LinearLayout(this);item.setOrientation(LinearLayout.VERTICAL);deviceItems[i]=item;
            LinearLayout card=new LinearLayout(this);deviceCards[i]=card;card.setGravity(Gravity.CENTER_VERTICAL);card.setMinimumHeight(Ui.dp(this,80));card.setPadding(Ui.dp(this,12),Ui.dp(this,10),0,Ui.dp(this,10));card.setBackground(Ui.touch(this,0,Ui.dp(this,16)));card.setFocusable(true);card.setOnClickListener(v->connectIfReady(index));
            previews[i]=new ImageView(this);previews[i].setBackground(Ui.background(Ui.tint(this),Ui.dp(this,12)));previews[i].setClipToOutline(true);card.addView(previews[i],new LinearLayout.LayoutParams(Ui.dp(this,96),Ui.dp(this,60)));
            LinearLayout summary=new LinearLayout(this);summary.setOrientation(LinearLayout.VERTICAL);summary.setPadding(Ui.dp(this,14),0,0,0);
            TextView name=Ui.text(this,d.name(),15,Ui.ink(this),true);name.setSingleLine(true);name.setEllipsize(TextUtils.TruncateAt.END);summary.addView(name);
            status[i]=Ui.text(this,L10n.text(R.string.ui_waiting_for_network_connection),12,Ui.muted(this),false);status[i].setMinLines(2);status[i].setMaxLines(2);status[i].setEllipsize(TextUtils.TruncateAt.END);status[i].setPadding(0,Ui.dp(this,4),0,0);summary.addView(status[i]);card.addView(summary,Ui.weight());
            Button details=Ui.button(this,"›");details.setTextSize(28);details.setTextColor(Ui.muted(this));details.setContentDescription(d.name()+L10n.text(R.string.ui_device_details));details.setOnClickListener(v->showDeviceDetails(index));card.addView(details,new LinearLayout.LayoutParams(Ui.dp(this,48),-1));
            item.addView(card);item.addView(Ui.divider(this));deviceList.addView(item);
        }
        LinearLayout empty=new LinearLayout(this);empty.setOrientation(LinearLayout.VERTICAL);empty.setGravity(Gravity.CENTER);empty.setPadding(Ui.dp(this,24),Ui.dp(this,48),Ui.dp(this,24),Ui.dp(this,48));emptyBox=empty;
        emptyTitle=Ui.text(this,L10n.text(R.string.ui_welcome_to_xremote),25,Ui.ink(this),true);emptyTitle.setGravity(Gravity.CENTER);emptyBody=Ui.text(this,"",14,Ui.muted(this),false);emptyBody.setGravity(Gravity.CENTER);emptyBody.setPadding(0,Ui.dp(this,10),0,0);
        empty.addView(emptyTitle);empty.addView(emptyBody);deviceList.addView(empty);
        scroll.addView(deviceList);panel.addView(scroll,new LinearLayout.LayoutParams(-1,-1));
        LinearLayout.LayoutParams panelLayout=new LinearLayout.LayoutParams(-1,0,1);panelLayout.setMargins(Ui.dp(this,8),0,Ui.dp(this,8),0);column.addView(panel,panelLayout);
        ImageButton add=new ImageButton(this);add.setImageDrawable(new XIcon(XIcon.PLUS,Color.WHITE));add.setPadding(Ui.dp(this,16),Ui.dp(this,16),Ui.dp(this,16),Ui.dp(this,16));add.setScaleType(ImageView.ScaleType.FIT_CENTER);add.setContentDescription(L10n.text(R.string.ui_import_network_configuration));add.setElevation(Ui.dp(this,6));
        add.setBackground(new android.graphics.drawable.RippleDrawable(android.content.res.ColorStateList.valueOf(Color.argb(60,255,255,255)),oval(Ui.blue(this)),oval(Color.WHITE)));add.setOnClickListener(v->importConfig());fab=add;
        FrameLayout.LayoutParams fabLayout=new FrameLayout.LayoutParams(Ui.dp(this,56),Ui.dp(this,56),Gravity.BOTTOM|Gravity.END);fabLayout.setMargins(0,0,Ui.dp(this,20),Ui.dp(this,20));root.addView(add,fabLayout);
        setContentView(root);Ui.insets(root);applyFilter();
    }
    private static android.graphics.drawable.GradientDrawable oval(int color){android.graphics.drawable.GradientDrawable d=new android.graphics.drawable.GradientDrawable();d.setShape(android.graphics.drawable.GradientDrawable.OVAL);d.setColor(color);return d;}
    private static final class PullScroll extends ScrollView{
        Runnable refresh;private float startY;private boolean atTop;
        PullScroll(android.content.Context c){super(c);}
        @Override public boolean dispatchTouchEvent(MotionEvent e){
            if(e.getActionMasked()==MotionEvent.ACTION_DOWN){startY=e.getY();atTop=getScrollY()==0;}
            else if(e.getActionMasked()==MotionEvent.ACTION_UP&&atTop&&getScrollY()==0&&e.getY()-startY>getResources().getDisplayMetrics().density*80&&refresh!=null)refresh.run();
            return super.dispatchTouchEvent(e);
        }
    }
    private String viewName(){return view==1?L10n.text(R.string.ui_online_devices):view==2?L10n.text(R.string.ui_offline_devices):L10n.text(R.string.ui_all_devices);}
    private void collapseHeader(boolean collapse){
        if(collapse==headerCollapsed)return;headerCollapsed=collapse;if(headerAnim!=null)headerAnim.cancel();
        headerAnim=android.animation.ValueAnimator.ofFloat(headerFraction,collapse?1f:0f);headerAnim.setDuration(180);headerAnim.addUpdateListener(a->applyHeaderFraction((float)a.getAnimatedValue()));headerAnim.start();
    }
    private void applyHeaderFraction(float f){
        headerFraction=f;headerBar.getLayoutParams().height=Ui.dp(this,60-12*f);headerBar.requestLayout();
        searchWrap.setAlpha(1-f);searchWrap.getLayoutParams().height=Math.round(Ui.dp(this,54)*(1-f));searchWrap.requestLayout();title.setTextSize(18-2*f);
    }
    private void applyFilter(){
        boolean configured=app.tunnel!=null&&app.tunnel.isConfigured();String q=query.trim().toLowerCase(java.util.Locale.ROOT);int shown=0;
        for(int i=0;i<Device.ALL.length;i++){
            Device d=Device.ALL[i];boolean match=configured&&(view==0||(reachable[i]==1)==(view==1))&&(q.isEmpty()||d.name().toLowerCase(java.util.Locale.ROOT).contains(q)||d.ip().contains(q));
            deviceItems[i].setVisibility(match?View.VISIBLE:View.GONE);if(match)shown++;
        }
        fab.setVisibility(configured?View.GONE:View.VISIBLE);searchWrap.setVisibility(configured&&searchOpen?View.VISIBLE:View.GONE);if(configured&&searchOpen)applyHeaderFraction(headerFraction);
        emptyBox.setVisibility(shown==0?View.VISIBLE:View.GONE);emptyTitle.setVisibility(configured?View.GONE:View.VISIBLE);
        emptyBody.setText(configured?L10n.text(R.string.ui_no_matching_devices):L10n.text(R.string.ui_tap_the_button_at_the_bottom_right_and_import_the_netwo));title.setText(viewName());
    }
    private String version(){try{return getPackageManager().getPackageInfo(getPackageName(),0).versionName;}catch(Exception e){return "";}}
    @Override public void onResume(){super.onResume();Theme.applySystemBars(this);refreshDetails();refreshPreviews();app.ensureTunnel();app.tunnel.listen(this);app.tunnel.start();controlTimer.post(pollControl);applyFilter();}
    private void refreshPreviews(){
        for(int i=0;i<Device.ALL.length;i++){
            Bitmap image=DesktopPreview.load(this,Device.ALL[i]);
            previews[i].setScaleType(image==null?ImageView.ScaleType.CENTER_INSIDE:ImageView.ScaleType.CENTER_CROP);
            previews[i].setImageBitmap(image);
            if(image==null)previews[i].setImageResource(R.drawable.desktop_preview);
            previews[i].setContentDescription(Device.ALL[i].name()+(image==null?L10n.text(R.string.ui_no_desktop_preview_yet):L10n.text(R.string.ui_thumbnail_of_the_last_connected_desktop)));
        }
        if(selectedDevice>=0)renderDetailPreview();
    }
    private String account(Device d){
        try{String saved=app.store.get("credential."+d.id());if(saved!=null){String user=new JSONObject(saved).optString("user").trim();if(!user.isEmpty())return user;}}catch(Exception ignored){}
        return d.user();
    }
    private String shortAccount(Device d,String user){String prefix=d.hostName()+"\\";return user.regionMatches(true,0,prefix,0,prefix.length())?user.substring(prefix.length()):user;}
    private String displayAccount(Device d){return shortAccount(d,account(d));}
    private void refreshDetails(){if(selectedDevice>=0){detailAccount.setText(displayAccount(Device.ALL[selectedDevice]));detailStatus.setText(status[selectedDevice].getText());detailStatus.setTextColor(status[selectedDevice].getCurrentTextColor());}}
    private void connectIfReady(int index){if(reachable[index]==1)connectDevice(Device.ALL[index]);else Toast.makeText(this,L10n.text(R.string.ui_device_temporarily_unavailable_to_connect_wait_for_the),Toast.LENGTH_SHORT).show();}
    private TextView infoRow(LinearLayout content,String label,String value){
        if(content.getChildCount()>0)content.addView(Ui.divider(this));
        TextView caption=Ui.text(this,label,12,Ui.muted(this),false);caption.setPadding(0,Ui.dp(this,12),0,Ui.dp(this,4));content.addView(caption);
        TextView text=Ui.text(this,value,15,Ui.ink(this),false);text.setTextIsSelectable(true);content.addView(text);return text;
    }
    private void showDeviceDetails(int index){
        armBack(true);
        selectedDevice=index;Device d=Device.ALL[index];
        LinearLayout root=new LinearLayout(this);root.setOrientation(LinearLayout.VERTICAL);Ui.page(root);
        ImageButton back=Ui.iconButton(this,XIcon.BACK,Ui.ink(this),L10n.text(R.string.ui_back));back.setOnClickListener(v->showOverview());root.addView(Ui.header(this,L10n.text(R.string.ui_device_details_2),back),new LinearLayout.LayoutParams(-1,Ui.dp(this,60)));
        ScrollView scroll=new ScrollView(this);LinearLayout content=new LinearLayout(this);content.setOrientation(LinearLayout.VERTICAL);content.setPadding(Ui.dp(this,16),Ui.dp(this,8),Ui.dp(this,16),Ui.dp(this,24));
        detailPreview=new ImageView(this);detailPreview.setBackground(Ui.background(Ui.tint(this),Ui.dp(this,16)));detailPreview.setClipToOutline(true);content.addView(detailPreview,new LinearLayout.LayoutParams(-1,Ui.dp(this,190)));
        detailPreviewLabel=Ui.text(this,"",12,Ui.muted(this),false);detailPreviewLabel.setPadding(0,Ui.dp(this,6),0,Ui.dp(this,18));content.addView(detailPreviewLabel);
        content.addView(Ui.text(this,d.name(),18,Ui.ink(this),true));detailStatus=Ui.text(this,"",14,Ui.muted(this),false);detailStatus.setPadding(0,Ui.dp(this,8),0,Ui.dp(this,16));content.addView(detailStatus);
        Button connect=Ui.primary(this,L10n.text(R.string.ui_connect_device));connect.setOnClickListener(v->connectIfReady(index));content.addView(connect,new LinearLayout.LayoutParams(-1,Ui.dp(this,48)));
        Button sessions=Ui.button(this,L10n.text(R.string.ui_view_sign_in_and_connections));sessions.setOnClickListener(v->sessions(d));content.addView(sessions,new LinearLayout.LayoutParams(-1,Ui.dp(this,48)));
        TextView infoTitle=Ui.text(this,L10n.text(R.string.ui_device_information),15,Ui.ink(this),true);infoTitle.setPadding(0,Ui.dp(this,18),0,Ui.dp(this,12));content.addView(infoTitle);
        LinearLayout info=new LinearLayout(this);info.setOrientation(LinearLayout.VERTICAL);info.setPadding(0,0,0,Ui.dp(this,12));
        infoRow(info,L10n.text(R.string.ui_device_name),d.hostName().isEmpty()?L10n.text(R.string.ui_not_recorded):d.hostName());infoRow(info,L10n.text(R.string.ui_connection_address),d.ip()+":"+d.remotePort());
        info.addView(Ui.divider(this));TextView accountLabel=Ui.text(this,L10n.text(R.string.ui_sign_in_account),12,Ui.muted(this),false);accountLabel.setPadding(0,Ui.dp(this,16),0,Ui.dp(this,4));info.addView(accountLabel);
        LinearLayout accountLine=new LinearLayout(this);accountLine.setGravity(Gravity.CENTER_VERTICAL);detailAccount=Ui.text(this,displayAccount(d),16,Ui.ink(this),false);detailAccount.setTextIsSelectable(true);accountLine.addView(detailAccount,new LinearLayout.LayoutParams(0,-2,1));Button editAccount=Ui.button(this,L10n.text(R.string.ui_edit));editAccount.setContentDescription(L10n.text(R.string.ui_edit_sign_in_account_and_password));editAccount.setOnClickListener(v->credentials(d,false));accountLine.addView(editAccount);info.addView(accountLine);
        infoRow(info,L10n.text(R.string.ui_connection_protocol),d.protocol()==Device.Protocol.VNC?L10n.text(R.string.ui_vnc_screen_sharing):L10n.text(R.string.ui_rdp_remote_desktop));info.addView(Ui.divider(this));hardwareContent=new LinearLayout(this);hardwareContent.setOrientation(LinearLayout.VERTICAL);info.addView(hardwareContent);content.addView(info);
        LinearLayout hardwareHeader=new LinearLayout(this);hardwareHeader.setGravity(Gravity.CENTER_VERTICAL);hardwareHeader.setPadding(0,Ui.dp(this,16),0,0);hardwareHeader.addView(Ui.text(this,L10n.text(R.string.ui_storage),15,Ui.ink(this),true),new LinearLayout.LayoutParams(0,-2,1));
        hardwareRefresh=Ui.button(this,L10n.text(R.string.ui_refresh));hardwareRefresh.setOnClickListener(v->refreshStorage());hardwareHeader.addView(hardwareRefresh);content.addView(hardwareHeader);
        storageContent=new LinearLayout(this);storageContent.setOrientation(LinearLayout.VERTICAL);content.addView(storageContent);
        scroll.addView(content);root.addView(scroll,new LinearLayout.LayoutParams(-1,0,1));setContentView(root);Ui.insets(root);refreshDetails();renderDetailPreview();loadHardware(false);
    }
    private void loadHardware(boolean refresh){
        if(selectedDevice<0)return;int index=selectedDevice,generation=++hardwareGeneration;Device device=Device.ALL[index];var cache=getSharedPreferences("hardware-cache",MODE_PRIVATE);JSONObject saved=null;
        hardwareContent.removeAllViews();storageContent.removeAllViews();
        try{String value=cache.getString(device.id()+".data",null);if(value!=null){JSONObject data=new JSONObject(value);if(data.optInt("schema")!=1||!device.hostName().equalsIgnoreCase(data.optString("machine")))throw new IllegalArgumentException();renderHardware(data);saved=data;}}catch(Exception ignored){cache.edit().remove(device.id()+".data").remove(device.id()+".savedAt").apply();hardwareContent.removeAllViews();storageContent.removeAllViews();}
        final JSONObject previous=saved;
        if(previous!=null&&!refresh){hardwareRefresh.setEnabled(true);hardwareRefresh.setText(L10n.text(R.string.ui_refresh));return;}
        hardwareRefresh.setEnabled(false);hardwareRefresh.setText(previous==null?"Reading…":"Updating…");
        TextView hint=Ui.text(this,L10n.text(R.string.ui_updating_showing_the_previous_read_result),12,Ui.muted(this),false);
        if(previous==null){infoRow(hardwareContent,L10n.text(R.string.ui_system_and_hardware),"Reading…");infoRow(storageContent,L10n.text(R.string.ui_disk_space),"Reading…");}else storageContent.addView(hint);
        control.hardware(Device.ALL[index],refresh,(data,error)->{
            boolean valid=data!=null&&data.optInt("schema")==1&&device.hostName().equalsIgnoreCase(data.optString("machine"));
            if(valid)cache.edit().putString(device.id()+".data",data.toString()).putLong(device.id()+".savedAt",System.currentTimeMillis()).apply();
            if(isFinishing()||isDestroyed()||selectedDevice!=index||hardwareGeneration!=generation)return;
            hardwareRefresh.setEnabled(true);hardwareRefresh.setText(L10n.text(R.string.ui_refresh));
            if(data==null&&previous!=null){hint.setText(L10n.text(R.string.ui_update_incomplete_showing_the_previous_result));if(refresh)Toast.makeText(this,error==null?L10n.text(R.string.ui_please_refresh_later):error,Toast.LENGTH_SHORT).show();return;}
            hardwareContent.removeAllViews();storageContent.removeAllViews();
            if(data==null){infoRow(hardwareContent,L10n.text(R.string.ui_system_and_hardware_unavailable),error==null?L10n.text(R.string.ui_please_refresh_later):error);infoRow(storageContent,L10n.text(R.string.ui_disk_space_unavailable),L10n.text(R.string.ui_please_refresh_later));return;}
            try{
                if(!valid)throw new IllegalArgumentException();
                renderHardware(data);
            }catch(Exception e){
                hardwareContent.removeAllViews();storageContent.removeAllViews();
                if(previous!=null)try{renderHardware(previous);storageNote(storageContent,L10n.text(R.string.ui_update_incomplete_showing_the_previous_result));return;}catch(Exception ignored){}
                infoRow(hardwareContent,L10n.text(R.string.ui_hardware_information_unavailable),L10n.text(R.string.ui_update_the_device_management_service_and_refresh));infoRow(storageContent,L10n.text(R.string.ui_disk_space_unavailable),L10n.text(R.string.ui_please_refresh_later));
            }
        });
    }
    private String bytes(long value){if(value<0)return L10n.text(R.string.ui_unknown);double number=value;String[] units={"B","KiB","MiB","GiB","TiB","PiB"};int unit=0;while(number>=1024&&unit<units.length-1){number/=1024;unit++;}return String.format(java.util.Locale.getDefault(),"%.1f %s",number,units[unit]);}
    private JSONObject savedStorage(Device device,JSONObject baseline){
        var cache=getSharedPreferences("hardware-cache",MODE_PRIVATE);
        try{String saved=cache.getString(device.id()+".storage",null);if(saved!=null&&cache.getLong(device.id()+".storageSavedAt",0)>cache.getLong(device.id()+".savedAt",0)){JSONObject data=new JSONObject(saved);if(data.optInt("schema")==1&&device.hostName().equalsIgnoreCase(data.optString("machine")))return data;}}catch(Exception ignored){}
        return baseline;
    }
    private void refreshStorage(){
        if(selectedDevice<0)return;int index=selectedDevice,generation=++hardwareGeneration;Device device=Device.ALL[index];var cache=getSharedPreferences("hardware-cache",MODE_PRIVATE);
        hardwareRefresh.setEnabled(false);hardwareRefresh.setText("Updating…");TextView hint=Ui.text(this,L10n.text(R.string.ui_updating_storage_keeping_the_previous_result),12,Ui.muted(this),false);storageContent.addView(hint);
        control.storage(device,(data,error)->{
            boolean valid=data!=null&&data.optInt("schema")==1&&device.hostName().equalsIgnoreCase(data.optString("machine"))&&data.optJSONArray("disks")!=null&&data.optJSONArray("volumes")!=null;
            if(valid&&data.optJSONArray("disks").length()==0&&data.optJSONArray("volumes").length()==0&&data.optJSONArray("unavailable")!=null&&data.optJSONArray("unavailable").length()>0)valid=false;
            if(valid)cache.edit().putString(device.id()+".storage",data.toString()).putLong(device.id()+".storageSavedAt",System.currentTimeMillis()).apply();
            if(isFinishing()||isDestroyed()||selectedDevice!=index||hardwareGeneration!=generation)return;
            hardwareRefresh.setEnabled(true);hardwareRefresh.setText(L10n.text(R.string.ui_refresh));
            if(!valid){hint.setText(L10n.text(R.string.ui_storage_update_incomplete_showing_the_previous_result));Toast.makeText(this,error==null?L10n.text(R.string.ui_storage_information_unavailable_please_refresh_later):error,Toast.LENGTH_SHORT).show();return;}
            storageContent.removeAllViews();try{renderStorage(data);}catch(Exception e){infoRow(storageContent,L10n.text(R.string.ui_storage_information_unavailable),L10n.text(R.string.ui_please_refresh_later));}
        });
    }
    private String localTime(String value){try{return java.time.Instant.parse(value).atZone(java.time.ZoneId.systemDefault()).format(java.time.format.DateTimeFormatter.ofPattern("yyyy-MM-dd HH:mm"));}catch(Exception ignored){return L10n.text(R.string.ui_unknown);}}
    private String names(org.json.JSONArray values){if(values==null||values.length()==0)return L10n.text(R.string.ui_temporarily_unavailable);StringBuilder text=new StringBuilder();for(int i=0;i<values.length();i++){if(i>0)text.append("\n");text.append(values.optString(i));}return text.toString();}
    private void renderHardware(JSONObject data)throws Exception{
        if(!data.optString("model").isEmpty())infoRow(hardwareContent,L10n.text(R.string.ui_model),data.optString("model"));
        JSONObject os=data.optJSONObject("os");
        if(os!=null){infoRow(hardwareContent,L10n.text(R.string.ui_system),os.optString("name")+" · "+os.optString("version")+" · "+os.optString("architecture"));infoRow(hardwareContent,L10n.text(R.string.ui_last_boot),localTime(os.optString("lastBootAt")));}
        JSONObject windows=data.optJSONObject("windows");
        if(windows!=null){String version=windows.optString("displayVersion"),build=windows.optString("build");infoRow(hardwareContent,"Windows",windows.optString("name")+" · "+windows.optString("architecture")+"\n"+(version.isEmpty()?"":version+" · ")+L10n.text(R.string.ui_system_build)+build);infoRow(hardwareContent,L10n.text(R.string.ui_last_boot),localTime(windows.optString("lastBootAt")));}
        JSONObject cpu=data.optJSONObject("cpu");if(cpu!=null){int cores=cpu.optInt("cores"),threads=cpu.optInt("threads"),sockets=cpu.optInt("sockets");String value=names(cpu.optJSONArray("models"));if(cores>0)value+="\n"+(sockets>1?sockets+L10n.text(R.string.ui_sockets):"")+cores+L10n.text(R.string.ui_cores)+threads+L10n.text(R.string.ui_threads);infoRow(hardwareContent,"CPU",value);}
        if(!data.isNull("memoryBytes"))infoRow(hardwareContent,L10n.text(R.string.ui_memory_capacity),bytes(data.getLong("memoryBytes")));
        org.json.JSONArray gpus=data.optJSONArray("gpus"),visibleGpus=new org.json.JSONArray();if(gpus!=null)for(int i=0;i<gpus.length();i++){String name=gpus.optString(i),lower=name.toLowerCase(java.util.Locale.ROOT);if(!lower.contains("basic display")&&!name.contains("Basic Display"))visibleGpus.put(name);}if(visibleGpus.length()>0)infoRow(hardwareContent,L10n.text(R.string.ui_gpu_gpu),names(visibleGpus));
        renderStorage(savedStorage(Device.ALL[selectedDevice],data));
        org.json.JSONArray unavailable=data.optJSONArray("unavailable");if(unavailable!=null&&unavailable.length()>0)hardwareContent.addView(Ui.text(this,L10n.text(R.string.ui_some_device_information_could_not_be_read),12,Ui.muted(this),false));
    }
    private LinearLayout diskCard(String title,String model){
        LinearLayout card=new LinearLayout(this);card.setOrientation(LinearLayout.VERTICAL);card.setPadding(Ui.dp(this,14),Ui.dp(this,14),Ui.dp(this,14),Ui.dp(this,14));card.setBackground(Ui.background(Ui.field(this),Ui.dp(this,16)));Ui.margins(card,0,0,0,Ui.dp(this,12));
        card.addView(Ui.text(this,title,15,Ui.ink(this),true));
        if(!model.isEmpty()){TextView caption=Ui.text(this,model,12,Ui.muted(this),false);caption.setSingleLine(true);caption.setEllipsize(TextUtils.TruncateAt.MIDDLE);caption.setPadding(0,Ui.dp(this,4),0,0);caption.setContentDescription(model);card.addView(caption);}
        storageContent.addView(card);return card;
    }
    private void storageNote(LinearLayout card,String value){TextView note=Ui.text(this,value,13,Ui.muted(this),false);note.setPadding(0,Ui.dp(this,10),0,0);card.addView(note);}
    private void storageVolume(LinearLayout card,JSONObject volume)throws Exception{
        long size=volume.isNull("sizeBytes")?-1:volume.getLong("sizeBytes"),free=volume.isNull("freeBytes")?-1:volume.getLong("freeBytes");boolean valid=size>0&&free>=0&&free<=size;double percent=valid?100.0*(size-free)/size:0;
        LinearLayout line=new LinearLayout(this);line.setGravity(Gravity.CENTER_VERTICAL);line.setPadding(0,Ui.dp(this,12),0,0);
        String drive=volume.optString("drive"),label=volume.optString("label");if(!label.isEmpty()&&!label.equalsIgnoreCase(L10n.text(R.string.ui_local_disk))&&!label.equals(L10n.text(R.string.ui_local_disk)))drive+=" · "+label;
        TextView name=Ui.text(this,drive,14,Ui.ink(this),true);name.setSingleLine(true);name.setEllipsize(TextUtils.TruncateAt.END);line.addView(name,new LinearLayout.LayoutParams(0,-2,1));
        if(valid)line.addView(Ui.text(this,String.format(java.util.Locale.getDefault(),"%.1f%%",percent),14,percent>=90?Color.rgb(190,75,62):Ui.blue(this),true));card.addView(line);
        TextView metrics=Ui.text(this,valid?L10n.text(R.string.ui_used)+bytes(size-free)+L10n.text(R.string.ui_total_capacity)+bytes(size):L10n.text(R.string.ui_total_capacity_2)+bytes(size)+L10n.text(R.string.ui_used_space_unknown),13,Ui.muted(this),false);metrics.setPadding(0,Ui.dp(this,4),0,0);card.addView(metrics);
        if(valid){android.widget.ProgressBar bar=new android.widget.ProgressBar(this,null,android.R.attr.progressBarStyleHorizontal);bar.setMax(1000);bar.setProgress((int)Math.round(percent*10));bar.setProgressTintList(android.content.res.ColorStateList.valueOf(percent>=90?Color.rgb(190,75,62):Ui.blue(this)));LinearLayout.LayoutParams layout=new LinearLayout.LayoutParams(-1,Ui.dp(this,6));layout.topMargin=Ui.dp(this,6);card.addView(bar,layout);}
    }
    private void renderStorage(JSONObject data)throws Exception{
        org.json.JSONArray disks=data.optJSONArray("disks"),volumes=data.optJSONArray("volumes");java.util.HashSet<Integer> shown=new java.util.HashSet<>();
        TextView count=Ui.text(this,disks!=null&&disks.length()>0?disks.length()+L10n.text(R.string.ui_physical_disks):L10n.text(R.string.ui_physical_disk_information_unavailable),12,Ui.muted(this),false);count.setPadding(0,0,0,Ui.dp(this,10));storageContent.addView(count);
        if(disks!=null)for(int i=0;i<disks.length();i++){
            JSONObject disk=disks.getJSONObject(i);int index=disk.getInt("index"),matched=0;boolean shared=false;LinearLayout card=diskCard(L10n.text(R.string.ui_disk)+index,disk.optString("model"));
            if(volumes!=null)for(int j=0;j<volumes.length();j++){
                JSONObject volume=volumes.getJSONObject(j);org.json.JSONArray indices=volume.optJSONArray("diskNumbers");if(indices==null)continue;boolean belongs=false;for(int k=0;k<indices.length();k++)if(indices.optInt(k,-1)==index)belongs=true;
                if(belongs&&indices.length()==1&&!shown.contains(j)){storageVolume(card,volume);shown.add(j);matched++;}else if(belongs&&indices.length()>1)shared=true;
            }
            if(shared)storageNote(card,L10n.text(R.string.ui_see_spanning_partitions_below));else if(matched==0)storageNote(card,L10n.text(R.string.ui_total_capacity_2)+bytes(disk.optLong("sizeBytes",-1))+L10n.text(R.string.ui_no_readable_partitions));
        }
        if(volumes!=null)for(int j=0;j<volumes.length();j++)if(!shown.contains(j)){
            JSONObject volume=volumes.getJSONObject(j);org.json.JSONArray indices=volume.optJSONArray("diskNumbers");String mapping=L10n.text(R.string.ui_owning_disk_not_confirmed);
            if(indices!=null&&indices.length()>0){mapping=L10n.text(R.string.ui_related_disks);for(int k=0;k<indices.length();k++)mapping+=(k>0?", ":"")+indices.getInt(k);}
            storageVolume(diskCard(indices!=null&&indices.length()>1?L10n.text(R.string.ui_spanning_partitions):L10n.text(R.string.ui_other_partitions),mapping),volume);shown.add(j);
        }
        TextView updated=Ui.text(this,L10n.text(R.string.ui_collected_at)+localTime(data.optString("observedAt")),12,Ui.muted(this),false);updated.setPadding(0,Ui.dp(this,2),0,Ui.dp(this,12));storageContent.addView(updated);
    }
    private void renderDetailPreview(){
        Bitmap image=DesktopPreview.load(this,Device.ALL[selectedDevice]);detailPreview.setScaleType(image==null?ImageView.ScaleType.CENTER_INSIDE:ImageView.ScaleType.CENTER_CROP);detailPreview.setImageBitmap(image);
        if(image==null)detailPreview.setImageResource(R.drawable.desktop_preview);
        detailPreviewLabel.setText(image==null?L10n.text(R.string.ui_automatically_save_the_desktop_preview_after_the_first):L10n.text(R.string.ui_last_connected_desktop_updates_automatically_after_each));
    }
    private void showOverview(){
        helpVisible=false;
        menuScroll=null;
        menuScrollY=0;
        armBack(false);
        selectedDevice=-1;hardwareGeneration++;hardwareContent=null;storageContent=null;hardwareRefresh=null;detailStatus=null;detailAccount=null;detailPreview=null;detailPreviewLabel=null;setContentView(overview);applyFilter();refreshPreviews();
    }
    private void generatePreviews(int from,int to){
        if(previewNext>=0)return;previewNext=from;previewEnd=to;previewCompleted=previewSkipped=0;previewFailures="";nextPreview();
    }
    private void nextPreview(){
        while(previewNext>=0&&previewNext<previewEnd){
            Device d=Device.ALL[previewNext++];
            try{String saved=app.store.get("credential."+d.id());if(saved!=null){JSONObject c=new JSONObject(saved);String user=c.getString("user").trim(),password=c.getString("password");if((d.protocol()==Device.Protocol.VNC||!user.isEmpty())&&!password.isEmpty()){
                Toast.makeText(this,L10n.text(R.string.ui_generating_preview)+d.name()+L10n.text(R.string.ui_returning_stops_it),Toast.LENGTH_SHORT).show();
                startActivityForResult(new Intent(this,d.protocol()==Device.Protocol.VNC?VncActivity.class:RdpActivity.class).putExtra("device",d.id()).putExtra("user",user).putExtra("password",password).putExtra("previewOnly",true),PREVIEW_CAPTURE);return;
            }}}catch(Exception ignored){}previewSkipped++;previewFailures+=d.name()+L10n.text(R.string.ui_no_saved_sign_in_credentials_n);
        }
        previewNext=-1;refreshPreviews();Toast.makeText(this,L10n.text(R.string.ui_desktop_previews_updated)+previewCompleted+L10n.text(R.string.ui_devices)+(previewSkipped>0?", "+previewSkipped+L10n.text(R.string.ui_devices_need_manual_connection_confirmation):""),Toast.LENGTH_LONG).show();
        if(previewSkipped>0)Ui.dialog(this).setTitle(L10n.text(R.string.ui_desktop_preview_update_result)).setMessage(L10n.text(R.string.ui_updated)+previewCompleted+L10n.text(R.string.ui_devices_n_n)+previewFailures+L10n.text(R.string.ui_nsave_credentials_under_account_and_password_or_connect)).setPositiveButton(L10n.text(R.string.ui_got_it),null).show();
    }
    @Override public void onPause(){controlTimer.removeCallbacks(pollControl);app.tunnel.listen(null);super.onPause();}
    @Override public void changed(String message,int[] states){
        network.setText(message.startsWith("Devices reachable ")
                ? L10n.text(R.string.ui_devices_reachable)+message.substring("Devices reachable ".length()) : message);
        for(int i=0;i<states.length;i++){reachable[i]=states[i];if(states[i]!=1)usage[i]=null;renderUsage(i);}
        applyFilter();refreshUsage();
    }
    private void renderUsage(int i){String protocol=Device.ALL[i].protocol().name();status[i].setText(reachable[i]==1?"● "+protocol+L10n.text(R.string.ui_reachable_n)+(usage[i]==null?L10n.text(R.string.ui_usage_state_unknown):usage[i].text()):reachable[i]==-1?"● "+protocol+L10n.text(R.string.ui_temporarily_unreachable_n)+(usage[i]==null?L10n.text(R.string.ui_usage_state_unknown):usage[i].text()):reachable[i]==2?L10n.text(R.string.ui_checking):L10n.text(R.string.ui_waiting_for_network_connection));status[i].setTextColor(reachable[i]==1?Ui.ok(this):Ui.muted(this));deviceCards[i].setContentDescription(Device.ALL[i].name()+", "+status[i].getText()+(reachable[i]==1?L10n.text(R.string.ui_tap_to_connect):L10n.text(R.string.ui_temporarily_unavailable_to_connect)));if(selectedDevice==i)refreshDetails();}
    private void refreshUsage(){for(int i=0;i<Device.ALL.length;i++){if((reachable[i]!=1&&reachable[i]!=-1)||querying[i])continue;final int n=i;querying[i]=true;control.status(Device.ALL[i],(data,error)->{querying[n]=false;usage[n]=null;if(data!=null)try{org.json.JSONArray rows=data.getJSONArray("sessions");int active=0;boolean local=false;for(int j=0;j<rows.length();j++){JSONObject s=rows.getJSONObject(j).getJSONObject("session");if(s.optBoolean("isRemoteActive"))active++;if(s.optInt("protocol")==0&&s.optInt("state")==0&&!s.isNull("locked")&&!s.getBoolean("locked"))local=true;}usage[n]=new DeviceUsage(active,local);}catch(Exception ignored){}renderUsage(n);});}}
    private void sessions(Device d){
        LinearLayout list=new LinearLayout(this);list.setOrientation(LinearLayout.VERTICAL);list.setPadding(Ui.dp(this,20),Ui.dp(this,4),Ui.dp(this,20),0);
        ScrollView scroll=new ScrollView(this);scroll.addView(list);
        AlertDialog dialog=Ui.dialog(this).setTitle(L10n.text(R.string.ui_sign_in_and_connections)).setView(scroll).setNeutralButton(L10n.text(R.string.ui_refresh),null).setPositiveButton(L10n.text(R.string.ui_close),null).create();
        dialog.setOnShowListener(v->{dialog.getButton(AlertDialog.BUTTON_NEUTRAL).setOnClickListener(b->loadSessions(d,dialog,list));loadSessions(d,dialog,list);});dialog.show();
    }
    private void loadSessions(Device d,AlertDialog dialog,LinearLayout list){
        if(!dialog.isShowing())return;dialog.getButton(AlertDialog.BUTTON_NEUTRAL).setEnabled(false);list.removeAllViews();list.addView(Ui.text(this,L10n.text(R.string.ui_reading_sign_in_and_connections),14,Ui.muted(this),false));
        control.status(d,(data,error)->{
            if(isFinishing()||isDestroyed()||!dialog.isShowing())return;
            dialog.getButton(AlertDialog.BUTTON_NEUTRAL).setEnabled(true);list.removeAllViews();
            if(data==null){list.addView(Ui.text(this,L10n.text(R.string.ui_connection_status_unavailable),16,Ui.ink(this),true));TextView failure=Ui.text(this,error==null?L10n.text(R.string.ui_please_refresh_later):error,14,Ui.muted(this),false);failure.setPadding(0,Ui.dp(this,12),0,Ui.dp(this,16));list.addView(failure);return;}
            try{
                org.json.JSONArray rows=data.getJSONArray("sessions");int activeCount=0,visibleCount=0;
                for(int j=0;j<rows.length();j++){JSONObject s=rows.getJSONObject(j).getJSONObject("session");boolean active=s.optBoolean("isRemoteActive");if(s.optString("user").trim().isEmpty()&&!active)continue;visibleCount++;if(active)activeCount++;}
                list.addView(Ui.text(this,d.name(),13,Ui.muted(this),false));
                TextView summary=Ui.text(this,activeCount>0?L10n.text(R.string.ui_there_are_currently)+activeCount+L10n.text(R.string.ui_active_remote_connections):L10n.text(R.string.ui_no_active_remote_connections_currently),16,Ui.ink(this),true);summary.setPadding(0,Ui.dp(this,8),0,Ui.dp(this,16));list.addView(summary);
                for(int j=0;j<rows.length();j++){
                    JSONObject row=rows.getJSONObject(j),s=row.getJSONObject("session");boolean active=s.optBoolean("isRemoteActive");String fullUser=s.optString("user").trim();if(fullUser.isEmpty()&&!active)continue;
                    String user=fullUser.isEmpty()?L10n.text(R.string.ui_account_not_reported):shortAccount(d,fullUser),client=s.optString("client").trim();boolean disconnected=s.optInt("state")==4,local=s.optInt("protocol")==0;
                    String source=client.isEmpty()?L10n.text(R.string.ui_not_recorded):client;for(Device known:Device.ALL)if(known.hostName().equalsIgnoreCase(client)||known.ip().equals(client))source=known.name();
                    final String sourceName=source;
                    LinearLayout card=new LinearLayout(this);card.setOrientation(LinearLayout.VERTICAL);card.setPadding(Ui.dp(this,14),Ui.dp(this,14),Ui.dp(this,14),Ui.dp(this,6));card.setBackground(Ui.background(Ui.field(this),Ui.dp(this,14)));Ui.margins(card,0,0,0,Ui.dp(this,12));
                    card.addView(Ui.text(this,user,18,Ui.ink(this),true));
                    String state=active?L10n.text(R.string.ui_remote_connected):disconnected?L10n.text(R.string.ui_disconnected):local?(!s.isNull("locked")&&s.optBoolean("locked")?L10n.text(R.string.ui_this_machine_is_locked):L10n.text(R.string.ui_local_sign_in)):L10n.text(R.string.ui_other_sign_in_state);
                    TextView stateText=Ui.text(this,state,13,active?Ui.blue(this):Ui.muted(this),true);stateText.setPadding(0,Ui.dp(this,6),0,Ui.dp(this,8));card.addView(stateText);
                    if(disconnected)card.addView(Ui.text(this,L10n.text(R.string.ui_the_account_stays_signed_in_open_programs_are_preserved),13,Ui.muted(this),false));
                    TextView sourceText=Ui.text(this,local&&!active&&!disconnected?L10n.text(R.string.ui_sign_in_method_this_device):L10n.text(R.string.ui_source_device)+source,13,Ui.muted(this),false);sourceText.setPadding(0,Ui.dp(this,6),0,0);card.addView(sourceText);
                    TextView extra=Ui.text(this,L10n.text(R.string.ui_windows_session_number)+s.optInt("id")+L10n.text(R.string.ui_nfull_account)+(fullUser.isEmpty()?L10n.text(R.string.ui_not_reported):fullUser)+L10n.text(R.string.ui_nsource_name)+(client.isEmpty()?L10n.text(R.string.ui_not_recorded):client),12,Ui.muted(this),false);extra.setPadding(0,Ui.dp(this,10),0,Ui.dp(this,4));extra.setVisibility(android.view.View.GONE);card.addView(extra);
                    LinearLayout actions=new LinearLayout(this);Button details=Ui.button(this,L10n.text(R.string.ui_details));details.setOnClickListener(v->{boolean expanded=extra.getVisibility()==android.view.View.VISIBLE;extra.setVisibility(expanded?android.view.View.GONE:android.view.View.VISIBLE);details.setText(expanded?L10n.text(R.string.ui_details):L10n.text(R.string.ui_collapse));});actions.addView(details,new LinearLayout.LayoutParams(0,Ui.dp(this,48),1));
                    if(active&&!row.isNull("ticket")){Button stop=Ui.button(this,L10n.text(R.string.ui_disconnect));stop.setTextColor(Ui.danger(this));actions.addView(stop);stop.setOnClickListener(v->Ui.dialog(this).setTitle(L10n.text(R.string.ui_disconnect_remote_connection)).setMessage(L10n.text(R.string.ui_disconnect)+(L10n.text(R.string.ui_not_recorded).equals(sourceName)?L10n.text(R.string.ui_device_with_unrecorded_source):sourceName)+L10n.text(R.string.ui_for_account)+user+L10n.text(R.string.ui_s_connection_n_nthe_account_stays_signed_in_programs_an)).setNegativeButton(L10n.text(R.string.ui_cancel),null).setPositiveButton(L10n.text(R.string.ui_disconnect),(x,n)->{stop.setEnabled(false);control.disconnect(d,row,(result,failure)->{if(isFinishing()||isDestroyed())return;Toast.makeText(this,result!=null&&result.optBoolean("disconnected")?L10n.text(R.string.ui_connection_closed):failure==null?L10n.text(R.string.ui_disconnection_not_yet_confirmed_please_refresh):failure,Toast.LENGTH_LONG).show();refreshUsage();if(dialog.isShowing())loadSessions(d,dialog,list);});}).show());}
                    card.addView(actions);list.addView(card);
                }
                if(visibleCount==0){TextView empty=Ui.text(this,L10n.text(R.string.ui_no_signed_in_accounts_currently),14,Ui.muted(this),false);empty.setPadding(0,0,0,Ui.dp(this,16));list.addView(empty);}
            }catch(Exception e){list.removeAllViews();list.addView(Ui.text(this,L10n.text(R.string.ui_sign_in_and_connection_information_unavailable_please_r),14,Ui.muted(this),false));}
        });
    }
    @Override public void onSaveInstanceState(Bundle out){super.onSaveInstanceState(out);out.putString("pendingControlDevice",pendingControlDevice);out.putInt("selectedDevice",selectedDevice);out.putInt("previewNext",previewNext);out.putInt("previewEnd",previewEnd);out.putInt("view",view);out.putInt("previewCompleted",previewCompleted);out.putInt("previewSkipped",previewSkipped);out.putString("previewFailures",previewFailures);out.putBoolean("menuVisible",menuScroll!=null);out.putInt("menuScrollY",menuScrollY);out.putBoolean("helpVisible",helpVisible);}
        @Override public void onConfigurationChanged(android.content.res.Configuration newConfig){
        super.onConfigurationChanged(newConfig);
        if("system".equals(Theme.getMode())){
            Theme.applySystemBars(this);
            if(headerAnim!=null)headerAnim.cancel();
            buildOverview();
            for(int i=0;i<Device.ALL.length;i++)renderUsage(i);
            if(menuScroll!=null)showMenu(menuScrollY);
        }
    }
    @Override public void onDestroy(){controlTimer.removeCallbacksAndMessages(null);if(control!=null)control.close();super.onDestroy();}
    private void connectDevice(Device d){
        try{
            String saved=app.store.get("credential."+d.id());
            if(saved!=null){
                JSONObject c=new JSONObject(saved);String u=c.getString("user").trim(),p=c.getString("password");
                if((d.protocol()==Device.Protocol.VNC||!u.isEmpty())&&!p.isEmpty()){openDesktop(d,u,p);return;}
            }
        }catch(Exception ignored){ }
        credentials(d,true);
    }
    private void openDesktop(Device d,String user,String password){
        startActivity(new Intent(this,d.protocol()==Device.Protocol.VNC?VncActivity.class:RdpActivity.class).putExtra("device",d.id()).putExtra("user",user).putExtra("password",password));
    }
    private void credentials(Device d,boolean connectAfter){
        LinearLayout form=new LinearLayout(this);form.setOrientation(LinearLayout.VERTICAL);form.setPadding(Ui.dp(this,22),Ui.dp(this,8),Ui.dp(this,22),0);
        EditText user=new EditText(this);user.setHint(L10n.text(R.string.ui_sign_in_username));user.setSingleLine(true);user.setText(d.user());user.setTextColor(Ui.ink(this));user.setHintTextColor(Ui.muted(this));user.setInputType(InputType.TYPE_CLASS_TEXT|InputType.TYPE_TEXT_VARIATION_VISIBLE_PASSWORD);form.addView(user);
        EditText password=new EditText(this);password.setHint(L10n.text(R.string.ui_sign_in_password));password.setSingleLine(true);password.setTextColor(Ui.ink(this));password.setHintTextColor(Ui.muted(this));password.setInputType(InputType.TYPE_CLASS_TEXT|InputType.TYPE_TEXT_VARIATION_PASSWORD);form.addView(password);
        if(d.protocol()==Device.Protocol.VNC){user.setVisibility(View.GONE);password.setHint(L10n.text(R.string.ui_mac_screen_sharing_vnc_password));}
        CheckBox remember=new CheckBox(this);remember.setText(L10n.text(R.string.ui_remember_password_saved_on_this_phone_only));remember.setTextColor(Ui.ink(this));remember.setChecked(!connectAfter);form.addView(remember);
        try{String saved=app.store.get("credential."+d.id());if(saved!=null){JSONObject c=new JSONObject(saved);user.setText(c.getString("user"));password.setText(c.getString("password"));remember.setChecked(true);}}catch(Exception e){Toast.makeText(this,L10n.text(R.string.ui_saved_credentials_are_unavailable_please_re_enter),Toast.LENGTH_SHORT).show();}
        AlertDialog.Builder builder=Ui.dialog(this).setTitle(d.name()).setView(form).setNegativeButton(L10n.text(R.string.ui_cancel),null).setPositiveButton(connectAfter?L10n.text(R.string.ui_connect):L10n.text(R.string.ui_save),null);
        if(!connectAfter)builder.setNeutralButton(L10n.text(R.string.ui_clear_saved_credentials),(x,n)->{app.store.remove("credential."+d.id());refreshDetails();Toast.makeText(this,L10n.text(R.string.ui_cleared_re_enter_on_the_next_connection),Toast.LENGTH_SHORT).show();});
        AlertDialog dialog=builder.create();
        dialog.setOnShowListener(v->dialog.getButton(AlertDialog.BUTTON_POSITIVE).setOnClickListener(b->{
            String u=user.getText().toString().trim(),p=password.getText().toString();
            if((connectAfter||remember.isChecked())&&((d.protocol()==Device.Protocol.RDP&&u.isEmpty())||p.isEmpty())){password.setError(d.protocol()==Device.Protocol.VNC?L10n.text(R.string.ui_enter_the_screen_sharing_vnc_password):L10n.text(R.string.ui_enter_this_computer_s_account_and_sign_in_password));return;}
            try{if(remember.isChecked())app.store.put("credential."+d.id(),new JSONObject().put("user",u).put("password",p).toString());else app.store.remove("credential."+d.id());}
            catch(Exception e){Toast.makeText(this,connectAfter?L10n.text(R.string.ui_the_password_could_not_be_saved_but_this_connection_can):L10n.text(R.string.ui_the_password_could_not_be_saved_please_retry),Toast.LENGTH_SHORT).show();if(!connectAfter)return;}
            refreshDetails();dialog.dismiss();if(connectAfter)openDesktop(d,u,p);else Toast.makeText(this,L10n.text(R.string.ui_account_settings_saved),Toast.LENGTH_SHORT).show();
        }));dialog.show();
    }
    private record DeviceInfoResult(int status,String message){}
    private void refreshDeviceInfos(Device[] devices){
        if(deviceInfoRefreshing){Toast.makeText(this,L10n.text(R.string.ui_device_information_is_updating_please_wait),Toast.LENGTH_SHORT).show();return;}
        deviceInfoRefreshing=true;
        AlertDialog progress=Ui.dialog(this).setTitle(L10n.text(R.string.ui_refresh_device_information)).setMessage(L10n.text(R.string.ui_preparing)).setPositiveButton(L10n.text(R.string.ui_close),null).show();
        nextDeviceInfo(devices,0,progress,new int[3],new StringBuilder());
    }
    private void nextDeviceInfo(Device[] devices,int index,AlertDialog progress,int[] totals,StringBuilder notes){
        if(isFinishing()||isDestroyed()){deviceInfoRefreshing=false;return;}
        if(index>=devices.length){
            deviceInfoRefreshing=false;String summary=L10n.text(R.string.ui_succeeded)+totals[0]+L10n.text(R.string.ui_devices_partially_updated)+totals[1]+L10n.text(R.string.ui_devices_failed)+totals[2]+L10n.text(R.string.ui_devices);
            String message=summary+"\n\n"+notes+L10n.text(R.string.ui_nexisting_storage_data_is_unchanged_and_still_refreshed);
            if(progress.isShowing())progress.setMessage(message);else Toast.makeText(this,summary,Toast.LENGTH_LONG).show();return;
        }
        Device device=devices[index];if(progress.isShowing())progress.setMessage(L10n.text(R.string.ui_updating)+(index+1)+" / "+devices.length+": "+device.name()+L10n.text(R.string.ui_n_nre_read_windows_cpu_memory_and_gpu_they_still_comple));
        requestDeviceInfo(device,result->{totals[result.status()]++;notes.append(device.name()).append(": ").append(result.message()).append("\n");nextDeviceInfo(devices,index+1,progress,totals,notes);});
    }
    private void requestDeviceInfo(Device device,java.util.function.Consumer<DeviceInfoResult> reply){
        control.hardware(device,true,(data,error)->{
            DeviceInfoResult result;
            try{
                if(data==null)throw new IllegalStateException(error==null?L10n.text(R.string.ui_device_information_unavailable_please_retry_later):error);
                if(data.optInt("schema")!=1||!device.hostName().equalsIgnoreCase(data.optString("machine")))throw new IllegalStateException(L10n.text(R.string.ui_device_information_response_mismatch_please_retry_later));
                var cache=getSharedPreferences("hardware-cache",MODE_PRIVATE);JSONObject previous=null;
                try{String saved=cache.getString(device.id()+".data",null);if(saved!=null){JSONObject value=new JSONObject(saved);if(value.optInt("schema")==1&&device.hostName().equalsIgnoreCase(value.optString("machine")))previous=value;}}catch(Exception ignored){}
                JSONObject updated=previous==null?new JSONObject(data.toString()):new JSONObject(previous.toString());
                java.util.HashSet<String> failed=new java.util.HashSet<>();org.json.JSONArray issues=data.optJSONArray("unavailable");if(issues!=null)for(int i=0;i<issues.length();i++)failed.add(issues.optString(i));
                String[] keys={"windows","cpu","memoryBytes","gpus","os","model"},sections={"Windows","CPU","Memory","GPU",L10n.text(R.string.ui_system),L10n.text(R.string.ui_model)};
                for(int i=0;i<keys.length;i++)if(!failed.contains(sections[i])&&!(i==0&&failed.contains("WindowsVersion"))&&data.has(keys[i]))updated.put(keys[i],data.get(keys[i]));
                org.json.JSONArray unavailable=new org.json.JSONArray();if(previous!=null){org.json.JSONArray old=previous.optJSONArray("unavailable");if(old!=null)for(int i=0;i<old.length();i++){String issue=old.optString(i);if(issue.equals("Disks")||issue.equals("Volumes")||issue.equals("DiskMapping"))unavailable.put(issue);}}
                boolean partial=false;for(String issue:new String[]{"Windows","WindowsVersion","CPU","Memory","GPU",L10n.text(R.string.ui_system),L10n.text(R.string.ui_model)})if(failed.contains(issue)){unavailable.put(issue);partial=true;}
                updated.put("unavailable",unavailable);updated.put("deviceInfoObservedAt",data.optString("observedAt"));
                var edit=cache.edit().putString(device.id()+".data",updated.toString()).putLong(device.id()+".deviceInfoSavedAt",System.currentTimeMillis());if(previous==null)edit.putLong(device.id()+".savedAt",System.currentTimeMillis());edit.apply();
                if(selectedDevice>=0&&Device.ALL[selectedDevice].id().equals(device.id()))loadHardware(false);
                result=new DeviceInfoResult(partial?1:0,partial?L10n.text(R.string.ui_partially_updated_items_that_failed_to_read_keep_the_pr):L10n.text(R.string.ui_updated_and_saved));
            }catch(Exception e){result=new DeviceInfoResult(2,e instanceof IllegalStateException&&e.getMessage()!=null?e.getMessage():L10n.text(R.string.ui_update_failed_the_previous_cache_is_kept));}
            reply.accept(result);
        });
    }
    private void showMenu(){
        showMenu(menuScroll==null?0:menuScrollY);
    }
    private void showMenu(int scrollY){
        menuScrollY=scrollY;
        helpVisible=false;
        armBack(true);
        LinearLayout root=new LinearLayout(this);root.setOrientation(LinearLayout.VERTICAL);Ui.page(root);
        ImageButton close=Ui.iconButton(this,XIcon.CLOSE,Ui.ink(this),L10n.text(R.string.ui_close));close.setOnClickListener(v->showOverview());
        root.addView(Ui.header(this,L10n.text(R.string.ui_devices_and_settings),close),new LinearLayout.LayoutParams(-1,Ui.dp(this,60)));
        ScrollView scroll=new ScrollView(this);menuScroll=scroll;LinearLayout content=new LinearLayout(this);content.setOrientation(LinearLayout.VERTICAL);content.setPadding(Ui.dp(this,16),0,Ui.dp(this,16),Ui.dp(this,16));
        scroll.setOnScrollChangeListener((v,x,y,oldX,oldY)->{if(menuScroll==scroll)menuScrollY=y;});
        int online=0;for(int r:reachable)if(r==1)online++;
        menuRow(content,XIcon.SEARCH,L10n.text(R.string.ui_search_devices),0,false,Ui.ink(this),()->{searchOpen=true;showOverview();searchInput.requestFocus();searchInput.post(()->((android.view.inputmethod.InputMethodManager)getSystemService(INPUT_METHOD_SERVICE)).showSoftInput(searchInput,android.view.inputmethod.InputMethodManager.SHOW_IMPLICIT));});
        content.addView(Ui.divider(this),dividerMargins());
        menuRow(content,XIcon.DEVICES,L10n.text(R.string.ui_all_devices),Device.ALL.length,view==0,Ui.ink(this),()->{view=0;showOverview();});
        menuRow(content,XIcon.ONLINE,L10n.text(R.string.ui_online_devices),online,view==1,Ui.ink(this),()->{view=1;showOverview();});
        menuRow(content,XIcon.OFFLINE,L10n.text(R.string.ui_offline_devices),Device.ALL.length-online,view==2,Ui.ink(this),()->{view=2;showOverview();});
        for(Device d:Device.ALL){
            content.addView(Ui.divider(this),dividerMargins());
            LinearLayout group=new LinearLayout(this);group.setGravity(Gravity.CENTER_VERTICAL);group.setPadding(Ui.dp(this,12),Ui.dp(this,4),0,Ui.dp(this,4));
            group.addView(Ui.icon(this,XIcon.DEVICES,Ui.muted(this),22));
            LinearLayout names=new LinearLayout(this);names.setOrientation(LinearLayout.VERTICAL);names.setPadding(Ui.dp(this,16),0,0,0);names.addView(Ui.text(this,d.name(),15,Ui.ink(this),true));names.addView(Ui.text(this,d.ip(),12,Ui.muted(this),false));group.addView(names,Ui.weight());
            ImageButton gear=Ui.iconButton(this,XIcon.GEAR,Ui.muted(this),d.name()+L10n.text(R.string.ui_manage));gear.setOnClickListener(v->deviceMenu(d,gear));group.addView(gear);content.addView(group);
        }
        content.addView(Ui.divider(this),dividerMargins());
        menuRow(content,XIcon.IMPORT,L10n.text(R.string.ui_import_network_configuration),-1,false,Ui.ink(this),this::importConfig);
        menuRow(content,XIcon.CLOCK,L10n.text(R.string.ui_background_retention)+app.graceSeconds()/60+L10n.text(R.string.ui_minutes),-1,false,Ui.ink(this),this::pickGrace);
        LinearLayout language=menuRow(content,XIcon.LANGUAGE,L10n.text(R.string.ui_language),-1,false,Ui.ink(this),this::toggleLanguage);
        language.addView(Ui.text(this,L10n.languageName(),12,Ui.muted(this),false));
        boolean isDark=Theme.isDark(this);
        LinearLayout themeRow=menuRow(content,isDark?XIcon.MOON:XIcon.SUN,L10n.text(R.string.ui_theme),-1,false,Ui.ink(this),this::toggleTheme);
        themeRow.addView(Ui.text(this,Theme.themeName(this),12,Ui.muted(this),false));
        menuRow(content,XIcon.INFO,L10n.text(R.string.ui_help),-1,false,Ui.ink(this),this::showHelp);
        menuRow(content,XIcon.POWER,L10n.text(R.string.ui_disconnect_and_exit),-1,false,Ui.danger(this),()->{app.exitNow();finishAndRemoveTask();});
        content.addView(Ui.divider(this),dividerMargins());
        LinearLayout footer=new LinearLayout(this);footer.setOrientation(LinearLayout.VERTICAL);footer.setGravity(Gravity.CENTER_HORIZONTAL);footer.setPadding(0,Ui.dp(this,16),0,Ui.dp(this,8));footer.setBackground(Ui.touch(this,0,Ui.dp(this,16)));footer.setOnClickListener(v->about());
        ImageView logo=new ImageView(this);logo.setImageResource(R.drawable.ic_xremote);footer.addView(logo,new LinearLayout.LayoutParams(Ui.dp(this,40),Ui.dp(this,40)));
        TextView label=Ui.text(this,"xRemote "+version(),12,Ui.muted(this),false);label.setGravity(Gravity.CENTER_HORIZONTAL);label.setPadding(0,Ui.dp(this,8),0,0);footer.addView(label,new LinearLayout.LayoutParams(-1,-2));content.addView(footer,new LinearLayout.LayoutParams(-1,-2));
        scroll.addView(content);root.addView(scroll,new LinearLayout.LayoutParams(-1,0,1));setContentView(root);Ui.insets(root);
        scroll.post(()->scroll.scrollTo(0,scrollY));
    }
    private void toggleLanguage(){
        int scrollY=menuScrollY;
        L10n.toggle();
        if(headerAnim!=null)headerAnim.cancel();
        buildOverview();
        for(int i=0;i<Device.ALL.length;i++)renderUsage(i);
        app.tunnel.listen(this);
        showMenu(scrollY);
    }
    private void toggleTheme(){
        int scrollY=menuScrollY;
        Theme.toggle(this);
        Theme.applySystemBars(this);
        if(headerAnim!=null)headerAnim.cancel();
        buildOverview();
        for(int i=0;i<Device.ALL.length;i++)renderUsage(i);
        app.tunnel.listen(this);
        showMenu(scrollY);
    }
    private int deviceIndex(Device d){for(int i=0;i<Device.ALL.length;i++)if(Device.ALL[i].id().equals(d.id()))return i;return 0;}
    private LinearLayout.LayoutParams dividerMargins(){LinearLayout.LayoutParams p=new LinearLayout.LayoutParams(-1,Math.max(1,Ui.dp(this,1)));p.setMargins(0,Ui.dp(this,6),0,Ui.dp(this,6));return p;}
    private LinearLayout menuRow(LinearLayout content,int icon,String label,int count,boolean selected,int color,Runnable action){
        LinearLayout row=new LinearLayout(this);row.setGravity(Gravity.CENTER_VERTICAL);row.setMinimumHeight(Ui.dp(this,48));row.setPadding(Ui.dp(this,12),Ui.dp(this,8),Ui.dp(this,12),Ui.dp(this,8));row.setBackground(Ui.touch(this,selected?Ui.tint(this):0,Ui.dp(this,16)));row.setClickable(true);row.setOnClickListener(v->action.run());
        int tint=selected?Ui.blue(this):color;row.addView(Ui.icon(this,icon,tint,22));
        TextView name=Ui.text(this,label,15,tint,selected);name.setPadding(Ui.dp(this,16),0,0,0);row.addView(name,Ui.weight());
        if(count>0)row.addView(Ui.text(this,String.valueOf(count),12,Ui.muted(this),false));
        content.addView(row,new LinearLayout.LayoutParams(-1,-2));
        return row;
    }
    private void deviceMenu(Device d,View anchor){
        Ui.dialog(this).setTitle(d.name()).setItems(new String[]{L10n.text(R.string.ui_sign_in_and_connections),L10n.text(R.string.ui_account_and_password),L10n.text(R.string.ui_rename),L10n.text(R.string.ui_refresh_device_information),L10n.text(R.string.ui_update_desktop_thumbnail),L10n.text(R.string.ui_import_management_pairing)},(x,n)->{
            switch(n){
                case 0:sessions(d);break;
                case 1:credentials(d,false);break;
                case 2:renameDevice(d);break;
                case 3:refreshDeviceInfos(new Device[]{d});break;
                case 4:generatePreviews(deviceIndex(d),deviceIndex(d)+1);break;
                case 5:pendingControlDevice=d.id();app.externalPicker(true);startActivityForResult(new Intent(Intent.ACTION_OPEN_DOCUMENT).setType("application/json").addCategory(Intent.CATEGORY_OPENABLE),IMPORT_CONTROL);break;
            }
        }).show();
    }
    private void loadAliases(){android.content.SharedPreferences p=getSharedPreferences("device_names",MODE_PRIVATE);Device.ALIASES.clear();for(Device d:Device.ALL){String v=p.getString(d.id(),"");if(!v.isEmpty())Device.ALIASES.put(d.id(),v);}}
    private void renameDevice(Device d){
        android.widget.EditText input=new android.widget.EditText(this);input.setSingleLine(true);input.setText(d.name());input.setTextColor(Ui.ink(this));input.setHintTextColor(Ui.muted(this));input.setSelectAllOnFocus(true);input.setPadding(Ui.dp(this,12),Ui.dp(this,10),Ui.dp(this,12),Ui.dp(this,10));input.setBackground(Ui.background(Ui.field(this),Ui.dp(this,10)));
        LinearLayout box=new LinearLayout(this);box.setPadding(Ui.dp(this,20),Ui.dp(this,8),Ui.dp(this,20),0);box.addView(input,new LinearLayout.LayoutParams(-1,Ui.dp(this,50)));
        Ui.dialog(this).setTitle(L10n.text(R.string.ui_rename)).setView(box).setNegativeButton(L10n.text(R.string.ui_cancel),null).setPositiveButton(L10n.text(R.string.ui_save),(x,w)->{
            String value=input.getText().toString().trim();android.content.SharedPreferences.Editor e=getSharedPreferences("device_names",MODE_PRIVATE).edit();
            if(value.isEmpty())e.remove(d.id());else e.putString(d.id(),value);e.apply();loadAliases();recreate();
        }).show();
    }
    private void importConfig(){app.externalPicker(true);Intent picker=new Intent(Intent.ACTION_OPEN_DOCUMENT).setType("*/*").addCategory(Intent.CATEGORY_OPENABLE);startActivityForResult(picker,IMPORT);}
    private void pickGrace(){Ui.dialog(this).setTitle(L10n.text(R.string.ui_keep_connection_after_leaving_the_app)).setItems(new String[]{L10n.text(R.string.ui_1_minutes),L10n.text(R.string.ui_2_minutes),L10n.text(R.string.ui_5_minutes)},(x,n)->{app.graceSeconds(new int[]{60,120,300}[n]);showMenu();}).show();}
    private void showHelp(){
        helpVisible=true;armBack(true);
        LinearLayout root=new LinearLayout(this);root.setOrientation(LinearLayout.VERTICAL);Ui.page(root);
        ImageButton close=Ui.iconButton(this,XIcon.CLOSE,Ui.ink(this),L10n.text(R.string.ui_close_help));close.setOnClickListener(v->showMenu());
        root.addView(Ui.header(this,L10n.text(R.string.ui_help),close),new LinearLayout.LayoutParams(-1,Ui.dp(this,60)));
        ScrollView scroll=new ScrollView(this);LinearLayout content=new LinearLayout(this);content.setOrientation(LinearLayout.VERTICAL);content.setPadding(Ui.dp(this,24),0,Ui.dp(this,24),Ui.dp(this,24));scroll.addView(content);
        helpSection(content,L10n.text(R.string.ui_first_connection),L10n.text(R.string.ui_open_devices_and_settings_import_network_configuration));
        helpSection(content,L10n.text(R.string.ui_find_and_manage_devices),L10n.text(R.string.ui_in_the_menu_search_devices_finds_by_name_or_address_and));
        helpSection(content,L10n.text(R.string.ui_remote_desktop_controls),L10n.text(R.string.ui_after_connecting_the_phone_enters_landscape_desktop_tap));
        helpSection(content,L10n.text(R.string.ui_sign_in_and_connections),L10n.text(R.string.ui_in_device_details_tap_view_sign_in_and_connections_or_i));
        helpSection(content,L10n.text(R.string.ui_device_information_and_pairing),L10n.text(R.string.ui_device_information_and_thumbnails_are_saved_on_this_dev));
        helpSection(content,L10n.text(R.string.ui_background_retention_and_exit),L10n.text(R.string.ui_after_switching_apps_or_locking_the_screen_the_connecti));
        helpSection(content,L10n.text(R.string.ui_connection_error),L10n.text(R.string.ui_if_the_device_is_offline_first_confirm_the_target_compu));
        root.addView(scroll,new LinearLayout.LayoutParams(-1,0,1));setContentView(root);Ui.insets(root);
    }
    private void helpSection(LinearLayout content,String heading,String body){
        TextView title=Ui.text(this,heading,17,Ui.ink(this),true);title.setPadding(0,Ui.dp(this,16),0,Ui.dp(this,10));content.addView(title);
        TextView text=Ui.text(this,body,15,Ui.ink(this),false);text.setTextIsSelectable(true);text.setLineSpacing(Ui.dp(this,4),1);content.addView(text,new LinearLayout.LayoutParams(-1,-2));
    }
    private void about(){String rdp;try{rdp=LibFreeRDP.freerdp_get_jni_version();}catch(Throwable e){rdp=L10n.text(R.string.ui_load_failed);}Ui.dialog(this).setTitle("xRemote "+version()).setMessage(L10n.text(R.string.ui_built_in_easytier_2_6_4_nbuilt_in_freerdp)+rdp+L10n.text(R.string.ui_n_nopen_source_licenses_and_component_sources_are_in_th)).setPositiveButton(L10n.text(R.string.ui_got_it),null).show();}
    private boolean backArmed;
    private void armBack(boolean on){
        if(on==backArmed)return;backArmed=on;
        if(on)getOnBackInvokedDispatcher().registerOnBackInvokedCallback(android.window.OnBackInvokedDispatcher.PRIORITY_DEFAULT,backToDevices);else getOnBackInvokedDispatcher().unregisterOnBackInvokedCallback(backToDevices);
    }
    @Override protected void onActivityResult(int request,int result,Intent data){
        super.onActivityResult(request,result,data);
        if(request==PREVIEW_CAPTURE){if(result==RESULT_CANCELED){previewNext=-1;refreshPreviews();Toast.makeText(this,L10n.text(R.string.ui_preview_update_stopped),Toast.LENGTH_SHORT).show();return;}if(result==RESULT_OK)previewCompleted++;else{previewSkipped++;if(previewNext>0&&previewNext<=Device.ALL.length)previewFailures+=Device.ALL[previewNext-1].name()+": "+(data==null?L10n.text(R.string.ui_connection_not_completed):data.getStringExtra("previewError"))+"\n";}nextPreview();return;}
        if(request!=IMPORT&&request!=IMPORT_CONTROL)return;app.externalPicker(false);app.ensureTunnel();
        if(request==IMPORT_CONTROL){
            if(result==RESULT_OK&&data!=null&&data.getData()!=null&&pendingControlDevice!=null){
                try(InputStream in=getContentResolver().openInputStream(data.getData())){if("all".equals(pendingControlDevice))control.importPairings(in);else control.importPairing(Device.find(pendingControlDevice),in);Toast.makeText(this,L10n.text(R.string.ui_management_pairing_saved),Toast.LENGTH_SHORT).show();refreshUsage();if(selectedDevice>=0)loadHardware(true);}
                catch(Exception e){Toast.makeText(this,L10n.text(R.string.ui_pairing_incomplete_select_the_pairing_file_that_matches),Toast.LENGTH_LONG).show();}
            }
            pendingControlDevice=null;return;
        }
        if(result==RESULT_OK&&data!=null&&data.getData()!=null){
            try(InputStream in=getContentResolver().openInputStream(data.getData())){
                app.tunnel.importConfig(in);Toast.makeText(this,L10n.text(R.string.ui_network_configuration_imported),Toast.LENGTH_SHORT).show();
                if(checkSelfPermission(android.Manifest.permission.POST_NOTIFICATIONS)!=android.content.pm.PackageManager.PERMISSION_GRANTED)
                    requestPermissions(new String[]{android.Manifest.permission.POST_NOTIFICATIONS},710);
            }
            catch(Exception e){Ui.dialog(this).setTitle(L10n.text(R.string.ui_configuration_not_imported)).setMessage(L10n.text(R.string.ui_select_the_one_prepared_for_this_phone_xremote_configur)).setPositiveButton(L10n.text(R.string.ui_got_it),null).show();}
        }
        app.tunnel.listen(this);app.tunnel.start();applyFilter();
    }
}

