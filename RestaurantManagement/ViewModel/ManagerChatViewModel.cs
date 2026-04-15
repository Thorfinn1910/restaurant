using QuanLyNhaHang.Models;
using QuanLyNhaHang.Services;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;

namespace QuanLyNhaHang.ViewModel
{
    public sealed class ManagerChatViewModel : BaseViewModel
    {
        private readonly IManagerChatService _chatService;
        private ObservableCollection<ChatMessage> _messages;
        private string _currentQuestion = string.Empty;
        private bool _isProcessing;

        public ManagerChatViewModel()
            : this(new ManagerChatApiService())
        {
        }

        internal ManagerChatViewModel(IManagerChatService chatService)
        {
            _chatService = chatService ?? throw new ArgumentNullException(nameof(chatService));
            _messages = new ObservableCollection<ChatMessage>();

            SendMessageCommand = new RelayCommand<object>((p) => CanSendMessage(), async (p) => await SendMessageAsync());
            ClearChatCommand = new RelayCommand<object>((p) => !IsProcessing && Messages.Count > 0, (p) => ClearChat());
        }

        public ObservableCollection<ChatMessage> Messages
        {
            get => _messages;
            set
            {
                _messages = value;
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public string CurrentQuestion
        {
            get => _currentQuestion;
            set
            {
                _currentQuestion = value ?? string.Empty;
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public bool IsProcessing
        {
            get => _isProcessing;
            set
            {
                _isProcessing = value;
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public ICommand SendMessageCommand { get; }
        public ICommand ClearChatCommand { get; }

        private bool CanSendMessage()
        {
            return !IsProcessing && !string.IsNullOrWhiteSpace(CurrentQuestion);
        }

        private async Task SendMessageAsync()
        {
            if (!CanSendMessage())
            {
                return;
            }

            string question = CurrentQuestion.Trim();
            CurrentQuestion = string.Empty;

            Messages.Add(new ChatMessage(question, true, DateTime.Now, "user"));
            CommandManager.InvalidateRequerySuggested();
            IsProcessing = true;

            try
            {
                ManagerChatServiceResult result = await _chatService.GetReplyAsync(question);
                string intentTag = result.IsSuccess ? "assistant" : "error";
                if (!string.IsNullOrWhiteSpace(result.ErrorCode))
                {
                    intentTag = result.IsSuccess ? "assistant" : result.ErrorCode;
                }

                Messages.Add(new ChatMessage(result.ReplyText, false, DateTime.Now, intentTag));
                CommandManager.InvalidateRequerySuggested();
            }
            catch (Exception)
            {
                Messages.Add(new ChatMessage(
                    "Da xay ra loi trong qua trinh xu ly cau hoi. Vui long thu lai.",
                    false,
                    DateTime.Now,
                    "error"));
                CommandManager.InvalidateRequerySuggested();
            }
            finally
            {
                IsProcessing = false;
            }
        }

        private void ClearChat()
        {
            Messages.Clear();
            CommandManager.InvalidateRequerySuggested();
        }
    }
}
