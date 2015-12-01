using Client.args;
using Client.connessione;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;

namespace Client
{
    /// <summary>
    /// Logica di interazione per MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        #region Variabili di istanza
        // TabServer
        private ObservableCollection<Server> serverTrovati;
        private ObservableCollection<Server> serverAggiunti;
        private IConnection connection;

        // TabHotkey
        private Dictionary<ModifierKeys, List<Key>> mappaEccezioni;
        private List<Key> listaTastiTmp;
        private List<ModifierKeys> listaModifierTmp;
        private Dictionary<String, KeyGesture> mappaHotKey;
        private ObservableCollection<KeyValuePair<String, String>> hotKeyList;
        private ObservableCollection<object> firstKeyComboBoxList;
        private ObservableCollection<object> secondKeyComboBoxList;
        private ObservableCollection<object> thirdKeyComboBoxList;

        #endregion

        public MainWindow()
        {
            InitializeComponent();
            #region TabServer Init
            //ListView 
            serverTrovati = new ObservableCollection<Server>();
            serverTrovatiListView.ItemsSource = serverTrovati;
            serverAggiunti = new ObservableCollection<Server>();
            serverListView.ItemsSource = serverAggiunti;
            //Connection
            connection = new Connection();
            //registro agli eventi di connection
            connection.ServerFound += connection_ServerFound;
            connection.ServerAuthRequired += connection_ServerAuthRequired;
            connection.ServerConnected += connection_ServerConnected;
            connection.ServerDisconnected += connection_ServerDisconnected;
            connection.ServerError += connection_ServerError;
            connection.RefreshConnections += connection_RefreshConnections;
            connection.SendBroadcast();
            #endregion

            #region TabHotKey Init
            //ListView           
            hotKeyList = new ObservableCollection<KeyValuePair<string, string>>();
            hotkeyListView.ItemsSource = hotKeyList;

            //ComboBox
            InitFirstKeyComboBoxList();
            InitSecondAndThirdKeyComboBoxList();

            firstKeyComboBox.ItemsSource = new ObservableCollection<object>(firstKeyComboBoxList);
            secondKeyComboBox.ItemsSource = new ObservableCollection<object>(secondKeyComboBoxList);
            thirdKeyComboBox.ItemsSource = new ObservableCollection<object>(thirdKeyComboBoxList);


            //other            
            thirdKeyComboBox.IsEnabled = false;
            listaModifierTmp = caricaListaModifier();
            listaTastiTmp = caricaListaTasti();
            generaListaEccezioni();
            mappaHotKey = caricaMappaHotKey();


            foreach (var entry in mappaHotKey)
            {
                KeyValuePair<String, String> listEntry = new KeyValuePair<string, string>(entry.Key, HotKeyToString(entry.Value));
                hotKeyList.Add(listEntry);
            }
            SelectedLabel.Content = hotKeyList[0].Key;

            # endregion
        }

        #region TabServer

        #region Event receivers di connection

        //viene invocato questo metodo per aggiornare la lista dei server connessi,
        //nel caso in cui qualcuno viene a cadere a causa di qualche eccezione
        void connection_RefreshConnections(object sender, EventArgs e)
        {

            //prendo la lista dei server connessi e li aggiungo ai serverTrovati
            List<ServerConnection> listServerConnessi = connection.getServers();
            serverAggiunti.Clear();
            foreach (ServerConnection cs in listServerConnessi)
            {
                serverAggiunti.Add(cs.Server);
            }

            if (serverAggiunti.Count == 0)
            {
                connection.ServerError += connection_ServerError;
            }
        }

        void connection_ServerFound(object sender, args.ServerEventArgs e)
        {
            Console.WriteLine("server aggiunto");
            Server server = e.Server;

            if (!serverTrovati.Contains(e.Server))
            {
                serverTrovati.Add(server);
            }
        }

        void connection_ServerDisconnected(object sender, args.ServerEventArgs e)
        {
            //lo rimuova dalla lista sotto
            serverAggiunti.Remove(e.Server);
            //lo inserisce nella lista sopra
            if (!serverTrovati.Contains(e.Server))
            {
                serverTrovati.Add(e.Server);
            }
        }

        void connection_ServerConnected(object sender, args.ServerEventArgs e)
        {
            //lo rimuovo dalla lista dei server trovati(lista in alto)
            serverTrovati.Remove(e.Server);
            //lo aggiungo alla lista dei server connessi(lista in basso)
            serverAggiunti.Add(e.Server);

        }

