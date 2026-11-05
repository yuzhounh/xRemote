package com.yuzhounh.xremote;

import android.content.Context;
import android.content.SharedPreferences;
import android.content.res.Configuration;
import java.util.Locale;

/** App language applies to interface resources without restarting activities or services. */
final class L10n {
    private static final Locale CHINESE = Locale.SIMPLIFIED_CHINESE;
    private static Context app;
    private static SharedPreferences settings;
    private static volatile Context localized;

    private L10n() {}

    static void initialize(Context context) {
        app = context.getApplicationContext();
        settings = app.getSharedPreferences("interface_language", Context.MODE_PRIVATE);
        apply(settings.getBoolean("english", false));
    }

    static boolean isEnglish() {
        return Locale.ENGLISH.getLanguage().equals(localized.getResources().getConfiguration().getLocales().get(0).getLanguage());
    }

    static String text(int id) {
        return localized.getString(id);
    }

    static String languageName() {
        return isEnglish() ? "English" : "简体中文";
    }

    static void toggle() {
        boolean english = !isEnglish();
        settings.edit().putBoolean("english", english).apply();
        apply(english);
    }

    private static void apply(boolean english) {
        Configuration config = new Configuration(app.getResources().getConfiguration());
        config.setLocale(english ? Locale.ENGLISH : CHINESE);
        localized = app.createConfigurationContext(config);
    }

}
