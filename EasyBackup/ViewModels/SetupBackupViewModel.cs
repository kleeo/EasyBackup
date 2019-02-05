﻿using ByteSizeLib;
using EasyBackup.Helpers;
using EasyBackup.Interfaces;
using EasyBackup.Models;
using EasyBackup.Views;
using GongSolutions.Wpf.DragDrop;
using MahApps.Metro.Controls.Dialogs;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace EasyBackup.ViewModels
{
    class SetupBackupViewModel : BaseViewModel, IDropTarget
    {
        private ObservableCollection<FolderFileItem> _items;
        private FolderFileItem _selectedItem;
        private string _backupLocation;
        private ulong _totalBackupSize;

        private string _checkBackupSizeStatus;
        private bool _isCheckBackupSizeStatusVisible;
        private Brush _checkBackupSizeBrush;

        public SetupBackupViewModel(IChangeViewModel viewModelChanger) : base(viewModelChanger)
        {
            Items = new ObservableCollection<FolderFileItem>();
            // upgrading settings: https://stackoverflow.com/a/534335
            if (Properties.Settings.Default.UpgradeRequired)
            {
                Properties.Settings.Default.Upgrade();
                Properties.Settings.Default.UpgradeRequired = false;
                Properties.Settings.Default.Save();
            }
            LoadBackupTemplate(Properties.Settings.Default.LastUsedBackupTemplatePath);
            IsCheckBackupSizeStatusVisible = false;
        }

        public ObservableCollection<FolderFileItem> Items
        {
            get { return _items; }
            set { _items = value; NotifyPropertyChanged(); }
        }

        public FolderFileItem SelectedItem
        {
            get { return _selectedItem; }
            set { _selectedItem = value; NotifyPropertyChanged(); NotifyPropertyChanged(nameof(IsItemSelected)); }
        }

        public bool IsItemSelected
        {
            get { return _selectedItem != null; }
        }

        public string BackupLocation
        {
            get { return _backupLocation; }
            set { _backupLocation = value; NotifyPropertyChanged(); }
        }

        public string CheckBackupSizeStatus
        {
            get { return _checkBackupSizeStatus; }
            set { _checkBackupSizeStatus = value; NotifyPropertyChanged(); }
        }

        public bool IsCheckBackupSizeStatusVisible
        {
            get { return _isCheckBackupSizeStatusVisible; }
            set { _isCheckBackupSizeStatusVisible = value; NotifyPropertyChanged(); }
        }

        public Brush CheckBackupSizeBrush
        {
            get { return _checkBackupSizeBrush; }
            set { _checkBackupSizeBrush = value; NotifyPropertyChanged(); }
        }

        public IDialogCoordinator DialogCoordinator { get; set; }

        public ICommand AddFolder
        {
            get { return new RelayCommand(ChooseFolder); }
        }

        private void ChooseFolder()
        {
            var dialog = new Ookii.Dialogs.Wpf.VistaFolderBrowserDialog();
            dialog.ShowNewFolderButton = true;
            if (dialog.ShowDialog(Application.Current.MainWindow).GetValueOrDefault())
            {
                AddPath(dialog.SelectedPath);
            }
        }

        public ICommand AddFile
        {
            get { return new RelayCommand(ChooseFile); }
        }

        private void ChooseFile()
        {
            var dialog = new Ookii.Dialogs.Wpf.VistaOpenFileDialog();
            dialog.Multiselect = true;
            dialog.ShowReadOnly = true;
            dialog.Title = "Choose a file";
            if (dialog.ShowDialog(Application.Current.MainWindow).GetValueOrDefault())
            {
                foreach (string fileName in dialog.FileNames)
                {
                    AddPath(fileName);
                }
            }
        }

        private void AddPath(string path)
        {
            var isDirectory = Directory.Exists(path);
            // if we don't already have this path, add the path
            if (Items.Where(x => x.Path == path).Count() == 0)
            {
                Items.Add(new FolderFileItem() { Path = path, IsDirectory = isDirectory, IsRecursive = isDirectory });
            }
            IsCheckBackupSizeStatusVisible = false;
        }

        public ICommand RemoveItem
        {
            get { return new RelayCommand<object>(list => RemoveItemFromList(list)); }
        }

        private void RemoveItemFromList(object items)
        {
            if (items != null)
            {
                System.Collections.IList list = (System.Collections.IList)items;
                var selection = list?.Cast<FolderFileItem>();
                for (int i = 0; i < selection.Count(); i++)
                {
                    Items.Remove(selection.ElementAt(i));
                    i--; // have to do this as selection array is modified when we do the remove O_o
                }
            }
        }

        public ICommand SaveTemplate
        {
            get { return new RelayCommand(SaveItemsToDisk); }
        }

        private void SaveItemsToDisk()
        {
            var saveFileDialog = new Ookii.Dialogs.Wpf.VistaSaveFileDialog();
            saveFileDialog.AddExtension = true;
            saveFileDialog.Filter = "Easy Backup Files | *.ebf";
            saveFileDialog.DefaultExt = "ebf";
            saveFileDialog.OverwritePrompt = true;
            saveFileDialog.Title = "Choose save location";
            if (saveFileDialog.ShowDialog(Application.Current.MainWindow).GetValueOrDefault())
            {
                var backupTemplate = new BackupTemplate() { Paths = Items.ToList(), BackupLocation = BackupLocation };
                var json = Newtonsoft.Json.JsonConvert.SerializeObject(backupTemplate);
                File.WriteAllText(saveFileDialog.FileName, json);
                UpdateLastUsedBackupPath(saveFileDialog.FileName);
            }
        }

        public ICommand LoadTemplate
        {
            get { return new RelayCommand(LoadItemsFromDisk); }
        }

        private void LoadItemsFromDisk()
        {
            var openFileDialog = new Ookii.Dialogs.Wpf.VistaOpenFileDialog();
            openFileDialog.AddExtension = true;
            openFileDialog.Filter = "Easy Backup Files | *.ebf";
            openFileDialog.DefaultExt = "ebf";
            openFileDialog.Title = "Choose Easy Backup File";
            if (openFileDialog.ShowDialog(Application.Current.MainWindow).GetValueOrDefault())
            {
                LoadBackupTemplate(openFileDialog.FileName);
                UpdateLastUsedBackupPath(openFileDialog.FileName);
            }
        }

        private void UpdateLastUsedBackupPath(string path)
        {
            Properties.Settings.Default.LastUsedBackupTemplatePath = path;
            Properties.Settings.Default.Save();
        }

        private void LoadBackupTemplate(string path)
        {
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                var backupTemplate = Newtonsoft.Json.JsonConvert.DeserializeObject<BackupTemplate>(json);
                if (backupTemplate != null)
                {
                    Items = new ObservableCollection<FolderFileItem>(backupTemplate.Paths);
                    BackupLocation = backupTemplate.BackupLocation;
                }
            }
        }

        public ICommand ChooseBackupLocation
        {
            get { return new RelayCommand(PickBackupFolder); }
        }

        private void PickBackupFolder()
        {
            var dialog = new Ookii.Dialogs.Wpf.VistaFolderBrowserDialog();
            dialog.ShowNewFolderButton = true;
            if (dialog.ShowDialog(Application.Current.MainWindow).GetValueOrDefault())
            {
                BackupLocation = dialog.SelectedPath;
            }
        }

        public ICommand PerformBackup
        {
            get { return new RelayCommand(StartBackup); }
        }

        private void StartBackup()
        {
            PushViewModel(new BackupInProgressViewModel(ViewModelChanger, Items.ToList(), BackupLocation));
        }

        public ICommand ShowAboutWindow
        {
            get { return new RelayCommand(ShowAboutWindowDialog); }
        }

        private void ShowAboutWindowDialog()
        {
            var aboutWindow = new AboutWindow();
            aboutWindow.Owner = Application.Current.MainWindow;
            aboutWindow.Show();
        }

        public ICommand CheckBackupSize
        {
            get { return new RelayCommand(ScanBackupAndCheckSize); }
        }

        private void ScanBackupAndCheckSize()
        {
            _totalBackupSize = 0;
            var backupPerformer = new BackupPerformer();
            backupPerformer.CalculatedBytesOfItem += BackupPerformer_CalculatedBytesOfItem;
            backupPerformer.CalculateBackupSize(Items.ToList(), BackupLocation);
            ulong freeDriveBytes = Utilities.DriveFreeBytes(BackupLocation);
            if (_totalBackupSize > freeDriveBytes)
            {
                CheckBackupSizeBrush = new SolidColorBrush(Colors.Red);
                CheckBackupSizeStatus = string.Format("Not enough free space -- need {0} but only have {1}",
                                        ByteSize.FromBytes(_totalBackupSize), ByteSize.FromBytes(freeDriveBytes));
            }
            else
            {
                CheckBackupSizeBrush = new SolidColorBrush(Colors.Green);
                CheckBackupSizeStatus = string.Format("There's enough space available! We need {0} and have {1} available.",
                                        ByteSize.FromBytes(_totalBackupSize), ByteSize.FromBytes(freeDriveBytes));
            }
            IsCheckBackupSizeStatusVisible = true;
        }

        private void BackupPerformer_CalculatedBytesOfItem(FolderFileItem item, ulong bytes)
        {
            _totalBackupSize += bytes;
        }

        public ICommand RemoveAllItems
        {
            get { return new RelayCommand(CheckAndRemoveAllItems); }
        }

        private async void CheckAndRemoveAllItems()
        {
            var result = await DialogCoordinator.ShowMessageAsync(this, "Warning!", "Are you sure you want to remove all items?", 
                MessageDialogStyle.AffirmativeAndNegative, new MetroDialogSettings()
            {
                AffirmativeButtonText = "Yes",
                NegativeButtonText = "No",
                    ColorScheme = MetroDialogColorScheme.Theme
                });
            if (result == MessageDialogResult.Affirmative)
            {
                Items.Clear();
            }
        }

        #region IDropTarget

        public void DragOver(IDropInfo dropInfo)
        {
            if (dropInfo.Data is DataObject && (dropInfo.Data as DataObject).GetFileDropList().Count > 0)
            {
                dropInfo.DropTargetAdorner = DropTargetAdorners.Highlight;
                dropInfo.Effects = DragDropEffects.Copy;
            }
        }

        public void Drop(IDropInfo dropInfo)
        {
            if (dropInfo.Data is DataObject)
            {
                var stringCollection = (dropInfo.Data as DataObject).GetFileDropList();
                foreach (string path in stringCollection)
                {
                    AddPath(path);
                }
            }
        }

        #endregion
    }
}