        void connection_ServerAuthRequired(object sender, args.ServerEventArgs e)
        {
            Server server = e.Server;
            InserisciPasswordDialogBox ipDialogBox = new InserisciPasswordDialogBox(server.Name);
            ipDialogBox.Owner = this;
            ipDialogBox.ShowDialog();
            if (ipDialogBox.DialogResult.HasValue && (bool)ipDialogBox.DialogResult)
            {
                //Password inserita                     
                string password = ipDialogBox.Password;
                server.Password = password;
                connection.Connect(ref server);
            }
        }

        void connection_ServerError(object sender, args.ServerErrorEventsArgs e)
        {
            switch (e.ErrorCode)
            {
                case ServerErrorEventsArgs.SERVER_ERROR:
                    ErrorMessage("Problema con la rete del Server " + e.Server.Name);
                    serverAggiunti.Remove(e.Server);
                    serverTrovati.Remove(e.Server);
                    break;
                case ServerErrorEventsArgs.NETWORK_ERROR:
                    ErrorMessage("Connessione assente!");
                    serverAggiunti.Clear();
                    serverTrovati.Clear();
                    break;
                case ServerErrorEventsArgs.KEEP_ALIVE_NOT_RECEIVED:
                    ErrorMessage("Problemi di connessione col Server " + e.Server.Name);
                    serverAggiunti.Remove(e.Server);
                    serverTrovati.Remove(e.Server);
                    break;
                case ServerErrorEventsArgs.LOGIN_ERROR:
                    ErrorMessage("Errore nella fase di LOGIN col server:\n " + e.Server.Name);
                    break;
                case ServerErrorEventsArgs.PASSWORD_ERROR:
                    ErrorMessage("Password errata!");
                    break;
                default:
                    ErrorMessage("Si è verificato un errore inatteso");
                    break;
            }
        }

        #endregion

        #region Button click action

        private void aggiornaButton_Click(object sender, RoutedEventArgs e)
        {
            if (isConnectedToInternet())
            {
                //Svuoto la lista dei server trovati
                serverTrovati.Clear();
                connection.SendBroadcast();
            }
            else
            {
                ErrorMessage("Connessione Assente!");
            }

        }//aggiornaButton_Click

        private void aggiungiButton_Click(object sender, RoutedEventArgs e)
        {
            if (isConnectedToInternet())
            {
                Server server = (Server)serverTrovatiListView.SelectedItem;
                if (server != null)
                {
                    connection.Connect(ref server);
                }
            }
            else
            {
                ErrorMessage("Connessione Assente!");
            }
        }//aggiungiButton_Click

        private void plusButton_Click(object sender, RoutedEventArgs e)
        {
            AggiungiServerDialogBox asDialogBox = new AggiungiServerDialogBox();
            asDialogBox.Owner = this;
            asDialogBox.ShowDialog();
            if (asDialogBox.DialogResult.HasValue && (bool)asDialogBox.DialogResult)
            {
                if (!serverTrovati.Contains(asDialogBox.Server))
                {
                    serverTrovati.Add(asDialogBox.Server);
                }
            }
        }

        private void upButton_Click(object sender, RoutedEventArgs e)
        {
            var servers = serverAggiunti;
            Server selected = (Server)serverListView.SelectedItem;
            if (selected == null)
            {
                return;
            }
            int index = servers.IndexOf(selected);
            if (index != 0)
            {
                var tmp = servers[index];
                servers[index] = servers[index - 1];
                servers[index - 1] = tmp;
                serverListView.SelectedItem = tmp;
                connection.SwitchPosition(index, index - 1);
            }
        }

        private void downButton_Click(object sender, RoutedEventArgs e)
        {
            if (isConnectedToInternet())
            {
                var servers = serverAggiunti;
                Server selected = (Server)serverListView.SelectedItem;
                if (selected == null) { return; }
                int index = servers.IndexOf(selected);
                if (index != servers.Count - 1)
                {
                    var tmp = servers[index];
                    servers[index] = servers[index + 1];
                    servers[index + 1] = tmp;
                    serverListView.SelectedItem = tmp;
                    connection.SwitchPosition(index, index + 1);
                }
            }
            else
            {
                ErrorMessage("Connessione Assente!");
            }
        }

