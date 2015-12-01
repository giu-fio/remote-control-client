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
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;


namespace Client
{
    /// <summary>
    /// Logica di interazione per ServerTileUserControl.xaml
    /// </summary>
    public partial class ServerTileUserControl : UserControl
    {
        private string nomeServer;
        private int index;
        private Brush color;

        public ServerTileUserControl(string nomeServer, int index, Brush color)
        {
            this.nomeServer = nomeServer;
            this.index = index;
            this.color = color;
            InitializeComponent();
        }

        public string NomeServer
        {
            get { return nomeServer; }
            set { nomeServer = value; }
        }

        public int Index
        {
            get { return index; }
            set { index = value; }
        }

        public Brush Color
        {
            get { return color; }
            set { color = value; }
        }

        public void ProgressCompleted()
        {

            DoubleAnimation animationDisappearBar = new DoubleAnimation();
            animationDisappearBar.From = 1;
            animationDisappearBar.To = 0;
            // animationDisappearBar.BeginTime = TimeSpan.FromSeconds(3);
            animationDisappearBar.Duration = new Duration(TimeSpan.FromSeconds(1));
            downloadPanel.BeginAnimation(OpacityProperty, animationDisappearBar);


            DoubleAnimation animationAppear = new DoubleAnimation();
            animationAppear.From = 0;
            animationAppear.To = 1;
            animationAppear.Duration = new Duration(TimeSpan.FromSeconds(1));
            animationAppear.Completed += animationAppear_Completed;
            completedImage.BeginAnimation(OpacityProperty, animationAppear);
        }

        public void StartProgress()
        {
            progressBar.Value = 0;
            DoubleAnimation animationDisappearBar = new DoubleAnimation();
            animationDisappearBar.From = 0;
            animationDisappearBar.To = 1;
            // animationDisappearBar.BeginTime = TimeSpan.FromSeconds(3);
            animationDisappearBar.Duration = new Duration(TimeSpan.FromSeconds(1));
            downloadPanel.BeginAnimation(OpacityProperty, animationDisappearBar);
        }

        public void CancelProgress()
        {
            DoubleAnimation animationDisappearBar = new DoubleAnimation();
            animationDisappearBar.From = 1;
            animationDisappearBar.To = 0;
            // animationDisappearBar.BeginTime = TimeSpan.FromSeconds(3);
            animationDisappearBar.Duration = new Duration(TimeSpan.FromSeconds(1));
            downloadPanel.BeginAnimation(OpacityProperty, animationDisappearBar);
        }

        void animationAppear_Completed(object sender, EventArgs e)
        {
            DoubleAnimation animationDisappear = new DoubleAnimation();
            animationDisappear.From = 1;
            animationDisappear.To = 0;
            // animationDisappear.BeginTime = TimeSpan.FromSeconds(3);
            animationDisappear.Duration = new Duration(TimeSpan.FromSeconds(1));
            completedImage.BeginAnimation(OpacityProperty, animationDisappear);
        }


        
    }



}
