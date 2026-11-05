package com.yuzhounh.xremote;

import android.os.Handler;
import android.os.Looper;
import org.json.JSONObject;
import java.io.InputStream;
import java.net.URL;
import java.nio.charset.StandardCharsets;
import java.security.MessageDigest;
import java.security.SecureRandom;
import java.security.cert.X509Certificate;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import javax.net.ssl.HttpsURLConnection;
import javax.net.ssl.SSLContext;
import javax.net.ssl.TrustManager;
import javax.net.ssl.X509TrustManager;

final class ControlClient implements AutoCloseable {
    interface Reply { void done(JSONObject data, String error); }
    private final SecureStore store;
    private final ExecutorService io=Executors.newFixedThreadPool(2);
    private final Handler main=new Handler(Looper.getMainLooper());
    private volatile boolean closed;
    ControlClient(SecureStore store){this.store=store;}
    void importPairing(Device d,InputStream in)throws Exception{
        byte[] bytes=in.readNBytes(16385);if(bytes.length>16384)throw new IllegalArgumentException();
        JSONObject p=new JSONObject(new String(bytes,StandardCharsets.UTF_8));
        validatePairing(d,p);store.put("control."+d.id(),p.toString());
    }
    private void validatePairing(Device d,JSONObject p)throws Exception{
        if(p.optInt("schema")!=1||!d.ip().equals(p.optString("address"))||p.optInt("port")!=24443
            ||!p.optString("token").matches("[a-fA-F0-9]{64}")||!p.optString("certificateSha256").matches("[a-fA-F0-9]{64}"))
            throw new IllegalArgumentException(L10n.text(R.string.ui_pairing_file_does_not_match_the_device));
    }
    void importPairings(InputStream in)throws Exception{
        byte[] bytes=in.readNBytes(16385);if(bytes.length>16384)throw new IllegalArgumentException();
        JSONObject bundle=new JSONObject(new String(bytes,StandardCharsets.UTF_8));if(bundle.optInt("schema")!=1)throw new IllegalArgumentException();
        org.json.JSONArray rows=bundle.getJSONArray("devices");if(rows.length()==0||rows.length()>Device.ALL.length)throw new IllegalArgumentException();
        java.util.LinkedHashMap<String,JSONObject> values=new java.util.LinkedHashMap<>();
        for(int i=0;i<rows.length();i++){JSONObject p=rows.getJSONObject(i);Device match=null;for(Device d:Device.ALL)if(d.ip().equals(p.optString("address")))match=d;if(match==null||values.containsKey(match.id()))throw new IllegalArgumentException();validatePairing(match,p);values.put(match.id(),p);}
        for(var row:values.entrySet())store.put("control."+row.getKey(),row.getValue().toString());
    }
    void status(Device d,Reply reply){request(d,"GET","/v1/status",null,reply);}
    void hardware(Device d,boolean refresh,Reply reply){request(d,"GET","/v1/hardware?refresh="+refresh,null,reply);}
    void storage(Device d,Reply reply){request(d,"GET","/v1/storage?refresh=true",null,reply);}
    void disconnect(Device d,JSONObject row,Reply reply){
        try{request(d,"POST","/v1/disconnect",new JSONObject().put("sessionId",row.getJSONObject("session").getInt("id")).put("ticket",row.getString("ticket")),reply);}
        catch(Exception e){reply.done(null,L10n.text(R.string.ui_session_state_changed_please_refresh));}
    }
    private void request(Device d,String verb,String path,JSONObject body,Reply reply){
        if(closed)return;
        io.execute(()->{
            JSONObject result=null;String error=null;
            HttpsURLConnection c=null;
            try{
                String saved=store.get("control."+d.id());if(saved==null)throw new IllegalStateException(L10n.text(R.string.ui_the_management_pairing_file_for_this_device_has_not_bee));
                JSONObject p=new JSONObject(saved);String hex=p.getString("certificateSha256");byte[] pin=new byte[32];
                for(int i=0;i<32;i++)pin[i]=(byte)Integer.parseInt(hex.substring(i*2,i*2+2),16);
                SSLContext ssl=SSLContext.getInstance("TLS");
                ssl.init(null,new TrustManager[]{new X509TrustManager(){
                    public X509Certificate[] getAcceptedIssuers(){return new X509Certificate[0];}
                    public void checkClientTrusted(X509Certificate[] chain,String auth)throws java.security.cert.CertificateException{throw new java.security.cert.CertificateException();}
                    public void checkServerTrusted(X509Certificate[] chain,String auth)throws java.security.cert.CertificateException{
                        try{if(chain.length==0)throw new java.security.cert.CertificateException();chain[0].checkValidity();
                            if(!MessageDigest.isEqual(pin,MessageDigest.getInstance("SHA-256").digest(chain[0].getEncoded())))throw new java.security.cert.CertificateException();
                        }catch(java.security.cert.CertificateException e){throw e;}catch(Exception e){throw new java.security.cert.CertificateException(e);}
                    }
                }},new SecureRandom());
                c=(HttpsURLConnection)new URL("https://"+d.connectHost()+":"+d.controlPort()+path).openConnection();
                c.setSSLSocketFactory(ssl.getSocketFactory());c.setHostnameVerifier((host,session)->host.equals(d.connectHost()));
                c.setInstanceFollowRedirects(false);c.setConnectTimeout(3500);c.setReadTimeout(path.startsWith("/v1/hardware")||path.startsWith("/v1/storage")?22000:3500);c.setRequestMethod(verb);
                c.setRequestProperty("Authorization","Bearer "+p.getString("token"));c.setRequestProperty("Cache-Control","no-store");
                if(body!=null){c.setDoOutput(true);c.setRequestProperty("Content-Type","application/json");try(var out=c.getOutputStream()){out.write(body.toString().getBytes(StandardCharsets.UTF_8));}}
                int code=c.getResponseCode();if(code!=200)throw new IllegalStateException(path.startsWith("/v1/storage")?(code==404?L10n.text(R.string.ui_the_device_management_service_must_be_updated_to_refres):L10n.text(R.string.ui_storage_information_unavailable_check_the_service_and_p)):path.startsWith("/v1/hardware")?(code==404?L10n.text(R.string.ui_the_device_management_service_must_be_updated_to_read_h):L10n.text(R.string.ui_hardware_information_unavailable_check_the_service_and)):code==409?L10n.text(R.string.ui_the_session_changed_please_refresh_and_retry):code==423?L10n.text(R.string.ui_management_service_is_in_read_only_verification_mode):L10n.text(R.string.ui_management_service_unavailable_or_pairing_failed));
                try(InputStream in=c.getInputStream()){byte[] bytes=in.readNBytes(65537);if(bytes.length>65536)throw new IllegalStateException();result=new JSONObject(new String(bytes,StandardCharsets.UTF_8));}
            }catch(Exception e){error=e instanceof IllegalStateException&&e.getMessage()!=null?e.getMessage():path.startsWith("/v1/storage")?L10n.text(R.string.ui_storage_information_unavailable_check_the_network_servi):path.startsWith("/v1/hardware")?L10n.text(R.string.ui_hardware_information_unavailable_check_the_network_serv):L10n.text(R.string.ui_usage_state_unknown_check_the_network_service_and_pairi);}
            finally{if(c!=null)c.disconnect();}
            JSONObject data=result;String failure=error;main.post(()->{if(!closed)reply.done(data,failure);});
        });
    }
    public void close(){closed=true;io.shutdownNow();main.removeCallbacksAndMessages(null);}
}