        private void rimuoviButton_Click(object sender, RoutedEventArgs e)
        {            
            if (isConnectedToInternet())
            {
                var servers = serverListView.SelectedItems;
                foreach (Server s in servers)
                {
                    connection.Disconnect(serverAggiunti.IndexOf(s));
                }
                if (serverAggiunti.Count == 1)
                {
                    connection.ServerError -= connection_ServerError;
                }
                
            }
            else
            {
                ErrorMessage("Connessione Assente!");
            }
            

        }//rimuoviButton_Click

        private void connettiButton_Click(object sender, RoutedEventArgs e)
        {
            if (isConnectedToInternet())
            {
                if (serverAggiunti.Count > 0)
                {
                    connection.Start();
                    ApplicationWindow applicationWindow = new ApplicationWindow(mappaHotKey, connection);
                    applicationWindow.Show();
                    connection.ServerError -= connection_ServerError;
                }
            }
            else
            {
                ErrorMessage("Connessione Assente!");
            }

        }//connettiButton_Click

        #endregion

        #region Private method

        private void ErrorMessage(String message)
        {
            string caption = "ERROR!";
            MessageBoxButton buttons = MessageBoxButton.OK;
            MessageBoxImage icon = MessageBoxImage.Error;
            MessageBox.Show(message, caption, buttons, icon);
        }

        private bool isConnectedToInternet()
        {
            return System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable();
        }

        #endregion

        #endregion

        #region TabHotkey

        #region Private method

        private void InitSecondAndThirdKeyComboBoxList()
        {
            secondKeyComboBoxList = new ObservableCollection<object>();
            thirdKeyComboBoxList = new ObservableCollection<object>();
            List<ModifierKeys> listaModifier = caricaListaModifier();
            foreach (ModifierKeys tasto in listaModifier)
            {
                secondKeyComboBoxList.Add(tasto);
            }

            List<Key> lista = caricaListaTasti();
            foreach (Key tasto in lista)
            {
                secondKeyComboBoxList.Add(tasto);
                thirdKeyComboBoxList.Add(tasto);
            }

            if (firstKeyComboBox.SelectedItem != null && firstKeyComboBox.SelectedItem.Equals(ModifierKeys.Control))
            {
                String tastoDaEliminare = "D";
                for (int i = 0; i < 10; i++)
                {
                    string tmp = tastoDaEliminare + i;
                    secondKeyComboBoxList.Remove(Enum.Parse(typeof(Key), tmp));
                    thirdKeyComboBoxList.Remove(Enum.Parse(typeof(Key), tmp));
                }
            }


        }
        private void InitFirstKeyComboBoxList()
        {
            firstKeyComboBoxList = new ObservableCollection<object>();
            firstKeyComboBoxList.Add(ModifierKeys.Control);
            firstKeyComboBoxList.Add(ModifierKeys.Alt);
        }
        private List<ModifierKeys> caricaListaModifier()
        {
            if (listaModifierTmp == null)
            {
                listaModifierTmp = new List<ModifierKeys> { ModifierKeys.Control, ModifierKeys.Alt, ModifierKeys.Shift };
            }
            return listaModifierTmp;
        }
        private List<Key> caricaListaTasti()
        {
            if (listaTastiTmp == null)
            {
                listaTastiTmp = new List<Key> { Key.D0, Key.D1, Key.D2, Key.D3, Key.D4, Key.D5, Key.D6, Key.D7, Key.D8, Key.D9, Key.A, Key.B, Key.C, Key.D, Key.E, Key.F, Key.G, Key.H, Key.I, Key.J, Key.K, Key.L, Key.M, Key.N, Key.O, Key.P, Key.Q, Key.R, Key.S, Key.T, Key.U, Key.V, Key.W, Key.X, Key.Y, Key.Z, Key.F1, Key.F2, Key.F3, Key.F4, Key.F5, Key.F6, Key.F7, Key.F8, Key.F9, Key.F10, Key.F11, Key.F12 };
            }
            return listaTastiTmp;
        }

        private void svuotaTutto()
        {
            secondKeyComboBox.SelectedIndex = -1;
            firstKeyComboBox.SelectedIndex = -1;
            thirdKeyComboBox.SelectedIndex = -1;

        }

