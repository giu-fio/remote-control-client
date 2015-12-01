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
using Client.connessione;
using System.Globalization;
using System.Net;

namespace Client
{
    /// <summary>
    /// Logica di interazione per AggiungiServerDialogBox.xaml
    /// </summary>
    public partial class AggiungiServerDialogBox : Window
    {

        private Server server;

        public AggiungiServerDialogBox()
        {
            InitializeComponent();           
            server = new Server(){Name="Server1", IP = "192.168.1.1", ControlPort=1500};
            
            textGrid.DataContext = server;
                
        }

               

        private void okButton_Click(object sender, RoutedEventArgs e)
        {
            if (!IsValid(this)) return;
            server.ClipboardPort =(short)( server.ControlPort + 1);   
       
            this.DialogResult = true;

        }

        // Validate all dependency objects in a window 
        bool IsValid(DependencyObject node)
        {
            // Check if dependency object was passed 
            if (node != null)
            {
                // Check if dependency object is valid. 
                // NOTE: Validation.GetHasError works for controls that have validation rules attached  
                bool isValid = !Validation.GetHasError(node);
                if (!isValid)
                {
                    // If the dependency object is invalid, and it can receive the focus, 
                    // set the focus 
                    if (node is IInputElement) Keyboard.Focus((IInputElement)node);
                    return false;
                }
            }

            // If this dependency object is valid, check all child dependency objects 
            foreach (object subnode in LogicalTreeHelper.GetChildren(node))
            {
                if (subnode is DependencyObject)
                {
                    // If a child dependency object is invalid, return false immediately, 
                    // otherwise keep checking 
                    if (IsValid((DependencyObject)subnode) == false) return false;
                }
            }

            // All dependency objects are valid 
            return true;

        }

        public Server Server
        {
            get { return server; }
        }
    }




}
