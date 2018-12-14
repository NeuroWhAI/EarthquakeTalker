using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using EarthquakeTalker;

namespace UnitTestProject
{
    [TestClass]
    public class ConvertTest
    {
        [TestMethod]
        public void ConvertKmaEqkToTextTest()
        {
            string html = "<p class=\"p_hypen\"> 포항 여진 발생 현황 : 총 100회<br/>· 2.0~3.0 미만  : 92회<br/>· 3.0~4.0 미만  :  6회<br/>· 4.0~5.0 미만  :  2회</p>";
            string answer = @"포항 여진 발생 현황 : 총 100회
· 2.0~3.0 미만 : 92회
· 3.0~4.0 미만 : 6회
· 4.0~5.0 미만 : 2회".Replace("\r", "");

            string result = Util.ConvertHtmlToText(html).Trim();

            Assert.AreEqual(result, answer);
        }

        [TestMethod]
        public void ConvertKmaMicroEqkToTextTest()
        {
            string html = "<p class=\"title-blue\">[최근 미소지진 발생 현황(규모 2.0미만)]</p><p class=\"p_hypen\"> 국내 미소지진<br/> · 포항여진 : 2018/12/13 20:26:28&#40;규모:1.8 / 깊이:6km&#41;<br/> · 미소지진 : 2018/12/12 23:46:08 경북 안동시 동북동쪽 6km 지역&#40;규모:1.8 / 깊이:11km&#41;</p>";
            string answer = @"[최근 미소지진 발생 현황(규모 2.0미만)]
국내 미소지진
· 포항여진 : 2018/12/13 20:26:28(규모:1.8 / 깊이:6km)
· 미소지진 : 2018/12/12 23:46:08 경북 안동시 동북동쪽 6km 지역(규모:1.8 / 깊이:11km)".Replace("\r", "");

            string result = Util.ConvertHtmlToText(html).Trim();

            Assert.AreEqual(result, answer);
        }
    }
}