        private void generaListaEccezioni()
        {
            mappaEccezioni = new Dictionary<ModifierKeys, List<Key>>();
            List<Key> listaTmp = new List<Key>() { Key.C, Key.X, Key.V, Key.N, Key.S, Key.O, Key.P, Key.Z, Key.A, Key.F, Key.F4 };
            mappaEccezioni.Add(ModifierKeys.Control, listaTmp);
            listaTmp = new List<Key>() { Key.F4 };
            mappaEccezioni.Add(ModifierKeys.Alt, listaTmp);
        }
        private string HotKeyToString(KeyGesture kg)
        {
            return kg.Modifiers + "+" + kg.Key;

        }


        private Dictionary<string, KeyGesture> caricaMappaHotKey()
        {
            StringCollection rc = Properties.Settings.Default["RemoteCopy"] as StringCollection;
            StringCollection rp = Properties.Settings.Default["RemotePaste"] as StringCollection;
            StringCollection cw = Properties.Settings.Default["CloseWindow"] as StringCollection;
            StringCollection ss = Properties.Settings.Default["SwitchServer"] as StringCollection;

            KeyGesture rcKeyGesture = KeyGestureFromStringCollection(rc);
            KeyGesture rpKeyGesture = KeyGestureFromStringCollection(rp);
            KeyGesture ssKeyGesture = KeyGestureFromStringCollection(ss);
            KeyGesture cwKeyGesture = KeyGestureFromStringCollection(cw);
            if (rcKeyGesture == null)
            {
                rcKeyGesture = new KeyGesture(Key.C, ModifierKeys.Control | ModifierKeys.Shift);
            }

            if (rpKeyGesture == null)
            {
                rpKeyGesture = new KeyGesture(Key.V, ModifierKeys.Control | ModifierKeys.Shift);
            }

            if (ssKeyGesture == null)
            {
                ssKeyGesture = new KeyGesture(Key.S, ModifierKeys.Control | ModifierKeys.Shift);
            }
            if (cwKeyGesture == null)
            {
                cwKeyGesture = new KeyGesture(Key.F4, ModifierKeys.Control | ModifierKeys.Shift);
            }

            var map = new Dictionary<string, KeyGesture>();
            map.Add("Remote Copy", rcKeyGesture);
            map.Add("Remote Paste", rpKeyGesture);
            map.Add("Switch Server", ssKeyGesture);
            map.Add("Close Control Window", cwKeyGesture);
            return map;

        }

        private KeyGesture KeyGestureFromStringCollection(StringCollection sc)
        {
            ModifierKeys mod1;
            ModifierKeys mod2;
            Key key;


            if (sc.Count == 2)
            {
                if (Enum.TryParse(sc[0], out mod1) && Enum.TryParse(sc[1], out key))
                {
                    return new KeyGesture(key, mod1);
                }

            }
            else if (sc.Count == 3)
            {
                if (Enum.TryParse(sc[0], out mod1) && Enum.TryParse(sc[1], out mod2) && Enum.TryParse(sc[2], out key))
                {
                    return new KeyGesture(key, mod1 | mod2);
                }

            }

            return null;
        }
        private StringCollection StringCollectionFromKeyGesture(KeyGesture kg)
        {
            if (kg == null)
            {
                throw new ArgumentNullException("Il parametro KeyGesture è null");
            }

            StringCollection sc = new StringCollection();

            String modifiers = kg.Modifiers.ToString();
            String[] m = modifiers.Split();
            sc.Add(m[0].Trim());
            if (m.Length == 2)
            {
                sc.Add(m[1].Trim());
            }
            sc.Add(kg.Key.ToString());

            return sc;



        }

        #endregion

        #region SelectionChanged

