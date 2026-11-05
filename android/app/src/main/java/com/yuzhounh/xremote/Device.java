package com.yuzhounh.xremote;

public record Device(String id, String name, String ip, String user, int port, Protocol protocol, String machineName) {
    public enum Protocol { RDP, VNC }
    public Device(String id, String name, String ip, String user, int port) {
        this(id, name, ip, user, port, Protocol.RDP, "");
    }
    public static final Device[] ALL = {
        new Device("home", "Home computer", "10.126.126.2", "DESKTOP-VTID97L\\Jing Wang", 23389),
        new Device("office", "Office PC - ERYING", "10.126.126.14", "DESKTOP-6M66646\\Jing Wang", 23390),
        new Device("office2", "Office PC - LENOVO", "10.126.126.15", "DESKTOP-4FBRTQT\\Jing Wang", 23394),
        new Device("server120", "Server 120", "10.126.126.12", "WIN-CMDHT2T2DC6\\Administrator", 23391),
        new Device("server140", "Server 140", "10.126.126.11", "WIN-M3NJNGUC8M5\\Administrator", 23392),
        new Device("server141", "Server 141", "10.126.126.13", "WIN-DTPRAA7C9TS\\Administrator", 23393),
        new Device("thinkpad-t530", "ThinkPad T530 - Windows", "10.126.126.17", "DESKTOP-SGTILFQ\\Jing Wang", 23395),
        new Device("thinkpad-x1", "ThinkPad X1 Carbon", "10.126.126.16", "DESKTOP-TJ62QT8\\Jing Wang", 23396),
        new Device("thinkpad-t530-ubuntu", "ThinkPad T530 - Ubuntu", "10.126.126.18", "yuzhounh", 23397, Protocol.RDP, "yuzhounh-ThinkPad-T530"),
        new Device("macbook", "MacBook Pro", "10.126.126.19", "", 23398, Protocol.VNC, "MacBook-Pro")
    };
    /** User-given display names, loaded from SharedPreferences by MainActivity. */
    public static final java.util.Map<String,String> ALIASES = new java.util.HashMap<>();
    @Override public String name() { String alias = ALIASES.get(id); return alias == null || alias.isEmpty() ? name : alias; }
    public String hostName() { if (!machineName.isEmpty()) return machineName; int slash = user.indexOf('\\'); return slash < 0 ? "" : user.substring(0, slash); }
    public int remotePort() { return protocol == Protocol.VNC ? 5900 : 3389; }
    public String connectHost() { return "127.0.0.1"; }
    public int connectPort() { return port; }
    public int controlPort() { return port + 1000; }
    public static String reachabilitySummary(int[] states) {
        int totalOnline = 0;
        StringBuilder parts = new StringBuilder();
        for (Protocol protocol : Protocol.values()) {
            int online = 0, total = 0;
            for (int i = 0; i < ALL.length; i++) if (ALL[i].protocol == protocol) {
                total++;
                if (i < states.length && states[i] == 1) online++;
            }
            totalOnline += online;
            if (total > 0) parts.append(" · ").append(protocol).append(' ').append(online).append('/').append(total);
        }
        return "Devices reachable " + totalOnline + "/" + ALL.length + parts;
    }
    public static Device find(String id) {
        for (Device d : ALL) if (d.id.equals(id)) return d;
        throw new IllegalArgumentException("Unknown device");
    }
}

