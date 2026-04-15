namespace QuanLyNhaHang.Services
{
    public sealed class ManagerChatServiceResult
    {
        private ManagerChatServiceResult(bool isSuccess, string replyText, string intentTag, string errorCode)
        {
            IsSuccess = isSuccess;
            ReplyText = replyText;
            IntentTag = intentTag;
            ErrorCode = errorCode;
        }

        public bool IsSuccess { get; }
        public string ReplyText { get; }
        public string IntentTag { get; }
        public string ErrorCode { get; }

        public static ManagerChatServiceResult Success(string replyText, string intentTag = "assistant")
        {
            return new ManagerChatServiceResult(true, replyText ?? string.Empty, intentTag, string.Empty);
        }

        public static ManagerChatServiceResult Failure(string message, string errorCode = "ERROR")
        {
            return new ManagerChatServiceResult(false, message ?? string.Empty, "error", errorCode ?? "ERROR");
        }
    }
}
