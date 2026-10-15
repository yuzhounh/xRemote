namespace _1RM
{
    internal static class Assert
    {
        // Preserve this storage identity for existing profiles, databases and credential keys.
        private const string APP_NAME_RAW = "1Remote";
        private const string PRODUCT_NAME = "xRemote";
#if DEBUG
        public const string APP_NAME = $"{APP_NAME_RAW}_Debug";
#if FOR_MICROSOFT_STORE_ONLY
        public const string APP_DISPLAY_NAME = $"{PRODUCT_NAME}(Store)_Debug";
#else
        public const string APP_DISPLAY_NAME = $"{PRODUCT_NAME}_Debug";
#endif
#else
        public const string APP_NAME = $"{APP_NAME_RAW}";
#if FOR_MICROSOFT_STORE_ONLY
        public const string APP_DISPLAY_NAME = $"{PRODUCT_NAME}(Store)";
#else
        public const string APP_DISPLAY_NAME = PRODUCT_NAME;
#endif
#endif


        public const string MS_APP_CENTER_SECRET = "===REPLACE_ME_WITH_APP_CENTER_SECRET===";
        public const string SENTRY_IO_DEN = "===REPLACE_ME_WITH_SENTRY_IO_DEN===";
        public const string STRING_SALT = "===REPLACE_ME_WITH_SALT===";
        public const string MS_STORE_PATH = "ms-windows-store://review/?productid=9PNMNF92JNFP";
    }
}
