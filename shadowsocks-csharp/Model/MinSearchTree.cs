using System;
using System.Collections.Generic;
using System.Text;

namespace Shadowsocks.Model
{
    public struct MinSearchTreeNode
    {
        public int range_min;
        public int range_max;
        public int count;
        public long min;
    }

    public class MinSearchTree
    {
        protected int _count;
        protected int _size;
        protected int _level;
        protected MinSearchTreeNode[] _tree;

        public MinSearchTree(int size)
        {
            _level = GetLevel(size);
            _size = size;
            _count = size + (1 << _level);
            _tree = new MinSearchTreeNode[2 << _level];
        }

        public int Size
        {
            get
            {
                return _size;
            }
        }

        protected int GetLevel(int size)
        {
            int ret = 0;
            for (int s = size; s > 1; s >>=1)
            {
                ret++;
            }
            if (size != (1 << ret))
                ++ret;
            return ret;
        }

        protected void _Init(int index, int level, int range_min, int range_max)
        {
            _tree[index].range_min = range_min;
            _tree[index].range_max = range_max;
            _tree[index].min = 0;
            _tree[index].count = range_max - range_min;
            if (level >= 0)
            {
                int l = index * 2;
                int r = l + 1;
                _Init(l, level - 1, range_min, range_min + (1 << level));
                _Init(r, level - 1, range_min + (1 << level), range_max);
            }
        }

        public void Init()
        {
            _Init(1, _level - 1, 0, _size);
            for (int i = _count; i < (2 << _level); ++i)
            {
                _tree[i].min = Int64.MaxValue;
            }
            int offset = 1 << _level;
            for (int i = _count >> 1; i < offset; ++i)
            {
                Maintain(i);
            }
        }

        public MinSearchTree Clone()
        {
            MinSearchTree tree = new MinSearchTree(_size);
            for (int i = 0; i < (2 << _level); ++i)
            {
                tree._tree[i] = _tree[i];
            }
            return tree;
        }

        public void Update(int[] add_list)
        {
            int offset = 1 << _level;
            for (int i = 0; i < add_list.Length; ++i)
            {
                if (add_list[i] > 0)
                {
                    _tree[offset + i].min += add_list[i];
                    add_list[i] = 0;
                    Maintain((offset + i) >> 1);
                }
            }
        }

        public void Update(Dictionary<int, long> add_map)
        {
            int offset = 1 << _level;
            foreach (KeyValuePair<int, long> pair in add_map)
            {
                _tree[offset + pair.Key].min += pair.Value;
                Maintain((offset + pair.Key) >> 1);
            }
            add_map.Clear();
            if (_tree[1].min > int.MaxValue)
            {
                for (int i = 1; i < _tree.Length; ++i)
                {
                    _tree[i].min -= int.MaxValue;
                }
            }
        }

        protected void Maintain(int index)
        {
            for (; index > 0; index >>= 1)
            {
                int l = index * 2;
                int r = l + 1;
                long min = Math.Min(_tree[l].min, _tree[r].min);
                int count = 0;
                if (min == _tree[l].min)
                    count += _tree[l].count;
                if (min == _tree[r].min)
                    count += _tree[r].count;
                if (_tree[index].min == min && _tree[index].count == count)
                    return;
                _tree[index].min = min;
                _tree[index].count = count;
            }
        }

        public void Update(int index, int add = 1)
        {
            index = (1 << _level) + index;
            _tree[index].min += add;
            Maintain(index >> 1);
        }

        public int FindMinCount(int index, int range_min, int range_max, out long min_val)
        {
            if (range_min == _tree[index].range_min && range_max == _tree[index].range_max)
            {
                min_val = _tree[index].min;
                return _tree[index].count;
            }
            int l = index * 2;
            int r = l + 1;
            int count = 0;
            long sub_min_val = Int64.MaxValue;
            if (_tree[l].range_max > range_min)
            {
                long out_val;
                int cnt = FindMinCount(l, range_min, Math.Min(range_max, _tree[l].range_max), out out_val);
                if (out_val < sub_min_val)
                {
                    sub_min_val = out_val;
                    count = cnt;
                }
                else if (out_val == sub_min_val)
                {
                    count += cnt;
                }
            }
            if (_tree[r].range_min < range_max)
            {
                long out_val;
                int cnt = FindMinCount(r, Math.Max(range_min, _tree[r].range_min), range_max, out out_val);
                if (out_val < sub_min_val)
                {
                    sub_min_val = out_val;
                    count = cnt;
                }
                else if (out_val == sub_min_val)
                {
                    count += cnt;
                }
            }
            min_val = sub_min_val;
            return count;
        }

        public int FindNthMin(int index, int range_min, int range_max, int nth, long val)
        {
            if (_tree[index].range_min + 1 == _tree[index].range_max)
            {
                return index - (1 << _level);
            }
            int l = index * 2;
            int r = l + 1;
            if (_tree[l].range_max > range_min)
            {
                if (_tree[r].range_min < range_max)
                {
                    long out_val;
                    int cnt = FindMinCount(l, range_min, _tree[l].range_max, out out_val);
                    if (out_val != val) cnt = 0;
                    if (cnt > nth)
                    {
                        return FindNthMin(l, range_min, _tree[l].range_max, nth, val);
                    }
                    else
                    {
                        return FindNthMin(r, _tree[r].range_min, range_max, nth - cnt, val);
                    }
                }
                else
                {
                    return FindNthMin(l, Math.Max(range_min, _tree[l].range_min), range_max, nth, val);
                }
            }
            else
            {
                return FindNthMin(r, range_min, Math.Min(range_max, _tree[r].range_max), nth, val);
            }
        }

        public int FindMinCount2(int index, int range_min, int range_max, out long min_val)
        {
            int offset = 1 << _level;
            long min = Int64.MaxValue;
            int cnt = 0;
            for (int i = range_min; i < range_max; ++i)
            {
                if (_tree[offset + i].min < min)
                {
                    min = _tree[offset + i].min;
                }
            }
            for (int i = range_min; i < range_max; ++i)
            {
                if (_tree[offset + i].min == min)
                {
                    ++cnt;
                }
            }
            min_val = min;
            return cnt;
        }

        public int FindNthMin2(int range_min, int range_max, int nth)
        {
            int offset = 1 << _level;
            long min = Int64.MaxValue;
            int cnt = 0;
            for (int i = range_min; i < range_max; ++i)
            {
                if (_tree[offset + i].min < min)
                {
                    min = _tree[offset + i].min;
                }
            }
            for (int i = range_min; i < range_max; ++i)
            {
                if (_tree[offset + i].min == min)
                {
                    if (cnt == nth)
                        return i;
                    ++cnt;
                }
            }
            return -1;
        }

        public int RandomFindIndex(int range_min, int range_max, Random random)
        {
            long out_val;
            int count = FindMinCount(1, range_min, range_max, out out_val);
            int nth = random.Next(count);
            int index = FindNthMin(1, range_min, range_max, nth, out_val);
            return index;
        }

        public long GetMin(int range_min, int range_max)
        {
            long ret;
            int cnt = FindMinCount(1, range_min, range_max, out ret);
            return ret;
        }
    }
}
