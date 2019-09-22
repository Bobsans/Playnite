﻿using Playnite;
using Playnite.API;
using Playnite.Common;
using Playnite.Database;
using Playnite.Plugins;
using Playnite.SDK;
using Playnite.SDK.Plugins;
using Playnite.Settings;
using Playnite.Commands;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Playnite.Windows;
using Playnite.DesktopApp.Markup;
using System.Text.RegularExpressions;
using Playnite.DesktopApp.Controls;

namespace Playnite.DesktopApp.ViewModels
{
    public class SelectableTrayIcon
    {
        public TrayIconType TrayIcon { get; }
        public object ImageSource { get; }

        public SelectableTrayIcon(TrayIconType trayIcon)
        {
            TrayIcon = trayIcon;
            ImageSource = ResourceProvider.GetResource(TrayIcon.GetDescription());
        }
    }

    public class SelectablePlugin : ObservableObject
    {
        public Plugin Plugin { get; set; }
        public ExtensionDescription Description { get; set; }
        public object PluginIcon { get; set; }

        private bool selected;
        public bool Selected
        {
            get => selected;
            set
            {
                selected = value;
                OnPropertyChanged();
            }
        }

        public SelectablePlugin()
        {
        }

        public SelectablePlugin(bool selected, Plugin plugin, ExtensionDescription description)
        {
            Selected = selected;
            Plugin = plugin;
            Description = description;
            if (!string.IsNullOrEmpty(description.Icon))
            {
                PluginIcon = Path.Combine(Path.GetDirectoryName(description.DescriptionPath), description.Icon);
            }
            else if (description.Type == ExtensionType.Script && description.Module.EndsWith("ps1", StringComparison.OrdinalIgnoreCase))
            {
                PluginIcon = ResourceProvider.GetResource("PowerShellIcon");
            }
            else if (description.Type == ExtensionType.Script && description.Module.EndsWith("py", StringComparison.OrdinalIgnoreCase))
            {
                PluginIcon = ResourceProvider.GetResource("PythonIcon");
            }
            else
            {
                PluginIcon = ResourceProvider.GetResource("CsharpIcon");
            }
        }
    }

    public class PluginSettings
    {
        public ISettings Settings { get; set; }
        public UserControl View { get; set; }
        public string Name { get; set; }
        public string Icon { get; set; }
    }

    public class SettingsViewModel : ObservableObject
    {
        private static ILogger logger = LogManager.GetLogger();
        private IWindowFactory window;
        private IDialogsFactory dialogs;
        private IResourceProvider resources;
        private GameDatabase database;
        private PlayniteApplication application;
        private PlayniteSettings originalSettings;
        private List<string> editedFields = new List<string>();

        public ExtensionFactory Extensions { get; set; }

        private PlayniteSettings settings;
        public PlayniteSettings Settings
        {
            get
            {
                return settings;
            }

            set
            {
                settings = value;
                OnPropertyChanged();
            }
        }

        public bool AnyGenericPluginSettings
        {
            get => GenericPluginSettings?.Any() == true;
        }

        public List<ThemeDescription> AvailableThemes
        {
            get => ThemeManager.GetAvailableThemes(ApplicationMode.Desktop);
        }

        public List<PlayniteLanguage> AvailableLanguages
        {
            get => Localization.AvailableLanguages;
        }

        public List<string> AvailableFonts
        {
            get => System.Drawing.FontFamily.Families.Where(a => !a.Name.IsNullOrEmpty()).Select(a => a.Name).ToList();
        }

        public List<SelectableTrayIcon> AvailableTrayIcons
        {
            get;
            private set;
        } = new List<SelectableTrayIcon>();

        public List<SelectablePlugin> PluginsList
        {
            get;
        }

        private Dictionary<Guid, PluginSettings> libraryPluginSettings;
        public Dictionary<Guid, PluginSettings> LibraryPluginSettings
        {
            get
            {
                if (libraryPluginSettings == null)
                {
                    libraryPluginSettings = GetLibraryPluginSettings();
                }

                return libraryPluginSettings;
            }
        }

