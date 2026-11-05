package com.yuzhounh.xremote;

import android.graphics.Canvas;
import android.graphics.ColorFilter;
import android.graphics.Paint;
import android.graphics.Path;
import android.graphics.PixelFormat;
import android.graphics.drawable.Drawable;

/** Line icons on a 24x24 grid, 1.8 stroke, round caps and joins, never filled. */
final class XIcon extends Drawable {
    static final int MENU=0, CLOSE=1, BACK=2, SEARCH=3, CHECK=4, GEAR=5, DEVICES=6, ONLINE=7, OFFLINE=8, CHEVRON=9, PLUS=10,
        KEY=11, REFRESH=12, IMAGE=13, LINK=14, CLOCK=15, INFO=16, POWER=17, USERS=18, IMPORT=19, LANGUAGE=20, SUN=21, MOON=22;
    private final int kind;
    private final Paint paint=new Paint(Paint.ANTI_ALIAS_FLAG);

    XIcon(int kind,int color){
        this.kind=kind;paint.setStyle(Paint.Style.STROKE);paint.setStrokeWidth(1.8f);paint.setStrokeCap(Paint.Cap.ROUND);paint.setStrokeJoin(Paint.Join.ROUND);paint.setColor(color);
    }
    private static void line(Path p,float x1,float y1,float x2,float y2){p.moveTo(x1,y1);p.lineTo(x2,y2);}
    private static void rect(Path p,float l,float t,float r,float b,float radius){p.addRoundRect(l,t,r,b,radius,radius,Path.Direction.CW);}

    @Override public void draw(Canvas canvas){
        android.graphics.Rect b=getBounds();if(b.isEmpty())return;
        canvas.save();canvas.translate(b.left,b.top);canvas.scale(b.width()/24f,b.height()/24f);
        Path p=new Path();
        switch(kind){
            case LANGUAGE:p.addCircle(12,12,9,Path.Direction.CW);p.addOval(8,3,16,21,Path.Direction.CW);line(p,3,12,21,12);break;
            case MENU:line(p,4,7,20,7);line(p,4,12,20,12);line(p,4,17,20,17);break;
            case CLOSE:line(p,6,6,18,18);line(p,18,6,6,18);break;
            case BACK:line(p,19,12,5,12);p.moveTo(11,6);p.lineTo(5,12);p.lineTo(11,18);break;
            case SEARCH:p.addCircle(10.5f,10.5f,6.5f,Path.Direction.CW);line(p,15.5f,15.5f,20,20);break;
            case CHECK:p.moveTo(5,12.5f);p.lineTo(10,17.5f);p.lineTo(19,7);break;
            case GEAR:{
                p.addCircle(12,12,3,Path.Direction.CW);
                for(int i=0;i<8;i++){double a=Math.PI/4*i;float c=(float)Math.cos(a),s=(float)Math.sin(a);line(p,12+c*6.6f,12+s*6.6f,12+c*8.4f,12+s*8.4f);}
                p.addCircle(12,12,5.4f,Path.Direction.CW);break;
            }
            case DEVICES:rect(p,3,4.5f,21,16,2);line(p,9,20,15,20);line(p,12,16,12,20);break;
            case ONLINE:rect(p,3,4.5f,21,16,2);line(p,9,20,15,20);line(p,12,16,12,20);p.moveTo(8.5f,10.5f);p.lineTo(11,13);p.lineTo(15.5f,8);break;
            case OFFLINE:rect(p,3,4.5f,21,16,2);line(p,9,20,15,20);line(p,12,16,12,20);line(p,9,7.5f,15,13.5f);line(p,15,7.5f,9,13.5f);break;
            case CHEVRON:p.moveTo(9,5);p.lineTo(16,12);p.lineTo(9,19);break;
            case PLUS:line(p,12,5,12,19);line(p,5,12,19,12);break;
            case KEY:p.addCircle(8,15,4,Path.Direction.CW);line(p,11,12,20,3.5f);line(p,17,6.5f,19.5f,9);line(p,14.5f,9,16.5f,11);break;
            case REFRESH:p.addArc(4,4,20,20,-40,280);p.moveTo(19,3.5f);p.lineTo(19,8.5f);p.lineTo(14,8.5f);break;
            case IMAGE:rect(p,3.5f,4.5f,20.5f,19.5f,2.5f);p.addCircle(9,10,1.6f,Path.Direction.CW);p.moveTo(4,17);p.lineTo(9.5f,12.5f);p.lineTo(13,15.5f);p.lineTo(15.5f,13.5f);p.lineTo(20,17.5f);break;
            case LINK:p.addRoundRect(3,8.5f,12,15.5f,3.5f,3.5f,Path.Direction.CW);p.addRoundRect(12,8.5f,21,15.5f,3.5f,3.5f,Path.Direction.CW);line(p,9,12,15,12);break;
            case CLOCK:p.addCircle(12,12,8.5f,Path.Direction.CW);p.moveTo(12,7);p.lineTo(12,12);p.lineTo(15.5f,14);break;
            case INFO:p.addCircle(12,12,8.5f,Path.Direction.CW);line(p,12,11,12,16.5f);line(p,12,7.6f,12,7.7f);break;
            case POWER:p.addArc(5,5.5f,19,19.5f,-60,300);line(p,12,3.5f,12,12);break;
            case USERS:p.addCircle(12,8,3.5f,Path.Direction.CW);p.moveTo(5,20);p.cubicTo(5,15.5f,8,14,12,14);p.cubicTo(16,14,19,15.5f,19,20);break;
            case IMPORT:line(p,12,4,12,15);p.moveTo(7.5f,10.5f);p.lineTo(12,15);p.lineTo(16.5f,10.5f);p.moveTo(4.5f,16);p.lineTo(4.5f,19.5f);p.lineTo(19.5f,19.5f);p.lineTo(19.5f,16);break;
            case SUN:
                p.addCircle(12,12,4.5f,Path.Direction.CW);
                line(p,12,2,12,4.5f);line(p,12,19.5f,12,22);line(p,2,12,4.5f,12);line(p,19.5f,12,22,12);
                line(p,4.93f,4.93f,6.7f,6.7f);line(p,17.3f,17.3f,19.07f,19.07f);line(p,4.93f,19.07f,6.7f,17.3f);line(p,17.3f,6.7f,19.07f,4.93f);
                break;
            case MOON:
                p.moveTo(21,12.5f);
                p.cubicTo(20.5f,17,16.5f,20.5f,12,20.5f);
                p.cubicTo(7.3f,20.5f,3.5f,16.7f,3.5f,12);
                p.cubicTo(3.5f,7.5f,7,3.5f,11.5f,3);
                p.cubicTo(10.5f,5.5f,11,8.5f,13,10.5f);
                p.cubicTo(15,12.5f,18,13,21,12.5f);
                break;
        }
        canvas.drawPath(p,paint);canvas.restore();
    }
    @Override public void setAlpha(int alpha){paint.setAlpha(alpha);}
    @Override public void setColorFilter(ColorFilter filter){paint.setColorFilter(filter);}
    @Override public int getOpacity(){return PixelFormat.TRANSLUCENT;}
}
