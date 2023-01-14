using MediaPlayer.Views;
using Microsoft.Win32;
using Ookii.Dialogs.Wpf;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace MediaPlayerApp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        public bool IsPlaying { get; set; } = false;
        public TimeSpan MediaTimeSpan { get; set; }
        private DispatcherTimer _timer;
        private readonly ObservableCollection<string> _currentPlaylist = new();

        public MainWindow()
        {
            InitializeComponent();

            QueueMedia.ItemsSource = _currentPlaylist;
        }

#pragma warning disable 67

        public event PropertyChangedEventHandler PropertyChanged;

#pragma warning restore 67

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            LoadPlaylists();

            this.KeyDown += MainWindow_KeyDown;
            // Update MediaState when media ended on its own
            MainMediaPlayer.MediaEnded += (sender, eventArgs) =>
            {
                Stop();
            };

            MainMediaPlayer.Play();
            MainMediaPlayer.Stop();

            _timer = new DispatcherTimer
            {
                Interval = new TimeSpan(0, 0, 0, 0, 500)
            };
            _timer.Tick += Timer_Tick;

            // Set default volume for media
            MainMediaPlayer.Volume = 0.7;
        }

        private void MainWindow_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Space)
            {
                if (IsPlaying)
                {
                    Pause();
                    return;
                }
                Play();
            }
        }

        private void Play()
        {
            IsPlaying = true;
            MainMediaPlayer.Play();
            _timer.Start();

            ButtonPlay.Content = this.Resources["icon_pause"] as DrawingImage;
        }

        private void Pause()
        {
            IsPlaying = false;
            MainMediaPlayer.Pause();
            _timer.Stop();

            ButtonPlay.Content = this.Resources["icon_play"] as DrawingImage;
        }

        private void Stop()
        {
            IsPlaying = false;
            MainMediaPlayer.Stop();
            _timer.Stop();
            SeekBar.Value = 0;
            TextblockCurrentTimestamp.Text = "00:00:00";

            ButtonPlay.Content = this.Resources["icon_play"] as DrawingImage;
        }

        private enum SkipOption
        {
            SkipForward,
            SkipBackward
        }

        private void Skip(SkipOption option)
        {
            if (MainMediaPlayer.Source == null)
                return;

            var path = MainMediaPlayer.Source.AbsolutePath;
            path = Path.GetFullPath(Uri.UnescapeDataString(path));

            int index = _currentPlaylist.IndexOf(path);
            int newIndex = 0;
            if (option == SkipOption.SkipForward && index + 1 != _currentPlaylist.Count)
            {
                newIndex = index + 1;
            }
            else if (option == SkipOption.SkipBackward && index > 0)
            {
                newIndex = index - 1;
            }

            PlayPath(_currentPlaylist[newIndex]);
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            var position = MainMediaPlayer.Position;
            SeekBar.Value = position.TotalSeconds;
            TextblockCurrentTimestamp.Text = string.Format("{0:00}:{1:00}:{2:00}", position.Hours, position.Minutes, position.Seconds);
        }

        private void ButtonPlay_Click(object sender, RoutedEventArgs e)
        {
            if (IsPlaying)
            {
                Pause();
                return;
            }
            Play();
        }

        private void ButtonStop_Click(object sender, RoutedEventArgs e)
        {
            Stop();
        }

        private void SeekBar_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            double value = SeekBar.Value;
            var newPosition = TimeSpan.FromSeconds(value);
            MainMediaPlayer.Position = newPosition;
        }

        private void MainMediaPlayer_MediaOpened(object sender, RoutedEventArgs e)
        {
            MediaTimeSpan = MainMediaPlayer.NaturalDuration.TimeSpan;

            SeekBar.Maximum = MainMediaPlayer.NaturalDuration.TimeSpan.TotalSeconds;
        }

        private void LoadPlaylists()
        {
            // Get all playlists in Playlists folder
            DirectoryInfo directory = new(Path.Combine(Directory.GetParent(Environment.CurrentDirectory).Parent.Parent.FullName,
                                            "Playlists"));
            var playlists = directory.GetFiles("*.txt");

            foreach (var playlistItem in playlists)
            {
                var newMenuItem = new MenuItem
                {
                    Header = Path.GetFileNameWithoutExtension(playlistItem.Name)
                };

                newMenuItem.Click += (s, e) =>
                {
                    _currentPlaylist.Clear();

                    string[] paths = File.ReadAllLines(playlistItem.FullName);
                    foreach (string path in paths)
                    {
                        _currentPlaylist.Add(path);
                    }
                };

                ButtonOpenPlaylist.Items.Add(newMenuItem);
            }
        }

        private void SeekBar_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
        {
            // Use this instead of Pause() so that when drag ended we can use IsPlaying to determine
            // if the video should be continued to play
            MainMediaPlayer.Pause();
            MainMediaPlayer.IsMuted = true;
        }

        private void SeekBar_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            // Continue to play when seek while media is playing
            if (IsPlaying)
            {
                MainMediaPlayer.Play();
            }
            MainMediaPlayer.IsMuted = false;
        }

        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            MainMediaPlayer.Volume = SliderVolume.Value;

            if (SliderVolume.Value == 0)
            {
                MainMediaPlayer.IsMuted = true;
                ButtonVolume.Content = this.Resources["icon_volume_off"] as DrawingImage;
            }
            else
            {
                MainMediaPlayer.IsMuted = false;
                ButtonVolume.Content = this.Resources["icon_volume"] as DrawingImage;
            }
        }

        private void ButtonVolume_Click(object sender, RoutedEventArgs e)
        {
            MainMediaPlayer.IsMuted = !MainMediaPlayer.IsMuted;
            if (MainMediaPlayer.IsMuted)
            {
                ButtonVolume.Content = this.Resources["icon_volume_off"] as DrawingImage;
            }
            else
            {
                ButtonVolume.Content = this.Resources["icon_volume"] as DrawingImage;
            }
        }

        private void ButtonExit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void ButtonOpenFile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Video files (*.mp4, *.wmv, *.avi)|*.mp4;*.wmv;*.avi|Audio files (*.mp3, *.wav)|*.mp3;*.wav",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic)
            };

            if (dialog.ShowDialog() == true)
            {
                _currentPlaylist.Clear();
                _currentPlaylist.Add(dialog.FileName);
                Stop();
                string filePath = dialog.FileName;

                PlayPath(filePath);
            }
        }

        private void PlayPath(string filePath)
        {
            QueueMedia.SelectedIndex = _currentPlaylist.IndexOf(filePath);
            MainMediaPlayer.Source = new Uri(filePath, UriKind.Absolute);
            Play();

            _timer = new DispatcherTimer
            {
                Interval = new TimeSpan(0, 0, 0, 1, 0)
            };

            _timer.Tick += Timer_Tick;
        }

        private void ButtonOpenFiles_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Video files (*.mp4, *.wmv, *.avi)|*.mp4;*.wmv;*.avi|Audio files (*.mp3, *.wav)|*.mp3;*.wav",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
                Multiselect = true
            };

            if (dialog.ShowDialog() == true)
            {
                Stop();
                _currentPlaylist.Clear();
                foreach (var name in dialog.FileNames)
                {
                    _currentPlaylist.Add(name);
                }
                string filePath = _currentPlaylist[0];
                PlayPath(filePath);
            }
        }

        private void ButtonOpenFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new VistaFolderBrowserDialog();

            if (dialog.ShowDialog() == true)
            {
                Stop();
                _currentPlaylist.Clear();

                // Get media files in the folder
                var directory = new DirectoryInfo(dialog.SelectedPath);
                var mediaFiles = directory.GetFiles()
                                          .Where(f => f.Extension is ".mp4" or ".wmv" or ".avi" or ".mp3" or ".wav")
                                          .ToArray();

                foreach (var mediaFile in mediaFiles)
                {
                    _currentPlaylist.Add(mediaFile.FullName);
                }

                string filePath = _currentPlaylist[0];
                PlayPath(filePath);
            }
        }

        private void ButtonShowQueue_Click(object sender, RoutedEventArgs e)
        {
            if (QueueMedia.Visibility == Visibility.Visible)
            {
                QueueMedia.Visibility = Visibility.Collapsed;
                QueueColumn.Width = new GridLength(0);
                return;
            }

            QueueMedia.Visibility = Visibility.Visible;
            QueueColumn.Width = new GridLength(160);
        }

        private void ButtonSkipBackward_Click(object sender, RoutedEventArgs e)
        {
            Skip(SkipOption.SkipBackward);
        }

        private void ButtonSkipForward_Click(object sender, RoutedEventArgs e)
        {
            Skip(SkipOption.SkipForward);
        }

        private void QueueMedia_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var file = QueueMedia.SelectedItem as string;
            if (file is not null)
            {
                PlayPath(file);
            }
        }

        private void ButtonSavePlaylist_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SavePlaylistDialog();
            dialog.ShowDialog();
            if (dialog.DialogResult == true)
            {
                var playlistPath = dialog.PlaylistPath;
                using StreamWriter writer = File.CreateText(playlistPath);
                foreach (var path in _currentPlaylist)
                {
                    writer.WriteLine(path);
                }
            }
        }
    }
}