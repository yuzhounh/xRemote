package com.yuzhounh.xremote;

import java.util.Arrays;
import java.util.HashSet;

public final class DeviceInventoryCheck {
    public static void main(String[] args) {
        if (Device.ALL.length != 10) throw new AssertionError("Expected ten devices");
        var ids = new HashSet<String>();
        var ports = new HashSet<Integer>();
        for (Device d : Device.ALL) {
            if (!ids.add(d.id()) || !ports.add(d.port())) throw new AssertionError("Duplicate device or port");
            if (d.hostName().isBlank()) throw new AssertionError("Missing identity: " + d.id());
        }
        for (String id : new String[]{"home","office","office2","server120","server140","server141","thinkpad-t530","thinkpad-x1"}) Device.find(id);
        int[] states = new int[10];
        Arrays.fill(states, 1);
        expect(states, "Devices reachable 10/10 · RDP 9/9 · VNC 1/1");
        Arrays.fill(states, -1);
        for (int i = 0; i < 6; i++) states[i] = 1;
        states[9] = 1;
        expect(states, "Devices reachable 7/10 · RDP 6/9 · VNC 1/1");
        Arrays.fill(states, 2);
        expect(states, "Devices reachable 0/10 · RDP 0/9 · VNC 0/1");
        if (Device.find("macbook").remotePort()!=5900 || Device.find("thinkpad-t530-ubuntu").remotePort()!=3389)
            throw new AssertionError("Incorrect protocol port");
        System.out.println("PASS ten devices, stable identities, protocol ports, and dynamic counts");
    }
    private static void expect(int[] states, String expected) {
        if (!Device.reachabilitySummary(states).equals(expected)) throw new AssertionError(Device.reachabilitySummary(states));
    }
}