        private Dictionary<Guid, PluginSettings> genericPluginSettings;
        public Dictionary<Guid, PluginSettings> GenericPluginSettings
        {
            get
            {
                if (genericPluginSettings == null)
                {
                    genericPluginSettings = GetGenericPluginSettings();
                }

                return genericPluginSettings;
            }
        }

        private UserControl selectedSectionView;
        public UserControl SelectedSectionView
        {
            get => selectedSectionView;
            set
            {
                selectedSectionView = value;
                OnPropertyChanged();
            }
        }

        private object selectedSectionItem;
        public object SelectedSectionItem
        {
            get => selectedSectionItem;
            set
            {
                selectedSectionItem = value;
                OnPropertyChanged();
            }
        }

        private readonly Dictionary<int, UserControl> sectionViews;

        #region Commands

        public RelayCommand<object> CancelCommand
        {
            get => new RelayCommand<object>((a) =>
            {
                CloseView();
            });
        }

        public RelayCommand<object> ConfirmCommand
        {
            get => new RelayCommand<object>((a) =>
            {
                ConfirmDialog();
            });
        }

        public RelayCommand<object> WindowClosingCommand
        {
            get => new RelayCommand<object>((a) =>
            {
                WindowClosing(false);
            });
        }

        public RelayCommand<object> SelectDbFileCommand
        {
            get => new RelayCommand<object>((a) =>
            {
                SelectDbFile();
            });
        }

        public RelayCommand<object> ClearWebCacheCommand
        {
            get => new RelayCommand<object>((url) =>
            {
                ClearWebcache();
            });
        }

        public RelayCommand<string> SetCoverArtAspectRatioCommand
        {
            get => new RelayCommand<string>((ratio) =>
            {
                SetCoverArtAspectRatio(ratio);
            });
        }

        public RelayCommand<object> SetDefaultFontSizes
        {
            get => new RelayCommand<object>((ratio) =>
            {
                Settings.FontSize = 14;
                Settings.FontSizeSmall = 12;
                Settings.FontSizeLarge = 15;
                Settings.FontSizeLarger = 20;
                Settings.FontSizeLargest = 29;
            });
        }

        public RelayCommand<RoutedPropertyChangedEventArgs<object>> SettingsTreeSelectedItemChangedCommand
        {
            get => new RelayCommand<RoutedPropertyChangedEventArgs<object>>((a) =>
            {
                SettingsTreeSelectedItemChanged(a);
            });
        }

        #endregion Commands

        public SettingsViewModel(
            GameDatabase database,
            PlayniteSettings settings,
            IWindowFactory window,
            IDialogsFactory dialogs,
            IResourceProvider resources,
            ExtensionFactory extensions,
            PlayniteApplication app)
        {
            Extensions = extensions;
            originalSettings = settings;
            Settings = settings.GetClone();
            Settings.PropertyChanged += (s, e) => editedFields.Add(e.PropertyName);
            this.database = database;
            this.window = window;
            this.dialogs = dialogs;
            this.resources = resources;
            this.application = app;

            AvailableTrayIcons = new List<SelectableTrayIcon>
            {
                new SelectableTrayIcon(TrayIconType.Default),
                new SelectableTrayIcon(TrayIconType.Bright),
                new SelectableTrayIcon(TrayIconType.Dark)
            };

            PluginsList = Extensions
                .GetExtensionDescriptors()
                .Select(a => new SelectablePlugin(Settings.DisabledPlugins?.Contains(a.FolderName) != true, null, a))
                .ToList();

            sectionViews = new Dictionary<int, UserControl>()
            {
                { 0, new Controls.SettingsSections.General() { DataContext = this } },
                { 1, new Controls.SettingsSections.AppearanceGeneral() { DataContext = this } },
                { 2, new Controls.SettingsSections.AppearanceAdvanced() { DataContext = this } },
                { 3, new Controls.SettingsSections.AppearanceDetailsView() { DataContext = this } },
                { 4, new Controls.SettingsSections.AppearanceGridView() { DataContext = this } },
                { 5, new Controls.SettingsSections.AppearanceLayout() { DataContext = this } },
                { 6, new Controls.SettingsSections.GeneralAdvanced() { DataContext = this } },
                { 7, new Controls.SettingsSections.Input() { DataContext = this } },
                { 8, new Controls.SettingsSections.Extensions() { DataContext = this } },
                { 9, new Controls.SettingsSections.Metadata() { DataContext = this } },
                { 10, new Controls.SettingsSections.EmptyParent() { DataContext = this } }
            };

            SelectedSectionView = sectionViews[0];
        }

