using Client.connessione;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.args
{
    public class ServerEventArgs : EventArgs
    {
        public Server Server { get; set; }
        public int Position { get; set; }
    }


    public class ServerErrorEventsArgs : ServerEventArgs
    {

        public const int CONNECTION_ERROR = 0;
        public const int NETWORK_ERROR = 1;
        public const int SERVER_ERROR = 2;
        public const int KEEP_ALIVE_NOT_RECEIVED = 3;
        public const int LOGIN_ERROR = 4;
        public const int CLIPBOARD_IN_ACTION = 5;
        public const int PASSWORD_ERROR = 6;

        /// <summary>
        /// 
        /// </summary>
        public int ErrorCode { get;set; }
    }

    public class ServerTranferEventArgs : ServerEventArgs
    {
        public int Percentage { get; set; }
        public bool Completed { get; set; }
        public bool Copy { get; set; }
    }
}
