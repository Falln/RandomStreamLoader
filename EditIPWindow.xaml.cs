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

namespace RandomStreamLoader
{
    /// <summary>
    /// Interaction logic for EditIPWindow.xaml
    /// </summary>
    public partial class EditIPWindow : Window
    {
        public EditIPWindow(List<TV> tvList)
        {
            InitializeComponent();

            foreach (TV tv in tvList)
            {
                ComboBoxItem item = new ComboBoxItem();
                item.Tag = tv;
                item.Content = tv.tvName;
                tvSelectorCombo.Items.Add(item);
            }
        }

        private void okBtn_Click(object sender, RoutedEventArgs e)
        {
            //Get the selected TV
            ComboBoxItem selectedBox = tvSelectorCombo.SelectedItem as ComboBoxItem;
            TV selectedTV = (TV)selectedBox.Tag;

            //Add the new IP
            string newIP = tvIPBox.Text.Replace(" ", ""); //TODO Add something that checks if the inputted string is a valid IP format
            selectedTV.tvIP = newIP;
            Properties.Settings.Default[selectedTV.tvName.Replace(" ", "")] = newIP;
            Properties.Settings.Default.Save();

            //Close the window
            this.Close();
        }

        private void cancelBtn_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void tvSelectorCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ComboBoxItem selectedBox = tvSelectorCombo.SelectedItem as ComboBoxItem;
            TV selectedTV = (TV)selectedBox.Tag;

            tvIPBox.Text = selectedTV.tvIP;
        }
    }
}