        private void SettingsTreeSelectedItemChanged(RoutedPropertyChangedEventArgs<object> selectedItem)
        {
            if (selectedItem.NewValue is TreeViewItem treeItem)
            {
                if (treeItem.Tag != null)
                {
                    var viewIndex = int.Parse(treeItem.Tag.ToString());
                    SelectedSectionView = sectionViews[viewIndex];
                }
                else
                {
                    SelectedSectionView = null;
                }
            }
            else if (selectedItem.NewValue is KeyValuePair<Guid, PluginSettings> plugin)
            {
                SelectedSectionView = plugin.Value.View;
            }
        }

        private Dictionary<Guid, PluginSettings> GetGenericPluginSettings()
        {
            var allSettings = new Dictionary<Guid, PluginSettings>();
            foreach (var plugin in Extensions.Plugins.Values.Where(a => a.Description.Type == ExtensionType.GenericPlugin))
            {
                var provSetting = plugin.Plugin.GetSettings(false);
                var provView = plugin.Plugin.GetSettingsView(false);
                if (provSetting != null && provView != null)
                {
                    provView.DataContext = provSetting;
                    provSetting.BeginEdit();
                    var plugSetting = new PluginSettings()
                    {
                        Name = plugin.Description.Name,
                        Settings = provSetting,
                        View = provView
                    };

                    allSettings.Add(plugin.Plugin.Id, plugSetting);
                }
            }

            return allSettings;
        }

        private Dictionary<Guid, PluginSettings> GetLibraryPluginSettings()
        {
            var allSettings = new Dictionary<Guid, PluginSettings>();
            foreach (var library in Extensions.LibraryPlugins)
            {
                var provSetting = library.GetSettings(false);
                var provView = library.GetSettingsView(false);
                if (provSetting != null && provView != null)
                {
                    provView.DataContext = provSetting;
                    provSetting.BeginEdit();
                    var plugSetting = new PluginSettings()
                    {
                        Name = library.Name,
                        Settings = provSetting,
                        View = provView,
                        Icon = library.LibraryIcon
                    };

                    allSettings.Add(library.Id, plugSetting);
                }
            }

            return allSettings;
        }

        public bool? OpenView()
        {
            return window.CreateAndOpenDialog(this);
        }

        public void CloseView()
        {
            if (libraryPluginSettings.HasItems())
            {
                foreach (var provider in libraryPluginSettings.Keys)
                {
                    libraryPluginSettings[provider].Settings.CancelEdit();
                }
            }

            if (genericPluginSettings.HasItems())
            {
                foreach (var provider in genericPluginSettings.Keys)
                {
                    genericPluginSettings[provider].Settings.CancelEdit();
                }
            }

            WindowClosing(true);
            window.Close(false);
        }

        public void WindowClosing(bool closingHandled)
        {
            if (closingHandled)
            {
                return;
            }
        }
 
        public void EndEdit()
        {
            Settings.CopyProperties(originalSettings, true, new List<string>()
            {
                nameof(PlayniteSettings.FilterSettings),
                nameof(PlayniteSettings.ViewSettings),
                nameof(PlayniteSettings.InstallInstanceId),
                nameof(PlayniteSettings.GridItemHeight),
                nameof(PlayniteSettings.WindowPositions),
                nameof(PlayniteSettings.Fullscreen)
            }, true);
        }

