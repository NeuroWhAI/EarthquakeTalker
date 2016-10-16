using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using LinqToTwitter;

namespace EarthquakeTalker
{
    public class IssueWatcher : TwitterWorker
    {
        public IssueWatcher(string keyword, string searchTerm, TimeSpan triggerTime,
            int maxStatusCount = 20, int maxTextLength = 32)
        {
            this.Keyword = keyword;
            this.SearchTerm = searchTerm;
            this.TriggerTime = triggerTime;
            this.MaxStatusCount = maxStatusCount;
            this.MaxTextLength = maxTextLength;
        }

        //#########################################################################################################

        public string Keyword
        { get; set; } = "";

        public string SearchTerm
        { get; set; } = "";

        public TimeSpan TriggerTime
        { get; set; }

        public int MaxStatusCount
        { get; set; } = 20;

        public int MaxTextLength
        { get; set; } = 32;

        //#########################################################################################################

        protected override void BeforeStart(MultipleTalker talker)
        {
            this.JobDelay = TimeSpan.FromSeconds(22.0);


            AuthorizeContext();
        }

        protected override void AfterStop(MultipleTalker talker)
        {
            m_twitterCtx = null;
        }

        protected override Message OnWork()
        {
            try
            {
                // 원래 딜레이로 복귀
                this.JobDelay = TimeSpan.FromSeconds(22.0);


                var searchResponse =
                    (from search in m_twitterCtx.Search
                    where search.Type == SearchType.Search &&
                          search.Query == this.SearchTerm &&
                          search.IncludeEntities == true &&
                          search.ResultType == ResultType.Recent &&
                          search.SearchLanguage == "ko" &&
                          search.Count == this.MaxStatusCount * 3
                    select search).FirstOrDefault();


                if (searchResponse != null && searchResponse.Statuses != null)
                {
                    var statuses = searchResponse.Statuses;

                    // 특정 길이 이상이거나 리트윗 등 방해되는 트윗은 제외.
                    statuses.RemoveAll(delegate (Status status)
                    {
                        return (status.Text.Length > MaxTextLength || status.Text.Contains("RT"));
                    });


                    if (statuses.Count >= this.MaxStatusCount)
                    {
                        var latestStatus = statuses.First();

                        var timespan = latestStatus.CreatedAt - statuses[this.MaxStatusCount - 1].CreatedAt;
                        timespan = timespan.Duration();

                        if (timespan <= this.TriggerTime)
                        {
                            /// UTC 시간을 한국 시간(UTC+09)으로 계산한 시간.
                            var latestTimeInKor = latestStatus.CreatedAt + TimeSpan.FromHours(9.0);

                            StringBuilder msg = new StringBuilder();
                            msg.AppendLine(latestTimeInKor.ToShortDateString() + " " + latestTimeInKor.ToShortTimeString());
                            msg.Append("트위터 ");
                            msg.Append(this.Keyword);
                            msg.Append(" 관련 트윗 ");
                            msg.Append(this.MaxStatusCount);
                            msg.AppendLine("개가");
                            msg.Append(timespan.TotalSeconds.ToString("F1") + "초 ");
                            msg.AppendLine("사이에 확인됨.");
                            msg.AppendLine("오류일 수 있으니 침착하시고 소식에 귀 기울여 주시기 바랍니다.");

                            msg.AppendLine();

                            msg.AppendLine("[트윗 내용]");
                            for (int i = 0; i < this.MaxStatusCount; ++i)
                            {
                                string text = statuses[i].Text.Replace('\n', ' ');

                                if (text.Length > 24)
                                    msg.AppendLine(text.Substring(0, 24) + "...");
                                else
                                    msg.AppendLine(text);
                            }


                            // 한번 트리거되면 도배를 방지하기 위해서 좀더 오래동안 작동하지 않음.
                            this.JobDelay = TimeSpan.FromMinutes(10.0);

                            return new Message()
                            {
                                Level = Message.Priority.High,
                                Sender = this.Keyword + " 관련 실시간 트윗",
                                Text = msg.ToString(),
                            };
                        }
                    }


                    Console.Write('#');
                }
            }
            catch (Exception exp)
            {
                Console.WriteLine(exp.Message);
                Console.WriteLine(exp.StackTrace);


                Thread.Sleep(10000);

                AuthorizeContext();
            }


            return null;
        }
    }
}
