using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

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

        public Guid Id
        { get; private set; } = Guid.NewGuid();

        public DateTime CreationTime
        { get; set; } = DateTime.UtcNow.AddHours(9); // KST

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
            str.AppendLine(CreationTime.ToLongTimeString());
            str.AppendLine("<< " + Sender + " >>");
            str.AppendLine("## " + Level.ToString() + " Level ##");
            str.Append(Text);


            return str.ToString();
        }

        public object Clone()
        {
            return new Message()
            {
                Id = Id,
                CreationTime = CreationTime,
                Level = Level,
                Sender = Sender,
                Text = Text,
                Preview = Preview,
                RetryCount = RetryCount,
            };
        }

        public void WriteToStream(BinaryWriter bw)
        {
            bw.Write(this.Id.ToByteArray());
            bw.Write(this.CreationTime.ToBinary());
            bw.Write((int)this.Level);
            bw.Write(this.Sender);
            bw.Write(this.Text);
            bw.Write(this.Preview);
            bw.Write(this.RetryCount);
        }

        public void ReadFromStrem(BinaryReader br)
        {
            this.Id = new Guid(br.ReadBytes(16));
            this.CreationTime = DateTime.FromBinary(br.ReadInt64());
            this.Level = (Priority)br.ReadInt32();
            this.Sender = br.ReadString();
            this.Text = br.ReadString();
            this.Preview = br.ReadBoolean();
            this.RetryCount = br.ReadInt32();
        }
    }
}
