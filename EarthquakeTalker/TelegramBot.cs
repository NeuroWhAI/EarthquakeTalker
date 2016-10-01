using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Net;

namespace EarthquakeTalker
{
    public class TelegramBot : Talker
    {
        public TelegramBot(string roomID)
        {
            this.RoomID = roomID;


            try
            {
                using (StreamReader sr = new StreamReader(new FileStream("telegrambotkey.txt", FileMode.Open)))
                {
                    BotKey = sr.ReadLine();


                    sr.Close();
                }
            }
            catch (FileNotFoundException)
            {
                Console.WriteLine("텔레그램 키 파일을 찾을 수 없습니다.");
            }
            catch (EndOfStreamException)
            {
                Console.WriteLine("텔레그램 키 파일을 읽을 수 없습니다.");
            }
        }

        //###########################################################################################################

        public string RoomID
        { get; set; }

        protected string BotKey
        { get; set; }

        //###########################################################################################################

        protected override bool Talk(Message message)
        {
            StringBuilder postData = new StringBuilder();
            postData.Append("{\"chat_id\": \"@");
            postData.Append(RoomID);
            postData.Append("\", \"disable_notification\": ");
            postData.Append((message.Level < Message.Priority.High) ? "true" : "false");
            postData.Append(", \"text\": \"");

            StringBuilder bodyData = new StringBuilder(message.ToString());
            bodyData.Replace("\\", "\\\\");
            bodyData.Replace("\"", "\\\"");
            bodyData.Replace("\'", "\\\'");
            bodyData.Replace("\n", "\\n");
            bodyData.Replace("\r", "\\r");
            bodyData.Replace("\b", "\\b");
            bodyData.Replace("\t", "\\t");
            bodyData.Replace("\a", "\\a");
            bodyData.Replace("\f", "\\f");
            bodyData.Replace("\v", "\\v");

            postData.Append(bodyData.ToString());

            postData.Append("\"}");
            byte[] byteArray = Encoding.UTF8.GetBytes(postData.ToString());


            var http = WebRequest.CreateHttp("https://api.telegram.org/" + BotKey + "/SendMessage");
            http.Method = "POST";
            http.ContentType = "application/json";
            http.ContentLength = byteArray.Length;

            Stream dataStream = http.GetRequestStream();
            dataStream.Write(byteArray, 0, byteArray.Length);
            dataStream.Close();


            http.GetResponseAsync();


            return true;
        }
    }
}
