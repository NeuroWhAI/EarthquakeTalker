using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
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

        private string EncodeToJson(string text)
        {
            StringBuilder buffer = new StringBuilder(text);
            buffer.Replace("\\", "\\\\");
            buffer.Replace("\"", "\\\"");
            buffer.Replace("\'", "\\\'");
            buffer.Replace("\n", "\\n");
            buffer.Replace("\r", "\\r");
            buffer.Replace("\b", "\\b");
            buffer.Replace("\t", "\\t");
            buffer.Replace("\a", "\\a");
            buffer.Replace("\f", "\\f");
            buffer.Replace("\v", "\\v");


            return buffer.ToString();
        }

        protected override bool Talk(Message message)
        {
            /*
             * HTTP 기반 API : https://core.telegram.org/bots/api
             */


            string[] imageTypes =
            {
                ".png", ".jpg", ".bmp", ".jpeg", ".gif", // TODO: More...?
            };


            string apiName = "SendMessage";
            StringBuilder postData = new StringBuilder();


            if (message.Text.TrimStart().StartsWith("http")
                && imageTypes.Any(imgType => message.Text.TrimEnd().EndsWith(imgType)))
            {
                apiName = "SendPhoto";

                postData.Append("\"caption\": \"");
                postData.Append(EncodeToJson(message.Sender));
                postData.Append("\", \"photo\": \"");
                postData.Append(message.Text.Trim());
                postData.Append("\"");
            }
            else
            {
                apiName = "SendMessage";

                if (message.Preview == false)
                {
                    postData.Append("\"disable_web_page_preview\": true");
                }

                postData.Append(", \"text\": \"");
                postData.Append(EncodeToJson(message.ToString()));
                postData.Append("\"");
            }


            return Send(message.Level, postData.ToString(), apiName);
        }

        private bool Send(Message.Priority priority, string parameters, string apiName)
        {
            /*
             * HTTP 기반 API : https://core.telegram.org/bots/api
             */


            StringBuilder postData = new StringBuilder();
            postData.Append("{\"chat_id\": \"@");
            postData.Append(RoomID);
            postData.Append("\", \"disable_notification\": ");
            postData.Append((priority < Message.Priority.High) ? "true" : "false");
            postData.Append(", ");
            postData.Append(parameters);
            postData.Append("}");
            byte[] byteArray = Encoding.UTF8.GetBytes(postData.ToString());


            for (int tryPost = 0; tryPost < 5; ++tryPost)
            {
                try
                {
                    var http = WebRequest.CreateHttp("https://api.telegram.org/" + BotKey + "/" + apiName);
                    http.Method = "POST";
                    http.ContentType = "application/json";
                    http.ContentLength = byteArray.Length;


                    Stream dataStream = http.GetRequestStream();
                    dataStream.Write(byteArray, 0, byteArray.Length);
                    dataStream.Close();


                    var res = http.GetResponse();


                    // 성공적으로 POST.
                    break;
                }
                catch (Exception exp)
                {
                    Console.WriteLine(exp.Message);
                    Console.WriteLine(exp.StackTrace);

                    // 잠시 대기했다가 다시 시도.
                    Thread.Sleep(3000);
                }
            }


            return true;
        }
    }
}
