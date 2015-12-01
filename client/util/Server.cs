using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Security;


namespace Client.connessione
{

    public class Server
    {
        private volatile UdpClient udpChannel;
        private volatile TcpClient tcpChannel;
        private volatile bool udEnabled;

        public Server() : this("", "", 0) { }

        public Server(String nome, String ip, short porta)
        {
            if (nome == null || ip == null)
            {
                throw new ArgumentNullException();
            }

            Name = nome;
            IP = ip;
            ControlPort = porta;
            ClipboardPort = (short)(ControlPort + 1);
        }

        public String Name { get; set; }
        public String IP { get; set; }
        public Int16 ControlPort { get; set; }
        public Int16 ClipboardPort { get; set; }
        public Int16 MousePort { get; set; }

        public bool UdpEnabled
        {
            get { return udEnabled; }
            set { udEnabled = value; }
        }

        public String Password { get; set; }


        public TcpClient TCPControlChannel { get; set; }
        public TcpClient TCPChannel
        {
            get { return tcpChannel; ; }
            set { tcpChannel = value; }
        }
        public UdpClient UDPChannel
        {
            get { return udpChannel; ; }
            set { udpChannel = value; }
        }

     
        public TcpClient ClipboardChannel { get; set; }

        public override bool Equals(System.Object obj)
        {
            // If parameter is null return false.
            if (obj == null)
            {
                return false;
            }

            // If parameter cannot be cast to Point return false.
            Server s = obj as Server;
            if ((System.Object)s == null)
            {
                return false;
            }

            // Return true if the fields match:
            return Name.Equals(s.Name) && IP.Equals(s.IP) && ControlPort ==s.ControlPort;
        }

        public bool Equals(Server s)
        {
            if (s == null)
            {
                return false;
            }

            return Name.Equals(s.Name) && IP.Equals(s.IP) && ControlPort == s.ControlPort;

        }

        public override int GetHashCode()
        {

            int hash = 17;
            hash = hash * 23 + Name.GetHashCode();
            hash = hash * 23 + IP.GetHashCode();
            hash = hash * 23 + ControlPort.GetHashCode();

            return hash;
        }


    }
}
