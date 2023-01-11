using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace MediaPlayerApp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Update MediaState when media ended on its own
            MainMediaPlayer.MediaEnded += (sender, eventArgs) =>
            {
                MainMediaPlayer.Stop();
            };

            MainMediaPlayer.Play();
            MainMediaPlayer.Stop();
        }

        private static MediaState GetMediaState(MediaElement myMedia)
        {
            FieldInfo hlp = typeof(MediaElement).GetField("_helper", BindingFlags.NonPublic | BindingFlags.Instance);
            object helperObject = hlp.GetValue(myMedia);
            FieldInfo stateField = helperObject.GetType().GetField("_currentState", BindingFlags.NonPublic | BindingFlags.Instance);
            MediaState state = (MediaState)stateField.GetValue(helperObject);
            return state;
        }

        private void ButtonPlay_Click(object sender, RoutedEventArgs e)
        {
            MainMediaPlayer.Play();
        }

        private void ButtonPause_Click(object sender, RoutedEventArgs e)
        {
            switch (GetMediaState(MainMediaPlayer))
            {
                case MediaState.Pause:
                    MainMediaPlayer.Play();
                    break;

                case MediaState.Stop:
                    MainMediaPlayer.Play();
                    break;

                default:
                    MainMediaPlayer.Pause();
                    break;
            }
        }

        private void ButtonStop_Click(object sender, RoutedEventArgs e)
        {
            MainMediaPlayer.Stop();
        }
    }
}