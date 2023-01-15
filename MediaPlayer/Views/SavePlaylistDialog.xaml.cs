using System;
using System.IO;
using System.Windows;

namespace MediaPlayer.Views
{
    /// <summary>
    /// Interaction logic for SavePlaylistDialog.xaml
    /// </summary>
    public partial class SavePlaylistDialog : Window
    {
        public string PlaylistName
        {
            get
            {
                return TextboxInput.Text;
            }
        }

        public string PlaylistPath
        {
            get
            {
                return Path.Combine(Directory.GetParent(Environment.CurrentDirectory).Parent.Parent.FullName,
                                    "Playlists",
                                    PlaylistName + ".txt");
            }
        }

        public SavePlaylistDialog()
        {
            InitializeComponent();

            TextboxInput.Text = "Untitled";
        }

        private void ButtonSave_Click(object sender, RoutedEventArgs e)
        {
            if (PlaylistName == null)
            {
                MessageBox.Show("Name cannot be empty", "Invalid name", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (File.Exists(PlaylistPath))
            {
                MessageBox.Show("Playlist with that name already exists", "Invalid name", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            this.DialogResult = true;
        }

        private void Window_ContentRendered(object sender, EventArgs e)
        {
            TextboxInput.SelectAll();
            TextboxInput.Focus();
        }
    }
}