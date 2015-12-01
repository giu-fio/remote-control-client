using Client.args;
using Client.connessione;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.InteropServices;
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
using System.ComponentModel;
using System.Diagnostics;

namespace Client
{
    /// <summary>
    /// Logica di interazione per ApplicationWindow.xaml
    /// </summary>
    public partial class ApplicationWindow : Window
    {
        #region DLL_mouse

        //serve per ottenere la posizione del mouse
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out PointAPI lpPoint);

        struct PointAPI
        {
            public int X;
            public int Y;
        }

        private BackgroundWorker backgroundWorkerCaptureMouse;

        #endregion

        public static RoutedCommand SwitchServer = new RoutedCommand();
        public static RoutedCommand RemoteCopy = new RoutedCommand();
        public static RoutedCommand RemotePaste = new RoutedCommand();
        public static RoutedCommand CloseWindow = new RoutedCommand();

        private Dictionary<String, KeyGesture> mapHotkey;
        private ObservableCollection<Server> servers;//lista dei server connessi
        private List<ServerTileUserControl> serverTiles;//Lista delle card che rappresentano i server connessi
        private List<string> colors;//background delle card dei server
        private IConnection connection;
        private bool captureMouseActivated;

        private volatile int selectedIndex;//indice del server attivo

        public ApplicationWindow(Dictionary<String, KeyGesture> mappaHotKey, IConnection connection)
        {
            InitializeComponent();
            aggiungiHandler();
            this.connection = connection;
            connection.ServerActivated += connection_ServerActivated;
            connection.ServerDeactivated += connection_ServerDeactivated;
            connection.ServerDisconnected += connection_ServerDisconnected;
            connection.ServerError += connection_ServerError;
            //cosi creo la lista dei server
            createListServer(connection.getServers());
            this.serverTiles = new List<ServerTileUserControl>();
            InitColors();
            this.mapHotkey = mappaHotKey;
            LoadServerTiles(servers);
            
            RegistraHotkey(mappaHotKey);

            backgroundWorkerCaptureMouse = new BackgroundWorker();
            backgroundWorkerCaptureMouse.DoWork += backgroundWorkerCaptureMouse_DoWork;
            backgroundWorkerCaptureMouse.WorkerSupportsCancellation = true;

        }

        private void createListServer(List<ConnectionServer> serverConnessi)
        {
            this.servers = new ObservableCollection<Server>();
            foreach (ConnectionServer cs in serverConnessi)
            {
                servers.Add(cs.Server);
            }
        }

        private void aggiungiHandler()
        {
            AddHandler(FrameworkElement.MouseDownEvent, new MouseButtonEventHandler(Grid_MouseDown));
            AddHandler(FrameworkElement.MouseUpEvent, new MouseButtonEventHandler(activeWindow_MouseUp));
            AddHandler(FrameworkElement.MouseWheelEvent, new MouseWheelEventHandler(activeWindow_MouseWheel));
            AddHandler(FrameworkElement.KeyDownEvent, new KeyEventHandler(activeWindow_KeyDown));
            AddHandler(FrameworkElement.KeyUpEvent, new KeyEventHandler(activeWindow_KeyUp));
        }

        #region initialization

        private void LoadServerTiles(ObservableCollection<Server> servers)
        {
            int index = 0;
            BrushConverter bc = new BrushConverter();
            foreach (var server in servers)
            {
                var serverTile = new ServerTileUserControl(server.Name, index + 1, (Brush)bc.ConvertFromString(colors[index]));
                serverTile.Width = 150;
                serverTile.Height = 150;
                serverTile.Margin = new Thickness(7);
                string effect = (index == 0) ? "z-depth4" : "z-depth1";
                serverTile.Effect = (System.Windows.Media.Effects.Effect)this.Resources[effect];
                serverTiles.Add(serverTile);
                stackPanel.Children.Add(serverTile);
                index++;
            }
        }

        private void InitColors()
        {
            colors = new List<string>();

            colors.Add("#E0F2F1");
            colors.Add("#B2DFDB");
            colors.Add("#80CBC4");
            colors.Add("#4DB6AC");
            colors.Add("#26A69A");
            colors.Add("#009688");
            colors.Add("#00897B");

        }

