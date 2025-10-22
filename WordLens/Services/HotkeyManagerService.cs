using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using WordLens.Messages;

namespace WordLens.Services
{
    public interface IHotkeyManagerService
    {
        Task StartAsync();
    }


    public class HotkeyManagerService : IHotkeyManagerService
    {
        readonly private IHotkeyService _hotkeyService;
        readonly private ISelectionService _selectionService;

        public HotkeyManagerService(
            IHotkeyService hotkeyService,
            ISelectionService selectionService)
        {
            _hotkeyService = hotkeyService;
            _selectionService = selectionService;
        }

        public async Task StartAsync()
        {
            // 订阅事件
            _hotkeyService.HotkeyTriggered += OnHotkeyTriggered;

            await _hotkeyService.StartAsync();
        }

        private void OnHotkeyTriggered(object? sender, EventArgs e)
        {
            var text = _selectionService.GetSelectedTex();
            if (string.IsNullOrWhiteSpace(text))
                return;

            WeakReferenceMessenger.Default.Send(new ShowPopupMessage(text));

        }
    }
}