using MediaPlayer.Hotkeys;
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
using System.Windows.Input;
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
        private bool _isShuffleEnabled = false;
        public TimeSpan MediaTimeSpan { get; set; }
        private DispatcherTimer _timer;
        private string _currentPlaylist;
        private ObservableCollection<string> _queue = new();
        private ObservableCollection<string> _originalQueue;

        public MainWindow()
        {
            InitializeComponent();

            QueueMedia.ItemsSource = _queue;
            HotkeysManager.SetupSystemHook();

            HotkeysManager.AddHotkey(ModifierKeys.Control, Key.S, () => Stop());
            HotkeysManager.AddHotkey(ModifierKeys.Control, Key.P, () => PlayPause());
            HotkeysManager.AddHotkey(ModifierKeys.Control, Key.OemPeriod, () => Skip(SkipOption.Forward));
            HotkeysManager.AddHotkey(ModifierKeys.Control, Key.OemComma, () => Skip(SkipOption.Backward));

            Closing += MainWindow_Closing;
        }

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            HotkeysManager.ShutdownSystemHook();
        }

#pragma warning disable 67

        public event PropertyChangedEventHandler PropertyChanged;

#pragma warning restore 67

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            LoadPlaylists();
            LoadSavedState();
            LoadRecentlyPlayed();

            this.KeyDown += MainWindow_KeyDown;
            // Update MediaState when media ended on its own
            MainMediaPlayer.MediaEnded += (sender, eventArgs) =>
            {
                if (_queue.Count > 0)
                {
                    Skip(SkipOption.Forward);
                }
                else
                {
                    Stop();
                }
            };

            _timer = new DispatcherTimer
            {
                Interval = new TimeSpan(0, 0, 0, 0, 500)
            };
            _timer.Tick += Timer_Tick;

            // Set default volume for media
            MainMediaPlayer.Volume = 0.7;
        }

        private void LoadSavedState()
        {
            var path = Path.Combine(Directory.GetParent(Environment.CurrentDirectory).Parent.Parent.FullName,
                                            "Data", "savedState.txt");
            if (!File.Exists(path))
            {
                File.Create(path);
                return;
            }

            var lines = File.ReadAllLines(path);

            var playlist = lines[0];
            var position = lines[1];

            // Parse playlist
            if (playlist != "-1")
                _currentPlaylist = playlist;

            // Add to queue
            for (int i = 2; i < lines.Length; i++)
            {
                _queue.Add(lines[i]);
            }

            // Load saved position
            if (position != "-1")
            {
                var tokens = position.Split(' ');
                var index = int.Parse(tokens[0]);
                var timespanInSeconds = double.Parse(tokens[1]);

                PlayIndex(index);
                SeekBar.Value = timespanInSeconds;
                Pause();
            }
        }

        private void LoadRecentlyPlayed()
        {
            var path = Path.Combine(Directory.GetParent(Environment.CurrentDirectory).Parent.Parent.FullName,
                                            "Data", "recentlyPlayed.txt");
            if (!File.Exists(path))
            {
                File.Create(path);
                return;
            }

            // Only show last 5 files
            var lines = File.ReadLines(path).Reverse().Take(5);
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var name = Path.GetFileName(line);
                var item = new MenuItem { Header = name };
                item.Click += (s, e) =>
                {
                    if (!_queue.Contains(line))
                    {
                        _queue.Add(line);
                    }
                    PlayIndex(_queue.IndexOf(line));
                };
                MenuRecent.Items.Add(item);
            }
        }

        private void MainWindow_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            switch (e.Key)
            {
                case System.Windows.Input.Key.Space:
                    if (IsPlaying)
                    {
                        Pause();
                        return;
                    }
                    Play();
                    break;

                case System.Windows.Input.Key.Up:
                    SliderVolume.Value += 0.05;
                    break;

                case System.Windows.Input.Key.Down:
                    SliderVolume.Value -= 0.05;
                    break;

                case System.Windows.Input.Key.Right:
                    SeekBar.Value += 5;
                    break;

                case System.Windows.Input.Key.Left:
                    if (SeekBar.Value - 5 < 0)
                    {
                        SeekBar.Value = 0;
                        return;
                    }
                    SeekBar.Value -= 5;
                    break;

                default:
                    break;
            }
        }

        private void PlayPause()
        {
            if (IsPlaying)
            {
                Pause();
                return;
            }
            Play();
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
            Forward,
            Backward
        }

        private void Skip(SkipOption option)
        {
            if (MainMediaPlayer.Source is null)
                return;
            if (_queue.Count == 0)
            {
                MainMediaPlayer.Source = null;
                MainMediaPlayer.Play();
                Stop();
                TextblockVideoLength.Text = "00:00:00";
                return;
            }

            int index = QueueMedia.SelectedIndex;
            int newIndex = 0;
            if (option == SkipOption.Forward && index + 1 != _queue.Count)
            {
                newIndex = index + 1;
            }
            else if (option == SkipOption.Backward && index > 0)
            {
                newIndex = index - 1;
            }

            PlayIndex(newIndex);
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
            TextblockCurrentTimestamp.Text = string.Format("{0:00}:{1:00}:{2:00}", newPosition.Hours, newPosition.Minutes, newPosition.Seconds);
        }

        private void MainMediaPlayer_MediaOpened(object sender, RoutedEventArgs e)
        {
            MediaTimeSpan = MainMediaPlayer.NaturalDuration.TimeSpan;

            SeekBar.Maximum = MediaTimeSpan.TotalSeconds;
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
                    _queue.Clear();

                    if (string.IsNullOrWhiteSpace(File.ReadAllText(playlistItem.FullName)))
                    {
                        Stop();
                        MainMediaPlayer.Source = null;
                        return;
                    }
                    string[] paths = File.ReadAllLines(playlistItem.FullName);
                    foreach (string path in paths)
                    {
                        _queue.Add(path);
                    }
                    _currentPlaylist = (string)newMenuItem.Header;
                    PlayIndex(0);
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
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos)
            };

            if (dialog.ShowDialog() == true)
            {
                _queue.Clear();
                _queue.Add(dialog.FileName);
                Stop();

                _currentPlaylist = null;
                PlayIndex(0);
            }
        }

        private void PlayIndex(int index)
        {
            if (!File.Exists(_queue[index]))
            {
                MessageBox.Show($"Cannot open the file: {_queue[index]}, it is moved or deleted!", "Invalid file", MessageBoxButton.OK, MessageBoxImage.Error);
                var missingFile = _queue[index];
                _queue.Where(x => x == missingFile).ToList().ForEach(x => _queue.Remove(x));
                PlayIndex(0);
                List<MenuItem> removeItems = new();
                foreach (MenuItem menuItem in MenuRecent.Items)
                {
                    if (menuItem.Header as string == Path.GetFileName(missingFile))
                        removeItems.Add(menuItem);
                }
                foreach (MenuItem menuItem in removeItems)
                {
                    MenuRecent.Items.Remove(menuItem);
                }
                return;
            }

            QueueMedia.SelectedIndex = index;
            MainMediaPlayer.Source = new Uri(_queue[index], UriKind.RelativeOrAbsolute);
            Title = $"{Path.GetFileName(_queue[index])} - MediaPlayer";
            _timer = new DispatcherTimer
            {
                Interval = new TimeSpan(0, 0, 0, 1, 0)
            };

            _timer.Tick += Timer_Tick;

            Play();

            var path = Path.Combine(Directory.GetParent(Environment.CurrentDirectory).Parent.Parent.FullName,
                                            "Data", "recentlyPlayed.txt");
            using StreamWriter writer = File.AppendText(path);
            writer.WriteLine(_queue[index]);

            var name = Path.GetFileName(_queue[index]);
            var item = new MenuItem { Header = name };
            item.Click += (s, e) =>
            {
                if (!_queue.Contains(_queue[index]))
                {
                    _queue.Add(_queue[index]);
                }
                PlayIndex(_queue.IndexOf(_queue[index]));
            };
            MenuRecent.Items.Add(item);
        }

        private void ButtonOpenFiles_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Video files (*.mp4, *.wmv, *.avi)|*.mp4;*.wmv;*.avi|Audio files (*.mp3, *.wav)|*.mp3;*.wav",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
                Multiselect = true
            };

            if (dialog.ShowDialog() == true)
            {
                Stop();
                _queue.Clear();
                foreach (var name in dialog.FileNames)
                {
                    _queue.Add(name);
                }

                _currentPlaylist = null;
                PlayIndex(0);
            }
        }

        private void ButtonOpenFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new VistaFolderBrowserDialog();

            if (dialog.ShowDialog() == true)
            {
                Stop();
                _queue.Clear();

                // Get media files in the folder
                var directory = new DirectoryInfo(dialog.SelectedPath);
                var mediaFiles = directory.GetFiles()
                                          .Where(f => f.Extension is ".mp4" or ".wmv" or ".avi" or ".mp3" or ".wav")
                                          .ToArray();

                foreach (var mediaFile in mediaFiles)
                {
                    _queue.Add(mediaFile.FullName);
                }

                _currentPlaylist = null;
                PlayIndex(0);
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
            Skip(SkipOption.Backward);
        }

        private void ButtonSkipForward_Click(object sender, RoutedEventArgs e)
        {
            Skip(SkipOption.Forward);
        }

        private void QueueMedia_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var index = QueueMedia.SelectedIndex;
            if (index != -1)
            {
                PlayIndex(index);
            }
        }

        private void ButtonSavePlaylist_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPlaylist is not null)
            {
                var playlistPath = Path.Combine(Directory.GetParent(Environment.CurrentDirectory).Parent.Parent.FullName,
                                            "Playlists", _currentPlaylist + ".txt");
                using StreamWriter writer = File.CreateText(playlistPath);
                foreach (var path in _queue)
                {
                    writer.WriteLine(path);
                }
                return;
            }

            var dialog = new SavePlaylistDialog();
            dialog.ShowDialog();
            if (dialog.DialogResult == true)
            {
                var playlistPath = dialog.PlaylistPath;
                using StreamWriter writer = File.CreateText(playlistPath);
                foreach (var path in _queue)
                {
                    writer.WriteLine(path);
                }

                var newMenuItem = new MenuItem
                {
                    Header = Path.GetFileNameWithoutExtension(dialog.PlaylistName)
                };

                newMenuItem.Click += (s, e) =>
                {
                    _queue.Clear();

                    if (string.IsNullOrWhiteSpace(File.ReadAllText(playlistPath)))
                    {
                        Stop();
                        MainMediaPlayer.Source = null;
                        return;
                    }
                    string[] paths = File.ReadAllLines(playlistPath);
                    foreach (string path in paths)
                    {
                        _queue.Add(path);
                    }
                    _currentPlaylist = (string)newMenuItem.Header;
                    PlayIndex(0);
                };
                ButtonOpenPlaylist.Items.Add(newMenuItem);
            }
        }

        private void ButtonRemoveItem_Click(object sender, RoutedEventArgs e)
        {
            var index = QueueMedia.SelectedIndex;

            _queue.RemoveAt(index);
        }

        private void ButtonAddToQueue_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Video files (*.mp4, *.wmv, *.avi)|*.mp4;*.wmv;*.avi|Audio files (*.mp3, *.wav)|*.mp3;*.wav",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
                Multiselect = true
            };

            if (dialog.ShowDialog() == true)
            {
                foreach (var name in dialog.FileNames)
                {
                    _queue.Add(name);
                }
            }
        }

        private void ButtonShuffle_Click(object sender, RoutedEventArgs e)
        {
            if (_isShuffleEnabled)
            {
                ButtonShuffle.Content = this.Resources["icon_random"] as DrawingImage;
                _isShuffleEnabled = false;
                if (_originalQueue is not null)
                {
                    _queue = _originalQueue;
                    QueueMedia.ItemsSource = _queue;
                }

                return;
            }

            _isShuffleEnabled = true;
            _originalQueue = new ObservableCollection<string>(_queue);
            var selectedItem = QueueMedia.SelectedItem as string;
            _queue.Shuffle();
            QueueMedia.SelectedItem = selectedItem;

            ButtonShuffle.Content = this.Resources["icon_random_on"] as DrawingImage;
        }

        private void WindowMain_Closed(object sender, EventArgs e)
        {
            var path = Path.Combine(Directory.GetParent(Environment.CurrentDirectory).Parent.Parent.FullName,
                                            "Data", "savedState.txt");
            if (QueueMedia.Items.Count == 0)
                return;
            using StreamWriter writer = File.CreateText(path);
            // Write playlist name
            if (_currentPlaylist != null)
            {
                writer.WriteLine(_currentPlaylist);
            }
            else
            {
                writer.WriteLine(-1);
            }

            // Write saved position
            if (MainMediaPlayer.Source is null)
            {
                writer.WriteLine(-1);
            }
            else
            {
                var source = Uri.UnescapeDataString(Path.GetFullPath(MainMediaPlayer.Source.AbsolutePath));
                var sourceIndex = _queue.IndexOf(source);
                if (sourceIndex < 0)
                {
                    writer.WriteLine(-1);
                }
                else
                {
                    writer.WriteLine($"{sourceIndex} {MainMediaPlayer.Position.TotalSeconds}");
                }
            }

            // Write current queue
            foreach (var item in _queue)
            {
                writer.WriteLine(item);
            }
        }

        private void ButtonClearQueue_Click(object sender, RoutedEventArgs e)
        {
            _queue.Clear();
        }

        private void ButtonNewPlaylist_Click(object sender, RoutedEventArgs e)
        {
            _currentPlaylist = null;
            var result = MessageBox.Show("Do you want to clear current queue?", "Queue clear", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                _queue.Clear();
            }
        }
    }

    public static class ThreadSafeRandom
    {
        [ThreadStatic] private static Random Local;

        public static Random ThisThreadsRandom
        {
            get { return Local ??= new Random(unchecked(Environment.TickCount * 31 + Environment.CurrentManagedThreadId)); }
        }
    }

    internal static class MyExtensions
    {
        public static void Shuffle<T>(this IList<T> list)
        {
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = ThreadSafeRandom.ThisThreadsRandom.Next(n + 1);
                (list[n], list[k]) = (list[k], list[n]);
            }
        }
    }
}