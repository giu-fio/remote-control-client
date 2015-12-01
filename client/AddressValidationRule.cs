using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Net;
using System.Text.RegularExpressions;

namespace Client
{
    public class AddressValidationRule : ValidationRule
    {
        public override ValidationResult Validate(object value, CultureInfo cultureInfo)
        {
            IPAddress ip;
            string ipString = (string)value;

            if (!contiene3Punti(ipString) || !IPAddress.TryParse(ipString, out ip))
            {

                return new ValidationResult(false, "Non è un indirizzo valido.");
            }

            return new ValidationResult(true, null);
        }
        private bool contiene3Punti(String s)
        {
            int count = 0;
            foreach (char c in s.ToCharArray())
            {
                if (c == '.')
                {
                    count++;
                }

            }

            return count == 3;

        }
    }

    public class NomeServerValidationRule : ValidationRule
    {
        public override ValidationResult Validate(object value, CultureInfo cultureInfo)
        {
            
            if (String.IsNullOrEmpty((string)value))
            {
                return new ValidationResult(false, "Non è un nome valido.");
            }
            return new ValidationResult(true, null);

        }



    }

    public class PortaValidationRule : ValidationRule
    {
        public override ValidationResult Validate(object value, CultureInfo cultureInfo)
        {

            short s;
            if (!short.TryParse((string)value, out s) || s < 1024)
            {
                return new ValidationResult(false, "Non è una porta valida.");
            }
            return new ValidationResult(true, null);

        }



    }
}
