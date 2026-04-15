using System.Threading;
using System.Threading.Tasks;

namespace QuanLyNhaHang.Services
{
    public interface IManagerChatService
    {
        Task<ManagerChatServiceResult> GetReplyAsync(string message, CancellationToken cancellationToken = default);
    }
}
