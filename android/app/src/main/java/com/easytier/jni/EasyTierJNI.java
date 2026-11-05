package com.easytier.jni;

/** Java ABI for the pinned EasyTier 2.6.4 Android JNI binary. */
public final class EasyTierJNI {
    static { System.loadLibrary("easytier_android_jni"); }
    private EasyTierJNI() {}
    public static native int parseConfig(String config);
    public static native int runNetworkInstance(String config);
    public static native int retainNetworkInstance(String[] instanceNames);
    public static native String collectNetworkInfos(int maxLength);
    public static native String getLastError();
}

