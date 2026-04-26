using MsBox.Avalonia;
using MsBox.Avalonia.Enums;

namespace AIT_App.Services;

/// <summary>
/// Унифицированные диалоги — чтобы не копипастить MsgAsync в каждом UserControl.
/// КОДЕР: используйте Dialogs.InfoAsync/WarnAsync/ErrorAsync/ConfirmAsync вместо собственных хелперов.
/// </summary>
public static class Dialogs
{
    public static async Task InfoAsync(string title, string text)
    {
        var box = MessageBoxManager.GetMessageBoxStandard(title, text, ButtonEnum.Ok, Icon.Info);
        await box.ShowWindowAsync();
    }

    public static async Task WarnAsync(string title, string text)
    {
        var box = MessageBoxManager.GetMessageBoxStandard(title, text, ButtonEnum.Ok, Icon.Warning);
        await box.ShowWindowAsync();
    }

    public static async Task ErrorAsync(string title, string text)
    {
        var box = MessageBoxManager.GetMessageBoxStandard(title, text, ButtonEnum.Ok, Icon.Error);
        await box.ShowWindowAsync();
    }

    /// <summary>
    /// Yes/No подтверждение. Возвращает true если пользователь нажал «Да».
    /// КОДЕР: всегда вызывайте перед DELETE и любым необратимым действием.
    /// </summary>
    public static async Task<bool> ConfirmAsync(string title, string text)
    {
        var box = MessageBoxManager.GetMessageBoxStandard(title, text, ButtonEnum.YesNo, Icon.Question);
        var result = await box.ShowWindowAsync();
        return result == ButtonResult.Yes;
    }
}
