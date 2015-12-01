using System;
using System.Text;

namespace Client.connessione
{
    class TipoComando
    {
        //MOUSE
        public const string MOUSE_MOVE = "MS_MOV";
     //   public const string MOUSE_ACTIVE_
        //mouse click
        public const string CLICK_LEFT_DOWN = "MS_CLD";
        public const string CLICK_LEFT_UP = "MS_CLU";
        public const string CLICK_RIGHT_DOWN = "MS_CRD";
        public const string CLICK_RIGHT_UP = "MS_CRU";
        public const string CLICK_MIDDLE_DOWN = "MS_CMD";
        public const string CLICK_MIDDLE_UP = "MS_CMU";
        //mouse scroll
        public const string MOUSE_SCROLL = "MS_WHL";

        //KEYBOARD
        public const string KEY_PRESS_DOWN = "KEY_DW";
        public const string KEY_PRESS_UP = "KEY_UP";

        //CLIPBOARD
        public const String CLIPBOARD_AUDIO = "CB_AUD";
        public const String CLIPBOARD_IMAGE = "CB_IMG";
        public const String CLIPBOARD_TEXT = "CB_TXT";
        public const String CLIPBOARD_FILES = "CB_FIL";
        public const string ACTIVE_CLIPBOARD_RC = "CB_ARC";
        public const string ACTIVE_CLIPBOARD_RP = "CB_ARP";
        public const string ACTIVE_CLIPBOARD_ACK = "CB_ACK";
        public const String CLIPBOARD_FILE_TYPE_FILE = "FL";
        public const String CLIPBOARD_FILE_TYPE_SUBFILE = "SF";
        public const String CLIPBOARD_FILE_TYPE_DIRECTORY = "DR";
        public const String CLIPBOARD_FILE_TYPE_SUBDIRECTORY = "SD";
        public const String CLIPBOARD_EMPTY = "CB_EMP";
        
        //FIND SERVER
        public const String FIND_SERVER = "FIND_S";
        public const String SERVER_INFORMATION = "SERVER";

        //LOGIN
        public const String REQUEST_LOGIN = "RQ_LOG";
        public const String LOGIN_CHALLENGE = "LOG_CH";
        public const String LOGIN_OK = "LOG_OK";
        public const String LOGIN_ERROR = "LOG_ER";

        //LOGIN PARAM

        public const String LOGIN_TCP_PORT = "TCP_PT";
        public const String LOGIN_UDP_PORT = "UDP_PT";
        public const String LOGIN_WIDTH_MONITOR = "MNT_WD";
        public const String LOGIN_HEIGHT_MONITOR = "MNT_HG";

        //CONNECTION
        public const string ACTIVE_SERVER = "ACTIVE";
        public const string DECTIVE_SERVER = "NO_ACT";
        public const string CLOSE_CONNECTION = "CLOSED";

        public const string KEEP_ALIVE_REQUEST = "KEP_RQ";
        public const string KEEP_ALIVE_ACK = "KEP_AK";

    }

    class TipoComandoBytes
    {
        //MOUSE
        public static readonly byte[] MOUSE_MOVE = Encoding.ASCII.GetBytes(TipoComando.MOUSE_MOVE);
        //mouse click
        public static readonly byte[] CLICK_LEFT_DOWN = Encoding.ASCII.GetBytes(TipoComando.CLICK_LEFT_DOWN);
        public static readonly byte[] CLICK_LEFT_UP = Encoding.ASCII.GetBytes(TipoComando.CLICK_LEFT_UP);
        public static readonly byte[] CLICK_RIGHT_DOWN = Encoding.ASCII.GetBytes(TipoComando.CLICK_RIGHT_DOWN);
        public static readonly byte[] CLICK_RIGHT_UP = Encoding.ASCII.GetBytes(TipoComando.CLICK_RIGHT_UP);
        public static readonly byte[] CLICK_MIDDLE_DOWN = Encoding.ASCII.GetBytes(TipoComando.CLICK_MIDDLE_DOWN);
        public static readonly byte[] CLICK_MIDDLE_UP = Encoding.ASCII.GetBytes(TipoComando.CLICK_MIDDLE_UP);
        //mouse scroll
        public static readonly byte[] MOUSE_SCROLL = Encoding.ASCII.GetBytes(TipoComando.MOUSE_SCROLL);

        //KEYBOARD
        public static readonly byte[] KEY_PRESS_DOWN = Encoding.ASCII.GetBytes(TipoComando.KEY_PRESS_DOWN);
        public static readonly byte[] KEY_PRESS_UP = Encoding.ASCII.GetBytes(TipoComando.KEY_PRESS_UP);

