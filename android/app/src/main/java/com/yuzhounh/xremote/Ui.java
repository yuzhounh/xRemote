package com.yuzhounh.xremote;

import android.app.Activity;
import android.app.AlertDialog;
import android.content.Context;
import android.content.res.ColorStateList;
import android.graphics.Color;
import android.graphics.Typeface;
import android.graphics.drawable.GradientDrawable;
import android.graphics.drawable.InsetDrawable;
import android.graphics.drawable.RippleDrawable;
import android.view.Gravity;
import android.view.View;
import android.view.ViewGroup;
import android.view.WindowInsets;
import android.view.Window;
import android.widget.Button;
import android.widget.ImageButton;
import android.widget.ImageView;
import android.widget.LinearLayout;
import android.widget.TextView;

/** Shared style: see review/mobile_design_system.md in the maintenance hub. */
final class Ui {
    static final int BLUE=Color.rgb(0x41,0x6D,0xC3), INK=Color.rgb(0x21,0x30,0x47), MUTED=Color.rgb(0x67,0x76,0x8C),
        TINT=Color.rgb(0xEA,0xF1,0xFC), BORDER=Color.rgb(0xE4,0xEA,0xF2), BG=Color.rgb(0xF8,0xFA,0xFD), FIELD=Color.rgb(0xF2,0xF5,0xFA),
        RIPPLE=0x22416DC3, DANGER=Color.rgb(190,75,62), OK=Color.rgb(27,127,93),
        BG_LIGHT=Color.rgb(0xF3,0xF2,0xFA), BG_DARK=Color.rgb(0x11,0x17,0x26);

    static int blue(Context c){return Theme.isDark(c)?Color.rgb(0x6F,0x98,0xE6):BLUE;}
    static int ink(Context c){return Theme.isDark(c)?Color.rgb(0xE8,0xED,0xF6):INK;}
    static int muted(Context c){return Theme.isDark(c)?Color.rgb(0x94,0xA3,0xBB):MUTED;}
    static int tint(Context c){return Theme.isDark(c)?Color.rgb(0x1F,0x2E,0x4D):TINT;}
    static int border(Context c){return Theme.isDark(c)?Color.rgb(0x26,0x32,0x48):BORDER;}
    static int bg(Context c){return Theme.isDark(c)?Color.rgb(0x14,0x1B,0x2B):BG;}
    static int field(Context c){return Theme.isDark(c)?Color.rgb(0x1B,0x24,0x38):FIELD;}
    static int surface(Context c){return Theme.isDark(c)?Color.rgb(0x10,0x17,0x26):Color.WHITE;}
    static int danger(Context c){return Theme.isDark(c)?Color.rgb(235,95,80):DANGER;}
    static int ok(Context c){return Theme.isDark(c)?Color.rgb(52,199,142):OK;}

