using Diacritics.Extensions;
using QuanLyNhaHang.Utils;
using System;

namespace QuanLyNhaHang.Services
{
    public sealed class ManagerChatMockResult
    {
        public ManagerChatMockResult(string replyText, string intentTag)
        {
            ReplyText = replyText;
            IntentTag = intentTag;
        }

        public string ReplyText { get; }
        public string IntentTag { get; }
    }

    public sealed class ManagerChatMockService
    {
        public ManagerChatMockResult GetReply(string question)
        {
            string normalizedQuestion = Normalize(question);

            if (ContainsAny(normalizedQuestion, "doanh thu", "tong thu", "revenue", "bao nhieu tien", "thu hom nay"))
            {
                decimal revenueToday = 12450000m;
                decimal revenueMonth = 286000000m;
                string message =
                    "Dữ liệu mô phỏng doanh thu:\n" +
                    $"- Hôm nay: {MoneyFormatter.FormatVnd(revenueToday)}\n" +
                    $"- Tháng này: {MoneyFormatter.FormatVnd(revenueMonth)}\n" +
                    "Bạn có thể hỏi thêm: \"Doanh thu theo tuần\" hoặc \"Top món bán chạy\".";
                return new ManagerChatMockResult(message, "revenue");
            }

            if (ContainsAny(normalizedQuestion, "top mon", "ban chay", "mon nao ban chay", "mon hot", "top dish"))
            {
                string message =
                    "Top món bán chạy (mô phỏng):\n" +
                    "1. Cơm gà nướng - 128 phần\n" +
                    "2. Bò lúc lắc - 97 phần\n" +
                    "3. Canh chua cá - 84 phần";
                return new ManagerChatMockResult(message, "top_dishes");
            }

            if (ContainsAny(normalizedQuestion, "ton kho", "nguyen lieu", "sap het", "thieu hang", "kho"))
            {
                string message =
                    "Nguyên liệu sắp hết (mô phỏng):\n" +
                    "- Ức gà: 3.5 kg\n" +
                    "- Cà chua: 2.0 kg\n" +
                    "- Nước mắm: 1.0 lít\n" +
                    "Khuyến nghị: nhập kho trong hôm nay để tránh thiếu món.";
                return new ManagerChatMockResult(message, "low_stock");
            }

            if (ContainsAny(normalizedQuestion, "nhan su", "cham cong", "ca lam", "vang", "di lam"))
            {
                string message =
                    "Tình hình nhân sự/chấm công (mô phỏng):\n" +
                    "- Đủ ca sáng: 12/13 nhân viên\n" +
                    "- Đủ ca tối: 14/14 nhân viên\n" +
                    "- Nghỉ phép hôm nay: 1 nhân viên";
                return new ManagerChatMockResult(message, "attendance");
            }

            if (ContainsAny(normalizedQuestion, "ban", "tinh trang ban", "dang dung", "ban trong", "ban nao"))
            {
                string message =
                    "Tình trạng bàn hiện tại (mô phỏng):\n" +
                    "- Bàn đang phục vụ: 9\n" +
                    "- Bàn trống: 6\n" +
                    "- Bàn có hóa đơn chờ thanh toán: 3";
                return new ManagerChatMockResult(message, "table_status");
            }

            return new ManagerChatMockResult(
                "Mình chưa nhận diện đúng ý hỏi. Bạn thử hỏi theo nhóm: doanh thu, top món bán chạy, tồn kho thấp, chấm công, tình trạng bàn.",
                "fallback");
        }

        private static string Normalize(string input)
        {
            return (input ?? string.Empty).RemoveDiacritics().ToLowerInvariant();
        }

        private static bool ContainsAny(string input, params string[] keywords)
        {
            foreach (string keyword in keywords)
            {
                if (input.Contains(keyword, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