        public void ConfirmDialog()
        {
            if (libraryPluginSettings.HasItems())
            {
                foreach (var provider in libraryPluginSettings.Keys)
                {
                    if (!libraryPluginSettings[provider].Settings.VerifySettings(out var errors))
                    {
                        dialogs.ShowErrorMessage(string.Join(Environment.NewLine, errors), libraryPluginSettings[provider].Name);
                        return;
                    }
                }
            }

            if (genericPluginSettings.HasItems())
            {
                foreach (var plugin in genericPluginSettings.Keys)
                {
                    if (!genericPluginSettings[plugin].Settings.VerifySettings(out var errors))
                    {
                        dialogs.ShowErrorMessage(string.Join(Environment.NewLine, errors), genericPluginSettings[plugin].Name);
                        return;
                    }
                }
            }

            var disabledPlugs = PluginsList.Where(a => !a.Selected)?.Select(a => a.Description.FolderName).ToList();
            if (Settings.DisabledPlugins?.IsListEqual(disabledPlugs) != true)
            {
                Settings.DisabledPlugins = PluginsList.Where(a => !a.Selected)?.Select(a => a.Description.FolderName).ToList();
            }

            if (editedFields.Contains(nameof(Settings.StartOnBoot)))
            {
                try
                {
                    PlayniteSettings.SetBootupStateRegistration(Settings.StartOnBoot);
                }
                catch (Exception e) when (!PlayniteEnvironment.ThrowAllErrors)
                {
                    logger.Error(e, "Failed to register Playnite to start on boot.");
                    dialogs.ShowErrorMessage(resources.GetString("LOCSettingsStartOnBootRegistrationError")
                        + Environment.NewLine + e.Message, "");
                }
            }

            EndEdit();
            originalSettings.SaveSettings();
            if (libraryPluginSettings.HasItems())
            {
                foreach (var provider in libraryPluginSettings.Keys)
                {
                    libraryPluginSettings[provider].Settings.EndEdit();
                }
            }

            if (genericPluginSettings.HasItems())
            {
                foreach (var plugin in genericPluginSettings.Keys)
                {
                    genericPluginSettings[plugin].Settings.EndEdit();
                }
            }

            if (editedFields?.Any() == true)
            {
                if (editedFields.IntersectsExactlyWith(
                    new List<string>()
                    {
                        nameof(Settings.Theme),
                        nameof(Settings.AsyncImageLoading),
                        nameof(Settings.DisableHwAcceleration),
                        nameof(Settings.DisableDpiAwareness),
                        nameof(Settings.DatabasePath),
                        nameof(Settings.DisabledPlugins),
                        nameof(Settings.EnableTray),
                        nameof(Settings.TrayIcon),
                        nameof(Settings.EnableControllerInDesktop),
                        nameof(Settings.Language),
                        nameof(Settings.FontFamilyName),
                        nameof(Settings.FontSize),
                        nameof(Settings.FontSizeSmall),
                        nameof(Settings.FontSizeLarge),
                        nameof(Settings.FontSizeLarger),
                        nameof(Settings.FontSizeLargest)
                    }))
                {
                    if (dialogs.ShowMessage(
                        resources.GetString("LOCSettingsRestartAskMessage"),
                        resources.GetString("LOCSettingsRestartTitle"),
                        MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                    {
                        application.Restart(new CmdLineOptions() { SkipLibUpdate = true });
                    }
                }
            }

            WindowClosing(true);
            window.Close(true);
        }

        public void SelectDbFile()
        {
            dialogs.ShowMessage(resources.GetString("LOCSettingsDBPathNotification"), "", MessageBoxButton.OK, MessageBoxImage.Warning);
            var path = dialogs.SelectFolder();
            if (!string.IsNullOrEmpty(path))
            {
                Settings.DatabasePath = path;
            }
        }

        public void ClearWebcache()
        {
            if (dialogs.ShowMessage(
                    resources.GetString("LOCSettingsClearCacheWarn"),
                    resources.GetString("LOCSettingsClearCacheTitle"),
                    MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                CefTools.Shutdown();
                Directory.Delete(PlaynitePaths.BrowserCachePath, true);
                application.Restart();
            }            
        }

        public void SetCoverArtAspectRatio(string ratio)
        {
            var regex = Regex.Match(ratio, @"(\d+):(\d+)");
            if (regex.Success)
            {

                Settings.GridItemWidthRatio = Convert.ToInt32(regex.Groups[1].Value);
                Settings.GridItemHeightRatio = Convert.ToInt32(regex.Groups[2].Value);
            }
        }
    }
}