        private void RegistraHotkey(Dictionary<String, KeyGesture> mappaHotKey)
        {
            KeyBinding remoteCopyKeyBinding = new KeyBinding(RemoteCopy, mappaHotKey["Remote Copy"]);
            InputBindings.Add(remoteCopyKeyBinding);

            KeyBinding remotePasteKeyBinding = new KeyBinding(RemotePaste, mappaHotKey["Remote Paste"]);
            InputBindings.Add(remotePasteKeyBinding);

            KeyBinding closeWindowKeyBinding = new KeyBinding(CloseWindow, mappaHotKey["Close Control Window"]);
            InputBindings.Add(closeWindowKeyBinding);

            KeyBinding switchServerKeyBinding = new KeyBinding(SwitchServer, mappaHotKey["Switch Server"]);
            switchServerKeyBinding.CommandParameter = -1;
            InputBindings.Add(switchServerKeyBinding);

            for (int i = 1; i <= servers.Count; i++)
            {
                KeyBinding switchServerKeyBinding2 = new KeyBinding(SwitchServer, new KeyGesture((Key)Enum.Parse(typeof(Key), "D" + i), ModifierKeys.Control));
                switchServerKeyBinding2.CommandParameter = i - 1;
                InputBindings.Add(switchServerKeyBinding2);

                KeyBinding switchServerKeyBinding3 = new KeyBinding(SwitchServer, new KeyGesture((Key)Enum.Parse(typeof(Key), "NumPad" + i), ModifierKeys.Control));
                switchServerKeyBinding3.CommandParameter = i - 1;
                InputBindings.Add(switchServerKeyBinding3);

            }

        }

        #endregion

        #region events_Connection


        private void connection_ServerDeactivated(object sender, args.ServerEventArgs e)
        {
            App.Current.Dispatcher.BeginInvoke((Action)delegate()
             {    //deseleziono la card 
                 serverTiles[e.Position].Effect = (System.Windows.Media.Effects.Effect)this.Resources["z-depth1"];
                 backgroundWorkerCaptureMouse.CancelAsync();
             });
        }


        private void connection_ServerActivated(object sender, args.ServerEventArgs e)
        {
            App.Current.Dispatcher.BeginInvoke((Action)delegate()
             {
                 //seleziono il server attivo
                 selectedIndex = e.Position;
                 serverTiles[selectedIndex].Effect = (System.Windows.Media.Effects.Effect)this.Resources["z-depth5"];
                 backgroundWorkerCaptureMouse.RunWorkerAsync();
             });
        }


        void connection_ServerDisconnected(object sender, args.ServerEventArgs e)
        {
            App.Current.Dispatcher.BeginInvoke((Action)delegate()
           {
               //rimuovo dalla lista dei server
               servers.RemoveAt(e.Position);
               //rimuovo la tile
               ServerTileUserControl tile = serverTiles[e.Position];
               stackPanel.Children.Remove(tile);
               serverTiles.RemoveAt(e.Position);

               if (servers.Count == 0)
               {
                   this.Close();
               }
           });
        }

        void connection_ServerError(object sender, args.ServerErrorEventsArgs e)
        {
            switch (e.ErrorCode)
            {
                case ServerErrorEventsArgs.SERVER_ERROR:
                    ErrorMessage("Problema con la rete del Server " + e.Server.Name);
                    App.Current.Dispatcher.BeginInvoke((Action)delegate()
                    {
                        //rimuovo dalla lista dei server
                        servers.RemoveAt(e.Position);
                        //rimuovo la tile
                        ServerTileUserControl tile = serverTiles[e.Position];
                        stackPanel.Children.Remove(tile);
                        serverTiles.RemoveAt(e.Position);

                        if (servers.Count == 0)
                        {
                            this.Close();
                        }

                    });
                    break;
                case ServerErrorEventsArgs.NETWORK_ERROR:
                    ErrorMessage("Connessione assente!");
                    this.Close();
                    break;
                case ServerErrorEventsArgs.KEEP_ALIVE_NOT_RECEIVED:
                    ErrorMessage("Problema con la rete del Server " + e.Server.Name);
                    App.Current.Dispatcher.BeginInvoke((Action)delegate()
                    {
                        //rimuovo dalla lista dei server
                        servers.RemoveAt(e.Position);
                        //rimuovo la tile
                        ServerTileUserControl tile = serverTiles[e.Position];
                        stackPanel.Children.Remove(tile);
                        serverTiles.RemoveAt(e.Position);

                        if (servers.Count == 0)
                        {
                            this.Close();
                        }

                    });
                    break;
                default:
                    ErrorMessage("Si è verificato un errore inatteso");
                    this.Close();
                    break;

            }

        }