        private void firstKeyComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {

            secondKeyComboBox.SelectedIndex = -1;
            ObservableCollection<object> newKeys = new ObservableCollection<object>(secondKeyComboBoxList);
            newKeys.Remove(firstKeyComboBox.SelectedItem);
            secondKeyComboBox.ItemsSource = newKeys;
            thirdKeyComboBox.IsEnabled = false;


        }
        private void secondKeyComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (secondKeyComboBox.SelectedItem != null)
            {

                if (secondKeyComboBox.SelectedItem.GetType().ToString().Equals("System.Windows.Input.ModifierKeys"))
                {
                    //se il secondo tasto selezionato è un Modifier abilito anche il terzo tasto
                    thirdKeyComboBox.IsEnabled = true;
                    ObservableCollection<object> newKeys = new ObservableCollection<object>(thirdKeyComboBoxList);
                    thirdKeyComboBox.ItemsSource = newKeys;


                }

                else
                {
                    //se il secondo tasto selezionato non è un Modifier disabilito il terzo tasto
                    thirdKeyComboBox.SelectedIndex = -1;
                    thirdKeyComboBox.IsEnabled = false;
                }
            }
        }

        private void hotkeyListView_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (hotkeyListView.SelectedItem != null)
            {
                SelectedLabel.Content = ((KeyValuePair<string, string>)hotkeyListView.SelectedItem).Key;
            }
        }


        #endregion

        private void applicaButton_Click(object sender, RoutedEventArgs e)
        {
            KeyValuePair<String, String> selected;
            KeyGesture kg;

            selected = (hotkeyListView.SelectedItem == null) ? hotKeyList[0] : (KeyValuePair<String, String>)hotkeyListView.SelectedItem;

            try
            {

                if (thirdKeyComboBox.SelectedItem == null)
                {
                    kg = new KeyGesture((Key)secondKeyComboBox.SelectedItem, (ModifierKeys)firstKeyComboBox.SelectedItem);
                }
                else
                {
                    kg = new KeyGesture((Key)thirdKeyComboBox.SelectedItem, (ModifierKeys)firstKeyComboBox.SelectedItem | (ModifierKeys)secondKeyComboBox.SelectedItem);
                }

                //controllo se sono stati immessi HOTKEY UGUALI per AZIONI DIVERSE

                bool combinazioneGiaEsistente = false;


                foreach (KeyValuePair<String, KeyGesture> entry in mappaHotKey)
                {
                    if (!selected.Key.Equals(entry.Key))
                    {
                        if (kg.Key.Equals(entry.Value.Key) && kg.Modifiers.Equals(entry.Value.Modifiers))
                        {
                            MessageBox.Show("ATTENZIONE non puoi registrare 2 o più HOTKEY uguali!!!!");
                            combinazioneGiaEsistente = true;
                            break;
                        }
                    }
                }

                if (!combinazioneGiaEsistente)
                {
                    if (mappaEccezioni.ContainsKey(kg.Modifiers) && mappaEccezioni[kg.Modifiers].Contains(kg.Key))
                    {
                        combinazioneGiaEsistente = true;
                        MessageBox.Show("ATTENZIONE!!\nNon puoi inserire HOTKEY DI SISTEMA\nConsulta la lista dei tasti di sistema");
                    }
                }

                if (combinazioneGiaEsistente)
                {
                    svuotaTutto();
                }
                else
                {
                    String key = selected.Key;
                    MessageBox.Show("Gli HOTKEY sono stati correttamente registrati");
                    mappaHotKey[selected.Key] = kg;
                    int index = hotKeyList.IndexOf(selected);
                    hotKeyList[index] = new KeyValuePair<string, string>(key, HotKeyToString(kg));
                    //hotKeyList.Add(new KeyValuePair<string, string>(key, HotKeyToString(kg)));
                    //hotKeyList.Remove(selected);

                }
            }
            catch (System.NullReferenceException )
            {
                svuotaTutto();
                MessageBox.Show("ATTENZIONE: inserire le combinazioni di tasto mancanti");
            }
        }

        #endregion

        #region altri metodi
        private void Window_Closing(object sender, CancelEventArgs e)
        {
            KeyGesture rcKeyGesture = mappaHotKey["Remote Copy"];
            Properties.Settings.Default["RemoteCopy"] = StringCollectionFromKeyGesture(rcKeyGesture);
            KeyGesture ssKeyGesture = mappaHotKey["Switch Server"];
            Properties.Settings.Default["SwitchServer"] = StringCollectionFromKeyGesture(ssKeyGesture);
            Properties.Settings.Default.Save();
        }

        #endregion

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Ctrl + C: Copy\nCtrl + X : Cut\nCtrl + V: Paste/Move\nCtrl + N: New... File, Tab, Entry, etc\nCtrl + S: Save.Ctrl + O: Open\nCtrl + P: Print\nCtrl + Z: Undo\nCtrl + A: Select all\nCtrl + F: Find\nCtrl + F4: Close tab or child window\nAlt + F4: Close active window\n", "Hotkey di Sistema");
        }

    }//class


}//namespace