        //CLIPBOARD
        public static readonly byte[] CLIPBOARD_AUDIO = Encoding.ASCII.GetBytes(TipoComando.CLIPBOARD_AUDIO);
        public static readonly byte[] CLIPBOARD_IMAGE = Encoding.ASCII.GetBytes(TipoComando.CLIPBOARD_IMAGE);
        public static readonly byte[] CLIPBOARD_TEXT = Encoding.ASCII.GetBytes(TipoComando.CLIPBOARD_TEXT);
        public static readonly byte[] CLIPBOARD_FILES = Encoding.ASCII.GetBytes(TipoComando.CLIPBOARD_FILES);
        public static readonly byte[] ACTIVE_CLIPBOARD_RC = Encoding.ASCII.GetBytes(TipoComando.ACTIVE_CLIPBOARD_RC);
        public static readonly byte[] ACTIVE_CLIPBOARD_RP = Encoding.ASCII.GetBytes(TipoComando.ACTIVE_CLIPBOARD_RP);
        public static readonly byte[] ACTIVE_CLIPBOARD_ACK = Encoding.ASCII.GetBytes(TipoComando.ACTIVE_CLIPBOARD_ACK);
        public static readonly byte[] CLIPBOARD_FILE_TYPE_FILE = Encoding.ASCII.GetBytes(TipoComando.CLIPBOARD_FILE_TYPE_FILE);
        public static readonly byte[] CLIPBOARD_FILE_TYPE_SUBFILE = Encoding.ASCII.GetBytes(TipoComando.CLIPBOARD_FILE_TYPE_SUBFILE);
        public static readonly byte[] CLIPBOARD_FILE_TYPE_DIRECTORY = Encoding.ASCII.GetBytes(TipoComando.CLIPBOARD_FILE_TYPE_DIRECTORY);
        public static readonly byte[] CLIPBOARD_FILE_TYPE_SUBDIRECTORY = Encoding.ASCII.GetBytes(TipoComando.CLIPBOARD_FILE_TYPE_SUBDIRECTORY);
        public static readonly byte[] CLIPBOARD_EMPTY = Encoding.ASCII.GetBytes(TipoComando.CLIPBOARD_EMPTY);

        //FIND SERVER
        public static readonly byte[] FIND_SERVER = Encoding.ASCII.GetBytes(TipoComando.FIND_SERVER);
        public static readonly byte[] SERVER_INFORMATION = Encoding.ASCII.GetBytes(TipoComando.SERVER_INFORMATION);

        //LOGIN
        public static readonly byte[] REQUEST_LOGIN = Encoding.ASCII.GetBytes(TipoComando.REQUEST_LOGIN);
        public static readonly byte[] LOGIN_CHALLENGE = Encoding.ASCII.GetBytes(TipoComando.LOGIN_CHALLENGE);
        public static readonly byte[] LOGIN_OK = Encoding.ASCII.GetBytes(TipoComando.LOGIN_OK);
        public static readonly byte[] LOGIN_ERROR = Encoding.ASCII.GetBytes(TipoComando.LOGIN_ERROR);

        //LOGIN PARAM

        public static readonly byte[] LOGIN_TCP_PORT = Encoding.ASCII.GetBytes(TipoComando.LOGIN_TCP_PORT);
        public static readonly byte[] LOGIN_UDP_PORT = Encoding.ASCII.GetBytes(TipoComando.LOGIN_UDP_PORT);
        public static readonly byte[] LOGIN_WIDTH_MONITOR = Encoding.ASCII.GetBytes(TipoComando.LOGIN_WIDTH_MONITOR);
        public static readonly byte[] LOGIN_HEIGHT_MONITOR = Encoding.ASCII.GetBytes(TipoComando.LOGIN_HEIGHT_MONITOR);

        //CONNECTION
        public static readonly byte[] ACTIVE_SERVER = Encoding.ASCII.GetBytes(TipoComando.ACTIVE_SERVER);
        public static readonly byte[] DEACTIVE_SERVER = Encoding.ASCII.GetBytes(TipoComando.DECTIVE_SERVER);
        public static readonly byte[] CLOSE_CONNECTION = Encoding.ASCII.GetBytes(TipoComando.CLOSE_CONNECTION);

        public static readonly byte[] KEEP_ALIVE_REQUEST = Encoding.ASCII.GetBytes(TipoComando.KEEP_ALIVE_REQUEST);
        public static readonly byte[] KEEP_ALIVE_ACK = Encoding.ASCII.GetBytes(TipoComando.KEEP_ALIVE_ACK);
    }
}