        #endregion

        #region events_Mouse

        void backgroundWorkerCaptureMouse_DoWork(object sender, System.ComponentModel.DoWorkEventArgs e)
        {
            System.ComponentModel.BackgroundWorker work = sender as System.ComponentModel.BackgroundWorker;
            int X = 1;
            int Y = 1;
            while (!work.CancellationPending)
            {
                PointAPI p = MoveCursor(X, Y);
                X = p.X;
                Y = p.Y;
            }
        }

        private PointAPI MoveCursor(int X, int Y)
        {
            PointAPI p = new PointAPI();
            GetCursorPos(out p);
            int newX = p.X;
            int newY = p.Y;
            if (newX != X || newY != Y)
            {
                X = (int)newX;
                Y = (int)newY;
                connection.SendMoveMouse(X, Y);
            }
            return p;
        }

        private void Grid_MouseDown(object sender, MouseButtonEventArgs e)
        {
            MouseClickType msg;
            e.Handled = true;
            switch (e.ChangedButton)
            {

                case MouseButton.Left:
                    msg = MouseClickType.LEFT_DOWN;
                    connection.SendMouseClick(msg);
                    //if (e.ClickCount == 2)
                    //{
                    //    connection.SendMouseClick(msg);
                    //}
                    break;

                case MouseButton.Middle:
                    msg = MouseClickType.MIDDLE_DOWN;
                    connection.SendMouseClick(msg);
                    break;

                case MouseButton.Right:
                    msg = MouseClickType.RIGHT_DOWN;
                    connection.SendMouseClick(msg);
                    break;

                default:
                    break;
            }
        }

        private void activeWindow_MouseUp(object sender, MouseButtonEventArgs e)
        {
            MouseClickType msg;
            e.Handled = true;
            switch (e.ChangedButton)
            {
                case MouseButton.Left:
                    msg = MouseClickType.LEFT_UP;
                    Debug.WriteLine("click up");
                    connection.SendMouseClick(msg);
                    break;

                case MouseButton.Middle:
                    msg = MouseClickType.MIDDLE_UP;
                    connection.SendMouseClick(msg);
                    break;

                case MouseButton.Right:
                    msg = MouseClickType.RIGHT_UP;
                    connection.SendMouseClick(msg);
                    break;

                default:
                    break;
            }
        }

