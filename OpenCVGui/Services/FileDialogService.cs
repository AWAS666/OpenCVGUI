using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace OpenCVGui.Services;

public class FileDialogService : IFileDialogService
{
    public async Task<IReadOnlyList<IStorageFile>?> OpenFilePickerAsync(FilePickerOpenOptions options)
    {
        var topLevel = GetTopLevel();
        if (topLevel == null)
            return null;

        return await topLevel.StorageProvider.OpenFilePickerAsync(options);
    }

    public async Task<IStorageFile?> SaveFilePickerAsync(FilePickerSaveOptions options)
    {
        var topLevel = GetTopLevel();
        if (topLevel == null)
            return null;

        return await topLevel.StorageProvider.SaveFilePickerAsync(options);
    }

    public TopLevel? GetTopLevel()
    {
        return Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;
    }
}

