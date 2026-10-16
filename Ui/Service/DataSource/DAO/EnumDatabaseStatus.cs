using System;
using System.Diagnostics;
using _1RM.Service;

namespace _1RM.Service.DataSource.DAO
{
    public enum EnumDatabaseStatus
    {
        NotConnectedYet,
        AccessDenied,
        LostConnection,
        OK,
        EncryptKeyError, // 数据加密密钥不匹配，唯一的原因是软件未使用官方发布版本.
        OtherError,
    }

    public readonly struct DatabaseStatus
    {
        public static DatabaseStatus New(EnumDatabaseStatus status, string extend = "")
        {
            if (status == EnumDatabaseStatus.OtherError)
            {
                Debug.Assert(!string.IsNullOrEmpty(extend));
            }
            var ret = new DatabaseStatus()
            {
                Status = status,
                ExtendInfo = extend
            };
            return ret;
        }
        public EnumDatabaseStatus Status { get; private init; }
        public string ExtendInfo { get; private init; }
        public string GetErrorMessage
        {
            get
            {
                if (Status == EnumDatabaseStatus.OtherError)
                {
                    return UiText.Get("Settings.DatabaseDetails", ExtendInfo);
                }
                return string.IsNullOrEmpty(ExtendInfo) ? Status.GetErrorInfo() : UiText.Get("Settings.DatabaseStatusDetails", Status.GetErrorInfo(), ExtendInfo);
            }
        }
    }


    public static class EnumConnectResultErrorInfo
    {
        public static string GetErrorInfo(this EnumDatabaseStatus result)
        {
            switch (result)
            {
                case EnumDatabaseStatus.AccessDenied:
                    return UiText.Get("Settings.DatabaseAccessDenied");

                case EnumDatabaseStatus.OK:
                    break;

                case EnumDatabaseStatus.NotConnectedYet:
                    return UiText.Get("Settings.DatabaseNotConnected");

                case EnumDatabaseStatus.LostConnection:
                    return UiText.Get("Settings.DatabaseDisconnected");

                case EnumDatabaseStatus.EncryptKeyError:
                    return UiText.Get("Settings.DatabaseEncryptionUnsupported");

                case EnumDatabaseStatus.OtherError:
                    return UiText.Get("Settings.DatabaseOtherError");

                default:
                    throw new ArgumentOutOfRangeException(nameof(result), result, null);
            }

            return "";
        }
    }
}