        private void activeWindow_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            e.Handled = true;
            connection.SendMouseScroll(e.Delta);
        }

        #endregion

        #region keyboard

        private void activeWindow_KeyDown(object sender, KeyEventArgs e)
        {
            Key key = (e.Key == Key.System ? e.SystemKey : e.Key);
            connection.SendKeyDown(key);
        }

        private void activeWindow_KeyUp(object sender, KeyEventArgs e)
        {
            Key key = (e.Key == Key.System ? e.SystemKey : e.Key);
            connection.SendKeyUp(key);
        }

        #endregion

        #region event_Hotkey

        private void SwitchServer_CanExecute(Object sender, CanExecuteRoutedEventArgs e)
        {

            e.CanExecute = true;
        }

        private void SwitchServer_Execute(Object sender, ExecutedRoutedEventArgs e)
        {
            backgroundWorkerCaptureMouse.CancelAsync();
            //prima forzo il KeyUp dei tasti per i tasti coinvolti dall'HotKey
            Key k = mapHotkey["Switch Server"].Key;
            String modifierKeys = mapHotkey["Switch Server"].Modifiers.ToString();
            String[] modifiers = modifierKeys.Split(',');
            Key[] mk = new Key[modifiers.Length];
            for (int j = 0; j < modifiers.Length; j++)
            {
                String modifier = modifiers[j].Trim();
                switch (modifier)
                {
                    case "Control":
                        mk[j] = Key.LeftCtrl;
                        break;
                    case "Shift":
                        mk[j] = Key.LeftShift;
                        break;
                    default:
                        mk[j] = Key.LeftAlt;
                        break;
                }
            }
            //prima invio il tasto
            connection.SendKeyUp(k);
            //poi invio i modifier keys
            for (int j = mk.Length - 1; j >= 0; j--)
            {
                connection.SendKeyUp(mk[j]);
            }

            int i = (int)e.Parameter;
            if (i < 0)
            {
                Select((selectedIndex + 1) % servers.Count);
            }
            else
            {
                Select(i);
            }
        }

        private void RemoteCopy_CanExecute(Object sender, CanExecuteRoutedEventArgs e)
        {
            Console.WriteLine("can exexute ss");
            e.CanExecute = true;
        }

        private void RemoteCopy_Execute(Object sender, ExecutedRoutedEventArgs e)
        {
            Console.WriteLine("Exexute");
            MessageBox.Show("AMALA PAZZA INTER AMALA");
        }

        private void RemotePaste_CanExecute(Object sender, CanExecuteRoutedEventArgs e)
        {
            Console.WriteLine("can exexute ss");
            e.CanExecute = true;
        }

        private void RemotePaste_Execute(Object sender, ExecutedRoutedEventArgs e)
        {
            Console.WriteLine("Exexute");
            MessageBox.Show("AMALA PAZZA INTER AMALA");
        }

        private void CloseWindowCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }

        private void CloseWindowCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            //prima forzo il KeyUp dei tasti per i tasti coinvolti dall'HotKey
            Key k = mapHotkey["Close Control Window"].Key;
            String modifierKeys = mapHotkey["Close Control Window"].Modifiers.ToString();
            String[] modifiers = modifierKeys.Split(',');
            Key[] mk = new Key[modifiers.Length];
            for (int j = 0; j < modifiers.Length; j++)
            {
                String modifier = modifiers[j].Trim();
                switch (modifier)
                {
                    case "Control":
                        mk[j] = Key.LeftCtrl;
                        break;
                    case "Shift":
                        mk[j] = Key.LeftShift;
                        break;
                    default:
                        mk[j] = Key.LeftAlt;
                        break;
                }
            }
            //prima invio il tasto
            connection.SendKeyUp(k);
            //poi invio i modifier keys
            for (int j = mk.Length - 1; j >= 0; j--)
            {
                connection.SendKeyUp(mk[j]);
            }
            this.Close();
        }


        #endregion


        /// <summary>
        /// Rende attivo il server in posizione "index".
        /// </summary>
        /// <param name="index">Posizione del server da attivare</param>
        private void Select(int index)
        {
            //Il metodo dovrà solo chiamare active server di Connection.
            //Ci sarà un evento nel caso in cui l'attivazione avviene correttamente e in quel caso si cambierà la profondità della card e si aggiornerà il selectedIndex

            if (index >= serverTiles.Count || index < 0)
            {
                throw new ArgumentOutOfRangeException("index:" + index + " #server:" + serverTiles.Count);
            }

            connection.Active(index);
        }

        private void ErrorMessage(String message)
        {
            string caption = "ERROR!";
            MessageBoxButton buttons = MessageBoxButton.OK;
            MessageBoxImage icon = MessageBoxImage.Error;
            MessageBox.Show(message, caption, buttons, icon);

        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            backgroundWorkerCaptureMouse.CancelAsync();
            connection.ServerActivated -= connection_ServerActivated;
            connection.ServerDeactivated -= connection_ServerDeactivated;
            connection.ServerDisconnected -= connection_ServerDisconnected;
            connection.ServerError -= connection_ServerError;
            connection.Stop();
        }

    }
}
