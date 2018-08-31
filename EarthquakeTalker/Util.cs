using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;

namespace EarthquakeTalker
{
    internal static class Util
    {
        public static string ConvertHtmlToText(string html)
        {
            StringBuilder msgBdr = new StringBuilder(html);
            msgBdr.Replace("<br>", "\n");
            msgBdr.Replace("<br/>", "\n");

            return WebUtility.HtmlDecode(msgBdr.ToString());
        }

        public static string RemoveHtmlTag(string html)
        {
            StringBuilder msgBdr = new StringBuilder();

            for (int i = 0; i < html.Length; ++i)
            {
                if (html[i] == '<')
                {
                    for (++i; i < html.Length && html[i] != '>'; ++i) ;
                }
                else
                {
                    msgBdr.Append(html[i]);
                }
            }

            return msgBdr.ToString();
        }
    }
}