    static int dp(Context c,float value){return Math.round(value*c.getResources().getDisplayMetrics().density);}
    static TextView text(Activity a,String value,int size,int color,boolean bold){TextView t=new TextView(a);t.setText(value);t.setTextSize(size);t.setTextColor(color);if(bold)t.setTypeface(null,Typeface.BOLD);return t;}
    static GradientDrawable background(int color,int radius){GradientDrawable d=new GradientDrawable();d.setColor(color);d.setCornerRadius(radius);return d;}
    /** Ripple feedback over a filled (or transparent when fill==0) rounded shape. */
    static RippleDrawable touch(Activity a,int fill,int radius){
        boolean dark=Theme.isDark(a);
        int ripple=dark?0x226F98E6:RIPPLE;
        int maskColor=dark?surface(a):Color.WHITE;
        return new RippleDrawable(ColorStateList.valueOf(ripple),fill==0?null:background(fill,radius),background(maskColor,radius));
    }
    static void page(View root){
        boolean dark=Theme.isDark(root.getContext());
        int[] colors=dark
            ? new int[]{Color.rgb(0x0C,0x12,0x20),Color.rgb(0x11,0x17,0x26),Color.rgb(0x16,0x15,0x1C)}
            : new int[]{Color.rgb(0xEC,0xF3,0xFF),Color.rgb(0xF3,0xF2,0xFA),Color.rgb(0xFA,0xF6,0xF0)};
        GradientDrawable d=new GradientDrawable(GradientDrawable.Orientation.TL_BR,colors);
        root.setBackground(d);
    }
    static Button button(Activity a,String value){Button b=new Button(a);b.setText(value);b.setAllCaps(false);b.setTextSize(13);b.setTextColor(blue(a));b.setMinHeight(dp(a,48));b.setMinimumHeight(dp(a,48));b.setMinWidth(0);b.setMinimumWidth(0);b.setPadding(dp(a,12),0,dp(a,12),0);b.setStateListAnimator(null);b.setBackground(touch(a,0,dp(a,16)));return b;}
    static Button primary(Activity a,String value){Button b=button(a,value);b.setTextColor(Color.WHITE);b.setBackground(touch(a,blue(a),dp(a,18)));return b;}
    static ImageView icon(Activity a,int kind,int color,int size){ImageView v=new ImageView(a);v.setImageDrawable(new XIcon(kind,color));v.setLayoutParams(new LinearLayout.LayoutParams(dp(a,size),dp(a,size)));return v;}
    /** 48dp square icon button with 12dp padding. */
    static ImageButton iconButton(Activity a,int kind,int color,String description){ImageButton b=new ImageButton(a);b.setImageDrawable(new XIcon(kind,color));b.setScaleType(ImageView.ScaleType.FIT_CENTER);b.setPadding(dp(a,12),dp(a,12),dp(a,12),dp(a,12));b.setBackground(touch(a,0,dp(a,24)));b.setContentDescription(description);b.setLayoutParams(new LinearLayout.LayoutParams(dp(a,48),dp(a,48)));return b;}
    static View divider(Activity a){View v=new View(a);v.setBackgroundColor(border(a));v.setLayoutParams(new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT,Math.max(1,dp(a,1))));return v;}
    static LinearLayout.LayoutParams weight(){return new LinearLayout.LayoutParams(0,ViewGroup.LayoutParams.WRAP_CONTENT,1);}
    /** Standard page header: left title, optional status line below it, right action. */
    static LinearLayout header(Activity a,String title,View action){
        LinearLayout bar=new LinearLayout(a);bar.setGravity(Gravity.CENTER_VERTICAL);bar.setPadding(dp(a,16),0,dp(a,8),0);
        TextView t=text(a,title,18,ink(a),true);t.setSingleLine(true);t.setEllipsize(android.text.TextUtils.TruncateAt.END);bar.addView(t,weight());if(action!=null)bar.addView(action);return bar;
    }
    static AlertDialog.Builder dialog(Activity a){
        int themeResId=Theme.isDark(a)?android.R.style.Theme_Material_Dialog_Alert:android.R.style.Theme_Material_Light_Dialog_Alert;
        return new AlertDialog.Builder(a,themeResId){
            @Override public AlertDialog.Builder setItems(CharSequence[] items,android.content.DialogInterface.OnClickListener listener){
                android.widget.ArrayAdapter<CharSequence> adapter=new android.widget.ArrayAdapter<CharSequence>(a,android.R.layout.simple_list_item_1,items){@Override public View getView(int position,View reuse,ViewGroup parent){TextView t=(TextView)super.getView(position,reuse,parent);t.setTextSize(14);t.setTextColor(ink(a));t.setMinHeight(dp(a,48));return t;}};
                super.setAdapter(adapter,listener);return this;
            }
            @Override public AlertDialog create(){
                AlertDialog dialog=super.create();
                Window window=dialog.getWindow();
                if(window!=null){
                    window.setBackgroundDrawable(new InsetDrawable(background(surface(a),dp(a,24)),dp(a,16)));
                    window.getDecorView().setClipToOutline(true);
                }
                return dialog;
            }
        };
    }
    static void margins(View v,int left,int top,int right,int bottom){LinearLayout.LayoutParams p=new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT,ViewGroup.LayoutParams.WRAP_CONTENT);p.setMargins(left,top,right,bottom);v.setLayoutParams(p);}
    static void insets(View root){root.setOnApplyWindowInsetsListener((v,insets)->{android.graphics.Insets i=insets.getInsets(WindowInsets.Type.systemBars()|WindowInsets.Type.displayCutout()|WindowInsets.Type.ime());v.setPadding(i.left,i.top,i.right,i.bottom);return insets;});root.requestApplyInsets();}
}
