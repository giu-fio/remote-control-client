using Client.connessione;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace Client
{
    public class ServerConnection
    {
        // Background worker results
        private const string ERROR_NETWORK = "Error Network";
        private const string ERROR_SERVER = "Error Server";
        private const string CLOSED_BY_SERVER = "Closed By Server";
        private const string STOP_BY_CLIENT = "Stopped By Server";
        //Timeouts
        private const int TIMEOUT_MIN = 5000;
        private const int TIMEOUT_MAX = 10000;
        private const int TIMEOUT_CLIPBOARD = 5000;
        //Bbackground worker
        private BackgroundWorker controlConnectionWorker;
        private BackgroundWorker clipboardConnectionWorker;
        //Variables 
        private Server server;
        private State state;
        private Connection connection;
        private Random random;

        //Clipboard thread variables
        private double partialFileDim;
        private double totalFileDim;
        private bool isCopy;
        private volatile MyClipboard clipboard;

        public ServerConnection(Server server, Connection connection)
        {
            this.server = server;
            this.connection = connection;
            this.state = State.CONNECTED;
            this.random = new Random();

            server.TCPControlChannel.GetStream().ReadTimeout = random.Next(TIMEOUT_MIN, TIMEOUT_MAX);
            clipboard = new MyClipboard();

            //Thread Control Connection  
            controlConnectionWorker = new BackgroundWorker();
            controlConnectionWorker.WorkerSupportsCancellation = true;
            controlConnectionWorker.DoWork += controlConnectionWorker_DoWork;
            controlConnectionWorker.RunWorkerCompleted += controlConnectionWorker_RunWorkerCompleted;
            controlConnectionWorker.WorkerReportsProgress = true;
            controlConnectionWorker.ProgressChanged += controlConnectionWorker_ProgressChanged;
            controlConnectionWorker.RunWorkerAsync();
            //Thread Clipboard Move
            clipboardConnectionWorker = new BackgroundWorker();
            clipboardConnectionWorker.WorkerSupportsCancellation = true;
            clipboardConnectionWorker.WorkerReportsProgress = true;
            clipboardConnectionWorker.DoWork += clipboardConnectionWorker_DoWork;
            clipboardConnectionWorker.ProgressChanged += clipboardConnectionWorker_ProgressChanged;
            clipboardConnectionWorker.RunWorkerCompleted += clipboardConnectionWorker_RunWorkerCompleted;
        }

        #region BackgroundWokers

        #region Clipboard
        void clipboardConnectionWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker work = (BackgroundWorker)sender;
            partialFileDim = 0;
            try
            {   //creo la connessione per la CLIPBOARD
                TcpClient clipboardConnection = new TcpClient(server.IP, server.ClipboardPort);
                server.ClipboardChannel = clipboardConnection;
                //imposto la TIME_OUT della clipboard
                server.ClipboardChannel.GetStream().ReadTimeout = TIMEOUT_CLIPBOARD;
                //vedo se effettuare un remoteCopy o remotePaste
                if (isCopy)
                {
                    doRemoteCopy(work);
                    Trace.TraceInformation("Operazione di Copy completata.");
                }
                else
                {
                    doRemotePaste(work);
                    Trace.TraceInformation("Operazione di Paste completata.");
                }
            }
            catch (NetworkException)
            {
                e.Result = ERROR_NETWORK;
                Trace.TraceError("NetworkException in clipboardConnectionWorker_DoWork().");
            }
            catch (Exception ex)
            {
                if (ex is IOException || ex is SocketException)
                {
                    e.Result = ERROR_SERVER;
                    Trace.TraceWarning("Exception di connessione in clipboardConnectionWorker_DoWork(). Stack trace:\n{0}.", ex.StackTrace);
                }
                else if (ex.Message.Equals("PROTOCOLLO VIOLATO"))
                {
                    e.Result = ERROR_SERVER;
                    Trace.TraceWarning("Exception di protocollo in clipboardConnectionWorker_DoWork(). Stack trace:\n{0}.", ex.StackTrace);
                }
                else
                {
                    Trace.TraceError("Exception in clipboardConnectionWorker_DoWork(). Stack trace:\n{0}\n.", ex.StackTrace);
                    throw;
                }
            }
            finally
            {
                if (server.ClipboardChannel != null) { server.ClipboardChannel.Close(); }
                if (work.CancellationPending && e.Result == null) { e.Result = STOP_BY_CLIENT; }
            }
        }

        void clipboardConnectionWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            connection.TransferReportProgress(this, e.ProgressPercentage, isCopy);
        }

        void clipboardConnectionWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (state == State.ACTIVE)
            {
                // 1° CASO: se Resul=NULL vuol dire che il trasferimento è stato completato
                // 2° CASO: se Result=STOP BY CLIENT, vuol dire che ho annullato il trasferimento e lo notifico a Connection
                // 3° CASO: vi è stato un errore
                if (e.Result == null)
                {
                    connection.TransferCompleted(this, clipboard, isCopy);
                }
                else if (e.Result.Equals(STOP_BY_CLIENT))
                {
                    connection.TransferStopped(this, isCopy);
                }
                else
                {
                    if (e.Result.Equals(ERROR_NETWORK))
                    {
                        state = State.CLOSED_ERROR_NETWORK;
                    }
                    else
                    {
                        state = State.CLOSED_ERROR_SERVER;
                    }
                    controlConnectionWorker.CancelAsync();
                }
            }
        }

        #endregion

        #region Control

        void controlConnectionWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker worker = (BackgroundWorker)sender;
            NetworkStream stream = null;
            bool keepAliveSended = false;
            while (!worker.CancellationPending)
            {
                try
                {
                    if (server.TCPControlChannel == null)
                    {
                        break;
                    }
                    if (keepAliveSended)
                    {
                        worker.ReportProgress(0, TipoComandoBytes.KEEP_ALIVE_REQUEST);
                    }
                    stream = server.TCPControlChannel.GetStream();
                    byte[] msg = new byte[1024];
                    ReadMessage(stream, msg, 6);
                    String messageReceived = Encoding.ASCII.GetString(msg, 0, 6);

                    switch (messageReceived)
                    {
                        case TipoComando.CLOSE_CONNECTION:
                            e.Result = CLOSED_BY_SERVER;
                            Trace.TraceInformation("Richiesta di chiusura da parte del server. Server: {0}:{1}:{2}", server.Name, server.IP, server.ControlPort);
                            worker.CancelAsync();
                            break;

                        case TipoComando.KEEP_ALIVE_REQUEST:
                            Trace.TraceInformation("Richiesta di Keepalive ricevuta. Server: {0}:{1}:{2}", server.Name, server.IP, server.ControlPort);
                            //Rispondo alla richiesta di keep alive con un keep alive ack
                            worker.ReportProgress(0, TipoComandoBytes.KEEP_ALIVE_ACK);
                            break;

                        case TipoComando.KEEP_ALIVE_ACK:
                            Trace.TraceInformation("Keepalive ack ricevuto. Server: {0}:{1}:{2}", server.Name, server.IP, server.ControlPort);
                            break;

                        case TipoComando.ACTIVE_CLIPBOARD_ACK:
                            worker.ReportProgress(0, TipoComandoBytes.ACTIVE_CLIPBOARD_ACK);
                            break;

                    }

                    keepAliveSended = false;
                }
                catch (IOException ioe)
                {
                    if (ioe.InnerException != null)
                    {
                        SocketException se = (SocketException)ioe.InnerException;
                        if (se.SocketErrorCode == SocketError.TimedOut)
                        {
                            //entro in questo blocco solo se è scaduto il timeout
                            //se è la prima volta che scade il timeout faccio una richiesta di keep alive
                            if (!keepAliveSended)
                            {
                                Trace.TraceInformation("Timeout scaduto. Server: {0}:{1}:{2}", server.Name, server.IP, server.ControlPort);
                                keepAliveSended = true;
                                continue;
                            }
                            Trace.TraceError("Keepalive ack non ricevuto. Server: {0}:{1}:{2}", server.Name, server.IP, server.ControlPort);
                        }
                    }
                    Trace.TraceError("IOException in controlConnectionWorker_DoWork(). Stack trace:\n{0}\n", ioe.StackTrace);
                    e.Result = ERROR_SERVER;
                    worker.CancelAsync();
                }
                catch (NetworkException)
                {
                    Trace.TraceError("NetworkException in controlConnectionWorker_DoWork().");
                    e.Result = ERROR_NETWORK;
                    worker.CancelAsync();
                }
            }
        }

        void controlConnectionWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            byte[] arg = (byte[])e.UserState;
            if (arg == TipoComandoBytes.ACTIVE_CLIPBOARD_ACK)
            {
                if (isCopy) { clipboard = new MyClipboard(); }
                clipboardConnectionWorker.RunWorkerAsync();
                return;
            }

            try
            {
                WriteMessage(server.TCPControlChannel.GetStream(), arg, 0, arg.Length);
            }
            catch (IOException ioe)
            {
                Trace.TraceError("IOException in controlConnectionWorker_ProgressChanged(). Stack trace:\n{0}\n", ioe.StackTrace);
                state = State.CLOSED_ERROR_SERVER;
                controlConnectionWorker.CancelAsync();
            }
            catch (NetworkException)
            {
                Trace.TraceError("NetworkException in controlConnectionWorker_ProgressChanged().");
                state = State.CLOSED_ERROR_NETWORK;
                controlConnectionWorker.CancelAsync();
            }
        }

        void controlConnectionWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            /*CASO 1: Chiuso da Disconnect -> Stato = CLOSED
            *        chiudo il mouse e chiudo gli stream e avverto Connection che ho terminato
            * CASO 2: Ricevo CLOSE_CONNECTION -> Stato = CONNECTED o ACTIVE
             *        imposto stato in CLOSED, chiudo il mouse e tutti gli stream e avverto Connection che ho terminato
            * CASO 3: Eccezione NETWORK
             *        imposto stato in CLOSED_ERROR_NETWORK, chiudo il mouse, chiudo gli stream, notifico Connection di
             *        chiudere tutto e che ho terminato
            * CASO 4: Eccezione SERVER
             *        imposto stato in CLOSED_ERROR_SERVER, chiudo il mouse e tutti gli stream e avverto Connection che
             *        ho terminato con errore  
            */
            clipboardConnectionWorker.CancelAsync();
            CloseServer();
            CloseMouse();
            if (state == State.CLOSED_ERROR_NETWORK || (e.Result != null && e.Result.Equals(ERROR_NETWORK)))
            {
                state = State.CLOSED_ERROR_NETWORK;
                connection.ClosedByNetworkError(this);
            }
            else if (e.Result != null && e.Result.Equals(CLOSED_BY_SERVER))
            {
                state = State.CLOSED;
                connection.ClosedByServer(this);
            }
            else if (state == State.CLOSED)
            {
                connection.Closed(this);
            }
            else if (state == State.CLOSED_ERROR_SERVER || (e.Result != null && e.Result.Equals(ERROR_SERVER)))
            {
                state = State.CLOSED_ERROR_SERVER;
                connection.ClosedByServerError(this);
            }
        }
        #endregion
        #endregion

        #region Mouse
        private void InitMouse()
        {
            //Creo la connessione per il mouse
            if (server.UdpEnabled) { server.UDPChannel = new UdpClient(server.IP, server.MousePort); }
            else { server.TCPChannel = new TcpClient(server.IP, server.MousePort); }
        }
        private void CloseMouse()
        {
            if (server.UdpEnabled)
            {
                if (server.UDPChannel != null)
                {
                    server.UDPChannel.Close();
                    server.UDPChannel = null;
                }
            }
            else
            {
                if (server.TCPChannel != null)
                {
                    server.TCPChannel.Close();
                    server.TCPChannel = null;
                }
            }
        }
        public void SendMoveMouse(int x, int y)
        {
            if (state != State.ACTIVE)
            {
                Trace.TraceWarning("Stato invalido!");
                return;
            }
            byte[] xB = BitConverter.GetBytes(x);
            byte[] yB = BitConverter.GetBytes(y);
            byte[] msg = new byte[TipoComando.MOUSE_MOVE.Length + 4 + 4];
            Array.Copy(TipoComandoBytes.MOUSE_MOVE, msg, 6);
            Array.Copy(xB, 0, msg, 6, 4);
            Array.Copy(yB, 0, msg, 10, 4);
            WriteMessageMouse(msg);
        }

        #endregion


        #region public method

        public void Active()
        {
            if (state != State.CONNECTED) return;
            try
            {
                if (server == null || server.TCPControlChannel == null)
                {
                    state = State.CLOSED_ERROR_SERVER;
                    controlConnectionWorker.CancelAsync();
                    return;
                }
                //invio il messaggio al server
                WriteMessage(server.TCPControlChannel.GetStream(), TipoComandoBytes.ACTIVE_SERVER, 0, 6);
                InitMouse();
                state = State.ACTIVE;

            }
            catch (NetworkException)
            {
                Trace.TraceError("NetworkException in Active().");
                state = State.CLOSED_ERROR_NETWORK;
                controlConnectionWorker.CancelAsync();
            }
            catch (Exception se)
            {
                Trace.TraceError("Exception in Active(). Stack trace:\n{0}\n", se.StackTrace);
                if (se is IOException || se is SocketException)
                {
                    state = State.CLOSED_ERROR_SERVER;
                    controlConnectionWorker.CancelAsync();
                }
                else
                {
                    state = State.CLOSED_ERROR_NETWORK;
                }
            }
        }

        public void Deactive()
        {
            if (state != State.ACTIVE) return;
            try
            {
                if (!clipboardConnectionWorker.IsBusy)
                {
                    WriteMessage(server.TCPControlChannel.GetStream(), TipoComandoBytes.DEACTIVE_SERVER, 0, 6);
                    state = State.CONNECTED;
                    connection.DeactiveCompleted(this);
                }
                else
                {
                    connection.ImpossibleToDeactive(this);
                }
            }
            catch (NetworkException)
            {
                Trace.TraceError("NetworkException in Deactive().");
                state = State.CLOSED_ERROR_NETWORK;
                controlConnectionWorker.CancelAsync();
            }
            catch (Exception se)
            {
                Trace.TraceError("Exception in Deactive(). Stack trace:\n{0}\n", se.StackTrace);
                if (se is IOException || se is SocketException)
                {
                    state = State.CLOSED_ERROR_SERVER;
                    controlConnectionWorker.CancelAsync();
                }
                else
                {
                    state = State.CLOSED_ERROR_NETWORK;
                    controlConnectionWorker.CancelAsync();
                }
            }
        }

        public void Disconnect()
        {
            try
            {
                WriteMessage(server.TCPControlChannel.GetStream(), TipoComandoBytes.CLOSE_CONNECTION, 0, 6);
                state = State.CLOSED;
                controlConnectionWorker.CancelAsync();
            }
            catch (NetworkException)
            {
                Trace.TraceError("NetworkException in Disconnect().");
                state = State.CLOSED_ERROR_NETWORK;
                controlConnectionWorker.CancelAsync();
            }
            catch (Exception se)
            {
                Trace.TraceError("Exception in Disconnect(). Stack trace:\n{0}\n", se.StackTrace);
                if (se is IOException || se is SocketException)
                {
                    state = State.CLOSED_ERROR_SERVER;
                    controlConnectionWorker.CancelAsync();
                }
                else
                {
                    state = State.CLOSED_ERROR_NETWORK;
                    controlConnectionWorker.CancelAsync();
                }
            }
        }

        public void ForceToDisconnect()
        {
            state = State.FORCED_CLOSED;
            controlConnectionWorker.CancelAsync();
        }

        public void SendMouseClick(MouseClickType type)
        {
            if (state != State.ACTIVE) return;
            try
            {
                byte[] msg = null;
                switch (type)
                {
                    case MouseClickType.LEFT_DOWN:
                        msg = TipoComandoBytes.CLICK_LEFT_DOWN;
                        break;
                    case MouseClickType.LEFT_UP:
                        msg = TipoComandoBytes.CLICK_LEFT_UP;
                        break;
                    case MouseClickType.MIDDLE_DOWN:
                        msg = TipoComandoBytes.CLICK_MIDDLE_DOWN;
                        break;
                    case MouseClickType.MIDDLE_UP:
                        msg = TipoComandoBytes.CLICK_MIDDLE_UP;
                        break;
                    case MouseClickType.RIGHT_DOWN:
                        msg = TipoComandoBytes.CLICK_RIGHT_DOWN;
                        break;
                    case MouseClickType.RIGHT_UP:
                        msg = TipoComandoBytes.CLICK_RIGHT_UP;
                        break;
                }

                WriteMessageMouse(msg);

            }
            catch (NetworkException)
            {
                Trace.TraceError("NetworkException in SendMouseClick().");
                state = State.CLOSED_ERROR_NETWORK;
                controlConnectionWorker.CancelAsync();
            }
            catch (Exception se)
            {
                Trace.TraceError("Exception in SendMouseClick(). Stack trace:\n{0}\n", se.StackTrace);
                if (se is IOException || se is SocketException)
                {
                    state = State.CLOSED_ERROR_SERVER;
                    controlConnectionWorker.CancelAsync();
                }
                else
                {
                    state = State.CLOSED_ERROR_SERVER;
                    controlConnectionWorker.CancelAsync();
                }
            }
        }

        public void SendMouseScroll(int delta)
        {
            if (state != State.ACTIVE) return;
            try
            {
                byte[] deltaB = BitConverter.GetBytes(delta);
                byte[] msg = new byte[TipoComando.MOUSE_SCROLL.Length + 4];
                Array.Copy(TipoComandoBytes.MOUSE_SCROLL, msg, TipoComando.MOUSE_SCROLL.Length);
                Array.Copy(deltaB, 0, msg, TipoComando.MOUSE_SCROLL.Length, 4);
                WriteMessageMouse(msg);
            }
            catch (NetworkException)
            {
                Trace.TraceError("NetworkException in SendMouseScroll().");
                state = State.CLOSED_ERROR_NETWORK;
                controlConnectionWorker.CancelAsync();
            }
            catch (Exception se)
            {
                Trace.TraceError("Exception in SendMouseScroll(). Stack trace:\n{0}\n", se.StackTrace);
                if (se is IOException || se is SocketException)
                {
                    state = State.CLOSED_ERROR_SERVER;
                    controlConnectionWorker.CancelAsync();
                }
                else
                {
                    state = State.CLOSED_ERROR_SERVER;
                    controlConnectionWorker.CancelAsync();
                }
            }
        }

        public void SendKeyDown(Key key)
        {
            if (state != State.ACTIVE) return;
            try
            {
                //invio il tipo di azione
                WriteMessage(server.TCPControlChannel.GetStream(), TipoComandoBytes.KEY_PRESS_DOWN, 0, TipoComandoBytes.KEY_PRESS_DOWN.Length);
                byte codTasto = (byte)KeyInterop.VirtualKeyFromKey(key);
                byte[] array = { codTasto };
                WriteMessage(server.TCPControlChannel.GetStream(), array, 0, array.Length);

            }
            catch (NetworkException)
            {
                Trace.TraceError("NetworkException in SendKeyDown().");
                state = State.CLOSED_ERROR_NETWORK;
                controlConnectionWorker.CancelAsync();
            }
            catch (Exception se)
            {
                Trace.TraceError("Exception in SendKeyDown(). Stack trace:\n{0}\n", se.StackTrace);
                if (se is IOException || se is SocketException)
                {
                    state = State.CLOSED_ERROR_SERVER;
                    controlConnectionWorker.CancelAsync();
                }
                else
                {
                    state = State.CLOSED_ERROR_SERVER;
                    controlConnectionWorker.CancelAsync();
                }
            }
        }

        public void SendKeyUp(Key key)
        {
            if (state != State.ACTIVE) return;
            try
            {
                byte codTasto;
                byte[] array = new byte[1];
                // DEVO GESTIRE IL CASO IN CUI VENGA PREMUTI RightAlt perché mi preme anche LeftCtrl e non si prende l'evento KEY UP di LeftCtrl, quindi lo Forzo!!!
                if (key == Key.RightAlt)
                {
                    Key tmp = Key.LeftCtrl;
                    //invio il tipo di KEY EVENT
                    WriteMessage(server.TCPControlChannel.GetStream(), TipoComandoBytes.KEY_PRESS_UP, 0, TipoComandoBytes.KEY_PRESS_UP.Length);
                    //invio il codice del tasto
                    codTasto = (byte)KeyInterop.VirtualKeyFromKey(tmp);
                    array[0] = codTasto;
                    WriteMessage(server.TCPControlChannel.GetStream(), array, 0, array.Length);
                }
                //invio il tipo di KEY EVENT
                WriteMessage(server.TCPControlChannel.GetStream(), TipoComandoBytes.KEY_PRESS_UP, 0, TipoComandoBytes.KEY_PRESS_UP.Length);
                //invio il codice del tasto
                codTasto = (byte)KeyInterop.VirtualKeyFromKey(key);
                array[0] = codTasto;
                WriteMessage(server.TCPControlChannel.GetStream(), array, 0, array.Length);

            }
            catch (NetworkException)
            {
                Trace.TraceError("NetworkException in SendKeyUp().");
                state = State.CLOSED_ERROR_NETWORK;
                controlConnectionWorker.CancelAsync();
            }
            catch (Exception se)
            {
                Trace.TraceError("Exception in SendKeyUp(). Stack trace:\n{0}\n", se.StackTrace);
                if (se is IOException || se is SocketException)
                {
                    state = State.CLOSED_ERROR_SERVER;
                    controlConnectionWorker.CancelAsync();
                }
                else
                {
                    throw;
                }
            }
        }

        public void RemoteCopy()
        {
            if (state != State.ACTIVE) return;
            try
            {
                //mando il mex per attivare la CLIPBOARD per effettuare il Remote Copy
                if (!clipboardConnectionWorker.IsBusy)
                {
                    isCopy = true;
                    WriteMessage(server.TCPControlChannel.GetStream(), TipoComandoBytes.ACTIVE_CLIPBOARD_RC, 0, TipoComandoBytes.ACTIVE_CLIPBOARD_RC.Length);
                    //remoteCopyInExecution = true;
                    //clipboard = new MyClipboard();
                    //clipboardConnectionWorker.RunWorkerAsync();
                }
            }
            catch (NetworkException)
            {
                Trace.TraceError("NetworkException in RemoteCopy().");
                state = State.CLOSED_ERROR_NETWORK;
                controlConnectionWorker.CancelAsync();
            }
            catch (Exception se)
            {
                Trace.TraceError("Exception in RemoteCopy(). Stack trace:\n{0}\n", se.StackTrace);
                if (se is IOException || se is SocketException)
                {
                    state = State.CLOSED_ERROR_SERVER;
                    controlConnectionWorker.CancelAsync();
                }
                else
                {
                    state = State.CLOSED_ERROR_SERVER;
                    controlConnectionWorker.CancelAsync();
                }
            }
        }

        public void RemotePaste(MyClipboard clipboardToSend)
        {
            if (state != State.ACTIVE) return;
            try
            {
                //mando il mex per attivare la CLIPBOARD per effettuare il Remote Copy
                if (!clipboardConnectionWorker.IsBusy && !clipboardToSend.isEmpty())
                {
                    clipboard = new MyClipboard(clipboardToSend);
                    isCopy = false;
                    WriteMessage(server.TCPControlChannel.GetStream(), TipoComandoBytes.ACTIVE_CLIPBOARD_RP, 0, TipoComandoBytes.ACTIVE_CLIPBOARD_RC.Length);

                }
            }
            catch (NetworkException )
            {
                Trace.TraceError("NetworkException in RemotePaste().");
                state = State.CLOSED_ERROR_NETWORK;
                controlConnectionWorker.CancelAsync();
            }
            catch (Exception se)
            {
                Trace.TraceError("Exception in RemotePaste(). Stack trace:\n{0}\n", se.StackTrace);
                if (se is IOException || se is SocketException)
                {
                    state = State.CLOSED_ERROR_SERVER;
                    controlConnectionWorker.CancelAsync();
                }
                else
                {
                    state = State.CLOSED_ERROR_SERVER;
                    controlConnectionWorker.CancelAsync();
                }
            }

        }

        public void CancelTransfer()
        {
            if (state != State.ACTIVE) return;
            clipboardConnectionWorker.CancelAsync();
        }

        public String GetState()
        {
            return state.ToString();
        }

        #endregion

        #region private method

        private void doRemotePaste(BackgroundWorker worker)
        {
            Trace.TraceInformation("Richiesta di Paste.");
            using (BufferedStream stream =new BufferedStream (server.ClipboardChannel.GetStream()))
            {
                byte[] command = null;
                byte[] numFiles = null;
                byte[] dimension = null;
                byte[] data = null;

                totalFileDim = clipboard.Dimension;
                //Controllo se la clipboard contiene file
                if (clipboard.ContainsFiles())
                {
                    if (!Directory.Exists(clipboard.DirectoryFileDir)) { return; }
                    command = TipoComandoBytes.CLIPBOARD_FILES;
                    numFiles = BitConverter.GetBytes(clipboard.NumFiles);
                    dimension = BitConverter.GetBytes(clipboard.Dimension);

                    WriteMessage(stream, command, 0, command.Length);
                    WriteMessage(stream, numFiles, 0, numFiles.Length);
                    WriteMessage(stream, dimension, 0, dimension.Length);


                    //prendo i file
                    string[] files = Directory.GetFiles(clipboard.DirectoryFileDir);
                    foreach (string pathFile in files)
                    {
                        String[] token = pathFile.Split('\\');
                        string nome = token[token.Length - 1];
                        SendFile(stream, pathFile, nome);
                    }
                    //prendo le directory
                    string[] directories = Directory.GetDirectories(clipboard.DirectoryFileDir);
                    foreach (string pathDir in directories)
                    {
                        String[] token = pathDir.Split('\\');
                        string nome = token[token.Length - 1];
                        SendDir(stream, pathDir, nome);
                    }
                }
                else
                {
                    if (clipboard.ContainsText())
                    {
                        command = TipoComandoBytes.CLIPBOARD_TEXT;
                        numFiles = BitConverter.GetBytes(1);
                        data = clipboard.TextClipboard;
                        int dim = data.Length;
                        dimension = BitConverter.GetBytes(dim);
                    }
                    else if (clipboard.ContainsImage())
                    {
                        command = TipoComandoBytes.CLIPBOARD_IMAGE;
                        numFiles = BitConverter.GetBytes(1);
                        data = clipboard.BytesImage;
                        int dim = data.Length;
                        dimension = BitConverter.GetBytes(dim);
                    }
                    else if (clipboard.ContainsAudio())
                    {
                        command = TipoComandoBytes.CLIPBOARD_AUDIO;
                        numFiles = BitConverter.GetBytes(1);
                        data = clipboard.BytesAudio;
                        int dim = data.Length;
                        dimension = BitConverter.GetBytes(dim);
                    }

                    WriteMessage(stream, command, 0, command.Length);
                    WriteMessage(stream, numFiles, 0, numFiles.Length);
                    WriteMessage(stream, dimension, 0, dimension.Length);

                    byte[] buffer = new byte[4096];
                    int byteLettiTot = 0;
                    int byteDaLeggere = 0;
                    int length = clipboard.Dimension;
                    while (byteLettiTot < length)
                    {
                        if (clipboardConnectionWorker.CancellationPending) { return; }
                        byteDaLeggere = (length - byteLettiTot < 4096) ? length - byteLettiTot : 4096;

                        Array.Copy(data, byteLettiTot, buffer, 0, byteDaLeggere);
                        WriteMessage(stream, buffer, 0, byteDaLeggere);

                        partialFileDim += byteDaLeggere;
                        byteLettiTot += byteDaLeggere;
                        double perc = (partialFileDim / totalFileDim) * 100;
                        clipboardConnectionWorker.ReportProgress((int)perc);
                    }

                    Trace.TraceInformation("Clipboard inviata.");
                }
            }
        }

        private void SendDir(Stream stream, string path, string nome)
        {
            String[] files = Directory.GetFiles(path);
            String[] directories = Directory.GetDirectories(path);
            int numFiles = files.Length + directories.Length;

            byte[] command = TipoComandoBytes.CLIPBOARD_FILE_TYPE_DIRECTORY;
            byte[] nomeBytes = new byte[260];

            for (int i = 0; i < nomeBytes.Length; i++)
            {
                nomeBytes[i] = Convert.ToByte(' ');
            }
            Array.Copy(Encoding.ASCII.GetBytes(nome), nomeBytes, nome.Length);
            byte[] numFilesByte = BitConverter.GetBytes(numFiles);

            WriteMessage(stream, command, 0, command.Length);
            WriteMessage(stream, nomeBytes, 0, nomeBytes.Length);
            WriteMessage(stream, numFilesByte, 0, numFilesByte.Length);

            foreach (String pathFiles in files)
            {
                String[] tokens = pathFiles.Split('\\');
                String nomeFile = tokens[tokens.Length - 1];
                SendFile(stream, pathFiles, nomeFile);
            }

            foreach (String dir in directories)
            {
                String[] tokens = dir.Split('\\');
                String nomeDir = tokens[tokens.Length - 1];
                SendDir(stream, dir, nomeDir);
            }

        }

        private void SendFile(Stream stream, string path, string nome)
        {
            if (clipboardConnectionWorker.CancellationPending)
            {
                //sono stato chiuso dall'esterno, quindi non mando niente
                return;
            }
            byte[] command = TipoComandoBytes.CLIPBOARD_FILE_TYPE_FILE;

            //array di byte di lunghezza 260 contenete il nome del file 
            byte[] nomeBytes = new byte[260];
            for (int i = 0; i < nomeBytes.Length; i++)
            {
                nomeBytes[i] = Convert.ToByte(' ');                
            }

            Array.Copy(Encoding.ASCII.GetBytes(nome), nomeBytes, nome.Length);

            //array di byte contenente la lunghezza del file
            FileInfo fInfo = new FileInfo(path);

            byte[] fileLengthByte = BitConverter.GetBytes((int)fInfo.Length);

            //invio file
            WriteMessage(stream, command, 0, command.Length);
            WriteMessage(stream, nomeBytes, 0, nomeBytes.Length);
            WriteMessage(stream, fileLengthByte, 0, fileLengthByte.Length);
            int length = (int)fInfo.Length;
            using (FileStream inputFile = File.OpenRead(path))
            {
                

                byte[] buffer = new byte[4096];
                int byteLettiTot = 0;
                int byteLetti = 0;
                int byteDaLeggere = 4096;
                while (byteLettiTot < length)
                {
                    if (length - byteLettiTot < 4096)
                    {
                        byteDaLeggere = length - byteLettiTot;
                    }

                    byteLetti = inputFile.Read(buffer, 0, byteDaLeggere);

                    if (clipboardConnectionWorker.CancellationPending)
                    {
                        return;
                    }

                    WriteMessage(stream, buffer, 0, byteLetti);
                    partialFileDim += byteLetti;
                    double perc = (partialFileDim / totalFileDim) * 100;
                    clipboardConnectionWorker.ReportProgress((int)perc);
                    byteLettiTot += byteLetti;
                }
               
            }
            Trace.TraceInformation("File inviato. File: {0}", path);
        }

        private void doRemoteCopy(BackgroundWorker worker)
        {
            Trace.TraceInformation("Richiesta di Copy.");
            using (NetworkStream stream = server.ClipboardChannel.GetStream())
            {
                byte[] msg = new Byte[1024];
                //Ricevo il comando di lunghezza 6
                ReadMessage(stream, msg, 6);
                String command = Encoding.ASCII.GetString(msg, 0, 6);
                //Ricevo il secondo parametro: il numero dei file 
                msg = new byte[4];
                ReadMessage(stream, msg, 4);
                int numFile = BitConverter.ToInt32(msg, 0);
                //Ricevo la dimensione dei file
                ReadMessage(stream, msg, 4);
                int dimensionFiles = BitConverter.ToInt32(msg, 0);
                totalFileDim = dimensionFiles;
                worker.ReportProgress(0);
                //ora vedo il tipo di file che ùsi è richiesto di copiare
                if (command.Equals(TipoComando.CLIPBOARD_FILES))
                {
                    String pathFileToCopy = null;
                    StringCollection sc = new StringCollection();
                    String tipo;
                    String nome;
                    int lengthNumFile; //lunghezza nel caso di file, numero file in caso di directory
                    String tempDirectory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), System.IO.Path.GetRandomFileName());
                    Directory.CreateDirectory(tempDirectory);

                    for (int i = 0; i < numFile; i++)
                    {
                        if (clipboardConnectionWorker.CancellationPending)
                        {
                            return;
                        }
                        //Ricevo il tipo di file
                        msg = new byte[2];
                        ReadMessage(stream, msg, msg.Length);
                        tipo = Encoding.ASCII.GetString(msg, 0, 2);
                        //Ricevo il nome del file
                        msg = new byte[260];
                        ReadMessage(stream, msg, msg.Length);
                        nome = Encoding.ASCII.GetString(msg, 0, msg.Length);
                        //Ricevo la lunghezza nel caso di file o numero file in caso di directory
                        msg = new byte[4];
                        ReadMessage(stream, msg, msg.Length);
                        lengthNumFile = BitConverter.ToInt32(msg, 0);
                        nome = nome.Trim();
                        pathFileToCopy = System.IO.Path.Combine(tempDirectory, nome);
                        sc.Add(pathFileToCopy);
                        if (tipo.Equals(TipoComando.CLIPBOARD_FILE_TYPE_FILE))
                        {
                            CreateFile(stream, pathFileToCopy, lengthNumFile);
                        }
                        else if (tipo.Equals(TipoComando.CLIPBOARD_FILE_TYPE_DIRECTORY))
                        {
                            CreateDirectory(stream, pathFileToCopy, lengthNumFile);
                        }
                        else
                        {
                            throw new Exception("Protocollo violato");
                        }

                    }
                    clipboard.DirectoryFileDir = tempDirectory;
                    clipboard.NumFiles = numFile;
                    clipboard.Dimension = dimensionFiles;
                }
                else
                {
                    //leggo il file
                    msg = new byte[dimensionFiles];

                    byte[] buffer = new byte[4096];
                    int byteLettiTot = 0;
                    int byteDaLeggere = 4096;
                    while (byteLettiTot < dimensionFiles)
                    {
                        if (clipboardConnectionWorker.CancellationPending) { return; }
                        if (dimensionFiles - byteLettiTot < 4096)
                        {
                            byteDaLeggere = dimensionFiles - byteLettiTot;
                        }
                        ReadMessage(stream, buffer, byteDaLeggere);
                        Array.Copy(buffer, 0, msg, byteLettiTot, byteDaLeggere);
                        byteLettiTot += byteDaLeggere;
                        //report progress
                        partialFileDim += byteDaLeggere;
                        double perc = (partialFileDim / totalFileDim) * 100;
                        clipboardConnectionWorker.ReportProgress((int)perc);
                    }
                    if (command.Equals(TipoComando.CLIPBOARD_IMAGE))
                    {
                        Trace.TraceInformation("Immagine ricevuta");
                        clipboard.BytesImage = msg;
                    }
                    else if (command.Equals(TipoComando.CLIPBOARD_TEXT))
                    {
                        Trace.TraceInformation("Testo ricevuto");
                        clipboard.TextClipboard = msg;
                    }
                    else if (command.Equals(TipoComando.CLIPBOARD_AUDIO))
                    {
                        Trace.TraceInformation("Audio ricevuto");
                        clipboard.BytesAudio = msg;
                    }
                    else if (command.Equals(TipoComando.CLIPBOARD_EMPTY))
                    {
                        Trace.TraceInformation("Clipboard vuota.");
                    }
                    else
                    {
                        throw new Exception("Protocollo violato");
                    }
                    clipboard.NumFiles = numFile;
                    clipboard.Dimension = dimensionFiles;
                }
            }
        }

        private void CreateFile(Stream stream, String path, int length)
        {
            
            using (FileStream output = File.Create(path))
            {
                byte[] buffer = new byte[4096];
                int byteLettiTot = 0;
                int byteDaLeggere = 4096;
                while (byteLettiTot < length)
                {
                    if (clipboardConnectionWorker.CancellationPending) { return; }
                    if (length - byteLettiTot < 4096)
                    {
                        byteDaLeggere = length - byteLettiTot;
                    }
                    ReadMessage(stream, buffer, byteDaLeggere);
                    output.Write(buffer, 0, byteDaLeggere);
                    byteLettiTot += byteDaLeggere;
                    //report progress
                    partialFileDim += byteDaLeggere;
                    double perc = (partialFileDim / totalFileDim) * 100;
                    clipboardConnectionWorker.ReportProgress((int)perc);
                }
            }
            
            Trace.TraceInformation("File ricevuto. File: {0}", path);
        }

        private void CreateDirectory(Stream stream, String path, int length)
        {
            Directory.CreateDirectory(path);
            for (int i = 0; i < length && !clipboardConnectionWorker.CancellationPending; i++)
            {
                byte[] msg = new byte[2];
                ReadMessage(stream, msg, msg.Length);
                String tipo = Encoding.ASCII.GetString(msg);
                msg = new byte[260];
                ReadMessage(stream, msg, msg.Length);
                String nome = Encoding.ASCII.GetString(msg);
                msg = new byte[4];
                ReadMessage(stream, msg, msg.Length);
                int j = BitConverter.ToInt32(msg, 0);
                String subPath = System.IO.Path.Combine(path, nome);

                if (tipo.Equals(TipoComando.CLIPBOARD_FILE_TYPE_FILE))
                {
                    CreateFile(stream, subPath, j);
                }
                else if (tipo.Equals(TipoComando.CLIPBOARD_FILE_TYPE_DIRECTORY))
                {
                    CreateDirectory(stream, subPath, j);
                }
                else
                {
                    throw new Exception("Protocollo Violato");
                }
            }
        }

        //Writes the message on the stream if the connection is available
        private void WriteMessage(Stream stream, byte[] msg, int start, int dim)
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

        //Reads the message on the stream if the connection is available
        private void ReadMessage(Stream stream, byte[] msg, int dim)
        {
            if (System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable())
            {
                int totali = 0;
                while (totali < dim)
                {
                    int letti = stream.Read(msg, totali, dim - totali);
                    if (letti == 0)
                    {
                        throw new IOException("No data is available for reading");
                    }
                    totali += letti;
                }
            }
            else
            {
                throw new NetworkException();
            }
        }
        //Writes the mouse messages on the mouse connection if the connection is available
        private void WriteMessageMouse(byte[] msg)
        {
            if (!System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable())
            {
                throw new NetworkException();
            }
            if (server.UdpEnabled)
            {
                server.UDPChannel.SendAsync(msg, msg.Length);
            }
            else
            {
                server.TCPChannel.GetStream().WriteAsync(msg, 0, msg.Length);
            }
        }

        public void CloseServer()
        {
            if (server.TCPControlChannel != null) { server.TCPControlChannel.Close(); }
            server.TCPControlChannel = null;
        }

        #endregion

        #region altro

        public Server Server
        {
            get { return server; }
            set { server = value; }

        }

        //serve per ottenere la posizione del mouse
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out PointAPI lpPoint);

        private enum State { ACTIVE, CLOSED, CLOSED_ERROR_NETWORK, CLOSED_ERROR_SERVER, CONNECTED, FORCED_CLOSED }
        public struct PointAPI { public int X; public int Y;}

        #endregion
    }
}
