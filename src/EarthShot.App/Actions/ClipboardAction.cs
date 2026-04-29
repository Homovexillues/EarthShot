using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Input.Platform;
using EarthShot.Core.Actions;
using EarthShot.Core.Processing;

namespace EarthShot.App.Actions;

public sealed class ClipboardAction(Func<IClipboard?> getClipboard) : IResultAction
{
    public bool CanHandle(ProcessingResult result)
    {
        return result is not null;
    }

    public async Task ExecuteAsync(
        ProcessingResult result,
        ActionContext context,
        CancellationToken cancellationToken = default
    )
    {
        if (result is not QrCodeResult qrCodeResult)
            return;

        var clipboard = getClipboard();
        if (clipboard is null)
        {
            Console.WriteLine("[ClipboardAction] Clipboard is null — cannot set text!");
            return;
        }
        Console.WriteLine($"[ClipboardAction] Setting clipboard text: {qrCodeResult.Text}");
        try
        {
            await clipboard.SetTextAsync(qrCodeResult.Text);
            Console.WriteLine("[ClipboardAction] SetTextAsync completed");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ClipboardAction] Failed to set clipboard text: {ex}");
        }
    }
}
