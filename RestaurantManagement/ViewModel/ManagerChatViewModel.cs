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
        private readonly ManagerChatMockService _chatMockService;
        private ObservableCollection<ChatMessage> _messages;
        private string _currentQuestion = string.Empty;
        private bool _isProcessing;

        public ManagerChatViewModel()
        {
            _chatMockService = new ManagerChatMockService();
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
            IsProcessing = true;

            try
            {
                await Task.Delay(300);
                ManagerChatMockResult result = _chatMockService.GetReply(question);
                Messages.Add(new ChatMessage(result.ReplyText, false, DateTime.Now, result.IntentTag));
            }
            finally
            {
                IsProcessing = false;
            }
        }

        private void ClearChat()
        {
            Messages.Clear();
        }
    }
}
