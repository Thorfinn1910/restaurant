using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace QuanLyNhaHang.Services
{
    public sealed class ManagerChatApiService : IManagerChatService
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly HttpClient _httpClient;
        private readonly Uri _endpointUri;

        public ManagerChatApiService()
            : this(ManagerChatApiOptions.FromConfig(), new HttpClient())
        {
        }

        internal ManagerChatApiService(ManagerChatApiOptions options, HttpClient httpClient)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _endpointUri = (options ?? throw new ArgumentNullException(nameof(options))).BuildEndpointUri();
            _httpClient.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        }

        public async Task<ManagerChatServiceResult> GetReplyAsync(string message, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return ManagerChatServiceResult.Failure("Vui long nhap cau hoi.", "EMPTY_REQUEST");
            }

            ManagerChatRequestDto payload = new ManagerChatRequestDto
            {
                Message = message.Trim()
            };

            try
            {
                using HttpResponseMessage response = await _httpClient.PostAsJsonAsync(
                    _endpointUri,
                    payload,
                    JsonOptions,
                    cancellationToken);

                string content = await response.Content.ReadAsStringAsync();
                ManagerChatResponseDto? apiResponse = DeserializeSafely(content);

                if (!response.IsSuccessStatusCode)
                {
                    string statusMessage = apiResponse?.ErrorMessage;
                    if (string.IsNullOrWhiteSpace(statusMessage))
                    {
                        statusMessage = $"Khong the ket noi chatbot (HTTP {(int)response.StatusCode}).";
                    }

                    string statusCode = string.IsNullOrWhiteSpace(apiResponse?.ErrorCode)
                        ? $"HTTP_{(int)response.StatusCode}"
                        : apiResponse.ErrorCode!;

                    return ManagerChatServiceResult.Failure(statusMessage, statusCode);
                }

                if (apiResponse == null)
                {
                    return ManagerChatServiceResult.Failure(
                        "Khong nhan duoc phan hoi hop le tu may chu chatbot.",
                        "INVALID_RESPONSE");
                }

                if (apiResponse.Success)
                {
                    string answer = apiResponse.Answer?.Trim() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(answer))
                    {
                        return ManagerChatServiceResult.Failure("Khong nhan duoc cau tra loi tu AI.", "EMPTY_ANSWER");
                    }

                    return ManagerChatServiceResult.Success(answer, "assistant");
                }

                string errorMessage = string.IsNullOrWhiteSpace(apiResponse.ErrorMessage)
                    ? "Chatbot hien khong the xu ly cau hoi nay. Vui long thu lai."
                    : apiResponse.ErrorMessage!;
                string errorCode = string.IsNullOrWhiteSpace(apiResponse.ErrorCode) ? "API_ERROR" : apiResponse.ErrorCode!;

                return ManagerChatServiceResult.Failure(errorMessage, errorCode);
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return ManagerChatServiceResult.Failure(
                    "Khong nhan duoc phan hoi tu chatbot (timeout). Vui long thu lai.",
                    "TIMEOUT");
            }
            catch (HttpRequestException)
            {
                return ManagerChatServiceResult.Failure(
                    "Khong ket noi duoc den chatbot. Vui long kiem tra backend.",
                    "NETWORK_ERROR");
            }
            catch (Exception)
            {
                return ManagerChatServiceResult.Failure(
                    "Da xay ra loi khi goi chatbot. Vui long thu lai.",
                    "CLIENT_ERROR");
            }
        }

        private static ManagerChatResponseDto? DeserializeSafely(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return null;
            }

            try
            {
                return JsonSerializer.Deserialize<ManagerChatResponseDto>(content, JsonOptions);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private sealed class ManagerChatRequestDto
        {
            public string Message { get; set; } = string.Empty;
        }

        private sealed class ManagerChatResponseDto
        {
            public bool Success { get; set; }
            public string? Answer { get; set; }
            public string? ErrorCode { get; set; }
            public string? ErrorMessage { get; set; }
        }
    }
}
