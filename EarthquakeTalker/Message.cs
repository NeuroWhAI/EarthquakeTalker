using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EarthquakeTalker
{
    public class Message : ICloneable
    {
        public enum Priority
        {
            Low,
            Normal,
            High,
            Critical,
        }

        //##############################################################################
        
        public Message(string text = "", string sender = "", Priority level = Priority.Normal)
        {
            Level = level;
            Sender = sender;
            Text = text;
        }

        //##############################################################################

        public Priority Level
        { get; set; }

        public string Sender
        { get; set; }

        public string Text
        { get; set; }

        public bool Preview
        { get; set; } = false;

        public int RetryCount
        { get; set; } = 0;

        //##############################################################################

        public override string ToString()
        {
            StringBuilder str = new StringBuilder();
            str.AppendLine("<< " + Sender + " >>");
            str.AppendLine("## " + Level.ToString() + " Level ##");
            str.Append(Text);


            return str.ToString();
        }

        public object Clone()
        {
            return new Message()
            {
                Level = Level,
                Sender = Sender,
                Text = Text,
                Preview = Preview,
                RetryCount = RetryCount,
            };
        }
    }
}
