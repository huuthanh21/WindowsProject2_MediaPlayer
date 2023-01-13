using System;
using System.ComponentModel;
using System.Windows;
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

        public MainWindow()
        {
            InitializeComponent();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Update MediaState when media ended on its own
            MainMediaPlayer.MediaEnded += (sender, eventArgs) =>
            {
                MainMediaPlayer.Stop();
                IsPlaying = false;
                SeekBar.Value = 0;
            };

            MainMediaPlayer.Play();
            MainMediaPlayer.Stop();

            _timer = new DispatcherTimer
            {
                Interval = new TimeSpan(0, 0, 0, 0, 500)
            };
            _timer.Tick += Timer_Tick;
        }

        private void Play()
        {
            IsPlaying = true;
            MainMediaPlayer.Play();
            _timer.Start();
        }

        private void Pause()
        {
            IsPlaying = false;
            MainMediaPlayer.Pause();
            _timer.Stop();
        }

        private void Stop()
        {
            IsPlaying = false;
            MainMediaPlayer.Stop();
            _timer.Stop();
            SeekBar.Value = 0;
            TextblockCurrentTimestamp.Text = "00:00:00";
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            var position = MainMediaPlayer.Position;
            TextblockCurrentTimestamp.Text = string.Format("{0:00}:{1:00}:{2:00}", position.Hours, position.Minutes, position.Seconds);
        }

        private void ButtonPlay_Click(object sender, RoutedEventArgs e)
        {
            if (!IsPlaying) Play();
        }

        private void ButtonPause_Click(object sender, RoutedEventArgs e)
        {
            switch (IsPlaying)
            {
                case true:
                    Pause();
                    break;

                default:
                    Play();
                    break;
            }
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
            Timer_Tick(sender, e);
        }

        private void MainMediaPlayer_MediaOpened(object sender, RoutedEventArgs e)
        {
            MediaTimeSpan = MainMediaPlayer.NaturalDuration.TimeSpan;

            SeekBar.Maximum = MainMediaPlayer.NaturalDuration.TimeSpan.TotalSeconds;
        }

        private void SeekBar_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
        {
            // Use this instead of Pause() so that when drag ended we can use IsPlaying to determine
            // if the video should be continued to play
            MainMediaPlayer.Pause();
            MainMediaPlayer.Volume = 0;
        }

        private void SeekBar_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            // Continue to play when seek while media is playing
            if (IsPlaying)
            {
                MainMediaPlayer.Play();
            }
            MainMediaPlayer.Volume = 0.5;
        }
    }
}