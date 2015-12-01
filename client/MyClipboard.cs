using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client
{
    public class MyClipboard
    {

        public string DirectoryFileDir { get; set; }
        public byte[] BytesImage { get; set; }
        public byte[] BytesAudio { get; set; }
        public byte[] TextClipboard { get; set; }
        public int NumFiles { get; set; }
        public int Dimension { get; set; }

        public MyClipboard()
        {
            DirectoryFileDir = null;
            BytesImage = null;
            BytesAudio = null;
            TextClipboard = null;
            NumFiles = 0;
            Dimension = 0;
        }

        public MyClipboard(MyClipboard clipboardToCopy)
        {
            DirectoryFileDir = clipboardToCopy.DirectoryFileDir;
            BytesImage = clipboardToCopy.BytesImage;
            BytesAudio = clipboardToCopy.BytesAudio;
            TextClipboard = clipboardToCopy.TextClipboard;
            NumFiles = clipboardToCopy.NumFiles;
            Dimension = clipboardToCopy.Dimension;
        }

        public bool ContainsFiles()
        {
            return DirectoryFileDir != null;
        }

        public bool ContainsAudio()
        {
            return BytesAudio != null;
        }

        public bool ContainsImage()
        {
            return BytesImage != null;
        }

        public bool ContainsText()
        {
            return TextClipboard != null;
        }

        public bool isEmpty()
        {
            return NumFiles == 0 && Dimension == 0;
        }

    }
}
