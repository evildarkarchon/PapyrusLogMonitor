using PapyrusMonitor.Core.Configuration;

namespace PapyrusMonitor.Core.Interfaces;

/// <summary>
///     Service for managing application settings
/// </summary>
public interface ISettingsService
{
    /// <summary>
    ///     Gets the current settings
    /// </summary>
    AppSettings Settings { get; }

    /// <summary>
    ///     Observable stream of settings changes
    /// </summary>
    IObservable<AppSettings> SettingsChanged { get; }

    /// <summary>
    ///     Gets the path to the settings file
    /// </summary>
    string SettingsFilePath { get; }

    /// <summary>
    ///     Loads settings from storage
    /// </summary>
    /// <returns>Task that completes when settings are loaded</returns>
    Task<AppSettings> LoadSettingsAsync();

    /// <summary>
    ///     Saves settings to storage
    /// </summary>
    /// <param name="settings">Settings to save</param>
    /// <returns>Task that completes when settings are saved</returns>
    Task SaveSettingsAsync(AppSettings settings);

    /// <summary>
    ///     Resets settings to defaults
    /// </summary>
    /// <returns>Task that completes when settings are reset</returns>
    Task ResetToDefaultsAsync();
}
