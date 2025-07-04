using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Character_Animator
{
    /// <summary>
    /// Interaction logic for RenameDialog.xaml
    /// </summary>
    public partial class RenameDialog : Window
    {
        public string AnimationName => NameBox.Text.Trim();

        public RenameDialog(string currentName = "")
        {
            InitializeComponent();
            NameBox.Text = currentName;
            NameBox.SelectAll();
            NameBox.Focus();
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(AnimationName))
            {
                DialogResult = true;
                Close();
            }
            else
            {
                MessageBox.Show("Name cannot be empty.", "Invalid", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
