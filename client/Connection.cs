using Client.args;
using Client.connessione;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace Client
{
    public class Connection : IConnection
    {

        #region Costanti
        // La porta di broadcast è fissa
        private const short PORT_BROADCAST = 9999;
        private const short PORT_SERVER_TROVATI = 10000;
        private const int TIMEOUT = 2000;
        private const int ERROR = 0;
        private const int PASSWORD_REQUIRED = 1;
        private const int PASSWORD_ERROR = 2;

        #endregion

        #region Variabile

        private BackgroundWorker sendBroadcastWorker;
        private BackgroundWorker riceviInfoServerWorker;
        private BackgroundWorker connectServerWorker;

        private BlockingCollection<Server> serverInConnessione;
        private CancellationTokenSource cancellationTokenSource;
        private ConcurrentQueue<Server> serverToConnect;
        private List<ServerConnection> serverConnessi;
        private int serverAttivoIndex;
        private int serverToActiveIndex;
        private bool started;

        private UdpClient broadcastReceiver;
        private MyClipboard clipboardClient;

        #endregion

        #region Eventi
        public event EventHandler<args.ServerEventArgs> ServerFound;
        public event EventHandler<args.ServerEventArgs> ServerConnected;
        public event EventHandler<args.ServerEventArgs> ServerDisconnected;
        public event EventHandler<args.ServerEventArgs> ServerActivated;
        public event EventHandler<args.ServerEventArgs> ServerDeactivated;
        public event EventHandler<args.ServerEventArgs> ServerAuthRequired;
        public event EventHandler<args.ServerErrorEventsArgs> ServerError;
        public event EventHandler<EventArgs> RefreshConnections;
        public event EventHandler<args.ServerTranferEventArgs> ServerTransferProgressChanged;
        public event EventHandler<args.ServerTranferEventArgs> ServerTransferCompleted;
        public event EventHandler<args.ServerTranferEventArgs> ServerTransferCancelled;
        #endregion

        public Connection()
        {
            started = false;
            clipboardClient = new MyClipboard();
            serverInConnessione = new BlockingCollection<Server>(10);
            serverToConnect = new ConcurrentQueue<Server>();
            serverConnessi = new List<ServerConnection>();
            sendBroadcastWorker = new BackgroundWorker();
            sendBroadcastWorker.WorkerSupportsCancellation = true;
            sendBroadcastWorker.DoWork += sendBroadcastWorker_DoWork;
            connectServerWorker = new BackgroundWorker();
            connectServerWorker.WorkerSupportsCancellation = true;
            connectServerWorker.WorkerReportsProgress = true;
            connectServerWorker.DoWork += connectServerWorker_DoWork;
            connectServerWorker.ProgressChanged += connectServerWorker_ProgressChanged;
            riceviInfoServerWorker = new BackgroundWorker();
            riceviInfoServerWorker.WorkerSupportsCancellation = true;
            riceviInfoServerWorker.WorkerReportsProgress = true;
            riceviInfoServerWorker.DoWork += riceviInfoServerWorker_DoWork;
            riceviInfoServerWorker.ProgressChanged += riceviInfoServerWorker_ProgressChanged;
        }

        #region Background worker

        void sendBroadcastWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            Trace.TraceInformation("Invio delle richiesta indirizzo in broadcast");
            byte[] message = Encoding.ASCII.GetBytes(TipoComando.FIND_SERVER);
            try
            {
                using (UdpClient u = new UdpClient())
                {
                    u.EnableBroadcast = true;
                    IPEndPoint ipep = new IPEndPoint(IPAddress.Parse("255.255.255.255"), PORT_BROADCAST);
                    u.Send(message, message.Length, ipep);
                    Trace.TraceInformation("Richiesta di indirizzo inviata");
                }
            }
            catch (SocketException se) { Trace.TraceWarning("SocketException in sendBroadcastWorker_DoWork(). Error code:{0}.", se.ErrorCode); }
            catch (Exception ex) { Trace.TraceError("Exception in sendBroadcastWorker_DoWork(). Stack trace:\n{0}.", ex.StackTrace); }
        }

        void riceviInfoServerWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            Trace.TraceInformation("In ascolto per le risposte di indirizzo.");
            BackgroundWorker worker = (BackgroundWorker)sender;
            try
            {
                using (UdpClient u = new UdpClient(PORT_SERVER_TROVATI))
                {
                    broadcastReceiver = u;
                    IPEndPoint ep = new IPEndPoint(IPAddress.Any, PORT_SERVER_TROVATI);
                    u.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveBuffer, 0);

                    while (!worker.CancellationPending)
                    {
                        byte[] b = new byte[1024];
                        byte[] ipByte = new byte[4];
                        byte[] portByte = new byte[2];

                        b = u.Receive(ref ep);
                        String command = Encoding.ASCII.GetString(b, 0, 6);

                        if (command.Equals(TipoComando.SERVER_INFORMATION))
                        {
                            String nomeServer = Encoding.ASCII.GetString(b, 6, 20).Trim();
                            Array.Copy(b, 26, ipByte, 0, 4);
                            Array.Copy(b, 30, portByte, 0, 2);
                            IPAddress ipServer = new IPAddress(ipByte);
                            Int16 portServer = BitConverter.ToInt16(portByte, 0);
                            Server server = new Server() { Name = nomeServer, IP = ipServer.ToString(), ControlPort = portServer, ClipboardPort = (short)(portServer + 1) };
                            worker.ReportProgress(0, server);
                            Trace.TraceInformation("Risposta di indirizzo ricevuta dal Server: {0}", server.Name);
                        }
                        else { Trace.TraceWarning("Comando errato ricevuto in riceviInfoServerWorker_DoWork(). Messaggio: {0} ", command); }
                    }//while
                }//using
            }
            catch (SocketException) { Trace.TraceInformation("Broadcast disativato"); }
            catch (ObjectDisposedException ode) { Trace.TraceError("ObjectDisposedException in riceviInfoServerWorker_DoWork(). Stack trace:\n{0}.", ode.StackTrace); }
            catch (Exception ex) { Trace.TraceError("Exception in riceviInfoServerWorker_DoWork(). Stack trace:\n{0}.", ex.StackTrace); }
        }

        void riceviInfoServerWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            Server server = (Server)e.UserState;
            ServerEventArgs args = new ServerEventArgs { Server = server, Position = -1 };
            EventHandler<ServerEventArgs> handler = ServerFound;
            if (handler != null) { handler(this, args); }
        }

        void connectServerWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker worker = (BackgroundWorker)sender;
            NetworkStream stream = null;
            Server server = null;
            cancellationTokenSource = new CancellationTokenSource();
            while (!worker.CancellationPending)
            {
                try
                {
                    serverInConnessione.TryTake(out server, -1, cancellationTokenSource.Token);

                    //connessione TCP con Server
                    server.TCPControlChannel = new TcpClient(server.IP, server.ControlPort);
                    stream = server.TCPControlChannel.GetStream();
                    stream.ReadTimeout = TIMEOUT;

                    //invio richiesta di login
                    byte[] reqLogin = Encoding.ASCII.GetBytes(TipoComando.REQUEST_LOGIN);
                    WriteMessage(stream, reqLogin, 0, reqLogin.Length);

                    //aspetto di ricevere la conferma di richiesta insieme alla string sfida
                    byte[] challengeMessage = new byte[1024];
                    ReadMessage(stream, challengeMessage, 0, 6);
                    String messageString = Encoding.ASCII.GetString(challengeMessage, 0, 6);

                    if (messageString.Equals(TipoComando.LOGIN_CHALLENGE))
                    {
                        //leggo il nonce
                        byte[] challenge = new byte[1024];
                        stream.Read(challenge, 0, 4);

                        if (server.Password != null)
                        {
                            String password = server.Password;
                            server.Password = null;

                            //concateno pasword e challenge
                            byte[] passwordBytes = new byte[password.Length + 4];
                            Array.Copy(Encoding.ASCII.GetBytes(password), passwordBytes, password.Length);
                            Array.Copy(challenge, 0, passwordBytes, password.Length, 4);

                            //calcolo l'hash
                            SHA256 sha = SHA256Managed.Create();
                            byte[] hash = sha.ComputeHash(passwordBytes);

                            //invio nuovamente la richiesta di login 
                            WriteMessage(stream, Encoding.ASCII.GetBytes(TipoComando.REQUEST_LOGIN), 0, TipoComando.REQUEST_LOGIN.Length);
                            WriteMessage(stream, hash, 0, hash.Length);
                            //invio hash
                            challengeMessage = new byte[1024];
                            ReadMessage(stream, challengeMessage, 0, 6);
                            messageString = Encoding.ASCII.GetString(challengeMessage, 0, 6);
                        }
                        else
                        {
                            //termino il tentativo di login
                            WriteMessage(stream, Encoding.ASCII.GetBytes(TipoComando.CLOSE_CONNECTION), 0, 6);
                            //Scateno l'evento di richiesta login
                            ServerEventArgs args = new ServerEventArgs() { Server = server, Position = -1 };
                            worker.ReportProgress(PASSWORD_REQUIRED, args);
                            CloseServer(server);
                            continue;
                        }
                    }

                    if (!messageString.Equals(TipoComando.LOGIN_OK))
                    {//connessione non effettuata o messaggio di risposta non valido
                        //chiudo connessioni aperte
                        CloseServer(server);
                        if (messageString.Equals(TipoComando.LOGIN_ERROR)){
                            ServerErrorEventsArgs args = new ServerErrorEventsArgs() { Server = server, Position = -1,ErrorCode = ServerErrorEventsArgs.PASSWORD_ERROR };
                            worker.ReportProgress(PASSWORD_ERROR, args);
                        }
                        continue;
                    }
                    
                    //Ho ricevuto la conferma di login.

                    //Leggo i paramentri di configurazione
                    byte[] confByte = new byte[1024];
                    ReadMessage(stream, confByte, 0, 6);
                    String conf = Encoding.ASCII.GetString(confByte, 0, 6);

                    if (!conf.Equals(TipoComando.LOGIN_UDP_PORT))
                    {
                        Trace.TraceWarning("Ricevuto un messaggio errato in connectServerWorker_DoWork(). Messaggio:{0}.", conf);
                        //chiudo connessioni aperte
                        ServerErrorEventsArgs args = new ServerErrorEventsArgs() { Server = server, Position = -1, ErrorCode = ServerErrorEventsArgs.LOGIN_ERROR };
                        EventHandler<ServerErrorEventsArgs> handler = ServerError;
                        if (handler != null)
                        {
                            handler(this, args);
                        }
                        CloseServer(server);
                        continue;
                    }

                    ParameterLogin(ref stream, ref server);
                    serverToConnect.Enqueue(server);
                    Trace.TraceInformation("Server connesso. Server: {0}:{1}:{2}", server.Name, server.IP, server.ControlPort);
                    worker.ReportProgress(serverToConnect.Count);
                }
                catch (NetworkException)
                {
                    Trace.TraceError("NetworkException in connectServerWorker_DoWork().");
                    //inizializzo l'evento da inviare
                    ServerErrorEventsArgs args = new ServerErrorEventsArgs()
                    {
                        Server = server,
                        Position = -1,
                        ErrorCode = ServerErrorEventsArgs.NETWORK_ERROR
                    };
                    worker.ReportProgress(ERROR, args);
                    CloseServer(server);


                }
                catch (OperationCanceledException)
                {
                    continue;
                }
                catch (Exception se)
                {
                    if (se is SocketException || se is IOException)
                    {
                        //inizializzo l'evento da inviare
                        ServerErrorEventsArgs args = new ServerErrorEventsArgs()
                        {
                            Server = server,
                            Position = -1,
                            ErrorCode = ServerErrorEventsArgs.CONNECTION_ERROR
                        };
                        worker.ReportProgress(ERROR, args);
                        CloseServer(server);
                    }
                    else
                    {
                        ServerErrorEventsArgs args = new ServerErrorEventsArgs()
                        {
                            Server = server,
                            Position = -1,
                            ErrorCode = ServerErrorEventsArgs.CONNECTION_ERROR
                        };
                        worker.ReportProgress(ERROR, args);
                        CloseServer(server);
                    }
                    Trace.TraceError("Exception in riceviInfoServerWorker_DoWork(). Stack trace:\n{0}.", se.StackTrace);
                }

            }
        }

        void connectServerWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            if (e.UserState == null)
            {
                Server server = null;
                while (!serverToConnect.TryDequeue(out server)) ;
                ServerConnection serverToInsert = new ServerConnection(server, this);
                ServerEventArgs args = null;
                serverConnessi.Add(serverToInsert);
                args = new ServerEventArgs()
                {
                    Server = server,
                    Position = serverConnessi.IndexOf(serverToInsert)
                };
                EventHandler<ServerEventArgs> handler = ServerConnected;
                if (handler != null)
                {
                    handler(this, args);
                }
            }
            else if (e.ProgressPercentage == ERROR)
            {
                ServerErrorEventsArgs args = (ServerErrorEventsArgs)e.UserState;
                EventHandler<ServerErrorEventsArgs> handler = ServerError;
                if (handler != null)
                {
                    handler(this, args);
                }
            }
            else if (e.ProgressPercentage == PASSWORD_REQUIRED)
            {
                ServerEventArgs args = (ServerEventArgs)e.UserState;
                EventHandler<ServerEventArgs> handler = ServerAuthRequired;
                if (handler != null) { handler(this, args); }
            }
            else if (e.ProgressPercentage == PASSWORD_ERROR)
            {
                ServerErrorEventsArgs args = (ServerErrorEventsArgs)e.UserState;
                EventHandler<ServerErrorEventsArgs> handler = ServerError;
                if (handler != null) { handler(this, args); }
            }
        }

        #endregion

        #region Metodi pubblici

        void IConnection.SendBroadcast()
        {
            if (!sendBroadcastWorker.IsBusy)
            {
                sendBroadcastWorker.RunWorkerAsync();
            }
            if (!riceviInfoServerWorker.IsBusy)
            {
                riceviInfoServerWorker.RunWorkerAsync();
            }
        }

        void IConnection.Connect(ref connessione.Server server)
        {
            serverInConnessione.TryAdd(server);
            if (!connectServerWorker.IsBusy) { connectServerWorker.RunWorkerAsync(); }
        }

        void IConnection.Disconnect(int position)
        {
            if (serverAttivoIndex == position && serverConnessi.Count > 1 && started)
            {
                int indexToActive = position + 1;
                if (position == serverConnessi.Count - 1)
                {
                    indexToActive = 0;
                }
                ServerConnection serverToDeactive = serverConnessi[serverAttivoIndex];
                serverToDeactive.Deactive();
                serverToActiveIndex = indexToActive;
            }
            Server server = null;
            ServerConnection curServer = null;
            server = serverConnessi[position].Server;
            curServer = serverConnessi[position];
            curServer.Disconnect();
            Trace.TraceInformation("Server disconnesso. Server: {0}:{1}:{2}", curServer.Server.Name, curServer.Server.IP, curServer.Server.ControlPort);

        }

        void IConnection.Active(int position)
        {
            if ((position > 0 || position < serverConnessi.Count) && serverConnessi.Count > 1)
            {
                ServerConnection serverToDeactive = serverConnessi[serverAttivoIndex];
                serverToActiveIndex = position;
                serverToDeactive.Deactive();
                
            }
        }

        List<ServerConnection> IConnection.getServers()
        {
            List<ServerConnection> listaServer = null;
            listaServer = new List<ServerConnection>(serverConnessi);
            return listaServer;
        }

        void IConnection.Start()
        {
            if (serverConnessi.Count == 0) return;
            if (started) return;
            Trace.TraceInformation("Start");

            //cancello i backgroundworker
            riceviInfoServerWorker.CancelAsync();
            broadcastReceiver.Close();
            cancellationTokenSource.Cancel();
            connectServerWorker.CancelAsync();
            //il primo server della lista diventa attivo
            ServerConnection serverToActive = serverConnessi[0];
            setActive(serverToActive);
            started = true;
        }

        void IConnection.Stop()
        {
            Trace.TraceInformation("Stop");
            if (serverConnessi.Count == 0)
            {
                //vuol dire che è avvenuta qualche eccezione quindi i server sono stati già rimossi
                //per cui aggiorno la lista dei serverConnessi della MainWindow
                started = false;
                serverAttivoIndex = -1;
                EventArgs args = new EventArgs();

                EventHandler<EventArgs> handlerRefresh = RefreshConnections;
                if (handlerRefresh != null)
                {
                    handlerRefresh(this, args);
                }
                return;
            }
            //chiudo tutte le connessioni attive
            while (serverConnessi.Count > 0)
            {
                ServerConnection server = serverConnessi[0];
                server.Disconnect();
                serverConnessi.Remove(server);
            }

            started = false;

        }

        void IConnection.SendMouseMove(int x, int y)
        {
            if (!started)
            { throw new InvalidOperationException("Connessione non è stata ancora avviata. Chiamata Start() non effetuata"); }
            ServerConnection currentServer = null;
            if (serverAttivoIndex < 0)
            {
                Trace.TraceWarning("ServerAttivoIndex è minore di 0. Index:{0}. In SendMouseMove() ", serverAttivoIndex);
                return;
            }
            currentServer = serverConnessi[serverAttivoIndex];
            currentServer.SendMoveMouse(x, y);
        }

        void IConnection.SendMouseClick(MouseClickType type)
        {
            if (!started)
            { throw new InvalidOperationException("Connessione non è stata ancora avviata. Chiamata Start() non effetuata"); }
            ServerConnection currentServer = null;
            if (serverAttivoIndex < 0)
            {
                Trace.TraceWarning("ServerAttivoIndex è minore di 0. Index:{0}. In SendMouseClick() ", serverAttivoIndex);
                return;
            }
            currentServer = serverConnessi[serverAttivoIndex];
            currentServer.SendMouseClick(type);
        }

        void IConnection.SendMouseScroll(int delta)
        {
            if (!started)
            { throw new InvalidOperationException("Connessione non è stata ancora avviata. Chiamata Start() non effetuata"); }
            ServerConnection currentServer = null;
            if (serverAttivoIndex < 0)
            {
                Trace.TraceWarning("ServerAttivoIndex è minore di 0. Index:{0}. In SendMouseScroll() ", serverAttivoIndex);
                return;
            }
            currentServer = serverConnessi[serverAttivoIndex];
            currentServer.SendMouseScroll(delta);
        }

        void IConnection.SendKeyDown(Key key)
        {
            if (!started)
            { throw new InvalidOperationException("Connessione non è stata ancora avviata. Chiamata Start() non effetuata"); }
            ServerConnection currentServer = null;
            if (serverAttivoIndex < 0)
            {
                Trace.TraceWarning("ServerAttivoIndex è minore di 0. Index:{0}. In SendKeyDown() ", serverAttivoIndex);
                return;
            }
            currentServer = serverConnessi[serverAttivoIndex];
            currentServer.SendKeyDown(key);
        }

        void IConnection.SendKeyUp(Key key)
        {
            if (!started)
            { throw new InvalidOperationException("Connessione non è stata ancora avviata. Chiamata Start() non effetuata"); }
            ServerConnection currentServer = null;
            if (serverAttivoIndex < 0)
            {
                Trace.TraceWarning("ServerAttivoIndex è minore di 0. Index:{0}. In SendKeyUp() ", serverAttivoIndex);
                return;
            }
            currentServer = serverConnessi[serverAttivoIndex];
            currentServer.SendKeyUp(key);
        }

        void IConnection.SwitchPosition(int pos1, int pos2)
        {
            ServerConnection s = serverConnessi[pos1];
            serverConnessi[pos1] = serverConnessi[pos2];
            serverConnessi[pos2] = s;
        }

        void IConnection.RemoteCopy()
        {
            if (!started)
            { throw new InvalidOperationException("Connessione non è stata ancora avviata. Chiamata Start() non effetuata"); }
            ServerConnection currentServer = null;
            if (serverAttivoIndex < 0)
            {
                Trace.TraceWarning("ServerAttivoIndex è minore di 0. Index:{0}. In RemoteCopy() ", serverAttivoIndex);
                return;
            }
            currentServer = serverConnessi[serverAttivoIndex];
            currentServer.RemoteCopy();
        }

        void IConnection.RemotePaste()
        {
            if (!started)
            { throw new InvalidOperationException("Connessione non è stata ancora avviata. Chiamata Start() non effetuata"); }
            ServerConnection currentServer = null;
            if (serverAttivoIndex < 0)
            {
                Trace.TraceWarning("ServerAttivoIndex è minore di 0. Index:{0}. In RemotePaste() ", serverAttivoIndex);
                return;
            }
            currentServer = serverConnessi[serverAttivoIndex];
            currentServer.RemotePaste(clipboardClient);
        }

        void IConnection.CancelTransfer()
        {
            if (!started)
            { throw new InvalidOperationException("Connessione non è stata ancora avviata. Chiamata Start() non effetuata"); }
            if (serverAttivoIndex < 0)
            {
                Trace.TraceWarning("ServerAttivoIndex è minore di 0. Index:{0}. In CancelTransfer() ", serverAttivoIndex);
                return;
            }
            ServerConnection currentServer = serverConnessi[serverAttivoIndex];
            currentServer.CancelTransfer();
        }

        #endregion

        #region Metodi privati

        private void setActive(ServerConnection serverCon)
        {
            //attivo il server
            serverCon.Active();
            Trace.TraceInformation("Server attivo. Server: {0}:{1}:{2}", serverCon.Server.Name, serverCon.Server.IP, serverCon.Server.ControlPort);
            //controllo se il server si è attivato o vi è stato qualche problema
            if (!serverCon.GetState().Equals("ACTIVE")) { return; }
            //cambio il serverAttivoIndex
            serverAttivoIndex = serverConnessi.IndexOf(serverCon);
            //inizializzo l'evento
            ServerEventArgs args = new ServerEventArgs()
            {
                Server = serverCon.Server,
                Position = serverAttivoIndex
            };
            EventHandler<ServerEventArgs> handler = ServerActivated;
            if (handler != null)
            {
                handler(this, args);
            }
        }
              

        // invocato solamente durante la fase di login, quindi si fa riferimento ai server che sono nella 
        // lista serverInConnessione e non severConnessi
        // non ci sono problemi di sincronizzazione perchè viene invocato su oggetti che gestisce solo lui
        // nella fase di login.
        private void CloseServer(Server server)
        {
            if (server.TCPControlChannel != null) { server.TCPControlChannel.Close(); }
            if (server.UDPChannel != null) { server.UDPChannel.Close(); }
            if (server.TCPChannel != null) { server.TCPChannel.Close(); }
            if (server.ClipboardChannel != null) { server.ClipboardChannel.Close(); }
            server.TCPControlChannel = null;
            server.TCPChannel = null;
            server.UDPChannel = null;
            server.ClipboardChannel = null;
        }

        private void ParameterLogin(ref NetworkStream stream, ref Server server)
        {   //leggo porta udp
            byte[] udpPortByte = new byte[1024];
            ReadMessage(stream, udpPortByte, 0, 2);
            Int16 port = BitConverter.ToInt16(udpPortByte, 0);
            if (port == 0)
            {
                server.UdpEnabled = false;
                server.MousePort = (short)(server.ClipboardPort + 1);
            }
            else
            {
                server.UdpEnabled = true;
                server.MousePort = port;
            }

            //Invio Le dimensioni del mio monitor
            int width = (int)SystemParameters.VirtualScreenWidth;
            int height = (int)SystemParameters.VirtualScreenHeight;
            byte[] screenCommand = Encoding.ASCII.GetBytes(TipoComando.LOGIN_WIDTH_MONITOR);
            byte[] dimByte = BitConverter.GetBytes(width);
            WriteMessage(stream, screenCommand, 0, screenCommand.Length);
            WriteMessage(stream, dimByte, 0, dimByte.Length);
            screenCommand = Encoding.ASCII.GetBytes(TipoComando.LOGIN_HEIGHT_MONITOR);
            dimByte = BitConverter.GetBytes(height);
            WriteMessage(stream, screenCommand, 0, screenCommand.Length);
            WriteMessage(stream, dimByte, 0, dimByte.Length);
        }

        private void WriteMessage(NetworkStream stream, byte[] msg, int start, int dim)
        {
            if (System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable())
            {
                stream.Write(msg, start, dim);
            }
            else
            {
                throw new NetworkException();
            }
        }

        private void ReadMessage(NetworkStream stream, byte[] msg, int start, int dim)
        {
            if (System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable())
            {
                if (stream.Read(msg, start, dim) == 0) { throw new IOException("No data is available"); }
            }
            else { throw new NetworkException(); }
        }

        #endregion

        #region metodi di callback
        internal void ClosedByNetworkError(ServerConnection serverConnection)
        {
            serverConnessi.Remove(serverConnection);
            serverAttivoIndex = -1;
            ServerErrorEventsArgs args = null;
            //inizializzo l'evento da inviare
            args = new ServerErrorEventsArgs()
            {
                Server = serverConnection.Server,
                Position = serverConnessi.IndexOf(serverConnection),
                ErrorCode = ServerErrorEventsArgs.NETWORK_ERROR
            };
            EventHandler<ServerErrorEventsArgs> handler = ServerError;
            if (handler != null)
            {
                handler(this, args);
            }

            while (serverConnessi.Count > 0)
            {
                serverConnessi[0].ForceToDisconnect();
                serverConnessi.RemoveAt(0);
            }

            EventArgs argsRefresh = new EventArgs();
            EventHandler<EventArgs> handlerRefresh = RefreshConnections;
            if (handlerRefresh != null)
            {
                handlerRefresh(this, argsRefresh);
            }
        }

        internal void Closed(ServerConnection serverConnection)
        {
            ServerEventArgs args = null;
            //inizializzo l'evento da inviare
            args = new ServerEventArgs()
            {
                Server = serverConnection.Server,
                Position = serverConnessi.IndexOf(serverConnection),
            };
            serverConnessi.Remove(serverConnection);
            if (serverConnessi.Count == 0)
            {
                started = false;
                serverAttivoIndex = -1;
            }
            EventHandler<ServerEventArgs> handler = ServerDisconnected;
            if (handler != null)
            {
                handler(this, args);
            }
            EventArgs argsRefresh = new EventArgs();
            EventHandler<EventArgs> handlerRefresh = RefreshConnections;
            if (handlerRefresh != null)
            {
                handlerRefresh(this, argsRefresh);
            }

        }

        internal void ClosedByServer(ServerConnection serverConnection)
        {
            ServerEventArgs args = null;
            int position = -1;
            position = serverConnessi.IndexOf(serverConnection);
            //inizializzo l'evento da inviare
            args = new ServerEventArgs()
            {
                Server = serverConnection.Server,
                Position = position,
            };
            serverConnessi.Remove(serverConnection);
            if (serverConnessi.Count == 0)
            {
                started = false;
                serverAttivoIndex = -1;
            }
            EventHandler<ServerEventArgs> handler = ServerDisconnected;
            if (handler != null)
            {
                handler(this, args);
            }
            EventArgs argsRefresh = new EventArgs();
            EventHandler<EventArgs> handlerRefresh = RefreshConnections;
            if (handlerRefresh != null)
            {
                handlerRefresh(this, argsRefresh);
            }
            if (serverAttivoIndex == position && started)
            {
                ServerConnection serverToActive = serverConnessi[0];
                setActive(serverToActive);
            }
            else if (position < serverAttivoIndex)
            {//Perché nella lista potrebbe precederlo
                serverAttivoIndex--;
            }
        }

        internal void ClosedByServerError(ServerConnection serverConnection)
        {
            ServerErrorEventsArgs args = null;
            int position = -1;
            position = serverConnessi.IndexOf(serverConnection);
            //inizializzo l'evento da inviare
            args = new ServerErrorEventsArgs()
            {
                Server = serverConnection.Server,
                Position = position,
                ErrorCode = ServerErrorEventsArgs.SERVER_ERROR
            };
            serverConnessi.Remove(serverConnection);
            if (serverConnessi.Count == 0)
            {
                started = false;
                serverAttivoIndex = -1;
            }
            EventHandler<ServerErrorEventsArgs> handler = ServerError;
            if (handler != null)
            {
                handler(this, args);
            }
            EventArgs argsRefresh = new EventArgs();
            EventHandler<EventArgs> handlerRefresh = RefreshConnections;
            if (handlerRefresh != null)
            {
                handlerRefresh(this, argsRefresh);
            }
            if (serverAttivoIndex == position && started)
            {
                ServerConnection serverToActive = serverConnessi[0];
                setActive(serverToActive);
            }
            else if (position < serverAttivoIndex)
            {//Perché nella lista potrebbe precederlo
                serverAttivoIndex--;
            }
        }

        internal void ImpossibleToDeactive(ServerConnection serverConnection)
        {
            ServerErrorEventsArgs args = null;
            args = new ServerErrorEventsArgs()
            {
                Server = serverConnection.Server,
                Position = serverConnessi.IndexOf(serverConnection),
                ErrorCode = ServerErrorEventsArgs.CLIPBOARD_IN_ACTION
            };
            EventHandler<ServerErrorEventsArgs> handler = ServerError;
            if (handler != null)
            {
                handler(this, args);
            }

        }

        internal void DeactiveCompleted(ServerConnection serverConnection)
        {
            //inizializzo l'evento
            ServerEventArgs args = new ServerEventArgs()
            {
                Server = serverConnection.Server,
                Position = serverAttivoIndex
            };
            EventHandler<ServerEventArgs> handler = ServerDeactivated;
            if (handler != null)
            {
                handler(this, args);
            }
            //posso attivare il server su cui ho fatto switch che
            //l'ho salvato nella variabile serverToActiveIndex 
            //quando ho chiamato il metodo IConnection.Active
            ServerConnection serverToActive = serverConnessi[serverToActiveIndex];
            Trace.TraceInformation("Server non attivo. Server: {0}:{1}:{2}", serverConnection.Server.Name, serverConnection.Server.IP, serverConnection.Server.ControlPort);
            setActive(serverToActive);
        }

        internal void TransferCompleted(ServerConnection serverConnection, MyClipboard clipboardServer, bool isCopy)
        {
            if (isCopy)
            {
                clipboardClient = new MyClipboard(clipboardServer);
            }
            ServerTranferEventArgs args = new ServerTranferEventArgs
            {
                Server = serverConnection.Server,
                Position = serverConnessi.IndexOf(serverConnection),
                Copy = isCopy,
                Completed = true,
                Percentage = 100
            };
            EventHandler<ServerTranferEventArgs> handler = this.ServerTransferCompleted;
            if (handler != null)
            {
                handler(this, args);
            }
        }

        internal void TransferStopped(ServerConnection serverConnection, bool isCopy)
        {
            ServerTranferEventArgs args = new ServerTranferEventArgs
            {
                Server = serverConnection.Server,
                Position = serverConnessi.IndexOf(serverConnection),
                Copy = isCopy,
                Completed = false,
                Percentage = 0
            };

            EventHandler<ServerTranferEventArgs> handler = this.ServerTransferCancelled;
            if (handler != null)
            {
                handler(this, args);
            }
        }

        internal void TransferReportProgress(ServerConnection serverConnection, int percentage, bool isCopy)
        {
            ServerTranferEventArgs args = new ServerTranferEventArgs
            {
                Server = serverConnection.Server,
                Position = serverConnessi.IndexOf(serverConnection),
                Copy = isCopy,
                Completed = false,
                Percentage = percentage
            };

            EventHandler<ServerTranferEventArgs> handler = this.ServerTransferProgressChanged;
            if (handler != null)
            {
                handler(this, args);
            }

        }

        #endregion
    }
}
