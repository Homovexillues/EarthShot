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
        return result is QrCodeResult;
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
            return;
        }
        try
        {
            await clipboard.SetTextAsync(qrCodeResult.Text);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ClipboardAction] Failed to set clipboard text: {ex}");
        }
    }
}
