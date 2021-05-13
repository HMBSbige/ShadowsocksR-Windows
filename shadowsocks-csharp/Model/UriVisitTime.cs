using System;

namespace Shadowsocks.Model
{
    public class UriVisitTime : IComparable
    {
        public DateTime visitTime;
        public string uri;
        public int index;

        public int CompareTo(object other)
        {
            if (other is not UriVisitTime)
            {
                throw new InvalidOperationException("CompareTo: Not a UriVisitTime");
            }

            return Equals(other) ? 0 : visitTime.CompareTo(((UriVisitTime)other).visitTime);
        }

    }
}
