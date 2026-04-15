using System;
using System.Configuration;
using System.Globalization;

namespace QuanLyNhaHang.Services
{
    public sealed class ManagerChatApiOptions
    {
        private const string DefaultBaseUrl = "http://localhost:8080";
        private const string DefaultPath = "/api/manager/chat";
        private const int DefaultTimeoutSeconds = 30;

        public ManagerChatApiOptions(string baseUrl, string path, int timeoutSeconds)
        {
            BaseUrl = string.IsNullOrWhiteSpace(baseUrl) ? DefaultBaseUrl : baseUrl.Trim();
            Path = NormalizePath(path);
            TimeoutSeconds = timeoutSeconds > 0 ? timeoutSeconds : DefaultTimeoutSeconds;
        }

        public string BaseUrl { get; }
        public string Path { get; }
        public int TimeoutSeconds { get; }

        public static ManagerChatApiOptions FromConfig()
        {
            string baseUrl = ConfigurationManager.AppSettings["ManagerChatApiBaseUrl"] ?? DefaultBaseUrl;
            string path = ConfigurationManager.AppSettings["ManagerChatApiPath"] ?? DefaultPath;

            int timeoutSeconds = DefaultTimeoutSeconds;
            string timeoutRaw = ConfigurationManager.AppSettings["ManagerChatApiTimeoutSeconds"] ?? string.Empty;
            if (int.TryParse(timeoutRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedTimeout) &&
                parsedTimeout > 0)
            {
                timeoutSeconds = parsedTimeout;
            }

            return new ManagerChatApiOptions(baseUrl, path, timeoutSeconds);
        }

        public Uri BuildEndpointUri()
        {
            if (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out Uri? baseUri))
            {
                baseUri = new Uri(DefaultBaseUrl, UriKind.Absolute);
            }

            return new Uri(baseUri, Path);
        }

        private static string NormalizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return DefaultPath;
            }

            string normalized = path.Trim();
            if (!normalized.StartsWith("/", StringComparison.Ordinal))
            {
                normalized = "/" + normalized;
            }

            return normalized;
        }
    }
}
