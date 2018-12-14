using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Net;

namespace EarthquakeTalker
{
    public static class Util
    {
        public static string ConvertHtmlToText(string html)
        {
            html = Regex.Replace(html, @" {2,}", " ");
            html = Regex.Replace(html, @"> +", ">");
            html = Regex.Replace(html, @" +<", "<");

            StringBuilder msgBdr = new StringBuilder(html);
            msgBdr.Replace("<br>", "\n");
            msgBdr.Replace("<br/>", "\n");
            msgBdr.Replace("</p>", "\n");

            return WebUtility.HtmlDecode(RemoveHtmlTag(msgBdr.ToString()));
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
