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

namespace Client
{
    /// <summary>
    /// Logica di interazione per InserisciPasswordDialogBox.xaml
    /// </summary>
    /// 
    public partial class InserisciPasswordDialogBox : Window
    {


        private String password;


        public InserisciPasswordDialogBox(String nomeServer)
        {
            InitializeComponent();
            serverNameTextBoxt.Text = nomeServer;


        }

        private void okButton_Click(object sender, RoutedEventArgs e)
        {            
            password = serverPasswordTextBoxt.Password;
            if (!String.IsNullOrEmpty(password))
            {
                this.DialogResult = true;
            }
            
        }

        public String Password
        {
            get { return password; }

        }



    }
}
