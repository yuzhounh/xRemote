package com.yuzhounh.xremote;

import android.os.Handler;
import android.os.Looper;
import android.util.Log;
import com.easytier.jni.EasyTierJNI;
import org.json.JSONArray;
import org.json.JSONObject;
import java.io.InputStream;
import java.net.InetSocketAddress;
import java.net.Socket;
import java.net.URI;
import java.nio.charset.StandardCharsets;
import java.util.Arrays;
import java.util.concurrent.Executors;
import java.util.concurrent.ScheduledExecutorService;
import java.util.concurrent.TimeUnit;

final class TunnelController {
    private static final Object NATIVE_LOCK = new Object();
    interface Listener { void changed(String message, int[] states); }
    private final SecureStore store;
    private final Handler main = new Handler(Looper.getMainLooper());
    private final ScheduledExecutorService io = Executors.newScheduledThreadPool(2);
    private final int[] states = new int[Device.ALL.length]; // 0 waiting; 1 reachable; -1 not reachable; 2 checking
    private Listener listener;
    private boolean running, starting;
    private volatile boolean stopped;
    private java.util.function.Supplier<String> message = () -> L10n.text(R.string.ui_connecting_starts_after_importing_the_network_configura);
    TunnelController(SecureStore store) { this.store = store; }
    boolean isClosed() { return stopped; }
    synchronized boolean isActive() { return running || starting; }
    synchronized void listen(Listener value) { listener = value; publish(); }
    private synchronized void publish() {
        Listener l = listener; String text = message.get(); int[] copy = states.clone();
        if (l != null) main.post(() -> { synchronized (TunnelController.this) { if (listener != l || stopped) return; } l.changed(text, copy); });
    }
    synchronized boolean isConfigured() { try { return store.get("network") != null; } catch (Exception e) { return false; } }
    void importConfig(InputStream in) throws Exception {
        byte[] data = in.readNBytes(65537); if (data.length > 65536) throw new IllegalArgumentException(L10n.text(R.string.ui_configuration_file_too_large));
        JSONObject cfg = new JSONObject(new String(data, StandardCharsets.UTF_8));
        if (cfg.optInt("schema", 0) != 1 || cfg.optString("networkName").isBlank() || cfg.optString("networkSecret").isBlank())
            throw new IllegalArgumentException(L10n.text(R.string.ui_select_xremote_network_configuration_file));
        JSONArray peers = cfg.getJSONArray("peers");
        if (peers.length() == 0 || peers.length() > 16) throw new IllegalArgumentException(L10n.text(R.string.ui_invalid_network_node_count));
        for (int i = 0; i < peers.length(); i++) {
            URI uri = new URI(peers.getString(i));
            if (!Arrays.asList("tcp", "udp", "quic", "ws", "wss").contains(uri.getScheme()) || uri.getHost() == null)
                throw new IllegalArgumentException(L10n.text(R.string.ui_invalid_network_node_address));
        }
        store.put("network", cfg.toString());
    }
    private String config() throws Exception {
        JSONObject c = new JSONObject(store.get("network"));
        StringBuilder t = new StringBuilder("instance_name = \"xremote-foreground\"\n")
            .append("instance_id = \"acfc7306-b448-478d-9dc5-cec55d823a34\"\n")
            .append("hostname = \"xremote-android\"\nipv4 = \"10.126.126.4/24\"\ndhcp = false\nlisteners = []\n")
            .append("[network_identity]\nnetwork_name = ").append(tomlString(c.getString("networkName")))
            .append("\nnetwork_secret = ").append(tomlString(c.getString("networkSecret"))).append('\n');
        JSONArray peers = c.getJSONArray("peers");
        for (int i = 0; i < peers.length(); i++) t.append("[[peer]]\nuri = ").append(tomlString(peers.getString(i))).append('\n');
        for (Device d : Device.ALL) t.append("[[port_forward]]\nbind_addr = \"127.0.0.1:").append(d.port())
            .append("\"\ndst_addr = \"").append(d.ip()).append(':').append(d.remotePort()).append("\"\nproto = \"tcp\"\n");
        for (Device d : Device.ALL) t.append("[[port_forward]]\nbind_addr = \"127.0.0.1:").append(d.port()+1000)
            .append("\"\ndst_addr = \"").append(d.ip()).append(":24443\"\nproto = \"tcp\"\n");
        return t.append("[flags]\nno_tun = true\nmulti_thread = false\n").toString();
    }
    private static String tomlString(String value) {
        // Android JSONStringer escapes every slash; TOML basic strings do not allow the \/ escape.
        return JSONObject.quote(value).replace("\\/", "/");
    }
    synchronized void start() {
        if (stopped || running || starting) return;
        if (!isConfigured()) { message = () -> L10n.text(R.string.ui_import_the_existing_network_configuration_first); publish(); return; }
        starting = true; message = () -> L10n.text(R.string.ui_connecting_to_the_private_network); publish();
        io.execute(() -> {
            int phase = R.string.ui_reading_configuration;
            try {
                String cfg = config();
                if (stopped) return;
                synchronized (NATIVE_LOCK) {
                    if (stopped) return;
                    phase = R.string.ui_loading_kernel;
                    if (EasyTierJNI.retainNetworkInstance(null) != 0) throw new IllegalStateException("Failed to clean up the old instance");
                    phase = R.string.ui_parsing_configuration;
                    if (EasyTierJNI.parseConfig(cfg) != 0) throw new IllegalStateException("Configuration parsing failed");
                    phase = R.string.ui_starting_network;
                    if (EasyTierJNI.runNetworkInstance(cfg) != 0) throw new IllegalStateException("Kernel failed to start");
                }
                synchronized (this) { running = true; starting = false; Arrays.fill(states, 2); message = () -> L10n.text(R.string.ui_checking_2) + Device.ALL.length + L10n.text(R.string.ui_devices_2); publish(); }
                for (int delay : new int[]{1, 6, 12}) io.schedule(this::check, delay, TimeUnit.SECONDS);
            } catch (Throwable e) {
                // Native parse errors can contain the complete TOML; never log it without redacting its identity.
                String detail = "Details unavailable";
                try {
                    JSONObject c = new JSONObject(store.get("network"));
                    detail = String.valueOf(e.getMessage());
                    for (String field : new String[]{"networkSecret", "networkName"}) {
                        String value = c.getString(field);
                        detail = detail.replace(value, "[REDACTED]").replace(JSONObject.quote(value), "[REDACTED]");
                    }
                } catch (Exception ignored) { }
                Log.e("XRemoteTunnel", L10n.text(phase) + "/" + e.getClass().getSimpleName() + ": " + detail);
                int failedPhase=phase;
                String reason=e.getClass().getSimpleName();
                synchronized (this) { starting = false; Arrays.fill(states, 0); message = () -> L10n.text(failedPhase) + L10n.text(R.string.ui_failed) + reason + L10n.text(R.string.ui_tap_refresh_to_retry); publish(); }
            }
        });
    }
    void refresh() { synchronized (this) { if (!running) { start(); return; } } io.execute(this::check); }
    private void check() {
        if (stopped) return;
        for (int i = 0; i < Device.ALL.length && !stopped; i++) {
            boolean ok = probe(Device.ALL[i]);
            synchronized (this) { states[i] = ok ? 1 : -1; message = () -> Device.reachabilitySummary(states); publish(); }
        }
        synchronized (this) {
            if (stopped) return;
            message = () -> Device.reachabilitySummary(states); publish();
        }
    }
    static boolean probe(Device device) {
        try (Socket s = new Socket()) {
            s.connect(new InetSocketAddress(device.connectHost(), device.connectPort()), 1400); s.setSoTimeout(1800);
            if (device.protocol() == Device.Protocol.VNC) {
                String banner = new String(s.getInputStream().readNBytes(12), StandardCharsets.US_ASCII);
                return banner.matches("RFB [0-9]{3}\\.[0-9]{3}\\n");
            }
            s.getOutputStream().write(new byte[]{3,0,0,19,14,(byte)224,0,0,0,0,0,1,0,8,0,3,0,0,0});
            byte[] h = s.getInputStream().readNBytes(4);
            return h.length == 4 && h[0] == 3 && h[1] == 0;
        } catch (Exception e) { return false; }
    }
    void close() {
        boolean hadInstance;
        synchronized (this) { hadInstance = running || starting; }
        stopped = true; synchronized (this) { listener = null; }
        io.shutdownNow();
        if (hadInstance) synchronized (NATIVE_LOCK) {
            try { EasyTierJNI.retainNetworkInstance(null); } catch (Throwable ignored) { }
        }
    }
}

