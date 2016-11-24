using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LinqToTwitter;

namespace EarthquakeTalker
{
    public interface ITweetFormatter
    {
        Message FormatTweet(Status tweet);
    }
}
