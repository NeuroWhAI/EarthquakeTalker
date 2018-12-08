using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using EarthquakeTalker;

namespace UnitTestProject
{
    [TestClass]
    public class QueryTest
    {
        [TestMethod]
        public void QueryKmaEqkImageTest()
        {
            Message msg = null;

            var formatter = new KMAEarthquakeFormatter();
            formatter.SendImage(new Action<Message>((m) =>
            {
                msg = m;
            }));

            Assert.IsTrue(msg.Text.StartsWith("http"));
        }
    }
}
