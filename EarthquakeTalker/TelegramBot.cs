using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.IO;
using System.Net;
using Telegram.Bot;

namespace EarthquakeTalker
{
    using ChatId = Telegram.Bot.Types.ChatId;
    using InputOnlineFile = Telegram.Bot.Types.InputFiles.InputOnlineFile;

    public class TelegramBot : Talker
    {
        public TelegramBot(string roomID)
        {
            this.TargetRoom = new ChatId(roomID);


            try
            {
                using (StreamReader sr = new StreamReader(new FileStream("telegrambotkey.txt", FileMode.Open)))
                {
                    string key = sr.ReadLine();

                    Client = new TelegramBotClient(key);

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

        private TelegramBotClient Client = null;

        public ChatId TargetRoom
        { get; set; }

        //###########################################################################################################

        protected override bool Talk(Message message)
        {
            string[] imageTypes =
            {
                ".png", ".jpg", ".bmp", ".jpeg", ".gif", // TODO: More...?
            };

            bool disableNoti = (message.Level < Message.Priority.High);

            try
            {
                if (message.Text.Contains('\n') == false
                    && imageTypes.Any(imgType => message.Text.TrimEnd().EndsWith(imgType)))
                {
                    InputOnlineFile photo = null;

                    if (message.Text.TrimStart().StartsWith("http"))
                    {
                        photo = new InputOnlineFile(message.Text.Trim());
                    }
                    else
                    {
                        photo = new InputOnlineFile(File.OpenRead(message.Text));
                    }

                    Client.SendPhotoAsync(
                        chatId: TargetRoom,
                        photo: photo,
                        caption: message.Sender,
                        disableNotification: disableNoti).Wait();
                }
                else
                {
                    Client.SendTextMessageAsync(
                        chatId: TargetRoom,
                        text: message.ToString(),
                        disableWebPagePreview: !message.Preview,
                        disableNotification: disableNoti).Wait();
                }
            }
            catch (Exception exp)
            {
                Console.WriteLine(exp.Message);
                Console.WriteLine(exp.StackTrace);

                return false;
            }


            return true;
        }
    }
}
