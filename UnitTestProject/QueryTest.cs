using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using EarthquakeTalker;

namespace UnitTestProject
{
    [TestClass]
    public class QueryTest
    {
        [TestMethod, TestCategory("heavy"), Ignore()]
        public void QueryKmaEqkImageTest()
        {
            Message msg = null;

            var formatter = new KMAEarthquakeFormatter();
            formatter.SendImage(new Action<Message>((m) =>
            {
                msg = m;
            }));

            if (msg != null)
            {
                Assert.IsTrue(msg.Text.StartsWith("http"));
                Assert.IsTrue(msg.Sender.Contains("진도") || msg.Sender == "기상청 지진 통보문");
            }
        }
    }
}
