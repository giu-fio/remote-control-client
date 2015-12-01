using Client.args;
using Client.connessione;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Client
{
    public enum MouseClickType { LEFT_DOWN, LEFT_UP, MIDDLE_DOWN, MIDDLE_UP, RIGHT_DOWN, RIGHT_UP }
    public interface IConnection
    {

        event EventHandler<ServerEventArgs> ServerFound;
        event EventHandler<ServerEventArgs> ServerConnected;
        event EventHandler<ServerEventArgs> ServerDisconnected;
        event EventHandler<ServerEventArgs> ServerActivated;
        event EventHandler<ServerEventArgs> ServerDeactivated;
        event EventHandler<ServerEventArgs> ServerAuthRequired;
        event EventHandler<ServerErrorEventsArgs> ServerError;
        event EventHandler<EventArgs> RefreshConnections;
        event EventHandler<ServerTranferEventArgs> ServerTransferProgressChanged;
        event EventHandler<ServerTranferEventArgs> ServerTransferCompleted;
        event EventHandler<ServerTranferEventArgs> ServerTransferCancelled;



        /// <summary>
        /// Invio di messaggi broadcast per richiedere le informazioni dei server
        /// </summary>
        void SendBroadcast();

        /// <summary>
        /// Connessione al Server server 
        /// </summary>
        /// <param name="server">Server a cui connettersi</param>
        void Connect(ref Server server);

        /// <summary>
        /// Disconnessione al server in posizione "position". 
        /// </summary>
        /// <param name="position">Posizione del server da disconnettere</param>
        void Disconnect(int position);

        /// <summary>
        /// Attiva il server in posizione "position". Se presente un server attivo viene deattivato.
        /// </summary>
        /// <param name="position">Indice del server da attivare</param>
        void Active(int position);

        /// <summary>
        /// Avvia la comunicazione con i server connessi. In particolare attiva il primo server tra quelli connessi.        
        /// </summary>
        void Start();

        /// <summary>
        /// Termina la comunicazione con i server connessi. In particolare deattiva tutti i server.
        /// </summary>
        void Stop();

        /// <summary>
        /// Invio la posizione del mouse
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        void SendMouseMove(int x, int y);

        /// <summary>
        /// Invio il click del mouse
        /// </summary>
        /// <param name="type">Tipo di click effettuato</param>
        void SendMouseClick(MouseClickType type);

        /// <summary>
        /// Inviolo scroll del mouse
        /// </summary>
        /// <param name="delta">Quantità di scroll</param>
        void SendMouseScroll(int delta);

        /// <summary>
        /// Invia la pressione del tasto key al server attivo
        /// </summary>
        /// <param name="key">Valore del tasto selezionato</param>
        void SendKeyDown(Key key);

        /// <summary>
        /// Invia il rilascio del tasto key al server attivo
        /// </summary>
        /// <param name="key">Valore del tasto rilasciato</param>
        void SendKeyUp(Key key);

        /// <summary>
        /// Inverte la posizione dei Server in posizione "pos1" e "pos2"
        /// </summary>
        /// <param name="pos1">posizione del primo server</param>
        /// <param name="pos2">posizione del secondo server</param>
        void SwitchPosition(int pos1, int pos2);

        /// <summary>
        /// Restituisce la lista dei server connessi
        /// </summary>
        /// <returns></returns>
        List<ServerConnection> getServers();

        /// <summary>
        /// Ottiene la clipboard del server
        /// </summary>
        /// <returns></returns>
        void RemoteCopy();

        /// <summary>
        /// Setta la clipboard del server
        /// </summary>
        /// <returns></returns>
        void RemotePaste();

        void CancelTransfer();

    }
}
