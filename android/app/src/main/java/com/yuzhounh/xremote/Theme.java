package com.yuzhounh.xremote;

import android.app.Activity;
import android.content.Context;
import android.content.SharedPreferences;
import android.content.res.Configuration;
import android.os.Build;
import android.view.View;
import android.view.Window;
import android.view.WindowInsetsController;

final class Theme {
    private static SharedPreferences settings;
    private static String mode = "system";

    private Theme() {}

    static void initialize(Context context) {
        if (context == null) return;
        try {
            settings = context.getApplicationContext().getSharedPreferences("app_theme", Context.MODE_PRIVATE);
            mode = settings.getString("theme_mode", "system");
        } catch (Throwable ignored) {}
    }

    static String getMode() {
        return mode;
    }

    static boolean isDark(Context context) {
        if ("dark".equals(mode)) return true;
        if ("light".equals(mode)) return false;
        if (context == null) return false;
        try {
            int nightMode = context.getResources().getConfiguration().uiMode & Configuration.UI_MODE_NIGHT_MASK;
            return nightMode == Configuration.UI_MODE_NIGHT_YES;
        } catch (Throwable ignored) {
            return false;
        }
    }

    static String themeName(Context context) {
        try {
            return isDark(context) ? L10n.text(R.string.ui_theme_dark) : L10n.text(R.string.ui_theme_light);
        } catch (Throwable ignored) {
            return isDark(context) ? "深色" : "浅色";
        }
    }

    static void toggle(Context context) {
        boolean nextDark = !isDark(context);
        setMode(nextDark ? "dark" : "light");
    }

    static void setMode(String newMode) {
        mode = newMode;
        if (settings != null) {
            try {
                settings.edit().putString("theme_mode", mode).apply();
            } catch (Throwable ignored) {}
        }
    }

    static void applySystemBars(Activity activity) {
        if (activity == null || activity.isFinishing()) return;
        try {
            Window window = activity.getWindow();
            if (window == null) return;
            boolean dark = isDark(activity);
            int barColor = dark ? Ui.BG_DARK : Ui.BG_LIGHT;
            window.setStatusBarColor(barColor);
            window.setNavigationBarColor(barColor);

            View decor = window.getDecorView();
            if (decor == null) return;

            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.R) {
                WindowInsetsController controller = decor.getWindowInsetsController();
                if (controller == null) {
                    controller = window.getInsetsController();
                }
                if (controller != null) {
                    int appearance = WindowInsetsController.APPEARANCE_LIGHT_STATUS_BARS | WindowInsetsController.APPEARANCE_LIGHT_NAVIGATION_BARS;
                    if (dark) {
                        controller.setSystemBarsAppearance(0, appearance);
                    } else {
                        controller.setSystemBarsAppearance(appearance, appearance);
                    }
                }
            } else {
                int flags = decor.getSystemUiVisibility();
                if (dark) {
                    flags &= ~View.SYSTEM_UI_FLAG_LIGHT_STATUS_BAR;
                    if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
                        flags &= ~View.SYSTEM_UI_FLAG_LIGHT_NAVIGATION_BAR;
                    }
                } else {
                    flags |= View.SYSTEM_UI_FLAG_LIGHT_STATUS_BAR;
                    if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
                        flags |= View.SYSTEM_UI_FLAG_LIGHT_NAVIGATION_BAR;
                    }
                }
                decor.setSystemUiVisibility(flags);
            }
        } catch (Throwable ignored) {}
    }
}
